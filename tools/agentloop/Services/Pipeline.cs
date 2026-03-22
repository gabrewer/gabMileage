using AgentLoop.Models;

namespace AgentLoop.Services;

public static class Pipeline
{
    public static readonly Dictionary<TaskType, string[]> TaskAgents = new()
    {
        [TaskType.Backend]  = ["test-writer", "backend-builder"],
        [TaskType.Frontend] = ["test-writer", "frontend-builder"],
        [TaskType.Both]     = ["test-writer", "backend-builder", "frontend-builder"],
    };

    public const int MaxReviewAttempts = 3;
}
