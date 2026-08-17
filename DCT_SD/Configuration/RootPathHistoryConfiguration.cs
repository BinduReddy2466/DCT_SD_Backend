using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class RootPathHistoryConfiguration : IEntityTypeConfiguration<RootPathHistory>
{
    public void Configure(EntityTypeBuilder<RootPathHistory> builder)
    {
        builder.ToTable("RootPathHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromPath).HasMaxLength(1000);
        builder.Property(h => h.ToPath).HasMaxLength(1000).IsRequired();
        builder.Property(h => h.Remarks).HasMaxLength(500).IsRequired();
        builder.Property(h => h.ModifiedByUsername).HasMaxLength(256).IsRequired();

        builder.HasIndex(h => h.ModifiedAt);
    }
}
