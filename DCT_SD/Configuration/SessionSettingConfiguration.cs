using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class SessionSettingConfiguration : IEntityTypeConfiguration<SessionSetting>
{
    public void Configure(EntityTypeBuilder<SessionSetting> builder)
    {
        builder.ToTable("SessionSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Action).HasConversion<int>().IsRequired();
        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}
