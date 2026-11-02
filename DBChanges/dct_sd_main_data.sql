/* =============================================================================
   DCT_SD - Data Population Script for dct_sd_main.sql
   File: dct_sd_main_data.sql
   Pairs with: dct_sd_main.sql (run this immediately after it, same release)

   Scope: data only, no schema changes. Populates dbo.__EFMigrationsHistory
   with the 5 baseline rows that mark this schema's migrations as already
   applied, so a future `dotnet ef database update` sees them as already
   applied instead of trying to re-run them against tables that already
   exist (Section 22 of dct_sd_main.sql creates the table itself; this
   script only inserts into it). Any new migration added after this
   baseline still runs normally.

   Idempotent: each INSERT is guarded (IF NOT EXISTS on MigrationId), so
   this is safe to re-run.

   Before running: select the correct target database (intentionally no
   USE statement - confirm you are connected to the right database first).

   Rollback: removing these specific rows is covered by
   dct_sd_main_rollback.sql, which drops dbo.__EFMigrationsHistory in full
   (see that script's Step 4 and its header for when that's safe to do).
   ============================================================================= */

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20260812140334_InitialCreate')
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260812140334_InitialCreate', '8.0.10');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20260812151625_AddRefreshTokens')
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260812151625_AddRefreshTokens', '8.0.10');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20260813060813_AddSettings')
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260813060813_AddSettings', '8.0.10');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20260817101906_AddOcrExtractionAndDocumentTypes')
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260817101906_AddOcrExtractionAndDocumentTypes', '8.0.10');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261005070000_AddUserLockoutEnd')
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20261005070000_AddUserLockoutEnd', '8.0.10');
GO

PRINT 'dct_sd_main_data.sql completed - __EFMigrationsHistory baselined with 5 migration rows.';
GO
