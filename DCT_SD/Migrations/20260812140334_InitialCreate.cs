using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DCT_SD.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmptyFolderRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RdCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RdName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FolderName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FolderPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FetchDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmptyFolderRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FetchRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourcePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: true),
                    ProcessedCount = table.Column<int>(type: "int", nullable: false),
                    ExecutedByUserId = table.Column<int>(type: "int", nullable: false),
                    ExecutedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FetchRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManualValidationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RdCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RdName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Entry = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TitleType = table.Column<int>(type: "int", nullable: true),
                    Plan = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Block = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Lot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TitleSequence = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MissingFieldsCsv = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExtractionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<int>(type: "int", nullable: true),
                    LockedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MigratedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualValidationRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsBaseMenu = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MigrationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MigrationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RdCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RdName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Entry = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TitleType = table.Column<int>(type: "int", nullable: true),
                    Plan = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Block = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Lot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TitleSequence = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MigrationStatus = table.Column<int>(type: "int", nullable: false),
                    SdStatus = table.Column<int>(type: "int", nullable: false),
                    MigratedToRdName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistryOffices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistryOffices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsSystemDefined = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RootPathHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ToPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: false),
                    ModifiedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RootPathHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeoutMinutes = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TitleSequenceLookups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TitleType = table.Column<int>(type: "int", nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Block = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Lot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Sequence = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TitleSequenceLookups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManualValidationDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManualValidationRequestId = table.Column<int>(type: "int", nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualValidationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualValidationDocuments_ManualValidationRequests_ManualValidationRequestId",
                        column: x => x.ManualValidationRequestId,
                        principalTable: "ManualValidationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManualValidationRemarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManualValidationRequestId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ByUserId = table.Column<int>(type: "int", nullable: false),
                    ByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualValidationRemarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualValidationRemarks_ManualValidationRequests_ManualValidationRequestId",
                        column: x => x.ManualValidationRequestId,
                        principalTable: "ManualValidationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MigrationDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationRecordId = table.Column<int>(type: "int", nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExistingFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    PerformedByUserId = table.Column<int>(type: "int", nullable: true),
                    PerformedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationDocuments_MigrationRecords_MigrationRecordId",
                        column: x => x.MigrationRecordId,
                        principalTable: "MigrationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserMenuPermissions",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    MenuId = table.Column<int>(type: "int", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMenuPermissions", x => new { x.UserId, x.MenuId });
                    table.ForeignKey(
                        name: "FK_UserMenuPermissions_Menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMenuPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Menus",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "DisplayOrder", "IsBaseMenu", "Key", "Label", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, true, "rd-config", "RD Configuration", null, null },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, true, "migration-monitoring", "Migration Monitoring", null, null },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4, true, "manual-validation", "Manual Validation", null, null },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5, true, "empty-folders", "Empty Entry Folders", null, null },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6, false, "user-management", "User Management", null, null },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 7, false, "roles", "Roles", null, null },
                    { 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 8, false, "settings", "Settings", null, null }
                });

            migrationBuilder.InsertData(
                table: "RegistryOffices",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "IsActive", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Quezon City", null, null },
                    { 2, "002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Manila City", null, null },
                    { 3, "107", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Cebu City", null, null },
                    { 4, "146", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Davao City", null, null }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Description", "IsSystemDefined", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Full system access.", true, "Administrator", null, null },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Administrator-delegated access to explicitly assigned modules.", true, "Sub-Admin", null, null },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Operational data-entry access to the core pipeline modules.", false, "Encoder", null, null },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "LARES quality-assurance review access.", false, "LARES QA", null, null },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "LRA quality-assurance review access.", false, "LRA QA", null, null }
                });

            migrationBuilder.InsertData(
                table: "TitleSequenceLookups",
                columns: new[] { "Id", "Block", "Lot", "Plan", "Sequence", "Title", "TitleType" },
                values: new object[,]
                {
                    { 1, "03", "19", "PLN-1187", "00512", "T-003310", 2 },
                    { 2, "07", "22", "PLN-0842", "00877", "T-091234", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmptyFolderRecords_FetchDateTime",
                table: "EmptyFolderRecords",
                column: "FetchDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_FetchRuns_StartedAt",
                table: "FetchRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FetchRuns_Status",
                table: "FetchRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ManualValidationDocuments_ManualValidationRequestId",
                table: "ManualValidationDocuments",
                column: "ManualValidationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualValidationRemarks_ManualValidationRequestId",
                table: "ManualValidationRemarks",
                column: "ManualValidationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualValidationRequests_ExtractionDate",
                table: "ManualValidationRequests",
                column: "ExtractionDate");

            migrationBuilder.CreateIndex(
                name: "IX_ManualValidationRequests_RequestNumber",
                table: "ManualValidationRequests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManualValidationRequests_Status",
                table: "ManualValidationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_Key",
                table: "Menus",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationDocuments_MigrationRecordId",
                table: "MigrationDocuments",
                column: "MigrationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRecords_MigrationDate",
                table: "MigrationRecords",
                column: "MigrationDate");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRecords_MigrationStatus",
                table: "MigrationRecords",
                column: "MigrationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRecords_RequestNumber",
                table: "MigrationRecords",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistryOffices_Code",
                table: "RegistryOffices",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RootPathHistories_ModifiedAt",
                table: "RootPathHistories",
                column: "ModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TitleSequenceLookups_Title_TitleType_Plan_Block_Lot",
                table: "TitleSequenceLookups",
                columns: new[] { "Title", "TitleType", "Plan", "Block", "Lot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMenuPermissions_MenuId",
                table: "UserMenuPermissions",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmptyFolderRecords");

            migrationBuilder.DropTable(
                name: "FetchRuns");

            migrationBuilder.DropTable(
                name: "ManualValidationDocuments");

            migrationBuilder.DropTable(
                name: "ManualValidationRemarks");

            migrationBuilder.DropTable(
                name: "MigrationDocuments");

            migrationBuilder.DropTable(
                name: "RegistryOffices");

            migrationBuilder.DropTable(
                name: "RootPathHistories");

            migrationBuilder.DropTable(
                name: "SessionSettings");

            migrationBuilder.DropTable(
                name: "TitleSequenceLookups");

            migrationBuilder.DropTable(
                name: "UserMenuPermissions");

            migrationBuilder.DropTable(
                name: "ManualValidationRequests");

            migrationBuilder.DropTable(
                name: "MigrationRecords");

            migrationBuilder.DropTable(
                name: "Menus");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
