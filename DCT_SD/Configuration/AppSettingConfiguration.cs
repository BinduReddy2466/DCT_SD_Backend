using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings", tb => tb.UseSqlOutputClause(false)); // trg_AppSettings_AuditLog blocks the default OUTPUT clause

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Category).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Key).HasMaxLength(64);
        builder.Property(s => s.Label).HasMaxLength(128);
        builder.Property(s => s.DataJson).IsRequired();
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.HasIndex(s => s.Category);
    }
}
