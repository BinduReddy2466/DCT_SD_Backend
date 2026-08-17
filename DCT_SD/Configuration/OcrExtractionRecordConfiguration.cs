using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class OcrExtractionRecordConfiguration : IEntityTypeConfiguration<OcrExtractionRecord>
{
    public void Configure(EntityTypeBuilder<OcrExtractionRecord> builder)
    {
        builder.ToTable("OcrExtractionRecords");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequestNumber).HasMaxLength(30).IsRequired();
        builder.Property(r => r.RdCode).HasMaxLength(20);
        builder.Property(r => r.RdName).HasMaxLength(150);
        builder.Property(r => r.FolderPath).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.TitleNumber).HasMaxLength(50);
        builder.Property(r => r.TitleType).HasConversion<int?>();
        builder.Property(r => r.ExtractionStatus).HasConversion<int>().IsRequired();

        builder.HasIndex(r => r.RequestNumber).IsUnique();
        builder.HasIndex(r => r.ExtractionDateTime);
        builder.HasIndex(r => r.ExtractionStatus);
        builder.HasIndex(r => r.FolderPath);

        builder.HasOne(r => r.FetchRun)
            .WithMany(f => f.OcrExtractionRecords)
            .HasForeignKey(r => r.FetchRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
