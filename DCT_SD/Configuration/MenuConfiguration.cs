using DCT_SD.Models;
using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("Menus");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Key).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Label).HasMaxLength(100).IsRequired();
        builder.Property(m => m.RowVersion).IsRowVersion();

        builder.HasIndex(m => m.Key).IsUnique();

        builder.HasData(
            new Menu { Id = 1, Key = MenuKeys.RdConfig, Label = "RD Configuration", DisplayOrder = 1, IsBaseMenu = true, CreatedAt = SeedTimestamp },
            new Menu { Id = 2, Key = MenuKeys.MigrationMonitoring, Label = "Migration Monitoring", DisplayOrder = 2, IsBaseMenu = true, CreatedAt = SeedTimestamp },
            new Menu { Id = 4, Key = MenuKeys.ManualValidation, Label = "Manual Validation", DisplayOrder = 4, IsBaseMenu = true, CreatedAt = SeedTimestamp },
            new Menu { Id = 5, Key = MenuKeys.EmptyFolders, Label = "Empty Entry Folders", DisplayOrder = 5, IsBaseMenu = true, CreatedAt = SeedTimestamp },
            new Menu { Id = 6, Key = MenuKeys.UserManagement, Label = "User Management", DisplayOrder = 6, IsBaseMenu = false, CreatedAt = SeedTimestamp },
            new Menu { Id = 7, Key = MenuKeys.Roles, Label = "Roles", DisplayOrder = 7, IsBaseMenu = false, CreatedAt = SeedTimestamp },
            new Menu { Id = 8, Key = MenuKeys.Settings, Label = "Settings", DisplayOrder = 8, IsBaseMenu = false, CreatedAt = SeedTimestamp }
        );
    }
}
