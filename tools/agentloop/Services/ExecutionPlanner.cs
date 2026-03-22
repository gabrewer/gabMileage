using System.Diagnostics;
using System.Text.Json;
using AgentLoop.Models;

namespace AgentLoop.Services;

public sealed record ExecutionPlan(
    Dictionary<string, string[]> Dependencies,
    string Reasoning
);

public static class ExecutionPlanner
{
    public static async Task<ExecutionPlan> PlanAsync(PrdTask[] tasks, string sprint, string? feedback = null)
    {
        var taskList = string.Join('\n', tasks.Select(t =>
            $"- {t.Id}: {t.Title}\n  {t.Description}\n  Acceptance criteria: {string.Join("; ", t.AcceptanceCriteria)}"));

        var systemPrompt = """
            You are an execution planner for a software build loop.
            Given a list of tasks, identify which tasks depend on others and must run after them.
            Respond with ONLY a JSON object — no markdown, no explanation outside the JSON:
            {
              "dependencies": {
                "TASK-001": [],
                "TASK-002": ["TASK-001"]
              },
              "reasoning": "one sentence"
            }
            Rules:
            - A task depends on another only if it requires types, entities, or infrastructure that the other task creates
            - If tasks touch completely different domains/files, they have no dependency
            - Keep the graph minimal — only add edges that are truly necessary
            - Domain modeling and API contract are handled as sprint-level phases — do not include them as task dependencies
            """;

        var userPrompt = feedback is not null
            ? $"Tasks to plan:\n{taskList}\n\nUser feedback on previous plan: {feedback}\nRevise the plan accordingly."
            : $"Tasks to plan:\n{taskList}";

        var psi = new ProcessStartInfo("claude")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[]
        {
            "-p", userPrompt,
            "--system-prompt", systemPrompt,
            "--output-format", "stream-json",
            "--verbose",
            "--model", "sonnet",
            "--dangerously-skip-permissions",
            "--allowedTools", "",
        })
        {
            psi.ArgumentList.Add(arg);
        }

        psi.Environment.Remove("CLAUDECODE");
        psi.Environment.Remove("CLAUDE_CODE_SSE_PORT");
        psi.Environment.Remove("CLAUDE_CODE_ENTRYPOINT");

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start claude for planning");

        var resultText = "";
        while (await proc.StandardOutput.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var t) && t.GetString() == "result" &&
                    root.TryGetProperty("subtype", out var s) && s.GetString() == "success" &&
                    root.TryGetProperty("result", out var r))
                {
                    resultText = r.GetString() ?? "";
                }
            }
            catch (JsonException) { }
        }

        var stderrText = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        // Debug log
        var debugPath = StateManager.PlannerDebugPath(sprint);
        await File.WriteAllTextAsync(debugPath,
            $"=== stdout (resultText) ===\n{resultText}\n\n=== stderr ===\n{stderrText}\n");

        // Extract JSON
        var jsonMatch = System.Text.RegularExpressions.Regex.Match(resultText, @"\{[\s\S]*\}");
        var json = jsonMatch.Success ? jsonMatch.Value : "";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var deps = new Dictionary<string, string[]>();
            if (doc.RootElement.TryGetProperty("dependencies", out var depsEl))
            {
                foreach (var prop in depsEl.EnumerateObject())
                {
                    deps[prop.Name] = prop.Value.EnumerateArray()
                        .Select(v => v.GetString() ?? "")
                        .Where(v => v.Length > 0)
                        .ToArray();
                }
            }
            var reasoning = doc.RootElement.TryGetProperty("reasoning", out var rEl)
                ? rEl.GetString() ?? "" : "";

            // Ensure every task has an entry
            foreach (var task in tasks)
            {
                if (!deps.ContainsKey(task.Id))
                    deps[task.Id] = [];
            }

            return new ExecutionPlan(deps, reasoning);
        }
        catch
        {
            var reason = !string.IsNullOrWhiteSpace(stderrText)
                ? $"planner error: {stderrText.Trim()[..Math.Min(120, stderrText.Trim().Length)]}"
                : $"planner returned invalid JSON: {json[..Math.Min(120, json.Length)]}";

            return new ExecutionPlan(
                tasks.ToDictionary(t => t.Id, _ => Array.Empty<string>()),
                $"fallback — {reason}"
            );
        }
    }

    /// <summary>
    /// Topological sort into waves. Tasks in the same wave have no dependencies
    /// on each other and all their dependencies were satisfied in prior waves.
    /// </summary>
    public static List<PrdTask[]> BuildWaves(PrdTask[] tasks, Dictionary<string, string[]> dependencies)
    {
        var waves = new List<PrdTask[]>();
        var completed = new HashSet<string>();
        var remaining = new Dictionary<string, PrdTask>(tasks.Select(t => KeyValuePair.Create(t.Id, t)));

        while (remaining.Count > 0)
        {
            var wave = remaining.Values
                .Where(t => (dependencies.GetValueOrDefault(t.Id) ?? []).All(dep => completed.Contains(dep)))
                .ToArray();

            if (wave.Length == 0)
            {
                // Circular dependency — dump the rest in one wave
                waves.Add([.. remaining.Values]);
                break;
            }

            waves.Add(wave);
            foreach (var t in wave)
            {
                completed.Add(t.Id);
                remaining.Remove(t.Id);
            }
        }

        return waves;
    }
}
