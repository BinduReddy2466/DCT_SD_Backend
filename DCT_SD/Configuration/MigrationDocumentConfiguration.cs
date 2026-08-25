using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class MigrationDocumentConfiguration : IEntityTypeConfiguration<MigrationDocument>
{
    public void Configure(EntityTypeBuilder<MigrationDocument> builder)
    {
        builder.ToTable("MigrationDocuments", tb => tb.UseSqlOutputClause(false)); // trg_MigrationDocuments_AuditLog blocks the default OUTPUT clause

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentName).HasMaxLength(200).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(260).IsRequired();
        builder.Property(d => d.Status).HasConversion<int>().IsRequired();
        builder.Property(d => d.ExistingFileName).HasMaxLength(260);
        builder.Property(d => d.PerformedByUsername).HasMaxLength(256);

        builder.HasIndex(d => d.MigrationRecordId);
        builder.HasIndex(d => new { d.MigrationRecordId, d.FileName }).IsUnique();
        builder.HasIndex(d => d.CodeLookupId);

        builder.HasOne(d => d.CodeLookup)
            .WithMany()
            .HasForeignKey(d => d.CodeLookupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
