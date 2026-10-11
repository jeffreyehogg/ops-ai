using OpsAI.Core.Models;

namespace OpsAI.Core.Interfaces;

public interface IWebhookDispatcher
{
    Task DispatchAlertAsync(Incident incident, TriageResult triage, CancellationToken ct = default);
}
