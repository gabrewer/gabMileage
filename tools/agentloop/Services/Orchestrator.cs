using System.Collections.Concurrent;
using AgentLoop.Models;
using AgentLoop.Ui;
using Spectre.Console;

namespace AgentLoop.Services;

/// <summary>
/// Top-level orchestration: sprint phases → task waves → PM summary.
/// </summary>
public sealed class Orchestrator : IDisposable
{
    private readonly Prd _prd;
    private readonly RunState _state;
    private readonly bool _skipApproval;
    private readonly string? _workDir;
    private readonly GitHubService _github;
    private readonly Dashboard _dashboard = new();
    private readonly ConcurrentDictionary<string, ActiveTaskDisplay> _activeAgents = new();

    private List<PrdTask[]> _waves = [];
    private int _currentWave;
    private string _currentPhase = "domain-modeling";
    private string _reasoning = "";

    public Orchestrator(Prd prd, RunState state, bool skipApproval, string? workDir = null)
    {
        _prd = prd;
        _state = state;
        _skipApproval = skipApproval;
        _workDir = workDir;
        _github = new GitHubService(workDir);
    }

    public async Task ExecuteAsync()
    {
        var pendingTasks = _prd.Tasks.Where(t => !StateManager.IsTaskTerminal(_state, t.Id)).ToArray();

        // ─── Plan ────────────────────────────────────────────
        var plan = await PlanAsync(pendingTasks);
        _waves = ExecutionPlanner.BuildWaves(pendingTasks, plan.Dependencies);
        _reasoning = plan.Reasoning;

        PrintPlan();

        if (!_skipApproval)
        {
            while (true)
            {
                var answer = AnsiConsole.Ask<string>(
                    "[dim]Proceed with this plan? (enter to confirm, or describe changes)[/]");
                if (string.IsNullOrWhiteSpace(answer)) break;

                plan = await PlanAsync(pendingTasks, answer);
                _waves = ExecutionPlanner.BuildWaves(pendingTasks, plan.Dependencies);
                _reasoning = plan.Reasoning;
                PrintPlan();
            }
        }

        Console.WriteLine();

        // ─── GitHub Issues ────────────────────────────────────
        await EnsureGitHubIssuesAsync();

        // ─── Dashboard ───────────────────────────────────────
        Console.CancelKeyPress += OnCancel;
        _dashboard.Start(RenderDashboard);

        try
        {
            await RunSprintPhasesAsync();
            await RunTaskWavesAsync();
            await RunSmokeTestAsync();
            await RunPmSummaryAsync();
        }
        finally
        {
            var final = RenderDashboard();
            _dashboard.Stop(final);
            Console.CancelKeyPress -= OnCancel;
        }

        PrintResults();
    }

    // ─── Sprint phases ───────────────────────────────────────────

    private async Task RunSprintPhasesAsync()
    {
        var phases = _prd.EffectivePhases;

        if (phases.DomainModeling && _state.Phases.DomainModeling != SprintPhaseStatus.Done)
            await RunSprintPhase("domain-modeling", "domain-modeler",
                "Defining domain model...",
                PromptBuilder.BuildDomainModelPrompt(_prd),
                s => s.Phases.DomainModeling, (s, v) => s.Phases.DomainModeling = v);
        else if (!phases.DomainModeling)
            _state.Phases.DomainModeling = SprintPhaseStatus.Done;

        if (phases.ApiContract && _state.Phases.ApiContract != SprintPhaseStatus.Done)
            await RunSprintPhase("api-contract", "api-developer",
                "Defining API contract...",
                PromptBuilder.BuildApiContractPrompt(_prd),
                s => s.Phases.ApiContract, (s, v) => s.Phases.ApiContract = v);
        else if (!phases.ApiContract)
            _state.Phases.ApiContract = SprintPhaseStatus.Done;
    }

