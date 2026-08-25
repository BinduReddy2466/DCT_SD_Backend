using DCT_SD.Helpers;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class ManualValidationRequestConfiguration : IEntityTypeConfiguration<ManualValidationRequest>
{
    public void Configure(EntityTypeBuilder<ManualValidationRequest> builder)
    {
        builder.ToTable("ManualValidationRequests", tb => tb.UseSqlOutputClause(false)); // trg_ManualValidationRequests_AuditLog blocks the default OUTPUT clause

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequestNumber).HasMaxLength(30).IsRequired();
        builder.Property(r => r.RdCode).HasMaxLength(20);
        builder.Property(r => r.RdName).HasMaxLength(150);
        builder.Property(r => r.EntryNumbersCsv).HasMaxLength(200);
        builder.Property(r => r.Title).HasMaxLength(50);
        builder.Property(r => r.TitleType).HasConversion<int?>();
        builder.Property(r => r.Plan).HasMaxLength(50);
        builder.Property(r => r.Block).HasMaxLength(50);
        builder.Property(r => r.Lot).HasMaxLength(50);
        builder.Property(r => r.TitleSequence).HasMaxLength(50);
        builder.Property(r => r.MissingFieldsCsv).HasMaxLength(500);
        builder.Property(r => r.UpdatedByUsername).HasMaxLength(256);
        builder.Property(r => r.LockedByUsername).HasMaxLength(256);

        // Status is stored as its human-readable display string (e.g. "Incomplete Extraction"),
        // not the short enum name - reuses the same map StatusDisplay already uses for the API.
        builder.Property(r => r.Status)
            .HasConversion(
                v => StatusDisplay.ManualValidationStatusToDisplay(v.ToString()),
                v => Enum.Parse<ManualValidationStatus>(StatusDisplay.ManualValidationStatusToApi(v) ?? v))
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(r => r.RequestNumber).IsUnique();
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.ExtractionDate);
        builder.HasIndex(r => r.OcrExtractionRecordId);

        builder.HasOne(r => r.OcrExtractionRecord)
            .WithMany(o => o.ManualValidationRequests)
            .HasForeignKey(r => r.OcrExtractionRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
