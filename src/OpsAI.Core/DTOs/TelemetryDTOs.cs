using OpsAI.Core.Enums;

namespace OpsAI.Core.DTOs;

public sealed record TelemetryIngestRequest(
    string ServiceName,
    string Environment,
    TelemetryType Type,
    string Title,
    string? Message,
    string? StackTrace,
    string? QueryText,
    double? ExecutionDurationMs,
    Dictionary<string, string>? Metadata
);

public sealed record IncidentIngestAcceptedResponse(
    Guid IncidentId,
    string Status,
    string TrackingUrl,
    DateTimeOffset IngestedAt
);

public sealed record IncidentDetailResponse(
    Guid Id,
    string ServiceName,
    string Environment,
    string Type,
    string Title,
    string? Message,
    string? StackTrace,
    string? QueryText,
    double? ExecutionDurationMs,
    string Status,
    DateTimeOffset IngestedAt,
    TriageDetailResponse? Triage
);

public sealed record TriageDetailResponse(
    Guid Id,
    string Severity,
    string RootCause,
    string ImpactScope,
    string RemediationPlan,
    string? SuggestedSqlFix,
    string? RunbookTags,
    string AiModelUsed,
    double TriageDurationMs,
    DateTimeOffset TriagedAt
);