    private async Task RunSprintPhase(
        string phase, string agent, string statusText, string description,
        Func<RunState, SprintPhaseStatus> getStatus,
        Action<RunState, SprintPhaseStatus> setStatus)
    {
        _currentPhase = phase;
        setStatus(_state, SprintPhaseStatus.Running);
        await StateManager.SaveStateAsync(_state);

        var display = new ActiveTaskDisplay
        {
            TaskId = "sprint", Agent = agent, Status = statusText,
        };
        var key = $"sprint:{agent}";
        _activeAgents[key] = display;

        var task = new PrdTask($"sprint-{phase}", $"{agent} — {_prd.Sprint}",
            TaskType.Both, description, []);

        var result = await AgentRunner.RunAsync(_state.RunId, task, agent, 1, display, workDir: _workDir);
        _activeAgents.TryRemove(key, out _);

        setStatus(_state, result.Approved ? SprintPhaseStatus.Done : SprintPhaseStatus.Failed);
        await StateManager.SaveStateAsync(_state);

        if (!result.Approved)
        {
            _dashboard.Stop(RenderDashboard());
            AnsiConsole.MarkupLine($"\n  [red]{agent} failed. Check .agentloop/logs/ for details.[/]\n");
            Environment.Exit(1);
        }
    }

    // ─── Task waves ──────────────────────────────────────────────

    private async Task RunTaskWavesAsync()
    {
        _currentPhase = "tasks";
        var runner = new TaskRunner(_state, _activeAgents, _github, _workDir);

        foreach (var wave in _waves)
        {
            _currentWave++;
            await Task.WhenAll(wave.Select(task => runner.RunAsync(task)));
        }
    }

    // ─── Sprint smoke test ───────────────────────────────────────

    private async Task RunSmokeTestAsync()
    {
        if (_state.Phases.SmokeTest == SprintPhaseStatus.Done) return;

        _currentPhase = "smoke-test";
        AnsiConsole.MarkupLine("  [rgb(99,102,241)]smoke test[/]");

        var workDir = _workDir ?? Directory.GetCurrentDirectory();
        var slnFiles = Directory.GetFiles(workDir, "*.sln", SearchOption.TopDirectoryOnly);
        if (slnFiles.Length == 0)
        {
            AnsiConsole.MarkupLine("  [yellow]  ⚠ No .sln found — skipping smoke test[/]");
            _state.Phases.SmokeTest = SprintPhaseStatus.Done;
            await StateManager.SaveStateAsync(_state);
            return;
        }

        // Full solution build
        AnsiConsole.MarkupLine("  [dim]  dotnet build...[/]");
        var buildOk = await RunProcessAsync("dotnet", $"build \"{slnFiles[0]}\" --no-restore -verbosity:quiet", workDir);
        if (!buildOk)
        {
            _state.Phases.SmokeTest = SprintPhaseStatus.Failed;
            await StateManager.SaveStateAsync(_state);
            _dashboard.Stop(RenderDashboard());
            AnsiConsole.MarkupLine("\n  [red]✗  Smoke test FAILED: solution does not build. Sprint is complete but the app will not run.[/]");
            AnsiConsole.MarkupLine("  [dim]  Check .agentloop/logs/ and fix compilation errors before proceeding.[/]\n");
            return;
        }

        // Full test suite
        AnsiConsole.MarkupLine("  [dim]  dotnet test...[/]");
        var testOk = await RunProcessAsync("dotnet", $"test \"{slnFiles[0]}\" --no-build --no-restore -verbosity:quiet", workDir);
        if (!testOk)
        {
            _state.Phases.SmokeTest = SprintPhaseStatus.Failed;
            await StateManager.SaveStateAsync(_state);
            AnsiConsole.MarkupLine("\n  [yellow]⚠  Smoke test: solution builds but tests are failing. App may run with degraded behaviour.[/]\n");
            // Don't halt — let PM summary still run so there's a record
            return;
        }

        _state.Phases.SmokeTest = SprintPhaseStatus.Done;
        await StateManager.SaveStateAsync(_state);
        AnsiConsole.MarkupLine("  [green]  ✓  Smoke test passed — solution builds and all tests pass[/]");
    }

