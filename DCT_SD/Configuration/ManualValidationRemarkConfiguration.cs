using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class ManualValidationRemarkConfiguration : IEntityTypeConfiguration<ManualValidationRemark>
{
    public void Configure(EntityTypeBuilder<ManualValidationRemark> builder)
    {
        builder.ToTable("ManualValidationRemarks");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Action).HasConversion<int>().IsRequired();
        builder.Property(r => r.Remarks).HasMaxLength(500).IsRequired();
        builder.Property(r => r.ByUsername).HasMaxLength(256).IsRequired();

        builder.HasIndex(r => r.ManualValidationRequestId);
    }
}
