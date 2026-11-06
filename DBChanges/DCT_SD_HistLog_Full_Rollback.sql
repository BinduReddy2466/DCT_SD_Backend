/* =============================================================================
   Rollback for DCT_SD_HistLog_Full.sql - drops the 16 _Log tables, 16 _Hist
   tables, and 48 triggers this script creates. These are brand-new objects
   (the environment had only main tables beforehand), so rollback is a
   straight DROP, not a column-level restore. Safe to re-run.
   ============================================================================= */

USE DCT_SD;
GO

-------------------------------------------------------------------------------
-- Users
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_Users_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_Users_I;
IF OBJECT_ID('dbo.trg_Users_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_Users_U;
IF OBJECT_ID('dbo.trg_Users_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_Users_D;
GO
IF OBJECT_ID('dbo.Users_Hist', 'U') IS NOT NULL DROP TABLE dbo.Users_Hist;
IF OBJECT_ID('dbo.Users_Log', 'U') IS NOT NULL DROP TABLE dbo.Users_Log;
GO

-------------------------------------------------------------------------------
-- RefreshTokens
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_RefreshTokens_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_RefreshTokens_I;
IF OBJECT_ID('dbo.trg_RefreshTokens_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_RefreshTokens_U;
IF OBJECT_ID('dbo.trg_RefreshTokens_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_RefreshTokens_D;
GO
IF OBJECT_ID('dbo.RefreshTokens_Hist', 'U') IS NOT NULL DROP TABLE dbo.RefreshTokens_Hist;
IF OBJECT_ID('dbo.RefreshTokens_Log', 'U') IS NOT NULL DROP TABLE dbo.RefreshTokens_Log;
GO

-------------------------------------------------------------------------------
-- AppSettings
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_AppSettings_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_AppSettings_I;
IF OBJECT_ID('dbo.trg_AppSettings_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_AppSettings_U;
IF OBJECT_ID('dbo.trg_AppSettings_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_AppSettings_D;
GO
IF OBJECT_ID('dbo.AppSettings_Hist', 'U') IS NOT NULL DROP TABLE dbo.AppSettings_Hist;
IF OBJECT_ID('dbo.AppSettings_Log', 'U') IS NOT NULL DROP TABLE dbo.AppSettings_Log;
GO

-------------------------------------------------------------------------------
-- CodeLookups
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_CodeLookups_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_CodeLookups_I;
IF OBJECT_ID('dbo.trg_CodeLookups_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_CodeLookups_U;
IF OBJECT_ID('dbo.trg_CodeLookups_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_CodeLookups_D;
GO
IF OBJECT_ID('dbo.CodeLookups_Hist', 'U') IS NOT NULL DROP TABLE dbo.CodeLookups_Hist;
IF OBJECT_ID('dbo.CodeLookups_Log', 'U') IS NOT NULL DROP TABLE dbo.CodeLookups_Log;
GO

-------------------------------------------------------------------------------
-- FetchRuns
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_FetchRuns_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FetchRuns_I;
IF OBJECT_ID('dbo.trg_FetchRuns_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FetchRuns_U;
IF OBJECT_ID('dbo.trg_FetchRuns_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FetchRuns_D;
GO
IF OBJECT_ID('dbo.FetchRuns_Hist', 'U') IS NOT NULL DROP TABLE dbo.FetchRuns_Hist;
IF OBJECT_ID('dbo.FetchRuns_Log', 'U') IS NOT NULL DROP TABLE dbo.FetchRuns_Log;
GO

-------------------------------------------------------------------------------
-- OcrExtractionRecords
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_OcrExtractionRecords_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionRecords_I;
IF OBJECT_ID('dbo.trg_OcrExtractionRecords_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionRecords_U;
IF OBJECT_ID('dbo.trg_OcrExtractionRecords_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionRecords_D;
GO
IF OBJECT_ID('dbo.OcrExtractionRecords_Hist', 'U') IS NOT NULL DROP TABLE dbo.OcrExtractionRecords_Hist;
IF OBJECT_ID('dbo.OcrExtractionRecords_Log', 'U') IS NOT NULL DROP TABLE dbo.OcrExtractionRecords_Log;
GO

-------------------------------------------------------------------------------
-- ManualValidationRequests
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_ManualValidationRequests_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ManualValidationRequests_I;
IF OBJECT_ID('dbo.trg_ManualValidationRequests_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ManualValidationRequests_U;
IF OBJECT_ID('dbo.trg_ManualValidationRequests_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ManualValidationRequests_D;
GO
IF OBJECT_ID('dbo.ManualValidationRequests_Hist', 'U') IS NOT NULL DROP TABLE dbo.ManualValidationRequests_Hist;
IF OBJECT_ID('dbo.ManualValidationRequests_Log', 'U') IS NOT NULL DROP TABLE dbo.ManualValidationRequests_Log;
GO

-------------------------------------------------------------------------------
-- MigrationRecords
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_MigrationRecords_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationRecords_I;
IF OBJECT_ID('dbo.trg_MigrationRecords_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationRecords_U;
IF OBJECT_ID('dbo.trg_MigrationRecords_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationRecords_D;
GO
IF OBJECT_ID('dbo.MigrationRecords_Hist', 'U') IS NOT NULL DROP TABLE dbo.MigrationRecords_Hist;
IF OBJECT_ID('dbo.MigrationRecords_Log', 'U') IS NOT NULL DROP TABLE dbo.MigrationRecords_Log;
GO

-------------------------------------------------------------------------------
-- MigrationDocuments
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_MigrationDocuments_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationDocuments_I;
IF OBJECT_ID('dbo.trg_MigrationDocuments_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationDocuments_U;
IF OBJECT_ID('dbo.trg_MigrationDocuments_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_MigrationDocuments_D;
GO
IF OBJECT_ID('dbo.MigrationDocuments_Hist', 'U') IS NOT NULL DROP TABLE dbo.MigrationDocuments_Hist;
IF OBJECT_ID('dbo.MigrationDocuments_Log', 'U') IS NOT NULL DROP TABLE dbo.MigrationDocuments_Log;
GO

-------------------------------------------------------------------------------
-- EmptyFolderRecords
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_EmptyFolderRecords_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmptyFolderRecords_I;
IF OBJECT_ID('dbo.trg_EmptyFolderRecords_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmptyFolderRecords_U;
IF OBJECT_ID('dbo.trg_EmptyFolderRecords_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmptyFolderRecords_D;
GO
IF OBJECT_ID('dbo.EmptyFolderRecords_Hist', 'U') IS NOT NULL DROP TABLE dbo.EmptyFolderRecords_Hist;
IF OBJECT_ID('dbo.EmptyFolderRecords_Log', 'U') IS NOT NULL DROP TABLE dbo.EmptyFolderRecords_Log;
GO

-------------------------------------------------------------------------------
-- FailedExtractionRecords
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_FailedExtractionRecords_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FailedExtractionRecords_I;
IF OBJECT_ID('dbo.trg_FailedExtractionRecords_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FailedExtractionRecords_U;
IF OBJECT_ID('dbo.trg_FailedExtractionRecords_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_FailedExtractionRecords_D;
GO
IF OBJECT_ID('dbo.FailedExtractionRecords_Hist', 'U') IS NOT NULL DROP TABLE dbo.FailedExtractionRecords_Hist;
IF OBJECT_ID('dbo.FailedExtractionRecords_Log', 'U') IS NOT NULL DROP TABLE dbo.FailedExtractionRecords_Log;
GO

-------------------------------------------------------------------------------
-- BrandingSettings
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_BrandingSettings_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_BrandingSettings_I;
IF OBJECT_ID('dbo.trg_BrandingSettings_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_BrandingSettings_U;
IF OBJECT_ID('dbo.trg_BrandingSettings_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_BrandingSettings_D;
GO
IF OBJECT_ID('dbo.BrandingSettings_Hist', 'U') IS NOT NULL DROP TABLE dbo.BrandingSettings_Hist;
IF OBJECT_ID('dbo.BrandingSettings_Log', 'U') IS NOT NULL DROP TABLE dbo.BrandingSettings_Log;
GO

-------------------------------------------------------------------------------
-- EmailTemplates
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_EmailTemplates_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmailTemplates_I;
IF OBJECT_ID('dbo.trg_EmailTemplates_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmailTemplates_U;
IF OBJECT_ID('dbo.trg_EmailTemplates_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_EmailTemplates_D;
GO
IF OBJECT_ID('dbo.EmailTemplates_Hist', 'U') IS NOT NULL DROP TABLE dbo.EmailTemplates_Hist;
IF OBJECT_ID('dbo.EmailTemplates_Log', 'U') IS NOT NULL DROP TABLE dbo.EmailTemplates_Log;
GO

-------------------------------------------------------------------------------
-- DocumentTypes
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_DocumentTypes_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_DocumentTypes_I;
IF OBJECT_ID('dbo.trg_DocumentTypes_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_DocumentTypes_U;
IF OBJECT_ID('dbo.trg_DocumentTypes_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_DocumentTypes_D;
GO
IF OBJECT_ID('dbo.DocumentTypes_Hist', 'U') IS NOT NULL DROP TABLE dbo.DocumentTypes_Hist;
IF OBJECT_ID('dbo.DocumentTypes_Log', 'U') IS NOT NULL DROP TABLE dbo.DocumentTypes_Log;
GO

-------------------------------------------------------------------------------
-- OcrExtractionEntries
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_OcrExtractionEntries_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionEntries_I;
IF OBJECT_ID('dbo.trg_OcrExtractionEntries_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionEntries_U;
IF OBJECT_ID('dbo.trg_OcrExtractionEntries_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_OcrExtractionEntries_D;
GO
IF OBJECT_ID('dbo.OcrExtractionEntries_Hist', 'U') IS NOT NULL DROP TABLE dbo.OcrExtractionEntries_Hist;
IF OBJECT_ID('dbo.OcrExtractionEntries_Log', 'U') IS NOT NULL DROP TABLE dbo.OcrExtractionEntries_Log;
GO

-------------------------------------------------------------------------------
-- ImageExtractionStaging
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.trg_ImageExtractionStaging_I', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ImageExtractionStaging_I;
IF OBJECT_ID('dbo.trg_ImageExtractionStaging_U', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ImageExtractionStaging_U;
IF OBJECT_ID('dbo.trg_ImageExtractionStaging_D', 'TR') IS NOT NULL DROP TRIGGER dbo.trg_ImageExtractionStaging_D;
GO
IF OBJECT_ID('dbo.ImageExtractionStaging_Hist', 'U') IS NOT NULL DROP TABLE dbo.ImageExtractionStaging_Hist;
IF OBJECT_ID('dbo.ImageExtractionStaging_Log', 'U') IS NOT NULL DROP TABLE dbo.ImageExtractionStaging_Log;
GO

-------------------------------------------------------------------------------
-- ufn_Get* functions (dropped last: Log_AppName/Log_HostName/Log_IPAddr and
-- Hist_AppName/Hist_HostName/Hist_IPAddr DEFAULT constraints on the tables
-- above reference these)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.ufn_GetAppName', 'FN') IS NOT NULL DROP FUNCTION dbo.ufn_GetAppName;
IF OBJECT_ID('dbo.ufn_GetHostName', 'FN') IS NOT NULL DROP FUNCTION dbo.ufn_GetHostName;
IF OBJECT_ID('dbo.ufn_GetIPAddress', 'FN') IS NOT NULL DROP FUNCTION dbo.ufn_GetIPAddress;
GO


