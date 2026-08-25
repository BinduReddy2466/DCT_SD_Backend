using DCT_SD.Helpers;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class MigrationRecordConfiguration : IEntityTypeConfiguration<MigrationRecord>
{
    public void Configure(EntityTypeBuilder<MigrationRecord> builder)
    {
        builder.ToTable("MigrationRecords", tb => tb.UseSqlOutputClause(false)); // trg_MigrationRecords_AuditLog blocks the default OUTPUT clause

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequestNumber).HasMaxLength(30).IsRequired();
        builder.Property(r => r.RdCode).HasMaxLength(20).IsRequired();
        builder.Property(r => r.RdName).HasMaxLength(150).IsRequired();
        builder.Property(r => r.EntryNumbersCsv).HasMaxLength(200);
        builder.Property(r => r.Title).HasMaxLength(50);
        builder.Property(r => r.TitleType).HasConversion<int?>();
        builder.Property(r => r.Plan).HasMaxLength(50);
        builder.Property(r => r.Block).HasMaxLength(50);
        builder.Property(r => r.Lot).HasMaxLength(50);
        builder.Property(r => r.TitleSequence).HasMaxLength(50);
        // MigrationStatus/SdStatus are stored as their human-readable display strings (e.g.
        // "Migrated to Existing Title/Entry Record"), reusing the same maps StatusDisplay
        // already uses for the API.
        builder.Property(r => r.MigrationStatus)
            .HasConversion(
                v => StatusDisplay.MigrationStatusToDisplay(v.ToString()),
                v => Enum.Parse<MigrationStatus>(StatusDisplay.MigrationStatusToApi(v) ?? v))
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(r => r.SdStatus)
            .HasConversion(
                v => StatusDisplay.SdStatusToDisplay(v.ToString()),
                v => Enum.Parse<SupportingDocumentStatus>(StatusDisplay.SdStatusToApi(v) ?? v))
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(r => r.MigratedToRdName).HasMaxLength(150).IsRequired();

        builder.HasIndex(r => r.RequestNumber).IsUnique();
        builder.HasIndex(r => r.MigrationDate);
        builder.HasIndex(r => r.MigrationStatus);
        builder.HasIndex(r => r.OcrExtractionRecordId);

        builder.HasOne(r => r.OcrExtractionRecord)
            .WithMany(o => o.MigrationRecords)
            .HasForeignKey(r => r.OcrExtractionRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Documents)
            .WithOne(d => d.MigrationRecord)
            .HasForeignKey(d => d.MigrationRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
