namespace AgentLoop.Models;

public sealed record AgentDef(string SystemPrompt, string Model, string[] Tools, bool Mcp);

public sealed record AgentResult(bool Approved, string? Feedback = null);

public sealed class ActiveTaskDisplay
{
    public required string TaskId { get; set; }
    public required string Agent { get; set; }
    public int Attempt { get; set; } = 1;
    public string Status { get; set; } = "";
    public long BytesWritten { get; set; }
    public string StartedAt { get; set; } = DateTime.UtcNow.ToString("O");
}
