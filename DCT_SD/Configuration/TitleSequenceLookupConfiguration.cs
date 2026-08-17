using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class TitleSequenceLookupConfiguration : IEntityTypeConfiguration<TitleSequenceLookup>
{
    public void Configure(EntityTypeBuilder<TitleSequenceLookup> builder)
    {
        builder.ToTable("TitleSequenceLookups");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).HasMaxLength(50).IsRequired();
        builder.Property(t => t.TitleType).HasConversion<int>().IsRequired();
        builder.Property(t => t.Plan).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Block).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Lot).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Sequence).HasMaxLength(50).IsRequired();

        builder.HasIndex(t => new { t.Title, t.TitleType, t.Plan, t.Block, t.Lot }).IsUnique();

        builder.HasData(
            new TitleSequenceLookup { Id = 1, Title = "T-003310", TitleType = TitleType.OCT, Plan = "PLN-1187", Block = "03", Lot = "19", Sequence = "00512" },
            new TitleSequenceLookup { Id = 2, Title = "T-091234", TitleType = TitleType.TCT, Plan = "PLN-0842", Block = "07", Lot = "22", Sequence = "00877" }
        );
    }
}
