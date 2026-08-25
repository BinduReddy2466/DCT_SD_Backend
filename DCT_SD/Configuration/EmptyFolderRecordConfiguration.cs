using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class EmptyFolderRecordConfiguration : IEntityTypeConfiguration<EmptyFolderRecord>
{
    public void Configure(EntityTypeBuilder<EmptyFolderRecord> builder)
    {
        builder.ToTable("EmptyFolderRecords", tb => tb.UseSqlOutputClause(false)); // trg_EmptyFolderRecords_AuditLog blocks the default OUTPUT clause

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RdCode).HasMaxLength(20);
        builder.Property(r => r.RdName).HasMaxLength(150);
        builder.Property(r => r.FolderName).HasMaxLength(260).IsRequired();
        builder.Property(r => r.FolderPath).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(50).IsRequired();

        builder.HasIndex(r => r.FetchDateTime);
        builder.HasIndex(r => r.FolderPath).IsUnique();
    }
}
