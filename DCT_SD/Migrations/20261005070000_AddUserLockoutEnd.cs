using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCT_SD.Migrations
{
    // Hand-written, not scaffolded: `dotnet ef migrations add` produced a huge, destructive diff
    // (dropping Roles/Menus/EmailTemplates/UserMenuPermissions/AppSettings and more) because
    // ApplicationDbContextModelSnapshot.cs is already far out of sync with both the live database
    // and the current C# model - a pre-existing drift from schema changes made outside EF
    // migrations, not something introduced here. This migration is scoped to exactly the one
    // column this feature needs; it does not attempt to reconcile that broader drift.
    /// <inheritdoc />
    public partial class AddUserLockoutEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockoutEndUtc",
                table: "Users");
        }
    }
}
