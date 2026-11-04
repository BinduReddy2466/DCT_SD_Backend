/* =============================================================================
   FULL rollback - drops ALL 16 main tables, their 16 _Log tables, 16 _Hist
   tables, and 48 triggers.

   *** WARNING - THIS PERMANENTLY DELETES ALL PRODUCTION DATA ***
   This is NOT limited to the audit trail. It also drops the main tables
   themselves (Users, RefreshTokens, AppSettings, CodeLookups, FetchRuns,
   OcrExtractionRecords, ManualValidationRequests, MigrationRecords,
   MigrationDocuments, EmptyFolderRecords, FailedExtractionRecords,
   BrandingSettings, EmailTemplates, DocumentTypes, OcrExtractionEntries,
   ImageExtractionStaging) - every user account, auth token, fetch run,
   OCR/migration/manual-validation record, etc. is destroyed and cannot be
   recovered without a database backup.

   Do NOT run this against a live/production environment unless the intent
   is a full environment teardown. For an audit-only rollback that leaves
   all application data intact, use DCT_SD_HistLog_Full_Rollback.sql instead.

   Main tables are dropped in foreign-key-safe order (children before
   parents, per the FK constraints defined in dct_sd_main.sql):
     MigrationDocuments -> ManualValidationRequests -> MigrationRecords ->
     OcrExtractionEntries -> OcrExtractionRecords -> RefreshTokens ->
     FetchRuns -> CodeLookups -> Users -> (remaining tables, no FKs)
   ============================================================================= */

USE DCT_SD;
GO

