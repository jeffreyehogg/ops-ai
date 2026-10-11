using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsAI.Core.Enums;
using OpsAI.Core.Interfaces;
using OpsAI.Core.Models;
using OpsAI.Infrastructure.Configuration;

namespace OpsAI.Infrastructure.Webhooks;

public sealed class WebhookDispatcher : IWebhookDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly WebhookConfiguration _config;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        HttpClient httpClient,
        IOptions<WebhookConfiguration> options,
        ILogger<WebhookDispatcher> logger)
    {
        _httpClient = httpClient;
        _config = options.Value;
        _logger = logger;
    }

    public async Task DispatchAlertAsync(Incident incident, TriageResult triage, CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("Webhooks disabled. Skipping alert dispatch for incident {IncidentId}.", incident.Id);
            return;
        }

        var severityIcon = triage.Severity switch
        {
            SeverityLevel.P1_Critical => "🔴 [P1 - CRITICAL]",
            SeverityLevel.P2_High => "🟠 [P2 - HIGH]",
            SeverityLevel.P3_Medium => "🟡 [P3 - MEDIUM]",
            _ => "🔵 [P4 - LOW]"
        };

        var markdownSummary = $"""
            {severityIcon} **Incident Triaged: {incident.Title}**
            • **Service:** `{incident.ServiceName}` ({incident.Environment})
            • **Telemetry Type:** `{incident.Type}`
            • **AI Model:** `{triage.AiModelUsed}` ({triage.TriageDurationMs:F0}ms)
            
            **Root Cause Analysis:**
            {triage.RootCause}
            
            **Impact Scope:**
            {triage.ImpactScope}
            
            **Remediation Action:**
            {triage.RemediationPlan}
            
            {(string.IsNullOrWhiteSpace(triage.SuggestedSqlFix) ? "" : $"**Suggested SQL / Index DDL:**\n```sql\n{triage.SuggestedSqlFix}\n```\n")}
            • **Incident ID:** `{incident.Id}`
            • **Tags:** `{triage.RunbookTags ?? "none"}`
            """;

        _logger.LogInformation("Generated Triage Alert Dispatch:\n{Alert}", markdownSummary);

        // Dispatch to Slack webhook if configured
        if (!string.IsNullOrWhiteSpace(_config.SlackWebhookUrl))
        {
            await SendSlackPayloadAsync(_config.SlackWebhookUrl, markdownSummary, ct);
        }

        // Dispatch to Discord webhook if configured
        if (!string.IsNullOrWhiteSpace(_config.DiscordWebhookUrl))
        {
            await SendDiscordPayloadAsync(_config.DiscordWebhookUrl, markdownSummary, ct);
        }
    }

    private async Task SendSlackPayloadAsync(string url, string text, CancellationToken ct)
    {
        try
        {
            var payload = new { text };
            var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Slack webhook alert dispatched successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send alert to Slack webhook.");
        }
    }

    private async Task SendDiscordPayloadAsync(string url, string text, CancellationToken ct)
    {
        try
        {
            var payload = new { content = text };
            var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Discord webhook alert dispatched successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send alert to Discord webhook.");
        }
    }
}
