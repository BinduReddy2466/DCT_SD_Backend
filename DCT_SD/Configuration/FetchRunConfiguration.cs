using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class FetchRunConfiguration : IEntityTypeConfiguration<FetchRun>
{
    public void Configure(EntityTypeBuilder<FetchRun> builder)
    {
        builder.ToTable("FetchRuns", tb => tb.UseSqlOutputClause(false)); // trg_FetchRuns_AuditLog blocks the default OUTPUT clause

        builder.HasKey(r => r.Id);

        builder.Property(r => r.SourcePath).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.Status).HasConversion<int?>();
        builder.Property(r => r.LastProcessedFolderPath).HasMaxLength(1000);
        builder.Property(r => r.ExecutedByUsername).HasMaxLength(256).IsRequired();
        builder.Property(r => r.RecordKind).HasMaxLength(20).IsRequired();
        builder.Property(r => r.FromPath).HasMaxLength(1000);
        builder.Property(r => r.Remarks).HasMaxLength(500);

        builder.HasIndex(r => new { r.RecordKind, r.StartedAt });
        builder.HasIndex(r => r.Status);
    }
}
