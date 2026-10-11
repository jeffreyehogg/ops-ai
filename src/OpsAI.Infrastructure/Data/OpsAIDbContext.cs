using Microsoft.EntityFrameworkCore;
using OpsAI.Core.Models;
using OpsAI.Infrastructure.Data.Configurations;

namespace OpsAI.Infrastructure.Data;

public class OpsAIDbContext : DbContext
{
    public OpsAIDbContext(DbContextOptions<OpsAIDbContext> options) : base(options)
    {
    }

    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<TriageResult> TriageResults => Set<TriageResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (Database.IsNpgsql())
        {
            modelBuilder.HasDefaultSchema("opsai");
        }

        modelBuilder.ApplyConfiguration(new IncidentConfiguration());
        modelBuilder.ApplyConfiguration(new TriageResultConfiguration());
    }
}
