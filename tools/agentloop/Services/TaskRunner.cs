using System.Collections.Concurrent;
using AgentLoop.Models;

namespace AgentLoop.Services;

/// <summary>
/// Runs the per-task pipeline: test → build → destroy → review (with fix loop) → commit.
/// </summary>
public sealed class TaskRunner(
    RunState state,
    ConcurrentDictionary<string, ActiveTaskDisplay> activeAgents,
    GitHubService github,
    string? workDir = null)
{
    private readonly string? _workDir = workDir;

    private int? GetIssueNumber(string taskId) =>
        state.Issues.TaskIssues.TryGetValue(taskId, out var n) ? n : null;

    private async Task UpdateIssueStatusSafe(string taskId, string emoji)
    {
        var issueNumber = GetIssueNumber(taskId);
        if (issueNumber is null) return;
        try { await github.UpdateIssueStatusAsync(issueNumber.Value, emoji); }
        catch { /* don't fail the pipeline for issue updates */ }
    }

    private async Task CommentOnIssueSafe(string taskId, string body)
    {
        var issueNumber = GetIssueNumber(taskId);
        if (issueNumber is null) return;
        try { await github.CommentOnIssueAsync(issueNumber.Value, body); }
        catch { /* don't fail the pipeline for issue comments */ }
    }

    private async Task MarkTaskDoneOnEpic(string taskId)
    {
        if (state.Issues.EpicIssue is not { } epic) return;
        try { await github.CheckTaskOnEpicAsync(epic, taskId); }
        catch { /* don't fail the pipeline for epic updates */ }
    }

    private static string Ts() => DateTime.UtcNow.ToString("HH:mm:ss") + " UTC";

    public async Task RunAsync(PrdTask task)
    {
        var ts = state.Tasks[task.Id];
        ts.Status = Models.TaskStatus.Testing;
        ts.StartedAt = DateTime.UtcNow.ToString("O");
        await StateManager.SaveStateAsync(state);

        // Mark issue as in-progress
        await UpdateIssueStatusSafe(task.Id, "🏃");
        await CommentOnIssueSafe(task.Id, $"🏃 **Pipeline started** at `{Ts()}`");
        ActivityLog.TaskStart(task.Id, task.Title);

        // 1. test-writer
        await CommentOnIssueSafe(task.Id, $"📝 **test-writer** started at `{Ts()}`");
        ActivityLog.AgentStart(task.Id, "test-writer", 1);
        await RunAgentPhase(task, "test-writer", "Writing tests...");
        ActivityLog.AgentDone(task.Id, "test-writer", 1, true);

        // 2. builders (parallel for 'both')
        ts.Status = Models.TaskStatus.Building;
        await StateManager.SaveStateAsync(state);
        var builders = GetBuilders(task.Type);
        await CommentOnIssueSafe(task.Id, $"🔨 **builders** started at `{Ts()}`");
        ActivityLog.AgentStart(task.Id, "builder", 1);
        await RunBuildersInParallel(task, builders, 1);

        // 3. build gate — skip destroyer entirely if the solution doesn't compile
        var buildError = await CheckBuildAsync();
        ActivityLog.BuildGate(task.Id, buildError is null, buildError);
        var buildAttempt = 1;
        while (buildError is not null && buildAttempt <= Pipeline.MaxReviewAttempts)
        {
            await CommentOnIssueSafe(task.Id,
                $"🔴 **Build failed** (attempt {buildAttempt}) — skipping destroyer, routing back to builder\n\n```\n{buildError}\n```");
            ts.Status = Models.TaskStatus.Building;
            await StateManager.SaveStateAsync(state);
            ActivityLog.AgentStart(task.Id, "builder", ++buildAttempt);
            await RunBuildersInParallel(task, builders, buildAttempt, $"BUILD FAILED — fix compilation errors before anything else:\n\n{buildError}");
            buildError = await CheckBuildAsync();
            ActivityLog.BuildGate(task.Id, buildError is null, buildError);
        }

        if (buildError is not null)
        {
            // Build still broken after max attempts — fail the task
            ts.Status = Models.TaskStatus.Failed;
            ts.LastFeedback = $"Build failed after {Pipeline.MaxReviewAttempts} attempts: {buildError}";
            ts.CompletedAt = DateTime.UtcNow.ToString("O");
            await StateManager.SaveStateAsync(state);
            await UpdateIssueStatusSafe(task.Id, "✋");
            await CommentOnIssueSafe(task.Id, $"✋ Blocked: build never compiled cleanly. {ts.LastFeedback}");
            return;
        }

        // 4. destroyer
        ts.Status = Models.TaskStatus.Destroying;
        await StateManager.SaveStateAsync(state);
        await CommentOnIssueSafe(task.Id, $"💥 **destroyer** started at `{Ts()}`");
        ActivityLog.AgentStart(task.Id, "destroyer", 1);
        var destroyResult = await RunAgentPhase(task, "destroyer", "Adversarial testing...");
        ActivityLog.AgentDone(task.Id, "destroyer", 1, true);

        // 5. review-agent with fix loop
        ts.Status = Models.TaskStatus.Reviewing;
        await StateManager.SaveStateAsync(state);

        var reviewContext = destroyResult.Feedback;
        var approved = false;

        for (var attempt = 1; attempt <= Pipeline.MaxReviewAttempts; attempt++)
        {
            ts.Attempts = attempt;

            var status = attempt == 1
                ? "Reviewing destroyer findings..."
                : $"Re-reviewing (attempt {attempt})...";

            await CommentOnIssueSafe(task.Id, attempt == 1
                ? $"🔍 **review-agent** started at `{Ts()}`"
                : $"🔁 **review-agent** attempt {attempt} started at `{Ts()}`");
            ActivityLog.AgentStart(task.Id, "review-agent", attempt);

            var result = await RunAgentPhase(task, "review-agent", status, attempt, reviewContext);

            ActivityLog.AgentDone(task.Id, "review-agent", attempt, result.Approved, result.Approved ? "SHIP IT" : result.Feedback?.Split('\n')[0]);
            if (result.Approved) { approved = true; break; }

            if (result.Feedback?.TrimStart().StartsWith("ESCALATE:", StringComparison.OrdinalIgnoreCase) == true)
            {
                ts.Status = Models.TaskStatus.Failed;
                ts.LastFeedback = result.Feedback;
                ts.CompletedAt = DateTime.UtcNow.ToString("O");
                await StateManager.SaveStateAsync(state);
                await UpdateIssueStatusSafe(task.Id, "👀");
                await CommentOnIssueSafe(task.Id, $"👀 Escalated: {result.Feedback}");
                return;
            }

            ts.LastFeedback = result.Feedback;

            if (attempt < Pipeline.MaxReviewAttempts)
            {
                await CommentOnIssueSafe(task.Id,
                    $"⚠️ **Changes needed** (attempt {attempt}) at `{Ts()}`\n\n> {ts.LastFeedback}");

                // Rebuild
                ts.Status = Models.TaskStatus.Building;
                ActivityLog.AgentStart(task.Id, "builder", attempt + 1);
                await CommentOnIssueSafe(task.Id, $"🔨 **builders** (fix attempt {attempt + 1}) started at `{Ts()}`");
                await RunBuildersInParallel(task, builders, attempt + 1, ts.LastFeedback);

                // Build gate after fix — no re-destroy, review re-checks the fix directly
                buildError = await CheckBuildAsync();
                ActivityLog.BuildGate(task.Id, buildError is null, buildError);
                if (buildError is not null)
                {
                    ts.LastFeedback = $"BUILD FAILED after fix attempt {attempt + 1}:\n\n{buildError}";
                    reviewContext = ts.LastFeedback;
                }
                else
                {
                    // Pass the original destroyer findings + what was fixed as context for re-review
                    reviewContext = $"Builder applied fixes for attempt {attempt + 1}. Original findings:\n\n{ts.LastFeedback}";
                }

                ts.Status = Models.TaskStatus.Reviewing;
                await StateManager.SaveStateAsync(state);
            }
        }

        if (!approved)
        {
            ts.Status = Models.TaskStatus.Failed;
            ts.CompletedAt = DateTime.UtcNow.ToString("O");
            ts.LastFeedback = ts.LastFeedback;
            await StateManager.SaveStateAsync(state);
            await UpdateIssueStatusSafe(task.Id, "✋");
            await CommentOnIssueSafe(task.Id, $"✋ Blocked: review failed after {Pipeline.MaxReviewAttempts} attempts");
            // Discard uncommitted changes so failed task code doesn't poison subsequent tasks
            await DiscardUncommittedChangesAsync();
            return;
        }

        // 5. git-committer
        ts.Status = Models.TaskStatus.Committed;
        await StateManager.SaveStateAsync(state);
        await RunAgentPhase(task, "git-committer", "Committing...");

        ts.Status = Models.TaskStatus.Done;
        ts.CompletedAt = DateTime.UtcNow.ToString("O");
        await StateManager.SaveStateAsync(state);
        ActivityLog.TaskDone(task.Id, Models.TaskStatus.Done);

        // Mark issue as done
        await UpdateIssueStatusSafe(task.Id, "✅");
        await MarkTaskDoneOnEpic(task.Id);
    }

    private async Task<AgentResult> RunAgentPhase(
        PrdTask task, string agent, string statusText, int attempt = 1, string? feedback = null)
    {
        var display = new ActiveTaskDisplay
        {
            TaskId = task.Id, Agent = agent, Attempt = attempt,
            Status = statusText,
        };
        var key = $"{task.Id}:{agent}";
        activeAgents[key] = display;

        try
        {
            return await AgentRunner.RunAsync(state.RunId, task, agent, attempt, display, feedback, _workDir);
        }
        finally
        {
            activeAgents.TryRemove(key, out _);
        }
    }

    private async Task RunBuildersInParallel(
        PrdTask task, string[] builders, int attempt, string? feedback = null)
    {
        await Task.WhenAll(builders.Select(async agent =>
        {
            var display = new ActiveTaskDisplay
            {
                TaskId = task.Id, Agent = agent, Attempt = attempt,
                Status = feedback is not null ? "Fixing review feedback..." : "Building...",
            };
            var key = $"{task.Id}:{agent}";
            activeAgents[key] = display;

            try
            {
                await AgentRunner.RunAsync(state.RunId, task, agent, attempt, display, feedback, _workDir);
            }
            finally
            {
                activeAgents.TryRemove(key, out _);
            }
        }));
    }

    private static string[] GetBuilders(TaskType type) =>
        (Pipeline.TaskAgents.GetValueOrDefault(type) ?? Pipeline.TaskAgents[TaskType.Backend])
            .Where(a => a != "test-writer")
            .ToArray();

    /// <summary>
    /// Discards all uncommitted working tree changes in the worktree so a failed task
    /// doesn't leave broken code that poisons subsequent tasks.
    /// </summary>
    private async Task DiscardUncommittedChangesAsync()
    {
        var workDir = _workDir ?? Directory.GetCurrentDirectory();
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "checkout -- .")
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            await proc.WaitForExitAsync();

            // Also remove untracked files added by the failed task
            var psi2 = new System.Diagnostics.ProcessStartInfo("git", "clean -fd")
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc2 = System.Diagnostics.Process.Start(psi2)!;
            await proc2.WaitForExitAsync();
        }
        catch { /* best-effort — don't fail the pipeline over cleanup */ }
    }

    /// <summary>
    /// Runs dotnet build in the worktree. Returns null on success, or the error output on failure.
    /// </summary>
    private async Task<string?> CheckBuildAsync()
    {
        var workDir = _workDir ?? Directory.GetCurrentDirectory();
        var slnFiles = Directory.GetFiles(workDir, "*.sln", SearchOption.TopDirectoryOnly);
        if (slnFiles.Length == 0) return null; // no solution file — skip gate

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"build \"{slnFiles[0]}\" --no-restore -verbosity:quiet")
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0) return null;

            // Return just the error lines — trim noise
            var errors = string.Join('\n', (stdout + "\n" + stderr)
                .Split('\n')
                .Where(l => l.Contains("error") || l.Contains("Error") || l.Contains("FAILED"))
                .Take(30));
            return string.IsNullOrWhiteSpace(errors) ? $"dotnet build exited {proc.ExitCode}" : errors;
        }
        catch (Exception ex)
        {
            return $"Build check failed to run: {ex.Message}";
        }
    }
}
