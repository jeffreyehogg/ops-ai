using System.Text.Json;
using FluentValidation;
using OpsAI.Api.Simulation;
using OpsAI.Core.DTOs;
using OpsAI.Core.Interfaces;
using OpsAI.Core.Models;

namespace OpsAI.Api.Endpoints;

public static class IncidentEndpoints
{
    public static RouteGroupBuilder MapIncidentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/incidents")
            .WithTags("Incidents");

        group.MapPost("/ingest", async (
            TelemetryIngestRequest request,
            IValidator<TelemetryIngestRequest> validator,
            IIncidentRepository repository,
            IIncidentChannel channel,
            CancellationToken ct) =>
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var incident = new Incident
            {
                ServiceName = request.ServiceName,
                Environment = request.Environment,
                Type = request.Type,
                Title = request.Title,
                Message = request.Message,
                StackTrace = request.StackTrace,
                QueryText = request.QueryText,
                ExecutionDurationMs = request.ExecutionDurationMs,
                MetadataJson = request.Metadata is not null ? JsonSerializer.Serialize(request.Metadata) : null
            };

            await repository.CreateAsync(incident, ct);
            await channel.EnqueueAsync(incident, ct);

            var response = new IncidentIngestAcceptedResponse(
                IncidentId: incident.Id,
                Status: incident.Status.ToString(),
                TrackingUrl: $"/api/v1/incidents/{incident.Id}",
                IngestedAt: incident.IngestedAt
            );

            return Results.Accepted(response.TrackingUrl, response);
        })
        .WithName("IngestIncident")
        .WithSummary("Ingest an application or database incident telemetry payload for asynchronous AI triaging.")
        .Produces<IncidentIngestAcceptedResponse>(StatusCodes.Status202Accepted)
        .ProducesValidationProblem();

        group.MapGet("/", async (
            IIncidentRepository repository,
            int? limit,
            CancellationToken ct) =>
        {
            var incidents = await repository.GetRecentAsync(limit ?? 50, ct);
            var response = incidents.Select(MapToDetailResponse);
            return Results.Ok(response);
        })
        .WithName("GetRecentIncidents")
        .WithSummary("Retrieve recently ingested incidents and their AI triage status.")
        .Produces<IEnumerable<IncidentDetailResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IIncidentRepository repository,
            CancellationToken ct) =>
        {
            var incident = await repository.GetByIdAsync(id, ct);
            if (incident is null)
            {
                return Results.NotFound(new { Message = $"Incident with ID '{id}' was not found." });
            }

            return Results.Ok(MapToDetailResponse(incident));
        })
        .WithName("GetIncidentById")
        .WithSummary("Retrieve detailed diagnostic findings and remediation plan for a specific incident.")
        .Produces<IncidentDetailResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        routes.MapPost("/api/v1/simulate", async (
            string? scenario,
            IValidator<TelemetryIngestRequest> validator,
            IIncidentRepository repository,
            IIncidentChannel channel,
            CancellationToken ct) =>
        {
            var request = EnterpriseSimulator.CreateScenario(scenario);

            var incident = new Incident
            {
                ServiceName = request.ServiceName,
                Environment = request.Environment,
                Type = request.Type,
                Title = request.Title,
                Message = request.Message,
                StackTrace = request.StackTrace,
                QueryText = request.QueryText,
                ExecutionDurationMs = request.ExecutionDurationMs,
                MetadataJson = request.Metadata is not null ? JsonSerializer.Serialize(request.Metadata) : null
            };

            await repository.CreateAsync(incident, ct);
            await channel.EnqueueAsync(incident, ct);

            var response = new IncidentIngestAcceptedResponse(
                IncidentId: incident.Id,
                Status: incident.Status.ToString(),
                TrackingUrl: $"/api/v1/incidents/{incident.Id}",
                IngestedAt: incident.IngestedAt
            );

            return Results.Accepted(response.TrackingUrl, new
            {
                Scenario = scenario ?? "slow_query",
                Incident = response,
                TelemetrySample = request
            });
        })
        .WithTags("Simulation")
        .WithName("SimulateIncident")
        .WithSummary("Trigger a live enterprise failure scenario (pool_exhaustion, slow_query, deadlock, timeout).")
        .Produces(StatusCodes.Status202Accepted);

        return group;
    }

    private static IncidentDetailResponse MapToDetailResponse(Incident incident)
    {
        return new IncidentDetailResponse(
            Id: incident.Id,
            ServiceName: incident.ServiceName,
            Environment: incident.Environment,
            Type: incident.Type.ToString(),
            Title: incident.Title,
            Message: incident.Message,
            StackTrace: incident.StackTrace,
            QueryText: incident.QueryText,
            ExecutionDurationMs: incident.ExecutionDurationMs,
            Status: incident.Status.ToString(),
            IngestedAt: incident.IngestedAt,
            Triage: incident.Triage is null ? null : new TriageDetailResponse(
                Id: incident.Triage.Id,
                Severity: incident.Triage.Severity.ToString(),
                RootCause: incident.Triage.RootCause,
                ImpactScope: incident.Triage.ImpactScope,
                RemediationPlan: incident.Triage.RemediationPlan,
                SuggestedSqlFix: incident.Triage.SuggestedSqlFix,
                RunbookTags: incident.Triage.RunbookTags,
                AiModelUsed: incident.Triage.AiModelUsed,
                TriageDurationMs: incident.Triage.TriageDurationMs,
                TriagedAt: incident.Triage.TriagedAt
            )
        );
    }
}
