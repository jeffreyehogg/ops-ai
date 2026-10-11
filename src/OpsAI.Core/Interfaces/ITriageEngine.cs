using OpsAI.Core.Models;

namespace OpsAI.Core.Interfaces;

public interface ITriageEngine
{
    Task<TriageResult> TriageAsync(Incident incident, CancellationToken ct = default);
}
