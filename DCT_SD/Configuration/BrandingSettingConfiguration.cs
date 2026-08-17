using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class BrandingSettingConfiguration : IEntityTypeConfiguration<BrandingSetting>
{
    public void Configure(EntityTypeBuilder<BrandingSetting> builder)
    {
        builder.ToTable("BrandingSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ImagePath).HasMaxLength(260);
        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}
