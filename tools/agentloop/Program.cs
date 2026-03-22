using System.CommandLine;
using AgentLoop.Models;
using AgentLoop.Services;
using Spectre.Console;

// ─── Plan command ──────────────────────────────────────────────────────

var planPrdOption = new Option<string>("--prd") { Description = "Path to the markdown PRD (e.g., docs/PRD.md)", Required = true };

var planCommand = new Command("plan", "Run the planning loop: Product Designer + PM plan all milestones")
{
    planPrdOption,
};

planCommand.SetAction(async (result, ct) =>
{
    var prdPath = result.GetValue(planPrdOption)!;
    var loop = new PlanningLoop(prdPath);
    await loop.ExecuteAsync();
});

// ─── Build command ─────────────────────────────────────────────────────

var buildPrdOption = new Option<string?>("--prd") { Description = "Sprint name to build (e.g., foundation)" };
var buildAllOption = new Option<bool>("--all") { Description = "Build ALL sprints in order, unattended" };
var resumeOption = new Option<bool>("--resume") { Description = "Resume a previous run" };
var branchOption = new Option<string?>("--branch") { Description = "Branch name prefix (e.g., trakpomo-mvp)" };
var yesOption = new Option<bool>("--yes") { Description = "Skip plan confirmation" };

var buildCommand = new Command("build", "Run the build loop in a worktree: executes sprints autonomously")
{
    buildPrdOption, buildAllOption, resumeOption, branchOption, yesOption,
};

buildCommand.SetAction(async (result, ct) =>
{
    var prdName = result.GetValue(buildPrdOption);
    var all = result.GetValue(buildAllOption);
    var resume = result.GetValue(resumeOption);
    var branchFlag = result.GetValue(branchOption);
    var yes = result.GetValue(yesOption);

    if (!all && string.IsNullOrEmpty(prdName))
    {
        AnsiConsole.MarkupLine("[red]Specify --prd <sprint-name> or --all to build all sprints.[/]");
        Environment.Exit(1);
        return;
    }

    // Git service for the main repo
    var mainGit = new GitService(Directory.GetCurrentDirectory());
    var baseBranch = await mainGit.GetCurrentBranchAsync();

    GitService wtGit;
    var worktreePrefix = branchFlag ?? (all ? "build-all" : prdName!);

    // Track whether we reused an existing worktree (branch already checked out — skip setup)
    var reusedWorktree = false;

    if (resume)
    {
        // Find the most recent existing worktree matching the prefix
        var wtDir = FindExistingWorktree(worktreePrefix);
        if (wtDir is not null)
        {
            wtGit = new GitService(wtDir);
            reusedWorktree = true;
            AnsiConsole.MarkupLine($"[rgb(99,102,241)]⬡[/]  [bold]Resuming in existing worktree[/]");
            AnsiConsole.MarkupLine($"[dim]  Work directory: {Markup.Escape(wtGit.WorkDir)}[/]");
            await CleanWorktreeAsync(wtGit.WorkDir);
        }
        else
        {
            // No existing worktree — create a new one and resume from state
            var wtName = $"{worktreePrefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            AnsiConsole.MarkupLine($"[rgb(99,102,241)]⬡[/]  [bold]Creating worktree (no existing found)[/]");
            wtGit = await mainGit.CreateWorktreeAsync(wtName, baseBranch);
            AnsiConsole.MarkupLine($"[dim]  Work directory: {Markup.Escape(wtGit.WorkDir)}[/]");
        }
    }
    else
    {
        var wtName = $"{worktreePrefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        AnsiConsole.MarkupLine($"[rgb(99,102,241)]⬡[/]  [bold]Creating worktree[/]");
        wtGit = await mainGit.CreateWorktreeAsync(wtName, baseBranch);
        AnsiConsole.MarkupLine($"[dim]  Work directory: {Markup.Escape(wtGit.WorkDir)}[/]");
    }
    Console.WriteLine();

    if (all)
    {
        // Discover sprint plans from main repo (they're committed)
        string[] sprintFiles;
        try
        {
            sprintFiles = PrdReader.DiscoverAllSprintPlans();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            Environment.Exit(1);
            return;
        }

        var sprintNames = sprintFiles
            .Select(f => PrdReader.ExtractSprintPrefix(f))
            .ToList();

        AnsiConsole.MarkupLine($"[rgb(99,102,241)]⬡[/]  [bold]Build Loop — Stacked Sprints[/]");
        AnsiConsole.MarkupLine($"[dim]  Base: {Markup.Escape(baseBranch)}[/]");
        AnsiConsole.MarkupLine($"[dim]  Found {sprintFiles.Length} sprint(s) to build[/]");
        for (var i = 0; i < sprintFiles.Length; i++)
        {
            var branchName = branchFlag is not null
                ? $"{branchFlag}/{sprintNames[i]}"
                : $"feature/{sprintNames[i]}";
            AnsiConsole.MarkupLine($"[dim]  {i + 1}. {Markup.Escape(sprintNames[i])} → {Markup.Escape(branchName)}[/]");
        }
        Console.WriteLine();

        // Execute sprints as a stack inside the worktree
        var parentBranch = baseBranch;
        for (var i = 0; i < sprintFiles.Length; i++)
        {
            var file = sprintFiles[i];
            var sprintBranch = branchFlag is not null
                ? $"{branchFlag}/{sprintNames[i]}"
                : $"feature/{sprintNames[i]}";

            AnsiConsole.MarkupLine($"[rgb(99,102,241)]━━━ Sprint {i + 1}/{sprintFiles.Length}: {Markup.Escape(sprintNames[i])} ━━━[/]");
            AnsiConsole.MarkupLine($"[dim]  {Markup.Escape(parentBranch)} → {Markup.Escape(sprintBranch)}[/]");
            Console.WriteLine();

            // Create stacked branch in the worktree — skip if resuming into existing worktree
            // (branch is already checked out there; trying again causes a git error)
            if (!reusedWorktree || i > 0)
                await wtGit.CreateStackedBranchAsync(sprintBranch, parentBranch);

            await RunSingleSprint(file, sprintBranch, resume, yes: true, wtGit.WorkDir);
            await CreateSprintPrAsync(sprintBranch, parentBranch, sprintNames[i], wtGit.WorkDir);

            parentBranch = sprintBranch;
        }

        Console.WriteLine();
        AnsiConsole.MarkupLine($"[green]  ✓  All {sprintFiles.Length} sprints complete.[/]");
        AnsiConsole.MarkupLine("[dim]  Branch stack:[/]");
        var stackParent = baseBranch;
        for (var i = 0; i < sprintNames.Count; i++)
        {
            var branchName = branchFlag is not null
                ? $"{branchFlag}/{sprintNames[i]}"
                : $"feature/{sprintNames[i]}";
            AnsiConsole.MarkupLine($"[dim]    {Markup.Escape(stackParent)} ← {Markup.Escape(branchName)}[/]");
            stackParent = branchName;
        }
        AnsiConsole.MarkupLine($"[dim]  Worktree: {Markup.Escape(wtGit.WorkDir)}[/]");
        Console.WriteLine();
    }
    else
    {
        // Single sprint in worktree
        var sprintBranch = branchFlag is not null
            ? $"{branchFlag}/{prdName}"
            : $"feature/{prdName}";

        // Single sprint — skip branch setup if resuming into existing worktree
        if (!reusedWorktree)
            await wtGit.CreateStackedBranchAsync(sprintBranch, baseBranch);
        await RunSingleSprint(prdName!, sprintBranch, resume, yes, wtGit.WorkDir);
        await CreateSprintPrAsync(sprintBranch, baseBranch, prdName!, wtGit.WorkDir);
    }
});

