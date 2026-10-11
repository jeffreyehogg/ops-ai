using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsAI.Core.Models;

namespace OpsAI.Infrastructure.Data.Configurations;

public sealed class TriageResultConfiguration : IEntityTypeConfiguration<TriageResult>
{
    public void Configure(EntityTypeBuilder<TriageResult> builder)
    {
        builder.ToTable("TriageResults");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Severity)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.RootCause)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.ImpactScope)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.RemediationPlan)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.SuggestedSqlFix)
            .HasMaxLength(4000);

        builder.Property(x => x.RunbookTags)
            .HasMaxLength(500);

        builder.Property(x => x.AiModelUsed)
            .HasMaxLength(100);

        builder.Property(x => x.TriagedAt)
            .HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => x.TriagedAt);
    }
}
