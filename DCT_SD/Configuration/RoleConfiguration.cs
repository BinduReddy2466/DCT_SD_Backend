using DCT_SD.Models;
using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(200);
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => r.Name).IsUnique();

        builder.HasData(
            new Role { Id = 1, Name = RoleNames.Administrator, Description = "Full system access.", IsSystemDefined = true, CreatedAt = SeedTimestamp },
            new Role { Id = 2, Name = RoleNames.SubAdmin, Description = "Administrator-delegated access to explicitly assigned modules.", IsSystemDefined = true, CreatedAt = SeedTimestamp },
            new Role { Id = 3, Name = RoleNames.Encoder, Description = "Operational data-entry access to the core pipeline modules.", IsSystemDefined = false, CreatedAt = SeedTimestamp },
            new Role { Id = 4, Name = RoleNames.LaresQa, Description = "LARES quality-assurance review access.", IsSystemDefined = false, CreatedAt = SeedTimestamp },
            new Role { Id = 5, Name = RoleNames.LraQa, Description = "LRA quality-assurance review access.", IsSystemDefined = false, CreatedAt = SeedTimestamp }
        );
    }
}