    private static async Task<bool> RunProcessAsync(string cmd, string args, string workDir)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(cmd, args)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    // ─── PM summary ──────────────────────────────────────────────

    private async Task RunPmSummaryAsync()
    {
        if (_state.Phases.PmSummary == SprintPhaseStatus.Done) return;

        _currentPhase = "summary";
        var display = new ActiveTaskDisplay
        {
            TaskId = "sprint", Agent = "pm", Status = "Writing sprint summary...",
        };
        _activeAgents["sprint:pm"] = display;

        var task = new PrdTask("sprint", _prd.Sprint, TaskType.Both, _prd.Description, []);
        await AgentRunner.RunAsync(_state.RunId, task, "pm", 1, display, workDir: _workDir);
        _activeAgents.TryRemove("sprint:pm", out _);

        _state.Phases.PmSummary = SprintPhaseStatus.Done;
        await StateManager.SaveStateAsync(_state);
    }

    // ─── GitHub Issues ──────────────────────────────────────────────

    private async Task EnsureGitHubIssuesAsync()
    {
        // Already have all issues in state — fully complete
        var allTasksHaveIssues = _prd.Tasks.All(t => _state.Issues.TaskIssues.ContainsKey(t.Id));
        if (_state.Issues.EpicIssue.HasValue && allTasksHaveIssues)
        {
            AnsiConsole.MarkupLine($"[dim]  Using existing epic issue #{_state.Issues.EpicIssue}[/]");
            return;
        }

        // Partial state from a previous failed run — skip straight to creation to fill in the gaps
        if (_state.Issues.EpicIssue.HasValue)
            AnsiConsole.MarkupLine($"[dim]  Resuming issue creation from epic #{_state.Issues.EpicIssue} ({_state.Issues.TaskIssues.Count}/{_prd.Tasks.Length} task issues exist)[/]");

        // Check if the plan loop already created issues on GitHub
        try
        {
            var existing = await _github.FindExistingSprintIssuesAsync(_prd.Sprint);
            if (existing is not null && existing.EpicIssue.HasValue)
            {
                _state.Issues = existing;
                await StateManager.SaveStateAsync(_state);
                AnsiConsole.MarkupLine($"[dim]  Found existing epic issue #{existing.EpicIssue} with {existing.TaskIssues.Count} task issues[/]");
                return;
            }
        }
        catch { /* fall through to create */ }

        // Create new issues — save partial progress after each issue so resume is safe
        AnsiConsole.MarkupLine("[dim]  Creating GitHub issues...[/]");
        var issueDelays = new[] { 10_000, 30_000, 60_000 };
        Exception? issueEx = null;
        for (var attempt = 0; attempt <= issueDelays.Length; attempt++)
        {
            try
            {
                var map = await _github.CreateSprintIssuesAsync(_prd, _state.Branch,
                    async partial =>
                    {
                        _state.Issues = partial;
                        await StateManager.SaveStateAsync(_state);
                    },
                    existing: _state.Issues.EpicIssue.HasValue ? _state.Issues : null);
                _state.Issues = map;
                await StateManager.SaveStateAsync(_state);
                AnsiConsole.MarkupLine($"[dim]  Epic: #{map.EpicIssue}, {map.TaskIssues.Count} task issues created[/]");
                issueEx = null;
                break;
            }
            catch (Exception ex)
            {
                issueEx = ex;
                if (attempt < issueDelays.Length)
                {
                    AnsiConsole.MarkupLine($"[yellow]  GitHub issue creation failed (attempt {attempt + 1}): {Markup.Escape(ex.Message)}[/]");
                    AnsiConsole.MarkupLine($"[dim]  Retrying in {issueDelays[attempt] / 1000}s...[/]");
                    await Task.Delay(issueDelays[attempt]);
                }
            }
        }

        if (issueEx is not null)
        {
            _dashboard.Stop(RenderDashboard());
            AnsiConsole.MarkupLine($"\n  [red]✗  GitHub issue creation failed after {issueDelays.Length + 1} attempts: {Markup.Escape(issueEx.Message)}[/]");
            AnsiConsole.MarkupLine("  [dim]  Fix the GitHub auth/network issue and re-run with --resume.[/]\n");
            Environment.Exit(1);
        }
    }

