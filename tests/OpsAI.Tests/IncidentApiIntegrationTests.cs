using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OpsAI.Core.DTOs;
using OpsAI.Core.Enums;

namespace OpsAI.Tests;

public sealed class IncidentApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IncidentApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/healthz");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IngestIncident_ShouldReturnAccepted_AndBeProcessedByWorker()
    {
        var request = new TelemetryIngestRequest(
            ServiceName: "customer-portal",
            Environment: "Production",
            Type: TelemetryType.ConnectionPoolExhaustion,
            Title: "Pool exhaustion on customer auth endpoint",
            Message: "The timeout period elapsed prior to obtaining a connection from the pool.",
            StackTrace: "at Npgsql.ConnectorPool.AllocateLong()",
            QueryText: null,
            ExecutionDurationMs: 15000.0,
            Metadata: new Dictionary<string, string> { { "host", "auth-worker-01" } }
        );

        var ingestResponse = await _client.PostAsJsonAsync("/api/v1/incidents/ingest", request);
        ingestResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var acceptedPayload = await ingestResponse.Content.ReadFromJsonAsync<IncidentIngestAcceptedResponse>();
        acceptedPayload.Should().NotBeNull();
        acceptedPayload!.IncidentId.Should().NotBeEmpty();

        // Allow worker to drain channel and complete triage
        IncidentDetailResponse? detail = null;
        for (int i = 0; i < 15; i++)
        {
            await Task.Delay(200);
            var detailResponse = await _client.GetAsync($"/api/v1/incidents/{acceptedPayload.IncidentId}");
            if (detailResponse.StatusCode == HttpStatusCode.OK)
            {
                detail = await detailResponse.Content.ReadFromJsonAsync<IncidentDetailResponse>();
                if (detail?.Status == "Triaged")
                {
                    break;
                }
            }
        }

        detail.Should().NotBeNull();
        detail!.Status.Should().Be("Triaged");
        detail.Triage.Should().NotBeNull();
        detail.Triage!.Severity.Should().Be(nameof(SeverityLevel.P1_Critical));
        detail.Triage.RootCause.Should().Contain("Connection Pool");
    }

    [Fact]
    public async Task SimulateEndpoint_ShouldTriggerRealisticScenario()
    {
        var response = await _client.PostAsync("/api/v1/simulate?scenario=slow_query", null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var incidentsResponse = await _client.GetAsync("/api/v1/incidents");
        incidentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var incidents = await incidentsResponse.Content.ReadFromJsonAsync<List<IncidentDetailResponse>>();
        incidents.Should().NotBeNull();
        incidents.Should().Contain(x => x.ServiceName == "billing-ledger-svc");
    }
}