// ─── Root command ──────────────────────────────────────────────────────

var rootCommand = new RootCommand("agentloop — AI agent build loop orchestrator")
{
    planCommand,
    buildCommand,
};

return await rootCommand.Parse(args).InvokeAsync();

// ─── Sprint execution ──────────────────────────────────────────────────

static async Task RunSingleSprint(string sprintNameOrPath, string branch, bool resume, bool yes, string? workDir = null)
{
    Prd prd;
    try
    {
        prd = await PrdReader.ReadAsync(sprintNameOrPath);
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
        return;
    }

    StateManager.EnsureDotDir();

    var existing = await StateManager.LoadStateAsync(prd.Sprint);
    RunState state;
    if (existing is not null && (resume || existing.Tasks.Count > 0))
    {
        state = existing;
        AnsiConsole.MarkupLine($"[dim]Resuming run {state.RunId} ({state.Sprint})[/]");
    }
    else
    {
        state = StateManager.NewRunState(prd.Sprint, branch);
    }

    ActivityLog.Init(state.RunId);
    ActivityLog.Sprint(prd.Sprint, branch);

    foreach (var task in prd.Tasks)
        StateManager.EnsureTaskState(state, task.Id);
    await StateManager.SaveStateAsync(state);

    using var orchestrator = new Orchestrator(prd, state, yes, workDir);
    await orchestrator.ExecuteAsync();
}

// ─── Worktree helpers ──────────────────────────────────────────────────

