using AgentLoop.Models;

namespace AgentLoop.Services;

/// <summary>
/// Appends timestamped entries to a single human-readable log file for the run.
/// Written to .agentloop/activity.log — one file, always fresh per run, easy to tail.
/// </summary>
public static class ActivityLog
{
    private static string? _path;
    private static readonly object _lock = new();

    public static void Init(string runId)
    {
        var dir = Path.Combine(StateManager.DotDir, "logs", runId);
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "activity.log");

        // Start fresh for this run
        File.WriteAllText(_path, $"[{Ts()}] ═══ AgentLoop started — run {runId} ═══\n");
    }

    public static void Sprint(string sprint, string branch) =>
        Write($"SPRINT  {sprint}  branch={branch}");

    public static void Phase(string phase) =>
        Write($"PHASE   {phase}");

    public static void TaskStart(string taskId, string title) =>
        Write($"TASK    {taskId} started  — {title}");

    public static void AgentStart(string taskId, string agent, int attempt) =>
        Write(attempt > 1
            ? $"AGENT   {taskId}/{agent} attempt {attempt}"
            : $"AGENT   {taskId}/{agent}");

    public static void AgentDone(string taskId, string agent, int attempt, bool ok, string? note = null) =>
        Write(ok
            ? $"OK      {taskId}/{agent}{(attempt > 1 ? $" attempt {attempt}" : "")} {note ?? ""}"
            : $"FAIL    {taskId}/{agent}{(attempt > 1 ? $" attempt {attempt}" : "")} {note ?? ""}");

    public static void AgentTimeout(string taskId, string agent, int timeoutMinutes, int retryNum) =>
        Write($"TIMEOUT {taskId}/{agent} exceeded {timeoutMinutes}m — restarting ({retryNum}/3)");

    public static void BuildGate(string taskId, bool ok, string? error = null) =>
        Write(ok
            ? $"BUILD   {taskId} OK"
            : $"BUILD   {taskId} FAILED — {error?.Split('\n')[0]}");

    public static void TaskDone(string taskId, Models.TaskStatus status) =>
        Write(status == Models.TaskStatus.Done
            ? $"DONE    {taskId}"
            : $"FAILED  {taskId} status={status}");

    public static void SmokeTest(bool ok, string? note = null) =>
        Write(ok ? $"SMOKE   OK {note}" : $"SMOKE   FAILED — {note}");

    public static void Info(string message) =>
        Write($"INFO    {message}");

    public static void Error(string message) =>
        Write($"ERROR   {message}");

    private static string Ts() => DateTime.UtcNow.ToString("HH:mm:ss");

    private static void Write(string line)
    {
        if (_path is null) return;
        var entry = $"[{Ts()}] {line}\n";
        lock (_lock)
        {
            try { File.AppendAllText(_path, entry); }
            catch { /* never crash the pipeline over logging */ }
        }
    }
}
