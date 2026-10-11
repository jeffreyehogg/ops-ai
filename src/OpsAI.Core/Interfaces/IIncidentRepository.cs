using OpsAI.Core.Models;

namespace OpsAI.Core.Interfaces;

public interface IIncidentRepository
{
    Task<Incident> CreateAsync(Incident incident, CancellationToken ct = default);
    Task<Incident?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Incident>> GetRecentAsync(int limit = 50, CancellationToken ct = default);
    Task UpdateAsync(Incident incident, CancellationToken ct = default);
}
