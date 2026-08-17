using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCT_SD.Migrations
{
    /// <inheritdoc />
    public partial class AddOcrExtractionAndDocumentTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ManualValidationDocuments_ManualValidationRequests_ManualValidationRequestId",
                table: "ManualValidationDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ManualValidationRemarks_ManualValidationRequests_ManualValidationRequestId",
                table: "ManualValidationRemarks");

            migrationBuilder.DropForeignKey(
                name: "FK_MigrationDocuments_MigrationRecords_MigrationRecordId",
                table: "MigrationDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_UserMenuPermissions_Menus_MenuId",
                table: "UserMenuPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserMenuPermissions_Users_UserId",
                table: "UserMenuPermissions");

            migrationBuilder.DropColumn(
                name: "Entry",
                table: "MigrationRecords");

            migrationBuilder.DropColumn(
                name: "Entry",
                table: "ManualValidationRequests");

            migrationBuilder.AddColumn<string>(
                name: "EntryNumbersCsv",
                table: "MigrationRecords",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OcrExtractionRecordId",
                table: "MigrationRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentTypeId",
                table: "MigrationDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntryNumbersCsv",
                table: "ManualValidationRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OcrExtractionRecordId",
                table: "ManualValidationRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentTypeId",
                table: "ManualValidationDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastProcessedAt",
                table: "FetchRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastProcessedFolderPath",
                table: "FetchRuns",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OcrExtractionRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FetchRunId = table.Column<int>(type: "int", nullable: true),
                    RdCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RdName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FolderPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TitleNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TitleType = table.Column<int>(type: "int", nullable: true),
                    DocumentCount = table.Column<int>(type: "int", nullable: false),
                    ExtractionStatus = table.Column<int>(type: "int", nullable: false),
                    ExtractionDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcrExtractionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcrExtractionRecords_FetchRuns_FetchRunId",
                        column: x => x.FetchRunId,
                        principalTable: "FetchRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OcrExtractionEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OcrExtractionRecordId = table.Column<int>(type: "int", nullable: false),
                    EntryNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcrExtractionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcrExtractionEntries_OcrExtractionRecords_OcrExtractionRecordId",
                        column: x => x.OcrExtractionRecordId,
                        principalTable: "OcrExtractionRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRecords_OcrExtractionRecordId",
                table: "MigrationRecords",
                column: "OcrExtractionRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationDocuments_DocumentTypeId",
                table: "MigrationDocuments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualValidationRequests_OcrExtractionRecordId",
                table: "ManualValidationRequests",
                column: "OcrExtractionRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualValidationDocuments_DocumentTypeId",
                table: "ManualValidationDocuments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmptyFolderRecords_FolderPath",
                table: "EmptyFolderRecords",
                column: "FolderPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_DocumentCode",
                table: "DocumentTypes",
                column: "DocumentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_DocumentName",
                table: "DocumentTypes",
                column: "DocumentName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcrExtractionEntries_EntryNumber",
                table: "OcrExtractionEntries",
                column: "EntryNumber");

            migrationBuilder.CreateIndex(
                name: "IX_OcrExtractionEntries_OcrExtractionRecordId_EntryNumber",
                table: "OcrExtractionEntries",
                columns: new[] { "OcrExtractionRecordId", "EntryNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcrExtractionRecords_ExtractionDateTime",
                table: "OcrExtractionRecords",
                column: "ExtractionDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_OcrExtractionRecords_ExtractionStatus",
                table: "OcrExtractionRecords",
                column: "ExtractionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_OcrExtractionRecords_FetchRunId",
                table: "OcrExtractionRecords",
                column: "FetchRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OcrExtractionRecords_FolderPath",
                table: "OcrExtractionRecords",
                column: "FolderPath");

            migrationBuilder.CreateIndex(
                name: "IX_OcrExtractionRecords_RequestNumber",
                table: "OcrExtractionRecords",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ManualValidationDocuments_DocumentTypes_DocumentTypeId",
                table: "ManualValidationDocuments",
                column: "DocumentTypeId",
                principalTable: "DocumentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ManualValidationDocuments_ManualValidationRequests_ManualValidationRequestId",
                table: "ManualValidationDocuments",
                column: "ManualValidationRequestId",
                principalTable: "ManualValidationRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ManualValidationRemarks_ManualValidationRequests_ManualValidationRequestId",
                table: "ManualValidationRemarks",
                column: "ManualValidationRequestId",
                principalTable: "ManualValidationRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ManualValidationRequests_OcrExtractionRecords_OcrExtractionRecordId",
                table: "ManualValidationRequests",
                column: "OcrExtractionRecordId",
                principalTable: "OcrExtractionRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MigrationDocuments_DocumentTypes_DocumentTypeId",
                table: "MigrationDocuments",
                column: "DocumentTypeId",
                principalTable: "DocumentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MigrationDocuments_MigrationRecords_MigrationRecordId",
                table: "MigrationDocuments",
                column: "MigrationRecordId",
                principalTable: "MigrationRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MigrationRecords_OcrExtractionRecords_OcrExtractionRecordId",
                table: "MigrationRecords",
                column: "OcrExtractionRecordId",
                principalTable: "OcrExtractionRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserMenuPermissions_Menus_MenuId",
                table: "UserMenuPermissions",
                column: "MenuId",
                principalTable: "Menus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserMenuPermissions_Users_UserId",
                table: "UserMenuPermissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ManualValidationDocuments_DocumentTypes_DocumentTypeId",
                table: "ManualValidationDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ManualValidationDocuments_ManualValidationRequests_ManualValidationRequestId",
                table: "ManualValidationDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ManualValidationRemarks_ManualValidationRequests_ManualValidationRequestId",
                table: "ManualValidationRemarks");

            migrationBuilder.DropForeignKey(
                name: "FK_ManualValidationRequests_OcrExtractionRecords_OcrExtractionRecordId",
                table: "ManualValidationRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MigrationDocuments_DocumentTypes_DocumentTypeId",
                table: "MigrationDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_MigrationDocuments_MigrationRecords_MigrationRecordId",
                table: "MigrationDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_MigrationRecords_OcrExtractionRecords_OcrExtractionRecordId",
                table: "MigrationRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_UserMenuPermissions_Menus_MenuId",
                table: "UserMenuPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserMenuPermissions_Users_UserId",
                table: "UserMenuPermissions");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropTable(
                name: "OcrExtractionEntries");

            migrationBuilder.DropTable(
                name: "OcrExtractionRecords");

            migrationBuilder.DropIndex(
                name: "IX_MigrationRecords_OcrExtractionRecordId",
                table: "MigrationRecords");

            migrationBuilder.DropIndex(
                name: "IX_MigrationDocuments_DocumentTypeId",
                table: "MigrationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ManualValidationRequests_OcrExtractionRecordId",
                table: "ManualValidationRequests");

            migrationBuilder.DropIndex(
                name: "IX_ManualValidationDocuments_DocumentTypeId",
                table: "ManualValidationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmptyFolderRecords_FolderPath",
                table: "EmptyFolderRecords");

            migrationBuilder.DropColumn(
                name: "EntryNumbersCsv",
                table: "MigrationRecords");

            migrationBuilder.DropColumn(
                name: "OcrExtractionRecordId",
                table: "MigrationRecords");

            migrationBuilder.DropColumn(
                name: "DocumentTypeId",
                table: "MigrationDocuments");

            migrationBuilder.DropColumn(
                name: "EntryNumbersCsv",
                table: "ManualValidationRequests");

            migrationBuilder.DropColumn(
                name: "OcrExtractionRecordId",
                table: "ManualValidationRequests");

            migrationBuilder.DropColumn(
                name: "DocumentTypeId",
                table: "ManualValidationDocuments");

            migrationBuilder.DropColumn(
                name: "LastProcessedAt",
                table: "FetchRuns");

            migrationBuilder.DropColumn(
                name: "LastProcessedFolderPath",
                table: "FetchRuns");

            migrationBuilder.AddColumn<string>(
                name: "Entry",
                table: "MigrationRecords",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Entry",
                table: "ManualValidationRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ManualValidationDocuments_ManualValidationRequests_ManualValidationRequestId",
                table: "ManualValidationDocuments",
                column: "ManualValidationRequestId",
                principalTable: "ManualValidationRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ManualValidationRemarks_ManualValidationRequests_ManualValidationRequestId",
                table: "ManualValidationRemarks",
                column: "ManualValidationRequestId",
                principalTable: "ManualValidationRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MigrationDocuments_MigrationRecords_MigrationRecordId",
                table: "MigrationDocuments",
                column: "MigrationRecordId",
                principalTable: "MigrationRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserMenuPermissions_Menus_MenuId",
                table: "UserMenuPermissions",
                column: "MenuId",
                principalTable: "Menus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserMenuPermissions_Users_UserId",
                table: "UserMenuPermissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