-------------------------------------------------------------------------------
-- MigrationDocuments  (FK -> MigrationRecords, CodeLookups)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_MigrationDocuments_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationDocuments_I;
IF OBJECT_ID('dbo.trg_MigrationDocuments_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationDocuments_U;
IF OBJECT_ID('dbo.trg_MigrationDocuments_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationDocuments_D;
GO
IF OBJECT_ID('dbo.MigrationDocuments_Hist', 'U') IS NOT NULL DROP TABLE dbo.MigrationDocuments_Hist;
IF OBJECT_ID('dbo.MigrationDocuments_Log', 'U') IS NOT NULL DROP TABLE dbo.MigrationDocuments_Log;
IF OBJECT_ID('dbo.MigrationDocuments', 'U') IS NOT NULL DROP TABLE dbo.MigrationDocuments;
GO

-------------------------------------------------------------------------------
-- ManualValidationRequests  (FK -> OcrExtractionRecords)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_ManualValidationRequests_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ManualValidationRequests_I;
IF OBJECT_ID('dbo.trg_ManualValidationRequests_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ManualValidationRequests_U;
IF OBJECT_ID('dbo.trg_ManualValidationRequests_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ManualValidationRequests_D;
GO
IF OBJECT_ID('dbo.ManualValidationRequests_Hist', 'U') IS NOT NULL DROP TABLE dbo.ManualValidationRequests_Hist;
IF OBJECT_ID('dbo.ManualValidationRequests_Log', 'U') IS NOT NULL DROP TABLE dbo.ManualValidationRequests_Log;
IF OBJECT_ID('dbo.ManualValidationRequests', 'U') IS NOT NULL DROP TABLE dbo.ManualValidationRequests;
GO

-------------------------------------------------------------------------------
-- MigrationRecords  (FK -> OcrExtractionRecords)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_MigrationRecords_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationRecords_I;
IF OBJECT_ID('dbo.trg_MigrationRecords_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationRecords_U;
IF OBJECT_ID('dbo.trg_MigrationRecords_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationRecords_D;
GO
IF OBJECT_ID('dbo.MigrationRecords_Hist', 'U') IS NOT NULL DROP TABLE dbo.MigrationRecords_Hist;
IF OBJECT_ID('dbo.MigrationRecords_Log', 'U') IS NOT NULL DROP TABLE dbo.MigrationRecords_Log;
IF OBJECT_ID('dbo.MigrationRecords', 'U') IS NOT NULL DROP TABLE dbo.MigrationRecords;
GO

-------------------------------------------------------------------------------
-- OcrExtractionEntries  (FK -> OcrExtractionRecords)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_OcrExtractionEntries_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionEntries_I;
IF OBJECT_ID('dbo.trg_OcrExtractionEntries_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionEntries_U;
IF OBJECT_ID('dbo.trg_OcrExtractionEntries_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionEntries_D;
GO
IF OBJECT_ID('dbo.OcrExtractionEntries_Hist', 'U') IS NOT NULL DROP TABLE dbo.OcrExtractionEntries_Hist;
IF OBJECT_ID('dbo.OcrExtractionEntries_Log', 'U') IS NOT NULL DROP TABLE dbo.OcrExtractionEntries_Log;
IF OBJECT_ID('dbo.OcrExtractionEntries', 'U') IS NOT NULL DROP TABLE dbo.OcrExtractionEntries;
GO

-------------------------------------------------------------------------------
-- OcrExtractionRecords  (FK -> FetchRuns)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_OcrExtractionRecords_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionRecords_I;
IF OBJECT_ID('dbo.trg_OcrExtractionRecords_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionRecords_U;
IF OBJECT_ID('dbo.trg_OcrExtractionRecords_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionRecords_D;
GO
IF OBJECT_ID('dbo.OcrExtractionRecords_Hist', 'U') IS NOT NULL DROP TABLE dbo.OcrExtractionRecords_Hist;
IF OBJECT_ID('dbo.OcrExtractionRecords_Log', 'U') IS NOT NULL DROP TABLE dbo.OcrExtractionRecords_Log;
IF OBJECT_ID('dbo.OcrExtractionRecords', 'U') IS NOT NULL DROP TABLE dbo.OcrExtractionRecords;
GO

-------------------------------------------------------------------------------
-- RefreshTokens  (FK -> Users)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_RefreshTokens_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_RefreshTokens_I;
IF OBJECT_ID('dbo.trg_RefreshTokens_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_RefreshTokens_U;
IF OBJECT_ID('dbo.trg_RefreshTokens_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_RefreshTokens_D;
GO
IF OBJECT_ID('dbo.RefreshTokens_Hist', 'U') IS NOT NULL DROP TABLE dbo.RefreshTokens_Hist;
IF OBJECT_ID('dbo.RefreshTokens_Log', 'U') IS NOT NULL DROP TABLE dbo.RefreshTokens_Log;
IF OBJECT_ID('dbo.RefreshTokens', 'U') IS NOT NULL DROP TABLE dbo.RefreshTokens;
GO

-------------------------------------------------------------------------------
-- FetchRuns  (referenced by OcrExtractionRecords, already dropped above)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_FetchRuns_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FetchRuns_I;
IF OBJECT_ID('dbo.trg_FetchRuns_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FetchRuns_U;
IF OBJECT_ID('dbo.trg_FetchRuns_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FetchRuns_D;
GO
IF OBJECT_ID('dbo.FetchRuns_Hist', 'U') IS NOT NULL DROP TABLE dbo.FetchRuns_Hist;
IF OBJECT_ID('dbo.FetchRuns_Log', 'U') IS NOT NULL DROP TABLE dbo.FetchRuns_Log;
IF OBJECT_ID('dbo.FetchRuns', 'U') IS NOT NULL DROP TABLE dbo.FetchRuns;
GO

-------------------------------------------------------------------------------
-- CodeLookups  (referenced by MigrationDocuments, already dropped above)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_CodeLookups_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_CodeLookups_I;
IF OBJECT_ID('dbo.trg_CodeLookups_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_CodeLookups_U;
IF OBJECT_ID('dbo.trg_CodeLookups_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_CodeLookups_D;
GO
IF OBJECT_ID('dbo.CodeLookups_Hist', 'U') IS NOT NULL DROP TABLE dbo.CodeLookups_Hist;
IF OBJECT_ID('dbo.CodeLookups_Log', 'U') IS NOT NULL DROP TABLE dbo.CodeLookups_Log;
IF OBJECT_ID('dbo.CodeLookups', 'U') IS NOT NULL DROP TABLE dbo.CodeLookups;
GO

-------------------------------------------------------------------------------
-- Users  (referenced by RefreshTokens, already dropped above)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_Users_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_Users_I;
IF OBJECT_ID('dbo.trg_Users_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_Users_U;
IF OBJECT_ID('dbo.trg_Users_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_Users_D;
GO
IF OBJECT_ID('dbo.Users_Hist', 'U') IS NOT NULL DROP TABLE dbo.Users_Hist;
IF OBJECT_ID('dbo.Users_Log', 'U') IS NOT NULL DROP TABLE dbo.Users_Log;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-------------------------------------------------------------------------------
-- AppSettings  (no FK)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_AppSettings_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_AppSettings_I;
IF OBJECT_ID('dbo.trg_AppSettings_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_AppSettings_U;
IF OBJECT_ID('dbo.trg_AppSettings_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_AppSettings_D;
GO
IF OBJECT_ID('dbo.AppSettings_Hist', 'U') IS NOT NULL DROP TABLE dbo.AppSettings_Hist;
IF OBJECT_ID('dbo.AppSettings_Log', 'U') IS NOT NULL DROP TABLE dbo.AppSettings_Log;
IF OBJECT_ID('dbo.AppSettings', 'U') IS NOT NULL DROP TABLE dbo.AppSettings;
GO

-------------------------------------------------------------------------------
-- EmptyFolderRecords  (no FK)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_EmptyFolderRecords_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmptyFolderRecords_I;
IF OBJECT_ID('dbo.trg_EmptyFolderRecords_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmptyFolderRecords_U;
IF OBJECT_ID('dbo.trg_EmptyFolderRecords_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmptyFolderRecords_D;
GO
IF OBJECT_ID('dbo.EmptyFolderRecords_Hist', 'U') IS NOT NULL DROP TABLE dbo.EmptyFolderRecords_Hist;
IF OBJECT_ID('dbo.EmptyFolderRecords_Log', 'U') IS NOT NULL DROP TABLE dbo.EmptyFolderRecords_Log;
IF OBJECT_ID('dbo.EmptyFolderRecords', 'U') IS NOT NULL DROP TABLE dbo.EmptyFolderRecords;
GO

-------------------------------------------------------------------------------
-- FailedExtractionRecords  (no FK)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_FailedExtractionRecords_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FailedExtractionRecords_I;
IF OBJECT_ID('dbo.trg_FailedExtractionRecords_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FailedExtractionRecords_U;
IF OBJECT_ID('dbo.trg_FailedExtractionRecords_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FailedExtractionRecords_D;
GO
IF OBJECT_ID('dbo.FailedExtractionRecords_Hist', 'U') IS NOT NULL DROP TABLE dbo.FailedExtractionRecords_Hist;
IF OBJECT_ID('dbo.FailedExtractionRecords_Log', 'U') IS NOT NULL DROP TABLE dbo.FailedExtractionRecords_Log;
IF OBJECT_ID('dbo.FailedExtractionRecords', 'U') IS NOT NULL DROP TABLE dbo.FailedExtractionRecords;
GO

-------------------------------------------------------------------------------
-- BrandingSettings  (no FK)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_BrandingSettings_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_BrandingSettings_I;
IF OBJECT_ID('dbo.trg_BrandingSettings_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_BrandingSettings_U;
IF OBJECT_ID('dbo.trg_BrandingSettings_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_BrandingSettings_D;
GO
IF OBJECT_ID('dbo.BrandingSettings_Hist', 'U') IS NOT NULL DROP TABLE dbo.BrandingSettings_Hist;
IF OBJECT_ID('dbo.BrandingSettings_Log', 'U') IS NOT NULL DROP TABLE dbo.BrandingSettings_Log;
IF OBJECT_ID('dbo.BrandingSettings', 'U') IS NOT NULL DROP TABLE dbo.BrandingSettings;
GO

-------------------------------------------------------------------------------
-- EmailTemplates  (no FK)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_EmailTemplates_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmailTemplates_I;
IF OBJECT_ID('dbo.trg_EmailTemplates_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmailTemplates_U;
IF OBJECT_ID('dbo.trg_EmailTemplates_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmailTemplates_D;
GO
IF OBJECT_ID('dbo.EmailTemplates_Hist', 'U') IS NOT NULL DROP TABLE dbo.EmailTemplates_Hist;
IF OBJECT_ID('dbo.EmailTemplates_Log', 'U') IS NOT NULL DROP TABLE dbo.EmailTemplates_Log;
IF OBJECT_ID('dbo.EmailTemplates', 'U') IS NOT NULL DROP TABLE dbo.EmailTemplates;
GO

-------------------------------------------------------------------------------
-- DocumentTypes  (no FK)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_DocumentTypes_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_DocumentTypes_I;
IF OBJECT_ID('dbo.trg_DocumentTypes_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_DocumentTypes_U;
IF OBJECT_ID('dbo.trg_DocumentTypes_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_DocumentTypes_D;
GO
IF OBJECT_ID('dbo.DocumentTypes_Hist', 'U') IS NOT NULL DROP TABLE dbo.DocumentTypes_Hist;
IF OBJECT_ID('dbo.DocumentTypes_Log', 'U') IS NOT NULL DROP TABLE dbo.DocumentTypes_Log;
IF OBJECT_ID('dbo.DocumentTypes', 'U') IS NOT NULL DROP TABLE dbo.DocumentTypes;
GO

-------------------------------------------------------------------------------
-- ImageExtractionStaging  (no FK)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_ImageExtractionStaging_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ImageExtractionStaging_I;
IF OBJECT_ID('dbo.trg_ImageExtractionStaging_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ImageExtractionStaging_U;
IF OBJECT_ID('dbo.trg_ImageExtractionStaging_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ImageExtractionStaging_D;
GO
IF OBJECT_ID('dbo.ImageExtractionStaging_Hist', 'U') IS NOT NULL DROP TABLE dbo.ImageExtractionStaging_Hist;
IF OBJECT_ID('dbo.ImageExtractionStaging_Log', 'U') IS NOT NULL DROP TABLE dbo.ImageExtractionStaging_Log;
IF OBJECT_ID('dbo.ImageExtractionStaging', 'U') IS NOT NULL DROP TABLE dbo.ImageExtractionStaging;
GO
