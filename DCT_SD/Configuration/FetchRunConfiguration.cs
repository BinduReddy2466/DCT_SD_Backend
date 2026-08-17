using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class FetchRunConfiguration : IEntityTypeConfiguration<FetchRun>
{
    public void Configure(EntityTypeBuilder<FetchRun> builder)
    {
        builder.ToTable("FetchRuns");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.SourcePath).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.LastProcessedFolderPath).HasMaxLength(1000);
        builder.Property(r => r.ExecutedByUsername).HasMaxLength(256).IsRequired();

        builder.HasIndex(r => r.StartedAt);
        builder.HasIndex(r => r.Status);
    }
}
