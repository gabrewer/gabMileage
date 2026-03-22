using AgentLoop.Models;
using Spectre.Console;

namespace AgentLoop.Services;

/// <summary>
/// Planning loop: Product Designer + PM collaborate to plan ALL milestones.
/// If they hit ambiguity, they write questions to a timestamped file.
/// User answers questions and re-runs. When no questions remain, sprint JSONs are produced.
/// All output files include a timestamp to prevent collisions across runs.
/// </summary>
public sealed class PlanningLoop
{
    private readonly string _prdPath;
    private readonly string _runTimestamp;

    // Answers file is NOT timestamped — the user creates it once and it persists across re-runs
    private const string AnswersPath = "docs/sprints/answers.md";

    public PlanningLoop(string prdPath)
    {
        _prdPath = prdPath;
        _runTimestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    }

    private string QuestionsPath => $"docs/sprints/questions-{_runTimestamp}.md";
    private string BriefSuffix => $"-brief-{_runTimestamp}.md";
    private string PrdSuffix => $"-{_runTimestamp}.json";

    /// <summary>
    /// Runs an agent and shows a live status line while it works.
    /// </summary>
    private static async Task<AgentResult> RunWithStatusAsync(
        string runId, PrdTask task, string agentName, ActiveTaskDisplay display)
    {
        using var cts = new CancellationTokenSource();
        var agentTask = AgentRunner.RunAsync(runId, task, agentName, 1, display);

        // Live status printer — updates every second while the agent runs
        var statusTask = Task.Run(async () =>
        {
            var spinner = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            var frame = 0;
            var lastStatus = "";
            var width = Math.Min(Console.WindowWidth - 6, 80);

            while (!cts.Token.IsCancellationRequested)
            {
                var status = display.Status;
                var bytes = display.BytesWritten;
                var kb = bytes > 0 ? $" [dim]{bytes / 1024}kb[/]" : "";
                var truncated = status.Length > width ? status[..(width - 1)] + "…" : status;

                if (status != lastStatus || frame % 10 == 0)
                {
                    AnsiConsole.Markup($"\r  [rgb(99,102,241)]{spinner[frame % spinner.Length]}[/]  {Markup.Escape(truncated)}{kb}   ");
                    lastStatus = status;
                }

                frame++;
                try { await Task.Delay(200, cts.Token); } catch (OperationCanceledException) { break; }
            }
        }, cts.Token);

        var result = await agentTask;
        await cts.CancelAsync();
        try { await statusTask; } catch { /* expected on cancel */ }

        // Clear the status line
        AnsiConsole.Markup($"\r{new string(' ', Math.Min(Console.WindowWidth - 1, 120))}\r");

        return result;
    }

