using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class OcrExtractionEntryConfiguration : IEntityTypeConfiguration<OcrExtractionEntry>
{
    public void Configure(EntityTypeBuilder<OcrExtractionEntry> builder)
    {
        builder.ToTable("OcrExtractionEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntryNumber).HasMaxLength(50).IsRequired();

        builder.HasIndex(e => new { e.OcrExtractionRecordId, e.EntryNumber }).IsUnique();
        builder.HasIndex(e => e.EntryNumber);

        builder.HasOne(e => e.OcrExtractionRecord)
            .WithMany(r => r.Entries)
            .HasForeignKey(e => e.OcrExtractionRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
