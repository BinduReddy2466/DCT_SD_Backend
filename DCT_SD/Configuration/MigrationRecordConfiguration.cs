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
        // already uses for the API. Nullable: only populated once ProcessingStatus (below)
        // reaches Completed - see MigrationRecord.cs.
        builder.Property(r => r.MigrationStatus)
            .HasConversion(
                v => v == null ? null : StatusDisplay.MigrationStatusToDisplay(v.Value.ToString()),
                v => v == null ? null : Enum.Parse<MigrationStatus>(StatusDisplay.MigrationStatusToApi(v) ?? v))
            .HasMaxLength(50);
        builder.Property(r => r.SdStatus)
            .HasConversion(
                v => v == null ? null : StatusDisplay.SdStatusToDisplay(v.Value.ToString()),
                v => v == null ? null : Enum.Parse<SupportingDocumentStatus>(StatusDisplay.SdStatusToApi(v) ?? v))
            .HasMaxLength(50);
        builder.Property(r => r.MigratedToRdName).HasMaxLength(150);

        builder.Property(r => r.MigrationType)
            .HasConversion(
                v => v == null ? null : StatusDisplay.MigrationTypeToDisplay(v.Value.ToString()),
                v => v == null ? null : Enum.Parse<MigrationType>(StatusDisplay.MigrationTypeToApi(v) ?? v))
            .HasMaxLength(30);
        builder.Property(r => r.ProcessedByUsername).HasMaxLength(256);
        builder.Property(r => r.ProcessingStatus)
            .HasConversion(
                v => v == null ? null : StatusDisplay.MigrationProcessingStatusToDisplay(v.Value.ToString()),
                v => v == null ? null : Enum.Parse<MigrationProcessingStatus>(StatusDisplay.MigrationProcessingStatusToApi(v) ?? v))
            .HasMaxLength(20);
        builder.Property(r => r.LastFailureReason).HasMaxLength(500);
        builder.Property(r => r.TargetSystemValidation).HasMaxLength(100);
        builder.Property(r => r.TargetSystemActivationStatus).HasMaxLength(20);
        builder.Property(r => r.TitleTargetSystemValidation).HasMaxLength(100);
        builder.Property(r => r.DocumentJson).HasColumnName("Document_Json");
        // Bounded (not MAX, unlike before 2026-11-27) - needs to be a key column in
        // UK_MigrationRecords_01 below, which NVARCHAR(MAX) cannot be. Same reasoning as
        // EntryNumbersCsv's own HasMaxLength(200) above.
        builder.Property(r => r.RangeEntryNumber).HasMaxLength(200);

        // Composite, not single-column: several rows now legitimately share one RequestNumber -
        // one MigrationRecords row is created per (sibling x split Entry Number Range value)
        // combination. Corrected 2026-11-27: keyed on RangeEntryNumber (the real per-combination
        // split value), not EntryNumbersCsv (now the preserved, display-only original CSV, which no
        // longer varies per combination and so can no longer enforce this). TitleSequence is
        // included per client's explicit requirement, even though RequestNumber alone already
        // pins one specific Title Sequence (one ManualValidationRequest row = one Title Record).
        builder.HasIndex(r => new { r.RequestNumber, r.RangeEntryNumber, r.TitleSequence })
            .IsUnique()
            .HasDatabaseName("UK_MigrationRecords_01");
        builder.HasIndex(r => r.MigrationDate);
        builder.HasIndex(r => r.MigrationStatus);
        builder.HasIndex(r => r.OcrExtractionRecordId);
        builder.HasIndex(r => r.ProcessingStatus);

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