/// <summary>
/// Pushes the sprint branch and creates a stacked PR on GitHub.
/// Skips gracefully if the PR already exists or git/gh is unavailable.
/// </summary>
static async Task CreateSprintPrAsync(string branch, string baseBranch, string sprintName, string workDir)
{
    AnsiConsole.MarkupLine($"  [dim]Pushing {Markup.Escape(branch)} and creating PR...[/]");
    try
    {
        // Push branch
        var pushPsi = new System.Diagnostics.ProcessStartInfo("git", $"push -u origin {branch}")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var pushProc = System.Diagnostics.Process.Start(pushPsi)!;
        await pushProc.WaitForExitAsync();

        // Load task completion stats from state
        var state = await StateManager.LoadStateAsync(sprintName);
        var total = state?.Tasks.Count ?? 0;
        var done = state?.Tasks.Values.Count(t => t.Status == AgentLoop.Models.TaskStatus.Done) ?? 0;
        var failed = state?.Tasks.Values.Count(t => t.Status == AgentLoop.Models.TaskStatus.Failed) ?? 0;

        var title = $"feat: Sprint — {sprintName} ({done}/{total} tasks)";
        var body = $"""
### TL;DR

Sprint `{sprintName}`: {done}/{total} tasks completed{(failed > 0 ? $", {failed} failed" : "")}.

### What changed?

See individual task commits on this branch.

### How to test?

1. `dotnet build` — solution should compile clean
2. Review task commits for specific changes

### Why make this change?

Automated sprint build via agentloop.

---

- [ ] My code follows the style guidelines of this project
- [ ] I have performed a self-review of my own code
- [ ] I have ensured there are no hard-to-understand areas that require inline comments
- [ ] My changes generate no new warnings
- [ ] I have added test steps
""";

        var prPsi = new System.Diagnostics.ProcessStartInfo("gh",
            $"pr create --title \"{title}\" --base {baseBranch} --head {branch} --body \"{body.Replace("\"", "\\\"")}\"")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var prProc = System.Diagnostics.Process.Start(prPsi)!;
        var stdout = await prProc.StandardOutput.ReadToEndAsync();
        var stderr = await prProc.StandardError.ReadToEndAsync();
        await prProc.WaitForExitAsync();

        if (prProc.ExitCode == 0)
            AnsiConsole.MarkupLine($"  [green]PR created: {Markup.Escape(stdout.Trim())}[/]");
        else if (stderr.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            AnsiConsole.MarkupLine($"  [dim]PR already exists for {Markup.Escape(branch)}[/]");
        else
            AnsiConsole.MarkupLine($"  [yellow]PR creation failed: {Markup.Escape(stderr.Trim())}[/]");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"  [yellow]PR creation skipped: {Markup.Escape(ex.Message)}[/]");
    }
}

/// <summary>
/// Discards any uncommitted changes in the worktree before resuming.
/// Tasks that were interrupted mid-run will be re-executed from scratch,
/// so their partial file changes are noise that blocks branch checkouts.
/// </summary>
static async Task CleanWorktreeAsync(string workDir)
{
    try
    {
        // Check if there's anything to clean first
        var statusPsi = new System.Diagnostics.ProcessStartInfo("git", "status --porcelain")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var statusProc = System.Diagnostics.Process.Start(statusPsi)!;
        var statusOut = await statusProc.StandardOutput.ReadToEndAsync();
        await statusProc.WaitForExitAsync();
        if (string.IsNullOrWhiteSpace(statusOut)) return;

        AnsiConsole.MarkupLine("[dim]  Discarding uncommitted changes from interrupted tasks...[/]");

        // Abort any in-progress merge/cherry-pick before resetting
        foreach (var abortCmd in new[] { "merge --abort", "cherry-pick --abort", "rebase --abort" })
        {
            var abortPsi = new System.Diagnostics.ProcessStartInfo("git", abortCmd)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var abortProc = System.Diagnostics.Process.Start(abortPsi)!;
            await abortProc.WaitForExitAsync(); // ignore exit code — no-op if none in progress
        }

        var resetPsi = new System.Diagnostics.ProcessStartInfo("git", "reset --hard HEAD")
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
        };
        using var resetProc = System.Diagnostics.Process.Start(resetPsi)!;
        await resetProc.WaitForExitAsync();

        var cleanPsi = new System.Diagnostics.ProcessStartInfo("git", "clean -fd")
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
        };
        using var cleanProc = System.Diagnostics.Process.Start(cleanPsi)!;
        await cleanProc.WaitForExitAsync();
    }
    catch { /* best-effort — don't block the resume */ }
}

/// <summary>
/// Finds the most recent existing worktree directory matching a prefix.
/// Looks in the sibling wt/ directory for folders starting with the prefix.
/// </summary>
static string? FindExistingWorktree(string prefix)
{
    var repoRoot = Directory.GetCurrentDirectory();
    var wtParent = Path.Combine(repoRoot, "..", "wt");
    wtParent = Path.GetFullPath(wtParent);

    if (!Directory.Exists(wtParent)) return null;

    // Find directories matching the prefix, sorted by name descending (most recent timestamp first).
    // Skip worktrees that are on auto-created wt/* branches — those are orphans from failed resumes.
    var candidates = Directory.GetDirectories(wtParent, $"{prefix}-*")
        .OrderByDescending(d => d);

    foreach (var dir in candidates)
    {
        // Check which branch this worktree is on
        var headFile = Path.Combine(dir, ".git");
        if (!File.Exists(headFile)) continue; // not a valid worktree

        // Read the HEAD via git to get the branch name
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "branch --show-current")
            {
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var branch = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();

            // Skip worktrees on auto-created wt/* branches (orphans from failed runs)
            if (branch.StartsWith("wt/")) continue;

            return dir;
        }
        catch { continue; }
    }

    return null;
}
