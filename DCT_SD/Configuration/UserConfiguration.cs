using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCT_SD.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", tb => tb.UseSqlOutputClause(false)); // trg_Users_AuditLog blocks the default OUTPUT clause

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Username).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(u => u.RoleName).HasMaxLength(50).IsRequired();
        builder.Property(u => u.MenuPermissionsCsv).HasMaxLength(400);
        builder.Property(u => u.Status).HasConversion<int>().IsRequired();
        builder.Property(u => u.RowVersion).IsRowVersion();

        builder.HasIndex(u => u.Username).IsUnique();

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
