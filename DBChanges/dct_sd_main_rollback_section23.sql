/* =============================================================================
   DCT_SD - Rollback Script (Section 23 ONLY) for dct_sd_main.sql
   File: dct_sd_main_rollback_section23.sql
   Pairs with: dct_sd_main.sql (reverses only its Section 23 additions)

   USE THIS SCRIPT (not dct_sd_main_rollback.sql) once the application has
   gone live and real data exists in the main application tables (Users,
   RefreshTokens, FetchRuns, OcrExtractionRecords, CodeLookups,
   ManualValidationRequests, MigrationRecords, MigrationDocuments,
   EmptyFolderRecords, FailedExtractionRecords, AppSettings,
   ImageExtractionStaging, RecordHistory, AuditLog, and the legacy
   BrandingSettings/EmailTemplates/DocumentTypes/OcrExtractionEntries).

   This script NEVER reads from, writes to, or drops any of those 18 main/
   legacy tables, and never touches dbo.__EFMigrationsHistory. It only
   reverses the Section 23 additions:
     1. Revokes the 22 GRANTs Section 23 issues (11 _Hist tables get
        SELECT+INSERT, 11 _Log tables get SELECT).
     2. Drops the 33 trg_<Table>_I/_U/_D triggers (3 per table, per
        guideline 6.e.1).
     3. Drops the 22 _Hist/_Log tables themselves.

   Because none of the above is read by the application at runtime (they
   exist purely to record what the triggers write), and because this script
   never touches a single main application table, this is safe to run
   AFTER go-live, with real production data already in the main tables -
   that data is never at risk, because this script never goes near it.

   What this script does NOT do / what you lose by running it:
     - Any audit history already recorded in the 22 _Hist/_Log tables (who
       changed what, and when) is deleted along with those tables. This is
       the expected, accepted cost of removing the audit feature itself -
       it is history ABOUT the main tables, not data belonging to them, and
       the main tables' own current data is completely unaffected.
     - The application's own functionality is unaffected after this runs:
       EF Core's UseSqlOutputClause(false) setting on these tables (added
       to make the triggers possible) is a no-op whether or not a trigger
       exists, so every insert/update/delete on the main tables keeps
       working exactly as before Section 23 was ever deployed - the app
       simply stops producing new Hist/Log rows, because there is no
       trigger left to write them.
     - No application code change is required to run this script, and none
       is required afterward.

   If the application itself (not just the audit feature) needs to be
   rolled back - i.e. you need to remove the main tables too - see
   dct_sd_main_rollback.sql instead. That script is explicitly scoped to a
   PRE-go-live window (same release, before real traffic) and will safely
   refuse to run once the main tables hold real data, by design - it is not
   a substitute for this script once the application is live.

   Safe to re-run: every step is IF EXISTS-guarded, so running this again
   after a successful run is a no-op, not an error.

   Before running: select the correct target database first (this script
   intentionally has no USE statement - confirm you are connected to the
   right database before executing, exactly like its sibling scripts).

   Requires SQL Server 2016+ (uses DROP TABLE/TRIGGER IF EXISTS syntax).
   ============================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    /* =========================================================================
       STEP 1 - Revoke the 22 grants Section 23 issues
       ========================================================================= */
    IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'DCTSDUser')
    BEGIN
        IF OBJECT_ID('dbo.Users_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.Users_Hist FROM [DCTSDUser];
        IF OBJECT_ID('dbo.RefreshTokens_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.RefreshTokens_Hist FROM [DCTSDUser];
        IF OBJECT_ID('dbo.AppSettings_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.AppSettings_Hist FROM [DCTSDUser];
        IF OBJECT_ID('dbo.CodeLookups_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.CodeLookups_Hist FROM [DCTSDUser];
        IF OBJECT_ID('dbo.FetchRuns_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.FetchRuns_Hist FROM [DCTSDUser];
        IF OBJECT_ID('dbo.OcrExtractionRecords_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.OcrExtractionRecords_Hist FROM [DCTSDUser];
        IF OBJECT_ID('dbo.ManualValidationRequests_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.ManualValidationRequests_Hist FROM [DCTSDUser];
        IF OBJECT_ID('dbo.MigrationRecords_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.MigrationRecords_Hist FROM [DCTSDUser];
        IF OBJECT_ID('dbo.MigrationDocuments_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.MigrationDocuments_Hist FROM [DCTSDUser];
        IF OBJECT_ID('dbo.EmptyFolderRecords_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.EmptyFolderRecords_Hist FROM [DCTSDUser];
        IF OBJECT_ID('dbo.FailedExtractionRecords_Hist','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.FailedExtractionRecords_Hist FROM [DCTSDUser];

        IF OBJECT_ID('dbo.Users_Log','U') IS NOT NULL REVOKE SELECT ON dbo.Users_Log FROM [DCTSDUser];
        IF OBJECT_ID('dbo.RefreshTokens_Log','U') IS NOT NULL REVOKE SELECT ON dbo.RefreshTokens_Log FROM [DCTSDUser];
        IF OBJECT_ID('dbo.AppSettings_Log','U') IS NOT NULL REVOKE SELECT ON dbo.AppSettings_Log FROM [DCTSDUser];
        IF OBJECT_ID('dbo.CodeLookups_Log','U') IS NOT NULL REVOKE SELECT ON dbo.CodeLookups_Log FROM [DCTSDUser];
        IF OBJECT_ID('dbo.FetchRuns_Log','U') IS NOT NULL REVOKE SELECT ON dbo.FetchRuns_Log FROM [DCTSDUser];
        IF OBJECT_ID('dbo.OcrExtractionRecords_Log','U') IS NOT NULL REVOKE SELECT ON dbo.OcrExtractionRecords_Log FROM [DCTSDUser];
        IF OBJECT_ID('dbo.ManualValidationRequests_Log','U') IS NOT NULL REVOKE SELECT ON dbo.ManualValidationRequests_Log FROM [DCTSDUser];
        IF OBJECT_ID('dbo.MigrationRecords_Log','U') IS NOT NULL REVOKE SELECT ON dbo.MigrationRecords_Log FROM [DCTSDUser];
        IF OBJECT_ID('dbo.MigrationDocuments_Log','U') IS NOT NULL REVOKE SELECT ON dbo.MigrationDocuments_Log FROM [DCTSDUser];
        IF OBJECT_ID('dbo.EmptyFolderRecords_Log','U') IS NOT NULL REVOKE SELECT ON dbo.EmptyFolderRecords_Log FROM [DCTSDUser];
        IF OBJECT_ID('dbo.FailedExtractionRecords_Log','U') IS NOT NULL REVOKE SELECT ON dbo.FailedExtractionRecords_Log FROM [DCTSDUser];
    END

    /* =========================================================================
       STEP 2 - Drop the 33 per-DML-type triggers (3 per table: _I/_U/_D,
       per guideline 6.e.1)
       ========================================================================= */
    DROP TRIGGER IF EXISTS dbo.trg_Users_I;
    DROP TRIGGER IF EXISTS dbo.trg_Users_U;
    DROP TRIGGER IF EXISTS dbo.trg_Users_D;
    DROP TRIGGER IF EXISTS dbo.trg_RefreshTokens_I;
    DROP TRIGGER IF EXISTS dbo.trg_RefreshTokens_U;
    DROP TRIGGER IF EXISTS dbo.trg_RefreshTokens_D;
    DROP TRIGGER IF EXISTS dbo.trg_AppSettings_I;
    DROP TRIGGER IF EXISTS dbo.trg_AppSettings_U;
    DROP TRIGGER IF EXISTS dbo.trg_AppSettings_D;
    DROP TRIGGER IF EXISTS dbo.trg_CodeLookups_I;
    DROP TRIGGER IF EXISTS dbo.trg_CodeLookups_U;
    DROP TRIGGER IF EXISTS dbo.trg_CodeLookups_D;
    DROP TRIGGER IF EXISTS dbo.trg_FetchRuns_I;
    DROP TRIGGER IF EXISTS dbo.trg_FetchRuns_U;
    DROP TRIGGER IF EXISTS dbo.trg_FetchRuns_D;
    DROP TRIGGER IF EXISTS dbo.trg_OcrExtractionRecords_I;
    DROP TRIGGER IF EXISTS dbo.trg_OcrExtractionRecords_U;
    DROP TRIGGER IF EXISTS dbo.trg_OcrExtractionRecords_D;
    DROP TRIGGER IF EXISTS dbo.trg_ManualValidationRequests_I;
    DROP TRIGGER IF EXISTS dbo.trg_ManualValidationRequests_U;
    DROP TRIGGER IF EXISTS dbo.trg_ManualValidationRequests_D;
    DROP TRIGGER IF EXISTS dbo.trg_MigrationRecords_I;
    DROP TRIGGER IF EXISTS dbo.trg_MigrationRecords_U;
    DROP TRIGGER IF EXISTS dbo.trg_MigrationRecords_D;
    DROP TRIGGER IF EXISTS dbo.trg_MigrationDocuments_I;
    DROP TRIGGER IF EXISTS dbo.trg_MigrationDocuments_U;
    DROP TRIGGER IF EXISTS dbo.trg_MigrationDocuments_D;
    DROP TRIGGER IF EXISTS dbo.trg_EmptyFolderRecords_I;
    DROP TRIGGER IF EXISTS dbo.trg_EmptyFolderRecords_U;
    DROP TRIGGER IF EXISTS dbo.trg_EmptyFolderRecords_D;
    DROP TRIGGER IF EXISTS dbo.trg_FailedExtractionRecords_I;
    DROP TRIGGER IF EXISTS dbo.trg_FailedExtractionRecords_U;
    DROP TRIGGER IF EXISTS dbo.trg_FailedExtractionRecords_D;

    /* =========================================================================
       STEP 3 - Drop the 22 _Hist/_Log tables
       Any audit rows already recorded in these tables are deleted with them
       (see header "What this script does NOT do" note) - this is history
       ABOUT the main tables, not main-table data, which is never touched.
       ========================================================================= */
    DROP TABLE IF EXISTS dbo.Users_Hist;
    DROP TABLE IF EXISTS dbo.Users_Log;
    DROP TABLE IF EXISTS dbo.RefreshTokens_Hist;
    DROP TABLE IF EXISTS dbo.RefreshTokens_Log;
    DROP TABLE IF EXISTS dbo.AppSettings_Hist;
    DROP TABLE IF EXISTS dbo.AppSettings_Log;
    DROP TABLE IF EXISTS dbo.CodeLookups_Hist;
    DROP TABLE IF EXISTS dbo.CodeLookups_Log;
    DROP TABLE IF EXISTS dbo.FetchRuns_Hist;
    DROP TABLE IF EXISTS dbo.FetchRuns_Log;
    DROP TABLE IF EXISTS dbo.OcrExtractionRecords_Hist;
    DROP TABLE IF EXISTS dbo.OcrExtractionRecords_Log;
    DROP TABLE IF EXISTS dbo.ManualValidationRequests_Hist;
    DROP TABLE IF EXISTS dbo.ManualValidationRequests_Log;
    DROP TABLE IF EXISTS dbo.MigrationRecords_Hist;
    DROP TABLE IF EXISTS dbo.MigrationRecords_Log;
    DROP TABLE IF EXISTS dbo.MigrationDocuments_Hist;
    DROP TABLE IF EXISTS dbo.MigrationDocuments_Log;
    DROP TABLE IF EXISTS dbo.EmptyFolderRecords_Hist;
    DROP TABLE IF EXISTS dbo.EmptyFolderRecords_Log;
    DROP TABLE IF EXISTS dbo.FailedExtractionRecords_Hist;
    DROP TABLE IF EXISTS dbo.FailedExtractionRecords_Log;

    COMMIT TRANSACTION;
    PRINT 'dct_sd_main_rollback_section23.sql completed successfully - Section 23 (Hist/Log tables and triggers) removed. All main application tables and their data were never touched.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrMsg NVARCHAR(MAX) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
END CATCH
GO
