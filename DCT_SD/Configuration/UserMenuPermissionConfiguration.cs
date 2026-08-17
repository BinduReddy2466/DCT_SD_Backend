using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class UserMenuPermissionConfiguration : IEntityTypeConfiguration<UserMenuPermission>
{
    public void Configure(EntityTypeBuilder<UserMenuPermission> builder)
    {
        builder.ToTable("UserMenuPermissions");

        builder.HasKey(p => new { p.UserId, p.MenuId });

        builder.HasOne(p => p.User)
            .WithMany(u => u.MenuPermissions)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Menu)
            .WithMany(m => m.UserMenuPermissions)
            .HasForeignKey(p => p.MenuId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
