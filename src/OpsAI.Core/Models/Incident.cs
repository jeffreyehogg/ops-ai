using OpsAI.Core.Enums;

namespace OpsAI.Core.Models;

public sealed class Incident
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ServiceName { get; init; } = string.Empty;
    public string Environment { get; init; } = "Production";
    public TelemetryType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Message { get; init; }
    public string? StackTrace { get; init; }
    public string? QueryText { get; init; }
    public double? ExecutionDurationMs { get; init; }
    public string? MetadataJson { get; init; }
    public DateTimeOffset IngestedAt { get; init; } = DateTimeOffset.UtcNow;
    public IncidentStatus Status { get; private set; } = IncidentStatus.Pending;
    public string? FailureReason { get; private set; }
    
    public TriageResult? Triage { get; private set; }

    public void MarkProcessing()
    {
        Status = IncidentStatus.Processing;
    }

    public void CompleteTriage(TriageResult triage)
    {
        ArgumentNullException.ThrowIfNull(triage);
        Triage = triage;
        Status = IncidentStatus.Triaged;
    }

    public void MarkFailed(string reason)
    {
        Status = IncidentStatus.Failed;
        FailureReason = reason;
    }
}
