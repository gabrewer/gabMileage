using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgentLoop.Models;

namespace AgentLoop.Services;

public static class AgentRunner
{
    public static async Task<AgentDef> LoadAgentDefAsync(string agent, string? workDir = null)
    {
        var filePath = Path.Combine(workDir ?? Directory.GetCurrentDirectory(), ".github", "agents", $"{agent}.agent.md");
        var content = await File.ReadAllTextAsync(filePath);
        // Normalize line endings so frontmatter parsing works on Windows (CRLF) and Unix (LF)
        content = content.Replace("\r\n", "\n").Replace('\r', '\n');

        // Parse YAML frontmatter
        if (!content.StartsWith("---"))
            return new AgentDef(content.Trim(), "gpt-5.2", [], false);

        var endIdx = content.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (endIdx < 0)
            return new AgentDef(content.Trim(), "gpt-5.2", [], false);

        var frontmatter = content[4..endIdx];
        var body = content[(endIdx + 5)..].Trim();

        var model = "gpt-5.2";
        string[] tools = [];
        var mcp = false;

        foreach (var line in frontmatter.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("model:"))
                model = trimmed["model:".Length..].Trim();
            else if (trimmed.StartsWith("tools:"))
                tools = trimmed["tools:".Length..].Trim().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            else if (trimmed.StartsWith("mcp:"))
                mcp = trimmed["mcp:".Length..].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        return new AgentDef(body, model, tools, mcp);
    }

    public static async Task<AgentResult> RunAsync(
        string runId,
        PrdTask task,
        string agent,
        int attempt,
        ActiveTaskDisplay display,
        string? lastFeedback = null,
        string? workDir = null,
        int timeoutRetry = 0)
    {
        var cwd = workDir ?? Directory.GetCurrentDirectory();
        var logAttempt = (agent is "review-agent" or "destroyer") ? attempt : (int?)null;
        var logPath = StateManager.GetLogPath(runId, task.Id, agent, logAttempt);
        var def = await LoadAgentDefAsync(agent, cwd);
        var agentPrompt = PromptBuilder.BuildAgentPrompt(task, agent, lastFeedback);

        // Copilot CLI has no --system-prompt flag; prepend the system prompt to the user prompt.
        var userPrompt = string.IsNullOrEmpty(def.SystemPrompt)
            ? agentPrompt
            : $"<system>\n{def.SystemPrompt}\n</system>\n\n{agentPrompt}";

        var args = new List<string>
        {
            "-p", userPrompt,
            "--output-format", "json",
            "--allow-all",
            "--no-ask-user",
        };
        // When mcp: true, don't restrict tools — MCP tool names aren't known ahead of time.
        // When mcp: false, restrict to the explicit tool list from the agent definition.
        if (!def.Mcp && def.Tools.Length > 0) { args.Add("--available-tools"); args.Add(string.Join(',', def.Tools)); }
        if (!string.IsNullOrEmpty(def.Model)) { args.Add("--model"); args.Add(def.Model); }

        var psi = new ProcessStartInfo("copilot")
        {
            RedirectStandardInput = true,   // Prevent stdin inheritance — avoids terminal blocking
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = cwd,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        // Strip Copilot CLI session markers so subprocesses aren't blocked as nested sessions
        psi.Environment.Remove("COPILOT_CLI");
        psi.Environment.Remove("COPILOT_RUN_APP");
        psi.Environment.Remove("COPILOT_LOADER_PID");
        psi.Environment.Remove("COPILOT_CLI_BINARY_VERSION");

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start copilot process for {agent}");

        // Close stdin immediately — copilot -p is fully non-interactive
        proc.StandardInput.Close();

        var resultText = "";
        var exitCode = -1;
        var bytesWritten = 0;
        var timedOut = false;

        // Per-agent timeout — kills the process if it hangs with no completion
        var timeoutMinutes = agent switch
        {
            "test-writer"   => 20,
            "backend-builder" or "frontend-builder" => 30,
            "destroyer"     => 15,
            "review-agent"  => 15,
            _               => 20,
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));

        // Read stderr concurrently to prevent buffer-full deadlock
        var stderrTask = proc.StandardError.ReadToEndAsync();

        // Write log incrementally as output arrives — visible in real time, not just at the end
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await using var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };

