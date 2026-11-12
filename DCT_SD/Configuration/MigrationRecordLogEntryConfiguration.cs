using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class MigrationRecordLogEntryConfiguration : IEntityTypeConfiguration<MigrationRecordLogEntry>
{
    public void Configure(EntityTypeBuilder<MigrationRecordLogEntry> builder)
    {
        builder.ToTable("MigrationRecords_Log");

        builder.HasKey(e => e.Log_ID);
        builder.Property(e => e.Log_ID).ValueGeneratedNever();

        builder.Property(e => e.Log_UserID).HasMaxLength(50);
        builder.Property(e => e.Log_Activity).HasMaxLength(1);
        builder.Property(e => e.MigrationType).HasMaxLength(30);
        builder.Property(e => e.ProcessingStatus).HasMaxLength(20);
        builder.Property(e => e.MigrationStatus).HasMaxLength(50);
        builder.Property(e => e.ProcessedByUsername).HasMaxLength(256);
        builder.Property(e => e.LastFailureReason).HasMaxLength(500);

        builder.HasIndex(e => e.Id);
    }
}
