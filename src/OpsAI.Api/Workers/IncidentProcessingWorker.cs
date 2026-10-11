using OpsAI.Core.Interfaces;

namespace OpsAI.Api.Workers;

public sealed class IncidentProcessingWorker : BackgroundService
{
    private readonly IIncidentChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITriageEngine _triageEngine;
    private readonly ILogger<IncidentProcessingWorker> _logger;

    public IncidentProcessingWorker(
        IIncidentChannel channel,
        IServiceScopeFactory scopeFactory,
        ITriageEngine triageEngine,
        ILogger<IncidentProcessingWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _triageEngine = triageEngine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IncidentProcessingWorker background consumer started.");

        try
        {
            await foreach (var incident in _channel.ReadAllAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation("Processing incident {IncidentId} for service {ServiceName}...", incident.Id, incident.ServiceName);

                    using var scope = _scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IIncidentRepository>();
                    var webhookDispatcher = scope.ServiceProvider.GetRequiredService<IWebhookDispatcher>();

                    incident.MarkProcessing();
                    await repository.UpdateAsync(incident, stoppingToken);

                    // Execute AI triage
                    var triageResult = await _triageEngine.TriageAsync(incident, stoppingToken);

                    incident.CompleteTriage(triageResult);
                    await repository.UpdateAsync(incident, stoppingToken);

                    _logger.LogInformation("Incident {IncidentId} triaged successfully with severity {Severity}.", incident.Id, triageResult.Severity);

                    // Dispatch notification
                    await webhookDispatcher.DispatchAlertAsync(incident, triageResult, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception processing incident {IncidentId}.", incident.Id);

                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var repository = scope.ServiceProvider.GetRequiredService<IIncidentRepository>();
                        incident.MarkFailed(ex.Message);
                        await repository.UpdateAsync(incident, CancellationToken.None);
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogError(dbEx, "Failed to mark incident {IncidentId} as failed in database.", incident.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("IncidentProcessingWorker stopped due to cancellation.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in IncidentProcessingWorker loop.");
        }
    }
}
