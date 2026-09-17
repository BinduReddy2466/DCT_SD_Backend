using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class FailedExtractionRecordConfiguration : IEntityTypeConfiguration<FailedExtractionRecord>
{
    public void Configure(EntityTypeBuilder<FailedExtractionRecord> builder)
    {
        builder.ToTable("FailedExtractionRecords", tb => tb.UseSqlOutputClause(false)); // trg_FailedExtractionRecords_AuditLog blocks the default OUTPUT clause

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RdCode).HasMaxLength(20);
        builder.Property(r => r.RdName).HasMaxLength(150);
        builder.Property(r => r.FolderName).HasMaxLength(260).IsRequired();
        builder.Property(r => r.FolderPath).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.FailureReason).HasMaxLength(500).IsRequired();

        builder.HasIndex(r => r.ExtractionDateTime);
        builder.HasIndex(r => r.FolderPath);
    }
}
