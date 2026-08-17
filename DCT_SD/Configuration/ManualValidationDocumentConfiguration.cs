using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class ManualValidationDocumentConfiguration : IEntityTypeConfiguration<ManualValidationDocument>
{
    public void Configure(EntityTypeBuilder<ManualValidationDocument> builder)
    {
        builder.ToTable("ManualValidationDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentName).HasMaxLength(200).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(260).IsRequired();

        builder.HasIndex(d => d.DocumentTypeId);

        builder.HasOne(d => d.DocumentType)
            .WithMany(t => t.ManualValidationDocuments)
            .HasForeignKey(d => d.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
