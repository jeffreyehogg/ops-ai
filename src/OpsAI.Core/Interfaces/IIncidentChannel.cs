using OpsAI.Core.Models;

namespace OpsAI.Core.Interfaces;

public interface IIncidentChannel
{
    ValueTask<bool> EnqueueAsync(Incident incident, CancellationToken ct = default);
    IAsyncEnumerable<Incident> ReadAllAsync(CancellationToken ct = default);
}
