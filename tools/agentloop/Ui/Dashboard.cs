using System.Collections.Concurrent;
using System.Text;
using AgentLoop.Models;

namespace AgentLoop.Ui;

public sealed class Dashboard : IDisposable
{
    private Timer? _timer;
    private int _frame;
    private readonly object _lock = new();

    private const string HideCursor = "\x1b[?25l";
    private const string ShowCursor = "\x1b[?25h";
    private const string EnterAlt = "\x1b[?1049h";
    private const string ExitAlt = "\x1b[?1049l";
    private const string CursorHome = "\x1b[H";
    private const string ClearScreen = "\x1b[2J";

    // Box drawing
    private const char H = '─';
    private const string TL = "╭";
    private const string TR = "╮";
    private const string BL = "╰";
    private const string BR = "╯";

    public string NextFrame()
    {
        var f = Theme.SpinnerFrames[_frame % Theme.SpinnerFrames.Length];
        Interlocked.Increment(ref _frame);
        return f;
    }

    public void Start(Func<string> contentFn, int intervalMs = 120)
    {
        Console.Write(EnterAlt + HideCursor + ClearScreen + CursorHome);
        Render(contentFn());
        _timer = new Timer(_ =>
        {
            lock (_lock) { Render(contentFn()); }
        }, null, intervalMs, intervalMs);
    }

    public void Stop(string? finalContent = null)
    {
        _timer?.Dispose();
        _timer = null;
        Console.Write(ExitAlt);
        Console.Write(ShowCursor);
        if (finalContent is not null)
            Console.WriteLine(finalContent);
    }

    public void Dispose() => _timer?.Dispose();

    private static void Render(string content) =>
        Console.Write(CursorHome + content + "\n\x1b[J");

    // ─── Rendering helpers ──────────────────────────────────────

