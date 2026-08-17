using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Key).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.Key).IsUnique();

        builder.Property(t => t.Label).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Recipients).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Subject).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Body).HasMaxLength(4000).IsRequired();
        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}