    public async Task ExecuteAsync()
    {
        StateManager.EnsureDotDir();
        var runId = _runTimestamp.Replace("-", "");

        var hasAnswers = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), AnswersPath));

        AnsiConsole.MarkupLine($"[rgb(99,102,241)]⬡[/]  [bold]Planning Loop[/]");
        AnsiConsole.MarkupLine($"[dim]  PRD: {Markup.Escape(_prdPath)}[/]");
        AnsiConsole.MarkupLine($"[dim]  Run: {Markup.Escape(_runTimestamp)}[/]");
        if (hasAnswers)
            AnsiConsole.MarkupLine($"[dim]  Answers: {Markup.Escape(AnswersPath)} (found)[/]");
        Console.WriteLine();

        // ─── Step 1: Product Designer ────────────────────────
        AnsiConsole.MarkupLine("[bold]Step 1:[/] Product Designer — expanding all milestones");

        var designerDisplay = new ActiveTaskDisplay
        {
            TaskId = "plan", Agent = "product-designer",
            Status = "Expanding milestone requirements...",
        };

        var designerTask = new PrdTask(
            "plan-design", "Sprint Briefs", TaskType.Both,
            BuildDesignerPrompt(hasAnswers), []);

        var designerResult = await RunWithStatusAsync(runId, designerTask, "product-designer", designerDisplay);

        if (!designerResult.Approved)
        {
            AnsiConsole.MarkupLine("[red]  Product Designer failed. Check .agentloop/logs/ for details.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[green]  ✓[/]  Sprint briefs written to [bold]docs/sprints/[/]");
        Console.WriteLine();

        // ─── Step 2: PM ──────────────────────────────────────
        AnsiConsole.MarkupLine("[bold]Step 2:[/] PM — breaking all milestones into sprint plans");

        var pmDisplay = new ActiveTaskDisplay
        {
            TaskId = "plan", Agent = "pm",
            Status = "Breaking down sprints into tasks...",
        };

        var pmTask = new PrdTask(
            "plan-pm", "Sprint Plans", TaskType.Both,
            BuildPmPrompt(hasAnswers), []);

        var pmResult = await RunWithStatusAsync(runId, pmTask, "pm", pmDisplay);

        if (!pmResult.Approved)
        {
            AnsiConsole.MarkupLine("[red]  PM failed. Check .agentloop/logs/ for details.[/]");
            return;
        }

        // ─── Check for questions ─────────────────────────────
        var questionsFile = Path.Combine(Directory.GetCurrentDirectory(), QuestionsPath);
        if (File.Exists(questionsFile))
        {
            var content = await File.ReadAllTextAsync(questionsFile);
            if (!string.IsNullOrWhiteSpace(content) && !content.Contains("NO QUESTIONS", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine();
                AnsiConsole.MarkupLine("[yellow]  The agents have questions that need answers before the plan is complete.[/]");
                AnsiConsole.MarkupLine($"[yellow]  Questions: [bold]{Markup.Escape(QuestionsPath)}[/][/]");
                Console.WriteLine();
                AnsiConsole.MarkupLine("[dim]  1. Read the questions[/]");
                AnsiConsole.MarkupLine($"[dim]  2. Write your answers to [bold]{Markup.Escape(AnswersPath)}[/][/]");
                AnsiConsole.MarkupLine("[dim]  3. Re-run: [bold]dotnet run -- plan --prd docs/PRD.md[/][/]");
                Console.WriteLine();
                return;
            }
        }

        // ─── GitHub Issues ─────────────────────────────────────
        Console.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 3:[/] Creating GitHub issues for each sprint");

        var github = new GitHubService();
        string[] sprintFiles;
        try
        {
            sprintFiles = PrdReader.DiscoverAllSprintPlans();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]  Warning: Could not discover sprint plans: {Markup.Escape(ex.Message)}[/]");
            sprintFiles = [];
        }

        foreach (var file in sprintFiles)
        {
            try
            {
                var prd = await PrdReader.ReadAsync(file);
                var branch = $"feature/{PrdReader.ExtractSprintPrefix(file)}";
                var map = await github.CreateSprintIssuesAsync(prd, branch);
                AnsiConsole.MarkupLine($"[green]  ✓[/]  {prd.Sprint}: epic #{map.EpicIssue}, {map.TaskIssues.Count} task issues");
            }
            catch (Exception ex)
            {
                var sprintName = PrdReader.ExtractSprintPrefix(file);
                AnsiConsole.MarkupLine($"[yellow]  Warning: Failed to create issues for {Markup.Escape(sprintName)}: {Markup.Escape(ex.Message)}[/]");
            }
        }

        // ─── Done ────────────────────────────────────────────
        Console.WriteLine();
        AnsiConsole.MarkupLine("[green]  ✓[/]  All sprint plans written to [bold]docs/sprints/[/]");
        Console.WriteLine();
        AnsiConsole.MarkupLine("[dim]  Review the plans, then run the build loop:[/]");
        AnsiConsole.MarkupLine("[bold]  dotnet run -- build --all[/]");
        Console.WriteLine();
    }

    private string BuildDesignerPrompt(bool hasAnswers)
    {
        var answersContext = hasAnswers
            ? $"""

                ## Previous Questions Answered

                The user has answered your previous questions. Read `{AnswersPath}` for their answers.
                Use these answers to resolve the ambiguities you flagged previously.
                Also read any previous sprint briefs in `docs/sprints/` to avoid duplicating work —
                build on what was already produced and refine it.
                """
            : "";

        return $"""
            # Plan All Milestones

            ## Your Mission

            Read the master PRD at `{_prdPath}`. Expand **every milestone** into a detailed sprint brief.

            Create the `docs/sprints/` directory if it doesn't exist.

            For each milestone, write a sprint brief to `docs/sprints/<NN>-<sprint-name>{BriefSuffix}`.
            Prefix with a zero-padded milestone number to indicate implementation order.
            Use a short kebab-case name derived from the milestone.
            Examples:
            - "Milestone 1 — Foundation" → `01-foundation`
            - "Milestone 2 — The Weekly/Daily Loop" → `02-weekly-daily`
            - "Milestone 3 — The Pomodoro Engine" → `03-pomodoro-engine`
            {answersContext}

            ## What each sprint brief should cover

            - **User stories** for each feature in the milestone
            - **Screen descriptions** — what the user sees, what actions are available, mobile vs desktop
            - **Interaction details** — forms, validation, navigation, loading/empty/error states
            - **Edge cases and decisions** — resolve ambiguity, don't leave it open
            - **Out of scope** — what's explicitly not in this milestone
            - **Dependencies** — what must exist from prior milestones

            ## Questions

            If you encounter ambiguity that you cannot resolve from the PRD alone,
            write your questions to `{QuestionsPath}`.

            Format each question clearly with context about why you're asking:

            ```markdown
            ## Question 1: <topic>
            <context about what you're trying to decide>
            <the specific question>
            ```

            If you have NO questions, write `NO QUESTIONS` to `{QuestionsPath}`.

            ## Rules

            - Plan ALL milestones, not just the first one
            - Be specific — make UX decisions, don't leave options open
            - Each brief should stand alone but note dependencies on prior milestones
            - Read the full PRD before starting — context matters
            """;
    }

    private string BuildPmPrompt(bool hasAnswers)
    {
        var answersContext = hasAnswers
            ? $"""

                ## Previous Questions Answered

                Read `{AnswersPath}` for answers to previously flagged questions.
                """
            : "";

        var jsonExample = """
            {
              "sprint": "<sprint-name>",
              "description": "<one paragraph describing what this sprint delivers>",
              "createdAt": "<ISO timestamp>",
              "order": 1,
              "phases": {
                "domainModeling": true,
                "apiContract": true
              },
              "tasks": [
                {
                  "id": "TASK-001",
                  "title": "<short descriptive title>",
                  "type": "backend | frontend | both",
                  "description": "<detailed description>",
                  "acceptanceCriteria": [
                    "<specific, testable criterion>"
                  ]
                }
              ]
            }
            """;

        return $"""
            # Create Sprint Plans for All Milestones

            ## Your Mission

            Read the following inputs:
            1. The master PRD at `{_prdPath}`
            2. ALL sprint briefs in `docs/sprints/` — find the most recent brief for each sprint
               (files ending in `{BriefSuffix}` are from this run)

            For each sprint brief, produce a structured sprint plan JSON file.
            Use the same `<NN>-<sprint-name>` prefix from the brief filename.
            Output to `docs/sprints/<NN>-<sprint-name>{PrdSuffix}`.
            Example: brief `01-foundation{BriefSuffix}` → plan `01-foundation{PrdSuffix}`.

            Create the `docs/sprints/` directory if it doesn't exist.
            {answersContext}

            ## JSON Format

            Each sprint JSON must follow this exact format:

            ```json
            {jsonExample}
            ```

            ## Task Rules

            - **id**: Sequential within each sprint (TASK-001, TASK-002, etc.)
            - **type**: `backend` for .NET work, `frontend` for TanStack/React, `both` for full-stack tasks
            - **description**: Detailed enough that a builder agent can implement without guessing. Include which vertical slice, key patterns, and technical context.
            - **acceptanceCriteria**: Specific, testable conditions. These become the test-writer's input.
            - Each task should be completable by a single agent in one focused session
            - Respect vertical slice boundaries — no task crosses slice data boundaries
            - Set `domainModeling` and `apiContract` phases to true unless the sprint has no domain/API changes
            - **order**: Set to the milestone number (1 for Milestone 1, 2 for Milestone 2, etc.). This determines the build execution sequence — sprints run in order and each builds on the previous sprint's code.

            ## Questions

            If the sprint briefs have unresolved ambiguity that prevents you from creating clear tasks,
            **append** your questions to `{QuestionsPath}` (don't overwrite the Product Designer's questions).

            If you have no additional questions, do not modify the questions file.

            ## Rules

            - Create sprint JSONs for ALL sprint briefs, not just one
            - Do not invent requirements beyond what's in the PRD and sprint briefs
            - Keep tasks small — prefer many small tasks over few large ones
            - The build loop handles parallelism, so splitting is free
            """;
    }
}