    // ─── Display ─────────────────────────────────────────────────

    private string RenderDashboard() =>
        _dashboard.BuildContent(_prd.Sprint, _state, _prd.Tasks,
            _activeAgents, _waves, _currentWave, _reasoning, _currentPhase);

    private void PrintPlan()
    {
        var width = Math.Min(Theme.TermWidth, 80);
        var phases = _prd.EffectivePhases;

        Console.WriteLine();
        AnsiConsole.MarkupLine($"[dim]  {new string('─', width - 4)}[/]");
        AnsiConsole.MarkupLine($"  [rgb(99,102,241)]⬡[/]  [bold]Execution plan[/]");
        if (!string.IsNullOrEmpty(_reasoning))
            AnsiConsole.MarkupLine($"[dim]  {Markup.Escape(_reasoning)}[/]");
        Console.WriteLine();

        if (phases.DomainModeling || phases.ApiContract)
        {
            AnsiConsole.MarkupLine("  [rgb(99,102,241)]sprint phases[/]");
            if (phases.DomainModeling) AnsiConsole.MarkupLine("    [dim]·[/]  [bold]domain-modeler[/]  Define domain model");
            if (phases.ApiContract) AnsiConsole.MarkupLine("    [dim]·[/]  [bold]api-developer[/]  Define API contract");
            AnsiConsole.MarkupLine("[dim]      ↓[/]");
        }

        for (var i = 0; i < _waves.Count; i++)
        {
            var parallel = _waves[i].Length > 1 ? "[dim]  parallel[/]" : "";
            AnsiConsole.MarkupLine($"  [rgb(99,102,241)]wave {i + 1}[/]{parallel}");
            foreach (var t in _waves[i])
                AnsiConsole.MarkupLine($"    [dim]·[/]  [bold]{Markup.Escape(t.Id)}[/]  {Markup.Escape(t.Title)}");
            if (i < _waves.Count - 1)
                AnsiConsole.MarkupLine("[dim]      ↓[/]");
        }

        Console.WriteLine();
        AnsiConsole.MarkupLine("[dim]  per task: test-writer → builders → destroyer → review-agent → git-committer[/]");
        Console.WriteLine();
        AnsiConsole.MarkupLine($"[dim]  {new string('─', width - 4)}[/]");
        Console.WriteLine();
    }

    private void PrintResults()
    {
        var doneCount = _prd.Tasks.Count(t => StateManager.IsTaskDone(_state, t.Id));
        var failedCount = _prd.Tasks.Length - doneCount;
        Console.WriteLine();
        if (failedCount == 0)
            AnsiConsole.MarkupLine($"  [green]✓  All {doneCount} tasks complete.[/]");
        else
            AnsiConsole.MarkupLine(
                $"  [yellow]✓  {doneCount} done, [red]{failedCount} failed[/].  Check .agentloop/logs/ for details.[/]");
        Console.WriteLine();
    }

    private void OnCancel(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _dashboard.Stop();
        Environment.Exit(0);
    }

    public void Dispose() => _dashboard.Dispose();

    private async Task<ExecutionPlan> PlanAsync(PrdTask[] tasks, string? feedback = null)
    {
        var label = feedback is not null ? "Revising plan…" : "Planning execution order…";
        AnsiConsole.MarkupLine($"[dim]  {label}[/]");
        return await ExecutionPlanner.PlanAsync(tasks, _prd.Sprint, feedback);
    }
}
