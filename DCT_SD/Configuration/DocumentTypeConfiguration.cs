using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("DocumentTypes");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentCode).HasMaxLength(20).IsRequired();
        builder.Property(d => d.DocumentName).HasMaxLength(200).IsRequired();
        builder.Property(d => d.RowVersion).IsRowVersion();

        builder.HasIndex(d => d.DocumentCode).IsUnique();
        builder.HasIndex(d => d.DocumentName).IsUnique();
    }
}
