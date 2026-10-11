using OpsAI.Core.Enums;

namespace OpsAI.Core.Models;

public sealed class TriageResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid IncidentId { get; init; }
    public SeverityLevel Severity { get; init; }
    public string RootCause { get; init; } = string.Empty;
    public string ImpactScope { get; init; } = string.Empty;
    public string RemediationPlan { get; init; } = string.Empty;
    public string? SuggestedSqlFix { get; init; }
    public string? RunbookTags { get; init; }
    public string AiModelUsed { get; init; } = string.Empty;
    public double TriageDurationMs { get; init; }
    public DateTimeOffset TriagedAt { get; init; } = DateTimeOffset.UtcNow;
}