    public string BuildContent(
        string sprintName,
        RunState state,
        PrdTask[] tasks,
        ConcurrentDictionary<string, ActiveTaskDisplay> activeAgents,
        List<PrdTask[]> waves,
        int currentWave,
        string reasoning,
        string currentPhase)
    {
        var width = Theme.TermWidth;
        var sb = new StringBuilder();

        // Header box
        var phaseStr = currentPhase switch
        {
            "domain-modeling" => "domain modeling  ",
            "api-contract" => "api contract  ",
            "summary" => "summary  ",
            _ => waves.Count > 1 ? $"wave {currentWave}/{waves.Count}  " : "",
        };
        var doneCount = tasks.Count(t => StateManager.IsTaskDone(state, t.Id));
        var headerRight = $"{phaseStr}{doneCount} / {tasks.Length} tasks done  ";

        sb.AppendLine($"{TL}{new string(H, Math.Max(0, width - 2))}{TR}");
        sb.AppendLine(LeftRight($"  \x1b[38;2;99;102;241m{Theme.Icons.Logo} agentloop\x1b[0m           \x1b[1m{sprintName}\x1b[0m", $"\x1b[2m{headerRight}\x1b[0m", width));
        sb.AppendLine(LeftRight($"  Branch: \x1b[38;2;99;102;241m{state.Branch}\x1b[0m", "", width));
        if (!string.IsNullOrEmpty(reasoning))
        {
            var maxLen = width - 6;
            var truncated = reasoning.Length > maxLen ? reasoning[..(maxLen - 1)] + "…" : reasoning;
            sb.AppendLine($"\x1b[2m  Plan: {truncated}\x1b[0m");
        }
        sb.AppendLine($"{BL}{new string(H, Math.Max(0, width - 2))}{BR}");
        sb.AppendLine();

        // Sprint phases
        var dmIcon = PhaseIcon(state.Phases.DomainModeling, currentPhase == "domain-modeling" ? NextFrame() : null);
        var apiIcon = PhaseIcon(state.Phases.ApiContract, currentPhase == "api-contract" ? NextFrame() : null);
        sb.AppendLine("\x1b[2m  sprint phases\x1b[0m");
        sb.AppendLine($"   {dmIcon}  \x1b[2mdomain-modeler\x1b[0m");
        sb.AppendLine($"   {apiIcon}  \x1b[2mapi-developer\x1b[0m");
        sb.AppendLine();

        // Tasks
        var idWidth = 10;
        var timeWidth = 9;
        var titleWidth = Math.Max(10, width - idWidth - timeWidth - 6);

        void RenderTask(PrdTask task)
        {
            var ts = state.Tasks.GetValueOrDefault(task.Id);
            var status = ts?.Status ?? Models.TaskStatus.Pending;
            var elapsed = Theme.FormatElapsed(ts?.StartedAt);
            var isActive = activeAgents.Keys.Any(k => k.StartsWith($"{task.Id}:"));
            var (icon, dim) = TaskIcon(status, isActive ? NextFrame() : null);
            var title = Theme.FixedWidth(task.Title, titleWidth);
            var id = Theme.FixedWidth(task.Id, idWidth);

            if (dim)
                sb.AppendLine($"   {icon}  \x1b[2m{id}  {title}  {elapsed}\x1b[0m");
            else
                sb.AppendLine($"   {icon}  \x1b[2m{id}\x1b[0m  {title}  \x1b[2m{elapsed}\x1b[0m");
        }

        if (waves.Count > 1)
        {
            for (var i = 0; i < waves.Count; i++)
            {
                var waveNum = i + 1;
                var label = waveNum < currentWave ? $"wave {waveNum}"
                    : waveNum == currentWave ? $"wave {waveNum}  ←"
                    : $"wave {waveNum}  (blocked)";
                sb.AppendLine($"\x1b[2m  {label}\x1b[0m");
                foreach (var t in waves[i]) RenderTask(t);
                sb.AppendLine();
            }
        }
        else
        {
            foreach (var t in tasks) RenderTask(t);
            sb.AppendLine();
        }

        // Divider
        sb.AppendLine();
        sb.AppendLine($"\x1b[2m  {new string(H, Math.Max(0, width - 4))}\x1b[0m");

        // Active agents
        if (activeAgents.IsEmpty)
        {
            sb.AppendLine("\x1b[2m  Idle\x1b[0m");
        }
        else
        {
            foreach (var active in activeAgents.Values)
            {
                var spin = NextFrame();
                var attemptStr = active.Agent is "review-agent" or "destroyer"
                    ? $"\x1b[2m attempt {active.Attempt}/6\x1b[0m" : "";
                var kb = active.BytesWritten > 0 ? $"{(active.BytesWritten + 1023) / 1024}KB" : "";
                var elapsed = Theme.FormatElapsed(active.StartedAt);
                var statusWidth = Math.Max(10, width - 75);
                var statusText = Theme.FixedWidth(active.Status, statusWidth);
                sb.AppendLine($"  \x1b[1m{Theme.FixedWidth(active.TaskId, 10)}\x1b[0m  \x1b[38;2;99;102;241m{Theme.FixedWidth(active.Agent, 18)}\x1b[0m{attemptStr}  \x1b[38;2;99;102;241m{spin}\x1b[0m  {statusText}  \x1b[2m{kb}  {elapsed}\x1b[0m");
            }
        }

        sb.AppendLine($"\x1b[2m  {new string(H, Math.Max(0, width - 4))}\x1b[0m");
        return sb.ToString();
    }

    private static string PhaseIcon(SprintPhaseStatus status, string? spinnerFrame) => status switch
    {
        SprintPhaseStatus.Done => $"\x1b[38;2;34;197;94m{Theme.Icons.Done}\x1b[0m",
        SprintPhaseStatus.Failed => $"\x1b[38;2;239;68;68m{Theme.Icons.Failed}\x1b[0m",
        SprintPhaseStatus.Running => $"\x1b[38;2;99;102;241m{spinnerFrame ?? Theme.Icons.Pending}\x1b[0m",
        _ => $"\x1b[2m{Theme.Icons.Pending}\x1b[0m",
    };

    private static (string icon, bool dim) TaskIcon(Models.TaskStatus status, string? spinnerFrame) => status switch
    {
        Models.TaskStatus.Done => ($"\x1b[38;2;34;197;94m{Theme.Icons.Done}\x1b[0m", true),
        Models.TaskStatus.Failed => ($"\x1b[38;2;239;68;68m{Theme.Icons.Failed}\x1b[0m", false),
        Models.TaskStatus.Pending => ($"\x1b[2m{Theme.Icons.Pending}\x1b[0m", true),
        _ => ($"\x1b[38;2;99;102;241m{spinnerFrame ?? Theme.Icons.Pending}\x1b[0m", false),
    };

    private static string LeftRight(string left, string right, int width)
    {
        var leftLen = StripAnsi(left).Length;
        var rightLen = StripAnsi(right).Length;
        var gap = Math.Max(1, width - leftLen - rightLen);
        return $"{left}{new string(' ', gap)}{right}";
    }

    private static string StripAnsi(string str) =>
        System.Text.RegularExpressions.Regex.Replace(str, @"\x1B\[[0-9;]*m", "");
}
