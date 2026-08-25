using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class RecordHistoryConfiguration : IEntityTypeConfiguration<RecordHistory>
{
    public void Configure(EntityTypeBuilder<RecordHistory> builder)
    {
        builder.ToTable("RecordHistory");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TableName).HasMaxLength(60).IsRequired();
        builder.Property(r => r.RefNo).HasMaxLength(30);
        builder.Property(r => r.Action).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Remarks).HasMaxLength(500);
        builder.Property(r => r.ByUsername).HasMaxLength(256);
        builder.Property(r => r.AppName).HasMaxLength(30);
        builder.Property(r => r.HostName).HasMaxLength(30);
        builder.Property(r => r.FromValue).HasMaxLength(1000);
        builder.Property(r => r.ToValue).HasMaxLength(1000);

        builder.HasIndex(r => new { r.TableName, r.RecordId, r.CreatedAt });
        builder.HasIndex(r => r.RefNo);
    }
}