        try
        {
            // Stream stdout line by line
            while (await proc.StandardOutput.ReadLineAsync(cts.Token) is { } line)
            {
                await logWriter.WriteLineAsync(line);
                bytesWritten += line.Length + 1;
                display.BytesWritten = bytesWritten;

                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    var status = ExtractStatus(root);
                    if (status is not null) display.Status = status;

                    if (root.TryGetProperty("type", out var typeProp))
                    {
                        var eventType = typeProp.GetString();

                        // Track the last assistant message content as the result text
                        if (eventType == "assistant.message" &&
                            root.TryGetProperty("data", out var msgData) &&
                            msgData.TryGetProperty("content", out var contentProp))
                        {
                            var content = contentProp.GetString() ?? "";
                            if (!string.IsNullOrEmpty(content))
                                resultText = content;
                        }

                        // Final result event — capture exit code
                        if (eventType == "result" &&
                            root.TryGetProperty("exitCode", out var exitProp))
                        {
                            exitCode = exitProp.GetInt32();
                        }
                    }
                }
                catch (JsonException) { /* partial line */ }
            }
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            await logWriter.WriteLineAsync($"\n--- TIMEOUT: agent exceeded {timeoutMinutes} minute limit, process killed ---");
            display.Status = $"TIMED OUT after {timeoutMinutes}m";
            try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
        }

        var stderrText = "";
        try { stderrText = await stderrTask; } catch { /* best effort */ }
        try { await proc.WaitForExitAsync(); } catch { /* best effort */ }

        if (!string.IsNullOrEmpty(stderrText))
            await logWriter.WriteLineAsync($"\n--- STDERR ---\n{stderrText}");

        // On timeout, restart the agent — up to 3 restarts before giving up
        if (timedOut)
        {
            if (timeoutRetry < 3)
            {
                ActivityLog.AgentTimeout(task.Id, agent, timeoutMinutes, timeoutRetry + 1);
                display.Status = $"Restarting after timeout (retry {timeoutRetry + 1}/3)...";
                return await RunAsync(runId, task, agent, attempt, display, lastFeedback, workDir, timeoutRetry + 1);
            }
            Spectre.Console.AnsiConsole.MarkupLine($"\n  [red]✗  {Spectre.Console.Markup.Escape(agent)} timed out 3 times on {Spectre.Console.Markup.Escape(task.Id)} — shutting down.[/]");
            Spectre.Console.AnsiConsole.MarkupLine("  [dim]  Re-run with --resume once the underlying issue is resolved.[/]\n");
            Environment.Exit(1);
            return default!; // unreachable
        }

        // Review agent: check for SHIP IT or ESCALATE
        // Use precise matching — breadcrumbs often contain "escalate" / "ship it" in prose.
        // The agent is instructed to emit exactly `ESCALATE: …` or `SHIP IT` as its final line.
        if (agent == "review-agent")
        {
            var escalated = ContainsVerdict(resultText, "ESCALATE:");
            if (escalated) return new AgentResult(false, resultText);
            var approved = ContainsVerdict(resultText, "SHIP IT");
            return new AgentResult(approved, approved ? null : resultText);
        }

        // Destroyer: always pass output back as feedback for review-agent
        if (agent == "destroyer")
            return new AgentResult(true, resultText);

        return new AgentResult(exitCode == 0);
    }

    private static string? ExtractStatus(JsonElement root)
    {
        if (!root.TryGetProperty("type", out var typeProp)) return null;
        var type = typeProp.GetString();

        // Tool usage is embedded in assistant.message.data.toolRequests
        if (type == "assistant.message" &&
            root.TryGetProperty("data", out var msgData) &&
            msgData.TryGetProperty("toolRequests", out var toolReqs) &&
            toolReqs.ValueKind == JsonValueKind.Array)
        {
            foreach (var req in toolReqs.EnumerateArray())
            {
                var name = req.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var detail = req.TryGetProperty("intentionSummary", out var summary)
                    ? summary.GetString() ?? ""
                    : "";
                if (string.IsNullOrEmpty(detail) && req.TryGetProperty("arguments", out var arguments))
                {
                    detail = TryGetString(arguments, "command")
                        ?? TryGetString(arguments, "file_path")
                        ?? TryGetString(arguments, "path")
                        ?? TryGetString(arguments, "pattern")
                        ?? TryGetString(arguments, "description")
                        ?? "";
                }
                var truncated = detail.Length > 60 ? detail[..59] + "…" : detail;
                if (!string.IsNullOrEmpty(name))
                    return string.IsNullOrEmpty(truncated) ? name : $"{name}: {truncated}";
            }
        }

        // Streaming text delta — show live typing progress
        if (type == "assistant.message_delta" &&
            root.TryGetProperty("data", out var deltaData) &&
            deltaData.TryGetProperty("deltaContent", out var delta))
        {
            var text = delta.GetString()?.Trim() ?? "";
            if (text.Length > 0)
                return text.Length > 70 ? text[..69] + "…" : text;
        }

        return null;
    }

    private static string? TryGetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString()
            : null;

    /// <summary>
    /// Checks whether the result text contains a verdict keyword as a standalone token
    /// on its own line, not buried inside breadcrumb prose.
    /// </summary>
    private static bool ContainsVerdict(string text, string verdict)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(verdict, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
