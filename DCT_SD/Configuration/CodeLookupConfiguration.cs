using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class CodeLookupConfiguration : IEntityTypeConfiguration<CodeLookup>
{
    public void Configure(EntityTypeBuilder<CodeLookup> builder)
    {
        builder.ToTable("CodeLookups", tb => tb.UseSqlOutputClause(false)); // trg_CodeLookups_AuditLog blocks the default OUTPUT clause

        builder.HasKey(c => c.Id);

        builder.Property(c => c.LookupType).HasMaxLength(30).IsRequired();
        builder.Property(c => c.Code).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasIndex(c => new { c.LookupType, c.Code });
        builder.HasIndex(c => c.LookupType);
    }
}
