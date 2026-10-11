using Microsoft.EntityFrameworkCore;
using OpsAI.Core.Interfaces;
using OpsAI.Core.Models;

namespace OpsAI.Infrastructure.Data;

public sealed class IncidentRepository : IIncidentRepository
{
    private readonly OpsAIDbContext _dbContext;

    public IncidentRepository(OpsAIDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Incident> CreateAsync(Incident incident, CancellationToken ct = default)
    {
        await _dbContext.Incidents.AddAsync(incident, ct);
        await _dbContext.SaveChangesAsync(ct);
        return incident;
    }

    public async Task<Incident?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Incidents
            .Include(x => x.Triage)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<Incident>> GetRecentAsync(int limit = 50, CancellationToken ct = default)
    {
        return await _dbContext.Incidents
            .Include(x => x.Triage)
            .OrderByDescending(x => x.IngestedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(Incident incident, CancellationToken ct = default)
    {
        var existing = await _dbContext.Incidents
            .Include(x => x.Triage)
            .FirstOrDefaultAsync(x => x.Id == incident.Id, ct);

        if (existing is null)
        {
            return;
        }

        _dbContext.Entry(existing).CurrentValues.SetValues(incident);

        if (incident.Triage is not null)
        {
            if (existing.Triage is null)
            {
                existing.CompleteTriage(incident.Triage);
                await _dbContext.TriageResults.AddAsync(incident.Triage, ct);
            }
            else
            {
                _dbContext.Entry(existing.Triage).CurrentValues.SetValues(incident.Triage);
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
