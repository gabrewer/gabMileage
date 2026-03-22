using AgentLoop.Models;

namespace AgentLoop.Services;

public static class PromptBuilder
{
    public static string BuildAgentPrompt(PrdTask task, string agent, string? lastFeedback = null)
    {
        var criteria = task.AcceptanceCriteria.Length > 0
            ? string.Join('\n', task.AcceptanceCriteria.Select((c, i) => $"{i + 1}. {c}"))
            : null;

        var @base = criteria is not null
            ? $"# Task: {task.Id} — {task.Title}\n\n{task.Description}\n\n## Acceptance Criteria\n{criteria}"
            : $"# Task: {task.Id} — {task.Title}\n\n{task.Description}";

        if (lastFeedback is not null && agent is "backend-builder" or "frontend-builder")
            return $"{@base}\n\n## Review Feedback to Address\n{lastFeedback}";

        if (agent == "destroyer")
            return $"{@base}\n\n## Your Mission\nReview the code that was just written for this task. Write adversarial tests. Try to break things. Report all findings.";

        if (agent == "review-agent")
        {
            var context = lastFeedback is not null
                ? $"\n\n## Destroyer Findings to Triage\n{lastFeedback}"
                : "";
            return $"{@base}{context}\n\n## Your Mission\nReview the destroyer's findings (if any). Run the full test suite and build. Emit SHIP IT, CHANGES NEEDED, or ESCALATE.";
        }

        return @base;
    }

    public static string BuildDomainModelPrompt(Prd prd)
    {
        var taskSummary = string.Join('\n', prd.Tasks.Select(t =>
            $"- {t.Id}: {t.Title} ({t.Type})\n  {t.Description}"));

        return $"""
            # Sprint: {prd.Sprint}

            {prd.Description}

            ## Tasks in This Sprint

            {taskSummary}

            ## Your Mission

            Define or update the domain model for this sprint's scope. Read the existing domain model in `/docs/domain/` if it exists.
            Output your domain model to `/docs/domain/{prd.Sprint}.md`.

            For each vertical slice touched by these tasks, define:
            - Events (name, payload, aggregate, trigger)
            - Aggregates (name, events emitted, commands handled, state)
            - Value Objects (name, constraints)
            - Commands (name, payload, handling aggregate)

            The event catalog is the foundational contract — lock it before builders begin.
            """;
    }

    public static string BuildApiContractPrompt(Prd prd)
    {
        var taskSummary = string.Join('\n', prd.Tasks.Select(t =>
            $"- {t.Id}: {t.Title} ({t.Type})\n  {t.Description}"));

        return $"""
            # Sprint: {prd.Sprint}

            {prd.Description}

            ## Tasks in This Sprint

            {taskSummary}

            ## Your Mission

            Define or update the API contract for this sprint's tasks. Read the domain model in `/docs/domain/{prd.Sprint}.md` first.
            Read the existing API contract in `/docs/api/` if it exists.
            Output your API contract to `/docs/api/{prd.Sprint}.md`.

            For each endpoint needed by these tasks, define:
            - Method and path
            - Request/response shapes
            - Error responses
            - Auth requirements
            - Owning vertical slice

            Also document any Wolverine messages that flow between slices and any SSE event types.
            """;
    }
}
