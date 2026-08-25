using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

// Read-only from the app's perspective - rows are written entirely by DB triggers.
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TableName).HasMaxLength(60).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(1).IsRequired();
        builder.Property(a => a.PerformedByLogin).HasMaxLength(128).IsRequired();
        builder.Property(a => a.AppName).HasMaxLength(30);
        builder.Property(a => a.HostName).HasMaxLength(30);

        builder.HasIndex(a => new { a.TableName, a.RecordId, a.LogDate });
    }
}
