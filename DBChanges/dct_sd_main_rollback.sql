/* =============================================================================
   DCT_SD - Rollback Script for dct_sd_main.sql
   File: dct_sd_main_rollback.sql
   Pairs with: dct_sd_main.sql (the deployment script this reverses)

   Scope: reverses EVERY object and data change dct_sd_main.sql makes - not
   just the Section 23 Hist/Log additions the previous version of this file
   covered. In reverse-dependency order, this script:
     1. Revokes every GRANT dct_sd_main.sql issues (Section 21's 14 main
        tables, Section 23's 22 _Hist/_Log tables).
     2. Drops the 33 trg_<Table>_I/_U/_D triggers (Section 23, 3 per table).
     3. Drops the 22 _Hist/_Log tables (Section 23).
     4. Drops dbo.__EFMigrationsHistory (Section 22) - this table was newly
        created by the deployment, so the whole table is removed, not just
        its baseline rows (see Safety Gate below for when this is allowed).
     5. Drops the 18 main/legacy tables from Sections 1-18 (Users,
        RefreshTokens, FetchRuns, OcrExtractionRecords, OcrExtractionEntries,
        CodeLookups, ManualValidationRequests, MigrationRecords,
        MigrationDocuments, EmptyFolderRecords, FailedExtractionRecords,
        AppSettings, AuditLog, RecordHistory, ImageExtractionStaging,
        BrandingSettings, EmailTemplates, DocumentTypes), in FK-safe
        children-before-parents order. Dropping a table removes its PK,
        FKs, unique/non-unique indexes, default constraints, and check
        constraints with it - these never need a separate DROP statement.

   Nothing in dct_sd_main.sql modifies a pre-existing object (no ALTER on
   anything that existed before this deployment, no stored procedures,
   views, or functions at all) - every object listed above was newly
   created by the deployment this script pairs with, on what was an empty
   database (see dct_sd_main.sql's own header). There is therefore no
   "restore the original definition" step required anywhere in this
   rollback - full removal IS the correct reversal for every object here.

   SAFETY GATE (critical - read before running):
   These are real application tables, not scratch objects, and by the time
   someone needs to roll back, the application may already have written
   real records into them. This script will NOT blindly drop tables that
   contain data. Before making any change at all, it checks every table
   dct_sd_main.sql creates for existing rows (and __EFMigrationsHistory
   specifically for migrations beyond this release's 5-row baseline). If
   anything is found, the ENTIRE rollback aborts with no changes made, and
   raises an error naming exactly which objects need a human decision
   (export the data, confirm it's disposable test data, etc.) before a
   schema rollback can proceed. This directly satisfies "do not delete
   existing production data or records created after deployment" - rather
   than assuming emptiness, it verifies it first and refuses to guess.

   This rollback is designed to be run within the same release/validation
   window as the deployment it pairs with - i.e. before the application
   has been pointed at live traffic against this schema. If it's being run
   long after go-live, the safety gate above is expected to trip, and that
   is the correct, intended outcome - it means this is no longer a same-
   release rollback but a decision requiring a DBA/stakeholder review.

   NOT reversible by this script / things that must be resolved manually
   first (see also Rollback_Mapping_And_Validation.md alongside this file):
     - Anything the safety gate flags. The script tells you exactly what
       and why; it does not attempt to work around it.
     - This script has not been executed against a live SQL Server by the
       author as of this version - see the validation checklist in
       Rollback_Mapping_And_Validation.md for how to do that (the
       DBChanges/local_schema_test/ harness in this repo exercises it
       against an isolated "scripttest" schema with no risk to real data).
     - Requires SQL Server 2016+ (uses DROP TABLE/TRIGGER IF EXISTS syntax).

   Before running: select the correct target database (this script
   intentionally does not include a USE statement, to avoid the exact
   mistake this project already made once with an unguarded USE statement -
   confirm you are connected to the right database before executing).

   Safe to re-run: every step past the safety gate is itself idempotent
   (IF EXISTS / IF NOT EXISTS / *_IF EXISTS guarded), so re-running this
   after a successful run is a no-op, not an error.
   ============================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @problem NVARCHAR(MAX) = N'';

    /* =========================================================================
       STEP 0 - SAFETY GATE
       Refuse to touch anything if any table this deployment created already
       holds data. See header note above.
       ========================================================================= */
    IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Users)
        SET @problem = @problem + 'Users; ';
    IF OBJECT_ID('dbo.RefreshTokens', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.RefreshTokens)
        SET @problem = @problem + 'RefreshTokens; ';
    IF OBJECT_ID('dbo.FetchRuns', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.FetchRuns)
        SET @problem = @problem + 'FetchRuns; ';
    IF OBJECT_ID('dbo.OcrExtractionRecords', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.OcrExtractionRecords)
        SET @problem = @problem + 'OcrExtractionRecords; ';
    IF OBJECT_ID('dbo.OcrExtractionEntries', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.OcrExtractionEntries)
        SET @problem = @problem + 'OcrExtractionEntries; ';
    IF OBJECT_ID('dbo.CodeLookups', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.CodeLookups)
        SET @problem = @problem + 'CodeLookups; ';
    IF OBJECT_ID('dbo.ManualValidationRequests', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.ManualValidationRequests)
        SET @problem = @problem + 'ManualValidationRequests; ';
    IF OBJECT_ID('dbo.MigrationRecords', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.MigrationRecords)
        SET @problem = @problem + 'MigrationRecords; ';
    IF OBJECT_ID('dbo.MigrationDocuments', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.MigrationDocuments)
        SET @problem = @problem + 'MigrationDocuments; ';
    IF OBJECT_ID('dbo.EmptyFolderRecords', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.EmptyFolderRecords)
        SET @problem = @problem + 'EmptyFolderRecords; ';
    IF OBJECT_ID('dbo.FailedExtractionRecords', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.FailedExtractionRecords)
        SET @problem = @problem + 'FailedExtractionRecords; ';
    IF OBJECT_ID('dbo.AppSettings', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.AppSettings)
        SET @problem = @problem + 'AppSettings; ';
    IF OBJECT_ID('dbo.AuditLog', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.AuditLog)
        SET @problem = @problem + 'AuditLog; ';
    IF OBJECT_ID('dbo.RecordHistory', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.RecordHistory)
        SET @problem = @problem + 'RecordHistory; ';
    IF OBJECT_ID('dbo.ImageExtractionStaging', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.ImageExtractionStaging)
        SET @problem = @problem + 'ImageExtractionStaging; ';
    IF OBJECT_ID('dbo.BrandingSettings', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.BrandingSettings)
        SET @problem = @problem + 'BrandingSettings; ';
    IF OBJECT_ID('dbo.EmailTemplates', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.EmailTemplates)
        SET @problem = @problem + 'EmailTemplates; ';
    IF OBJECT_ID('dbo.DocumentTypes', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.DocumentTypes)
        SET @problem = @problem + 'DocumentTypes; ';

    -- The 22 Hist/Log tables are populated only by the triggers above firing
    -- on the main tables, so if the main tables are empty these should be
    -- too - checked explicitly anyway, for defense in depth.
    IF OBJECT_ID('dbo.Users_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Users_Log)
        SET @problem = @problem + 'Users_Log; ';
    IF OBJECT_ID('dbo.Users_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Users_Hist)
        SET @problem = @problem + 'Users_Hist; ';
    IF OBJECT_ID('dbo.RefreshTokens_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.RefreshTokens_Log)
        SET @problem = @problem + 'RefreshTokens_Log; ';
    IF OBJECT_ID('dbo.RefreshTokens_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.RefreshTokens_Hist)
        SET @problem = @problem + 'RefreshTokens_Hist; ';
    IF OBJECT_ID('dbo.AppSettings_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.AppSettings_Log)
        SET @problem = @problem + 'AppSettings_Log; ';
    IF OBJECT_ID('dbo.AppSettings_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.AppSettings_Hist)
        SET @problem = @problem + 'AppSettings_Hist; ';
    IF OBJECT_ID('dbo.CodeLookups_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.CodeLookups_Log)
        SET @problem = @problem + 'CodeLookups_Log; ';
    IF OBJECT_ID('dbo.CodeLookups_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.CodeLookups_Hist)
        SET @problem = @problem + 'CodeLookups_Hist; ';
    IF OBJECT_ID('dbo.FetchRuns_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.FetchRuns_Log)
        SET @problem = @problem + 'FetchRuns_Log; ';
    IF OBJECT_ID('dbo.FetchRuns_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.FetchRuns_Hist)
        SET @problem = @problem + 'FetchRuns_Hist; ';
    IF OBJECT_ID('dbo.OcrExtractionRecords_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.OcrExtractionRecords_Log)
        SET @problem = @problem + 'OcrExtractionRecords_Log; ';
    IF OBJECT_ID('dbo.OcrExtractionRecords_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.OcrExtractionRecords_Hist)
        SET @problem = @problem + 'OcrExtractionRecords_Hist; ';
    IF OBJECT_ID('dbo.ManualValidationRequests_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.ManualValidationRequests_Log)
        SET @problem = @problem + 'ManualValidationRequests_Log; ';
    IF OBJECT_ID('dbo.ManualValidationRequests_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.ManualValidationRequests_Hist)
        SET @problem = @problem + 'ManualValidationRequests_Hist; ';
    IF OBJECT_ID('dbo.MigrationRecords_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.MigrationRecords_Log)
        SET @problem = @problem + 'MigrationRecords_Log; ';
    IF OBJECT_ID('dbo.MigrationRecords_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.MigrationRecords_Hist)
        SET @problem = @problem + 'MigrationRecords_Hist; ';
    IF OBJECT_ID('dbo.MigrationDocuments_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.MigrationDocuments_Log)
        SET @problem = @problem + 'MigrationDocuments_Log; ';
    IF OBJECT_ID('dbo.MigrationDocuments_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.MigrationDocuments_Hist)
        SET @problem = @problem + 'MigrationDocuments_Hist; ';
    IF OBJECT_ID('dbo.EmptyFolderRecords_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.EmptyFolderRecords_Log)
        SET @problem = @problem + 'EmptyFolderRecords_Log; ';
    IF OBJECT_ID('dbo.EmptyFolderRecords_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.EmptyFolderRecords_Hist)
        SET @problem = @problem + 'EmptyFolderRecords_Hist; ';
    IF OBJECT_ID('dbo.FailedExtractionRecords_Log', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.FailedExtractionRecords_Log)
        SET @problem = @problem + 'FailedExtractionRecords_Log; ';
    IF OBJECT_ID('dbo.FailedExtractionRecords_Hist', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.FailedExtractionRecords_Hist)
        SET @problem = @problem + 'FailedExtractionRecords_Hist; ';

    -- __EFMigrationsHistory: not a blanket "any row = abort" check (this
    -- table is SUPPOSED to have this release's 5 baseline rows) - abort
    -- only if it has MORE than that, meaning EF Core has applied further
    -- migrations since this deployment went out.
    IF OBJECT_ID('dbo.__EFMigrationsHistory', 'U') IS NOT NULL AND EXISTS (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId NOT IN (
            '20260812140334_InitialCreate',
            '20260812151625_AddRefreshTokens',
            '20260813060813_AddSettings',
            '20260817101906_AddOcrExtractionAndDocumentTypes',
            '20261005070000_AddUserLockoutEnd'
        )
    )
        SET @problem = @problem + '__EFMigrationsHistory (has migrations beyond this release''s baseline); ';

    IF LEN(@problem) > 0
    BEGIN
        RAISERROR('Rollback aborted, NO changes made. These objects contain data created after deployment and need a human decision (export/confirm disposable) before a schema rollback can proceed: %s', 16, 1, @problem);
    END

    /* =========================================================================
       STEP 1 - Revoke every grant dct_sd_main.sql issues
       (Section 23's 22 _Hist/_Log tables, then Section 21's 14 main tables)
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

        IF OBJECT_ID('dbo.Users','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.Users FROM [DCTSDUser];
        IF OBJECT_ID('dbo.RefreshTokens','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.RefreshTokens FROM [DCTSDUser];
        IF OBJECT_ID('dbo.FetchRuns','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.FetchRuns FROM [DCTSDUser];
        IF OBJECT_ID('dbo.OcrExtractionRecords','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.OcrExtractionRecords FROM [DCTSDUser];
        IF OBJECT_ID('dbo.CodeLookups','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.CodeLookups FROM [DCTSDUser];
        IF OBJECT_ID('dbo.ManualValidationRequests','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.ManualValidationRequests FROM [DCTSDUser];
        IF OBJECT_ID('dbo.MigrationRecords','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.MigrationRecords FROM [DCTSDUser];
        IF OBJECT_ID('dbo.MigrationDocuments','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.MigrationDocuments FROM [DCTSDUser];
        IF OBJECT_ID('dbo.EmptyFolderRecords','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.EmptyFolderRecords FROM [DCTSDUser];
        IF OBJECT_ID('dbo.FailedExtractionRecords','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.FailedExtractionRecords FROM [DCTSDUser];
        IF OBJECT_ID('dbo.AppSettings','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.AppSettings FROM [DCTSDUser];
        IF OBJECT_ID('dbo.ImageExtractionStaging','U') IS NOT NULL REVOKE SELECT, INSERT, UPDATE, DELETE ON dbo.ImageExtractionStaging FROM [DCTSDUser];
        IF OBJECT_ID('dbo.RecordHistory','U') IS NOT NULL REVOKE SELECT, INSERT ON dbo.RecordHistory FROM [DCTSDUser];
        IF OBJECT_ID('dbo.AuditLog','U') IS NOT NULL REVOKE SELECT ON dbo.AuditLog FROM [DCTSDUser];
    END

    /* =========================================================================
       STEP 2 - Drop the 33 per-DML-type triggers (Section 23): each of the
       11 tables has trg_<Table>_I / _U / _D (guideline 6.e.1), not one
       combined trigger.
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
       STEP 3 - Drop the 22 _Hist/_Log tables (Section 23)
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

    /* =========================================================================
       STEP 4 - dbo.__EFMigrationsHistory (Section 22)
       The safety gate already confirmed this table has nothing beyond this
       release's own baseline rows, so the whole table (newly created by
       this deployment) is dropped.
       ========================================================================= */
    DROP TABLE IF EXISTS dbo.__EFMigrationsHistory;

    /* =========================================================================
       STEP 5 - Drop the 18 main/legacy tables (Sections 1-18), FK-safe
       children-before-parents order. Dropping a table removes its PK, FKs,
       indexes, default constraints and check constraints with it.
       ========================================================================= */
    DROP TABLE IF EXISTS dbo.MigrationDocuments;          -- FK -> MigrationRecords, CodeLookups
    DROP TABLE IF EXISTS dbo.MigrationRecords;             -- FK -> OcrExtractionRecords
    DROP TABLE IF EXISTS dbo.ManualValidationRequests;     -- FK -> OcrExtractionRecords
    DROP TABLE IF EXISTS dbo.OcrExtractionEntries;         -- FK -> OcrExtractionRecords
    DROP TABLE IF EXISTS dbo.OcrExtractionRecords;         -- FK -> FetchRuns
    DROP TABLE IF EXISTS dbo.RefreshTokens;                -- FK -> Users
    DROP TABLE IF EXISTS dbo.FetchRuns;
    DROP TABLE IF EXISTS dbo.CodeLookups;
    DROP TABLE IF EXISTS dbo.Users;
    DROP TABLE IF EXISTS dbo.EmptyFolderRecords;
    DROP TABLE IF EXISTS dbo.FailedExtractionRecords;
    DROP TABLE IF EXISTS dbo.AppSettings;
    DROP TABLE IF EXISTS dbo.AuditLog;
    DROP TABLE IF EXISTS dbo.RecordHistory;
    DROP TABLE IF EXISTS dbo.ImageExtractionStaging;
    DROP TABLE IF EXISTS dbo.BrandingSettings;
    DROP TABLE IF EXISTS dbo.EmailTemplates;
    DROP TABLE IF EXISTS dbo.DocumentTypes;

    COMMIT TRANSACTION;
    PRINT 'dct_sd_main_rollback.sql completed successfully - every object created by dct_sd_main.sql has been removed.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrMsg NVARCHAR(MAX) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
END CATCH
GO
