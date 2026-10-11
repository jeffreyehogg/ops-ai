using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpsAI.Core.Enums;
using OpsAI.Core.Interfaces;
using OpsAI.Core.Models;
using OpsAI.Infrastructure.Configuration;

namespace OpsAI.Infrastructure.AI;

public sealed class SemanticKernelTriageEngine : ITriageEngine
{
    private readonly AIConfiguration _config;
    private readonly HeuristicTriageEngine _heuristicEngine;
    private readonly ILogger<SemanticKernelTriageEngine> _logger;
    private readonly IChatCompletionService? _chatService;

    public SemanticKernelTriageEngine(
        IOptions<AIConfiguration> options,
        HeuristicTriageEngine heuristicEngine,
        ILogger<SemanticKernelTriageEngine> logger)
    {
        _config = options.Value;
        _heuristicEngine = heuristicEngine;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_config.ApiKey) || _config.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var endpoint = new Uri(_config.GetEffectiveEndpoint());
                var apiKey = string.IsNullOrWhiteSpace(_config.ApiKey) ? "ollama" : _config.ApiKey;

                var builder = Kernel.CreateBuilder();
                builder.AddOpenAIChatCompletion(
                    modelId: _config.ModelId,
                    apiKey: apiKey,
                    endpoint: endpoint
                );

                var kernel = builder.Build();
                _chatService = kernel.GetRequiredService<IChatCompletionService>();
                _logger.LogInformation("Semantic Kernel initialized with provider: {Provider}, model: {ModelId}", _config.Provider, _config.ModelId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Semantic Kernel. Falling back to heuristic engine.");
                _chatService = null;
            }
        }
        else
        {
            _logger.LogInformation("No AI ApiKey configured. Running in deterministic heuristic mode.");
            _chatService = null;
        }
    }

    public async Task<TriageResult> TriageAsync(Incident incident, CancellationToken ct = default)
    {
        if (_chatService is null)
        {
            return await _heuristicEngine.TriageAsync(incident, ct);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var history = new ChatHistory();
            history.AddSystemMessage("""
                You are OpsAI, an elite enterprise Site Reliability Engineering (SRE) and Database Architect triage assistant.
                Analyze the incident telemetry, determine the root cause, assign an incident severity (P1_Critical, P2_High, P3_Medium, or P4_Low),
                assess the blast radius / impact scope, provide an actionable remediation plan, and suggest SQL DDL or query index optimizations if relevant.
                
                Respond ONLY with a valid JSON object matching this exact schema:
                {
                  "severity": "P1_Critical" | "P2_High" | "P3_Medium" | "P4_Low",
                  "rootCause": "Clear explanation of the technical failure mechanism",
                  "impactScope": "Blast radius on users, latency, and downstream dependencies",
                  "remediationPlan": "Step-by-step immediate and long-term mitigation actions",
                  "suggestedSqlFix": "SQL DDL index statement or query fix, or null if not a database issue",
                  "runbookTags": "comma,separated,tags"
                }
                Do not include markdown code fences (```json), commentary, or any text outside the JSON object.
                """);

            var userPrompt = $"""
                [INCIDENT TELEMETRY]
                Service: {incident.ServiceName}
                Environment: {incident.Environment}
                Type: {incident.Type}
                Title: {incident.Title}
                Message: {incident.Message ?? "N/A"}
                Execution Duration: {incident.ExecutionDurationMs?.ToString("F1") ?? "N/A"} ms
                Query Text: {incident.QueryText ?? "N/A"}
                Stack Trace:
                {incident.StackTrace ?? "N/A"}
                """;

            history.AddUserMessage(userPrompt);

            var settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    { "temperature", 0.1 }
                }
            };

            var chatResponse = await _chatService.GetChatMessageContentAsync(history, settings, cancellationToken: ct);
            stopwatch.Stop();

            var rawJson = CleanJsonOutput(chatResponse.Content ?? string.Empty);
            var parsed = JsonSerializer.Deserialize<StructuredTriagePayload>(rawJson, SerializerOptions);

            if (parsed is null)
            {
                throw new InvalidOperationException("Failed to deserialize LLM triage response.");
            }

            var severity = Enum.TryParse<SeverityLevel>(parsed.Severity, true, out var parsedSeverity)
                ? parsedSeverity
                : SeverityLevel.P3_Medium;

            return new TriageResult
            {
                IncidentId = incident.Id,
                Severity = severity,
                RootCause = parsed.RootCause ?? "Root cause identified by AI engine.",
                ImpactScope = parsed.ImpactScope ?? "Service impact assessed.",
                RemediationPlan = parsed.RemediationPlan ?? "Refer to standard runbook.",
                SuggestedSqlFix = parsed.SuggestedSqlFix,
                RunbookTags = parsed.RunbookTags ?? "ops-ai,automated-triage",
                AiModelUsed = $"{_config.Provider}:{_config.ModelId}",
                TriageDurationMs = stopwatch.Elapsed.TotalMilliseconds,
                TriagedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Semantic Kernel triage failed or timed out for incident {IncidentId}. Falling back to heuristic engine.", incident.Id);

            var fallbackResult = await _heuristicEngine.TriageAsync(incident, ct);
            return new TriageResult
            {
                IncidentId = fallbackResult.IncidentId,
                Severity = fallbackResult.Severity,
                RootCause = fallbackResult.RootCause,
                ImpactScope = fallbackResult.ImpactScope,
                RemediationPlan = fallbackResult.RemediationPlan,
                SuggestedSqlFix = fallbackResult.SuggestedSqlFix,
                RunbookTags = fallbackResult.RunbookTags,
                AiModelUsed = $"Heuristic-Fallback ({ex.GetType().Name})",
                TriageDurationMs = stopwatch.Elapsed.TotalMilliseconds,
                TriagedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private static string CleanJsonOutput(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..];
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed[3..];
        }

        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record StructuredTriagePayload(
        string? Severity,
        string? RootCause,
        string? ImpactScope,
        string? RemediationPlan,
        string? SuggestedSqlFix,
        string? RunbookTags
    );
}
