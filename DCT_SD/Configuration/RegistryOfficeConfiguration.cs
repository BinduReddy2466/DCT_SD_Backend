using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class RegistryOfficeConfiguration : IEntityTypeConfiguration<RegistryOffice>
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<RegistryOffice> builder)
    {
        builder.ToTable("RegistryOffices");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Code).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(150).IsRequired();
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => r.Code).IsUnique();

        builder.HasData(
            new RegistryOffice { Id = 1, Code = "004", Name = "Quezon City", IsActive = true, CreatedAt = SeedTimestamp },
            new RegistryOffice { Id = 2, Code = "002", Name = "Manila City", IsActive = true, CreatedAt = SeedTimestamp },
            new RegistryOffice { Id = 3, Code = "107", Name = "Cebu City", IsActive = true, CreatedAt = SeedTimestamp },
            new RegistryOffice { Id = 4, Code = "146", Name = "Davao City", IsActive = true, CreatedAt = SeedTimestamp }
        );
    }
}
