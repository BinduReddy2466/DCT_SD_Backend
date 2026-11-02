USE [logging]  -- DBA needs to create the Database
GO

/*
Technical User Create Instructions -->

As per CMRE guidance, the technical user should not be created via scripts.
This user needs to be created manually by the CMRE Team.

Please create the technical user with the name provided below (used for both
the AI tool and the application):
    1. [DCTSDUser]

Note: Once the database and user are created, please proceed to execute the
full script. The script includes table, index, and trigger creation and also
references this user for granting the required permissions (Section 21).
*/

/* =============================================================================
   DCT_SD - Production Deployment Script
   File: dct_sd_main.sql

   Scope: creates every table, column, default, primary key, foreign key,
   unique/non-unique index, check constraint, and audit trigger DCT_SD
   currently uses, for deployment to a newly created, empty database.
   Validated column-by-column against the live DCT_SD database schema.

   Out of scope by design:
     - CREATE DATABASE - CMRE creates the database manually; document its
       name in the Release Notes. The USE statement above assumes the
       database is named DCT_SD; update it if named differently.
     - CREATE LOGIN / CREATE USER - CMRE creates the two technical users
       manually (see instructions above); this script only grants
       permissions on tables to those two names (Section 21).

   Idempotent: every CREATE is guarded (IF NOT EXISTS / OBJECT_ID check), so
   this script can be re-run safely against a database that already has
   some or all of these objects. Purely additive - no DROP, no ALTER of
   existing data, no data-loss risk.
   ============================================================================= */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =============================================================================
   SECTION 1 - Users (no dependencies)
   ============================================================================= */
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id                  INT IDENTITY(1,1) NOT NULL,
        FirstName           NVARCHAR(100)     NOT NULL,
        LastName            NVARCHAR(100)     NOT NULL,
        Username            NVARCHAR(150)     NOT NULL,
        PasswordHash        NVARCHAR(MAX)     NOT NULL,
        RoleName            NVARCHAR(50)     NOT NULL,
        MenuPermissionsCsv  NVARCHAR(MAX)     NULL,
        Status              INT               NOT NULL CONSTRAINT DF_Users_Status DEFAULT ((1)),
        FailedLoginAttempts INT               NOT NULL CONSTRAINT DF_Users_FailedLoginAttempts DEFAULT ((0)),
        LastLoginAt         DATETIME2         NULL,
        IsDeleted           BIT               NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT ((0)),
        DeletedAt           DATETIME2         NULL,
        CreatedAt           DATETIME2         NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT               NULL,
        UpdatedAt           DATETIME2         NULL,
        UpdatedBy           INT               NULL,
        RowVersion          ROWVERSION        NOT NULL,
        LockoutEndUtc       DATETIME2         NULL,
        CONSTRAINT PK_Users PRIMARY KEY (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_Users_01' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_Users_01 ON dbo.Users (Username);
GO

/* =============================================================================
   SECTION 2 - RefreshTokens (depends on Users)
   ============================================================================= */
IF OBJECT_ID('dbo.RefreshTokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens
    (
        Id                   INT IDENTITY(1,1) NOT NULL,
        UserId               INT               NOT NULL,
        TokenHash            NVARCHAR(128)     NOT NULL,
        CreatedAt            DATETIME2         NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT (SYSUTCDATETIME()),
        ExpiresAt            DATETIME2         NOT NULL,
        RevokedAt            DATETIME2         NULL,
        ReplacedByTokenHash  NVARCHAR(128)     NULL,
        CreatedByIp          NVARCHAR(64)     NULL,
        CONSTRAINT PK_RefreshTokens PRIMARY KEY (Id),
        CONSTRAINT FK_RefreshTokens_01 FOREIGN KEY (UserId) REFERENCES dbo.Users (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_RefreshTokens_01' AND object_id = OBJECT_ID('dbo.RefreshTokens'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_RefreshTokens_01 ON dbo.RefreshTokens (TokenHash);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_01' AND object_id = OBJECT_ID('dbo.RefreshTokens'))
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_01 ON dbo.RefreshTokens (UserId);
GO

/* =============================================================================
   SECTION 3 - FetchRuns (no dependencies; shared table for fetch runs AND
   root-path-change history, discriminated by RecordKind)
   ============================================================================= */
IF OBJECT_ID('dbo.FetchRuns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FetchRuns
    (
        Id                      INT IDENTITY(1,1) NOT NULL,
        SourcePath              NVARCHAR(MAX)     NOT NULL,
        Status                  INT               NULL,
        TotalCount              INT               NULL,
        ProcessedCount          INT               NULL,
        LastProcessedFolderPath NVARCHAR(MAX)     NULL,
        LastProcessedAt         DATETIME2         NULL,
        ExecutedByUserId        INT               NOT NULL,
        ExecutedByUsername      NVARCHAR(MAX)     NOT NULL,
        StartedAt               DATETIME2         NOT NULL CONSTRAINT DF_FetchRuns_StartedAt DEFAULT (SYSUTCDATETIME()),
        CompletedAt             DATETIME2         NULL,
        RecordKind              NVARCHAR(20)      NOT NULL,
        FromPath                NVARCHAR(MAX)     NULL,
        Remarks                 NVARCHAR(MAX)     NULL,
        SummaryMessage          NVARCHAR(MAX)     NULL,
        RootPath                NVARCHAR(MAX)     NULL,
        CONSTRAINT PK_FetchRuns PRIMARY KEY (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_FetchRuns_01' AND object_id = OBJECT_ID('dbo.FetchRuns'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_FetchRuns_01 ON dbo.FetchRuns (RecordKind, StartedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FetchRuns_01' AND object_id = OBJECT_ID('dbo.FetchRuns'))
    CREATE NONCLUSTERED INDEX IX_FetchRuns_01 ON dbo.FetchRuns (RecordKind, StartedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FetchRuns_02' AND object_id = OBJECT_ID('dbo.FetchRuns'))
    CREATE NONCLUSTERED INDEX IX_FetchRuns_02 ON dbo.FetchRuns (Status);
GO

/* =============================================================================
   SECTION 4 - OcrExtractionRecords (depends on FetchRuns)
   ============================================================================= */
IF OBJECT_ID('dbo.OcrExtractionRecords', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OcrExtractionRecords
    (
        Id                  INT IDENTITY(1,1) NOT NULL,
        RequestNumber       NVARCHAR(30)      NOT NULL,
        FetchRunId          INT               NULL,
        RdCode              NVARCHAR(20)      NULL,
        RdName              NVARCHAR(150)     NULL,
        FolderPath          NVARCHAR(MAX)     NOT NULL,
        FolderPathHash      AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', FolderPath)) PERSISTED NOT NULL,
        EntryNumbersCsv     NVARCHAR(MAX)     NULL,
        TitleNumber         NVARCHAR(50)      NULL,
        TitleType           INT               NULL,
        DocumentCount       INT               NOT NULL CONSTRAINT DF_OcrExtractionRecords_DocumentCount DEFAULT ((0)),
        ExtractionStatus    INT               NOT NULL,
        ExtractionDateTime  DATETIME2         NOT NULL CONSTRAINT DF_OcrExtractionRecords_ExtractionDateTime DEFAULT (SYSUTCDATETIME()),
        DocumentsJson       NVARCHAR(MAX)     NULL,
        EntryNumberRange    NVARCHAR(MAX)     NOT NULL CONSTRAINT DF_OcrExtractionRecords_EntryNumberRange DEFAULT ('0'),
        CONSTRAINT PK_OcrExtractionRecords PRIMARY KEY (Id),
        CONSTRAINT CK_OcrExtractionRecords_DocumentsJson CHECK (DocumentsJson IS NULL OR ISJSON(DocumentsJson) = 1),
        CONSTRAINT FK_OcrExtractionRecords_01 FOREIGN KEY (FetchRunId) REFERENCES dbo.FetchRuns (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_OcrExtractionRecords_01' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_OcrExtractionRecords_01 ON dbo.OcrExtractionRecords (RequestNumber);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_01' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_01 ON dbo.OcrExtractionRecords (ExtractionDateTime);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_02' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_02 ON dbo.OcrExtractionRecords (ExtractionStatus);
-- Indexed on a SHA2_256 hash, not the raw column: FolderPath is NVARCHAR(MAX)
-- (column-size guideline), and NVARCHAR(MAX) cannot be an index key. The only
-- app query against this column (FailedExtractionController reprocess flow)
-- is a low-frequency admin action, not a hot path.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_03' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_03 ON dbo.OcrExtractionRecords (FolderPathHash);
GO

/* =============================================================================
   SECTION 5 - OcrExtractionEntries (depends on OcrExtractionRecords)
   Legacy - not referenced by current application code. Included for schema
   completeness.
   ============================================================================= */
IF OBJECT_ID('dbo.OcrExtractionEntries', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OcrExtractionEntries
    (
        Id                     INT IDENTITY(1,1) NOT NULL,
        OcrExtractionRecordId  INT               NOT NULL,
        EntryNumber            NVARCHAR(50)     NOT NULL,
        CONSTRAINT PK_OcrExtractionEntries PRIMARY KEY (Id),
        CONSTRAINT FK_OcrExtractionEntries_01 FOREIGN KEY (OcrExtractionRecordId) REFERENCES dbo.OcrExtractionRecords (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionEntries_01' AND object_id = OBJECT_ID('dbo.OcrExtractionEntries'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionEntries_01 ON dbo.OcrExtractionEntries (EntryNumber);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_OcrExtractionEntries_01' AND object_id = OBJECT_ID('dbo.OcrExtractionEntries'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_OcrExtractionEntries_01 ON dbo.OcrExtractionEntries (OcrExtractionRecordId, EntryNumber);
GO

/* =============================================================================
   SECTION 6 - CodeLookups (no dependencies)
   ============================================================================= */
IF OBJECT_ID('dbo.CodeLookups', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CodeLookups
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        LookupType  NVARCHAR(30)      NOT NULL,
        Code        NVARCHAR(150)     NOT NULL,
        Name        NVARCHAR(MAX)     NOT NULL,
        NameHash    AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', Name)) PERSISTED NOT NULL,
        DataJson    NVARCHAR(MAX)     NULL,
        IsActive    BIT               NOT NULL CONSTRAINT DF_CodeLookups_IsActive DEFAULT ((1)),
        CreatedAt   DATETIME2         NOT NULL CONSTRAINT DF_CodeLookups_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy   INT               NULL,
        UpdatedAt   DATETIME2         NULL,
        UpdatedBy   INT               NULL,
        RowVersion  ROWVERSION        NOT NULL,
        CONSTRAINT PK_CodeLookups PRIMARY KEY (Id),
        CONSTRAINT CK_CodeLookups_DataJson CHECK (DataJson IS NULL OR ISJSON(DataJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CodeLookups_01' AND object_id = OBJECT_ID('dbo.CodeLookups'))
    CREATE NONCLUSTERED INDEX IX_CodeLookups_01 ON dbo.CodeLookups (LookupType);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_CodeLookups_01' AND object_id = OBJECT_ID('dbo.CodeLookups'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_CodeLookups_01 ON dbo.CodeLookups (LookupType, Code);
-- Indexed on a SHA2_256 hash of Name, not the raw column: Name is
-- NVARCHAR(MAX) (column-size guideline), which cannot be an index key.
-- No current application query filters by Name, so this has no query
-- performance impact.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_CodeLookups_02' AND object_id = OBJECT_ID('dbo.CodeLookups'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_CodeLookups_02 ON dbo.CodeLookups (LookupType, NameHash);
GO

/* =============================================================================
   SECTION 7 - ManualValidationRequests (depends on OcrExtractionRecords)
   ============================================================================= */
IF OBJECT_ID('dbo.ManualValidationRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManualValidationRequests
    (
        Id                      INT IDENTITY(1,1) NOT NULL,
        RequestNumber           NVARCHAR(30)      NOT NULL,
        OcrExtractionRecordId   INT               NULL,
        RdCode                  NVARCHAR(20)      NULL,
        RdName                  NVARCHAR(150)     NULL,
        EntryNumbersCsv         NVARCHAR(MAX)     NULL,
        Title                   NVARCHAR(50)      NULL,
        TitleType               INT               NULL,
        [Plan]                  NVARCHAR(50)      NULL,
        Block                   NVARCHAR(50)      NULL,
        Lot                     NVARCHAR(50)      NULL,
        TitleSequence           NVARCHAR(50)      NULL,
        MissingFieldsCsv        NVARCHAR(MAX)     NOT NULL,
        DocumentsJson           NVARCHAR(MAX)     NULL,
        ExtractionDate          DATETIME2         NOT NULL CONSTRAINT DF_ManualValidationRequests_ExtractionDate DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId         INT               NULL,
        UpdatedByUsername       NVARCHAR(MAX)     NULL,
        UpdatedAt               DATETIME2         NULL,
        LockedByUserId          INT               NULL,
        LockedByUsername        NVARCHAR(MAX)     NULL,
        LockedAt                DATETIME2         NULL,
        MigratedAt              DATETIME2         NULL,
        Status                  NVARCHAR(30)      NOT NULL,
        EntryNumberRange        NVARCHAR(MAX)     NOT NULL CONSTRAINT DF_ManualValidationRequests_EntryNumberRange DEFAULT ('0'),
        CONSTRAINT PK_ManualValidationRequests PRIMARY KEY (Id),
        CONSTRAINT CK_ManualValidationRequests_DocumentsJson CHECK (DocumentsJson IS NULL OR ISJSON(DocumentsJson) = 1),
        CONSTRAINT FK_ManualValidationRequests_01 FOREIGN KEY (OcrExtractionRecordId) REFERENCES dbo.OcrExtractionRecords (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_ManualValidationRequests_01' AND object_id = OBJECT_ID('dbo.ManualValidationRequests'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_ManualValidationRequests_01 ON dbo.ManualValidationRequests (RequestNumber);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_01' AND object_id = OBJECT_ID('dbo.ManualValidationRequests'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_01 ON dbo.ManualValidationRequests (ExtractionDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_02' AND object_id = OBJECT_ID('dbo.ManualValidationRequests'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_02 ON dbo.ManualValidationRequests (Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_03' AND object_id = OBJECT_ID('dbo.ManualValidationRequests'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_03 ON dbo.ManualValidationRequests (OcrExtractionRecordId);
GO

/* =============================================================================
   SECTION 8 - MigrationRecords (depends on OcrExtractionRecords)
   ============================================================================= */
IF OBJECT_ID('dbo.MigrationRecords', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationRecords
    (
        Id                    INT IDENTITY(1,1) NOT NULL,
        RequestNumber         NVARCHAR(30)      NOT NULL,
        OcrExtractionRecordId INT               NULL,
        MigrationDate         DATETIME2         NOT NULL CONSTRAINT DF_MigrationRecords_MigrationDate DEFAULT (SYSUTCDATETIME()),
        RdCode                NVARCHAR(20)      NOT NULL,
        RdName                NVARCHAR(150)     NOT NULL,
        EntryNumbersCsv       NVARCHAR(MAX)     NULL,
        Title                 NVARCHAR(50)      NULL,
        TitleType             INT               NULL,
        [Plan]                NVARCHAR(50)      NULL,
        Block                 NVARCHAR(50)      NULL,
        Lot                   NVARCHAR(50)      NULL,
        TitleSequence         NVARCHAR(50)      NULL,
        MigratedToRdName      NVARCHAR(150)     NOT NULL,
        MigrationStatus       NVARCHAR(50)      NOT NULL,
        SdStatus              NVARCHAR(50)      NOT NULL,
        CONSTRAINT PK_MigrationRecords PRIMARY KEY (Id),
        CONSTRAINT FK_MigrationRecords_01 FOREIGN KEY (OcrExtractionRecordId) REFERENCES dbo.OcrExtractionRecords (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_MigrationRecords_01' AND object_id = OBJECT_ID('dbo.MigrationRecords'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_MigrationRecords_01 ON dbo.MigrationRecords (RequestNumber);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_01' AND object_id = OBJECT_ID('dbo.MigrationRecords'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_01 ON dbo.MigrationRecords (MigrationDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_02' AND object_id = OBJECT_ID('dbo.MigrationRecords'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_02 ON dbo.MigrationRecords (MigrationStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_03' AND object_id = OBJECT_ID('dbo.MigrationRecords'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_03 ON dbo.MigrationRecords (OcrExtractionRecordId);
GO

/* =============================================================================
   SECTION 9 - MigrationDocuments (depends on MigrationRecords, CodeLookups)
   ============================================================================= */
IF OBJECT_ID('dbo.MigrationDocuments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationDocuments
    (
        Id                   INT IDENTITY(1,1) NOT NULL,
        MigrationRecordId    INT               NOT NULL,
        CodeLookupId         INT               NULL,
        DocumentName         NVARCHAR(MAX)     NOT NULL,
        FileName             NVARCHAR(MAX)     NOT NULL,
        FileNameHash         AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', FileName)) PERSISTED NOT NULL,
        Status               INT               NOT NULL,
        ExistingFileName     NVARCHAR(MAX)      NULL,
        PerformedByUserId    INT               NULL,
        PerformedByUsername  NVARCHAR(MAX)      NULL,
        ActionDate           DATETIME2         NULL,
        CONSTRAINT PK_MigrationDocuments PRIMARY KEY (Id),
        CONSTRAINT FK_MigrationDocuments_01 FOREIGN KEY (MigrationRecordId) REFERENCES dbo.MigrationRecords (Id),
        CONSTRAINT FK_MigrationDocuments_02 FOREIGN KEY (CodeLookupId) REFERENCES dbo.CodeLookups (Id)
    );
END;
GO

-- Indexed on a SHA2_256 hash of FileName, not the raw column: FileName is
-- NVARCHAR(MAX) (column-size guideline), which cannot be an index key. No
-- current application query filters by FileName.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_MigrationDocuments_01' AND object_id = OBJECT_ID('dbo.MigrationDocuments'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_MigrationDocuments_01 ON dbo.MigrationDocuments (MigrationRecordId, FileNameHash);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationDocuments_01' AND object_id = OBJECT_ID('dbo.MigrationDocuments'))
    CREATE NONCLUSTERED INDEX IX_MigrationDocuments_01 ON dbo.MigrationDocuments (MigrationRecordId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationDocuments_02' AND object_id = OBJECT_ID('dbo.MigrationDocuments'))
    CREATE NONCLUSTERED INDEX IX_MigrationDocuments_02 ON dbo.MigrationDocuments (CodeLookupId);
GO

/* =============================================================================
   SECTION 10 - EmptyFolderRecords (no dependencies)
   ============================================================================= */
IF OBJECT_ID('dbo.EmptyFolderRecords', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmptyFolderRecords
    (
        Id             INT IDENTITY(1,1) NOT NULL,
        RdCode         NVARCHAR(20)      NULL,
        RdName         NVARCHAR(150)     NULL,
        FolderName     NVARCHAR(MAX)     NOT NULL,
        FolderPath     NVARCHAR(MAX)     NOT NULL,
        FolderPathHash AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', FolderPath)) PERSISTED NOT NULL,
        Status         NVARCHAR(50)      NOT NULL CONSTRAINT DF_EmptyFolderRecords_Status DEFAULT (N'Empty Entry Folder'),
        FetchDateTime  DATETIME2         NOT NULL CONSTRAINT DF_EmptyFolderRecords_FetchDateTime DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EmptyFolderRecords PRIMARY KEY (Id)
    );
END;
GO

-- Indexed on a SHA2_256 hash of FolderPath, not the raw column: FolderPath
-- is NVARCHAR(MAX) (column-size guideline), which cannot be an index key.
-- No current application query filters by exact FolderPath (only FolderName
-- substring search).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_EmptyFolderRecords_01' AND object_id = OBJECT_ID('dbo.EmptyFolderRecords'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_EmptyFolderRecords_01 ON dbo.EmptyFolderRecords (FolderPathHash);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmptyFolderRecords_01' AND object_id = OBJECT_ID('dbo.EmptyFolderRecords'))
    CREATE NONCLUSTERED INDEX IX_EmptyFolderRecords_01 ON dbo.EmptyFolderRecords (FetchDateTime);
GO

/* =============================================================================
   SECTION 11 - FailedExtractionRecords (no dependencies)
   ============================================================================= */
IF OBJECT_ID('dbo.FailedExtractionRecords', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FailedExtractionRecords
    (
        Id                  INT IDENTITY(1,1) NOT NULL,
        RdCode              NVARCHAR(20)      NULL,
        RdName              NVARCHAR(150)     NULL,
        FolderName          NVARCHAR(MAX)     NOT NULL,
        FolderPath          NVARCHAR(MAX)     NOT NULL,
        FolderPathHash      AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', FolderPath)) PERSISTED NOT NULL,
        FailureReason       NVARCHAR(MAX)     NOT NULL,
        ExtractionDateTime  DATETIME2         NOT NULL CONSTRAINT DF_FailedExtractionRecords_ExtractionDateTime DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FailedExtractionRecords PRIMARY KEY (Id)
    );
END;
GO

-- Indexed on a SHA2_256 hash of FolderPath, not the raw column: FolderPath
-- is NVARCHAR(MAX) (column-size guideline), which cannot be an index key.
-- No current application query filters by exact FolderPath (only substring
-- search).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_FailedExtractionRecords_01' AND object_id = OBJECT_ID('dbo.FailedExtractionRecords'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_FailedExtractionRecords_01 ON dbo.FailedExtractionRecords (FolderPathHash);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FailedExtractionRecords_01' AND object_id = OBJECT_ID('dbo.FailedExtractionRecords'))
    CREATE NONCLUSTERED INDEX IX_FailedExtractionRecords_01 ON dbo.FailedExtractionRecords (ExtractionDateTime);
GO

/* =============================================================================
   SECTION 12 - AppSettings (no dependencies)
   ============================================================================= */
IF OBJECT_ID('dbo.AppSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSettings
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        Category    NVARCHAR(30)      NOT NULL,
        [Key]       NVARCHAR(64)      NULL,
        Label       NVARCHAR(128)     NULL,
        DataJson    NVARCHAR(MAX)     NOT NULL CONSTRAINT DF_AppSettings_DataJson DEFAULT (N'{}'),
        CreatedAt   DATETIME2         NOT NULL CONSTRAINT DF_AppSettings_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy   INT               NULL,
        UpdatedAt   DATETIME2         NULL,
        UpdatedBy   INT               NULL,
        RowVersion  ROWVERSION        NOT NULL,
        CONSTRAINT PK_AppSettings PRIMARY KEY (Id),
        CONSTRAINT CK_AppSettings_DataJson CHECK (ISJSON(DataJson) = 1)
    );
END;
GO

-- UK_ prefix used for both (CMRE naming convention: all unique indexes use
-- UK_, including filtered ones).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_AppSettings_01' AND object_id = OBJECT_ID('dbo.AppSettings'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_AppSettings_01 ON dbo.AppSettings (Category) WHERE ([Key] IS NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_AppSettings_02' AND object_id = OBJECT_ID('dbo.AppSettings'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_AppSettings_02 ON dbo.AppSettings (Category, [Key]) WHERE ([Key] IS NOT NULL);
GO

/* =============================================================================
   SECTION 13 - AuditLog (no dependencies; populated by triggers - Section 20)
   ============================================================================= */
IF OBJECT_ID('dbo.AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog
    (
        Id                    INT IDENTITY(1,1) NOT NULL,
        TableName             NVARCHAR(60)      NOT NULL,
        RecordId              INT               NULL,
        Action                CHAR(1)           NOT NULL,
        OldValuesJson         NVARCHAR(MAX)     NULL,
        NewValuesJson         NVARCHAR(MAX)     NULL,
        PerformedByLogin      NVARCHAR(128)     NOT NULL CONSTRAINT DF_AuditLog_PerformedByLogin DEFAULT (SUSER_SNAME()),
        PerformedByAppUserId  INT               NULL,
        AppName               NVARCHAR(MAX)     NULL CONSTRAINT DF_AuditLog_AppName DEFAULT (APP_NAME()),
        HostName              NVARCHAR(30)      NULL CONSTRAINT DF_AuditLog_HostName DEFAULT (HOST_NAME()),
        IPAddress             NVARCHAR(64)      NULL,
        LogDate               DATETIME2         NOT NULL CONSTRAINT DF_AuditLog_LogDate DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AuditLog PRIMARY KEY (Id),
        CONSTRAINT CK_AuditLog_OldValuesJson CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        CONSTRAINT CK_AuditLog_NewValuesJson CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

-- Unique key per client request: enforces no two AuditLog rows share the
-- same TableName + RecordId + LogDate. TableName/RecordId identify the
-- source row being logged, so only a batch operation re-logging the exact
-- same source row at the exact same LogDate could collide - not a
-- realistic case, since each DML statement logs each affected row once.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_AuditLog_01' AND object_id = OBJECT_ID('dbo.AuditLog'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_AuditLog_01 ON dbo.AuditLog (TableName, RecordId, LogDate);
-- Required Log table index #2 (Log_Date, Log_UserID equivalent).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_Log_02' AND object_id = OBJECT_ID('dbo.AuditLog'))
    CREATE NONCLUSTERED INDEX IX_AuditLog_Log_02 ON dbo.AuditLog (LogDate, PerformedByAppUserId);
GO

/* =============================================================================
   SECTION 14 - RecordHistory (no dependencies; application-written workflow
   audit trail - distinct from AuditLog, see header note)
   ============================================================================= */
IF OBJECT_ID('dbo.RecordHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecordHistory
    (
        Id             INT IDENTITY(1,1) NOT NULL,
        TableName      NVARCHAR(60)      NOT NULL,
        RecordId       INT               NULL,
        RefNo          NVARCHAR(30)      NULL,
        Action         NVARCHAR(30)      NOT NULL,
        Remarks        NVARCHAR(MAX)     NULL,
        OldValuesJson  NVARCHAR(MAX)     NULL,
        NewValuesJson  NVARCHAR(MAX)     NULL,
        ByUserId       INT               NULL,
        ByUsername     NVARCHAR(MAX)     NULL,
        AppName        NVARCHAR(30)      NULL CONSTRAINT DF_RecordHistory_AppName DEFAULT (APP_NAME()),
        HostName       NVARCHAR(30)      NULL CONSTRAINT DF_RecordHistory_HostName DEFAULT (HOST_NAME()),
        IPAddress      NVARCHAR(64)      NULL,
        CreatedAt      DATETIME2         NOT NULL CONSTRAINT DF_RecordHistory_CreatedAt DEFAULT (SYSUTCDATETIME()),
        FromValue      NVARCHAR(MAX)     NULL,
        ToValue        NVARCHAR(MAX)     NULL,
        CONSTRAINT PK_RecordHistory PRIMARY KEY (Id),
        CONSTRAINT CK_RecordHistory_OldValuesJson CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        CONSTRAINT CK_RecordHistory_NewValuesJson CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

-- Unique key per client request: enforces no two RecordHistory rows share
-- the same TableName + RecordId + CreatedAt, for the same reasoning as
-- UK_AuditLog_01 above.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_RecordHistory_01' AND object_id = OBJECT_ID('dbo.RecordHistory'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_RecordHistory_01 ON dbo.RecordHistory (TableName, RecordId, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecordHistory_02' AND object_id = OBJECT_ID('dbo.RecordHistory'))
    CREATE NONCLUSTERED INDEX IX_RecordHistory_02 ON dbo.RecordHistory (RefNo);
-- Required Hist table index #2 (Hist_Date, Hist_UserID equivalent).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecordHistory_Hist_02' AND object_id = OBJECT_ID('dbo.RecordHistory'))
    CREATE NONCLUSTERED INDEX IX_RecordHistory_Hist_02 ON dbo.RecordHistory (CreatedAt, ByUserId);
GO

/* =============================================================================
   SECTION 15 - ImageExtractionStaging (no dependencies)
   ============================================================================= */
IF OBJECT_ID('dbo.ImageExtractionStaging', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ImageExtractionStaging
    (
        Id                INT IDENTITY(1,1) NOT NULL,
        FetchRunId        INT               NULL,
        FolderPath        NVARCHAR(MAX)     NOT NULL,
        FolderPathHash    AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', FolderPath)) PERSISTED NOT NULL,
        ImagePath         NVARCHAR(MAX)     NOT NULL,
        ImagePathHash     AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', ImagePath)) PERSISTED NOT NULL,
        DocumentTypeName  NVARCHAR(MAX)     NOT NULL,
        TitleNumber       NVARCHAR(50)      NULL,
        TitleType         INT               NULL,
        [Plan]            NVARCHAR(50)      NULL,
        Block             NVARCHAR(50)      NULL,
        Lot               NVARCHAR(50)      NULL,
        ClassifiedAt      DATETIME2         NOT NULL CONSTRAINT DF_ImageExtractionStaging_ClassifiedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ImageExtractionStaging PRIMARY KEY (Id)
    );
END;
GO

-- Indexed on SHA2_256 hashes, not the raw columns: both are NVARCHAR(MAX)
-- (column-size guideline), which cannot be an index key. This table has no
-- entity class in the current application code - not queried here at all.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_ImageExtractionStaging_01' AND object_id = OBJECT_ID('dbo.ImageExtractionStaging'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_ImageExtractionStaging_01 ON dbo.ImageExtractionStaging (FolderPathHash, ImagePathHash);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ImageExtractionStaging_01' AND object_id = OBJECT_ID('dbo.ImageExtractionStaging'))
    CREATE NONCLUSTERED INDEX IX_ImageExtractionStaging_01 ON dbo.ImageExtractionStaging (FolderPathHash);
GO
-- Note: FetchRunId has no enforced foreign key (matches current database
-- behavior, despite the column name).

/* =============================================================================
   SECTION 16-18 - Legacy tables, no longer referenced by current application
   code (superseded by AppSettings/CodeLookups). Included for schema
   completeness.
   ============================================================================= */
IF OBJECT_ID('dbo.BrandingSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BrandingSettings
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        ImagePath   NVARCHAR(MAX)     NULL,
        ImagePathHash AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', ImagePath)) PERSISTED,
        CreatedAt   DATETIME2         NOT NULL,
        CreatedBy   INT               NULL,
        UpdatedAt   DATETIME2         NULL,
        UpdatedBy   INT               NULL,
        RowVersion  ROWVERSION        NOT NULL,
        CONSTRAINT PK_BrandingSettings PRIMARY KEY (Id)
    );
END;
GO

-- Natural key per guideline c.5 (identity-only PK without a unique column
-- defeats its purpose): ImagePath is this table's only business column.
-- Indexed on a SHA2_256 hash of ImagePath, not the raw column: ImagePath is
-- NVARCHAR(MAX) (column-size guideline), which cannot be an index key.
-- SQL Server's unique index still treats NULL as a value needing to be
-- unique, preserving the original singleton-enforcement intent.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_BrandingSettings_01' AND object_id = OBJECT_ID('dbo.BrandingSettings'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_BrandingSettings_01 ON dbo.BrandingSettings (ImagePathHash);
GO

IF OBJECT_ID('dbo.EmailTemplates', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailTemplates
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        [Key]       NVARCHAR(64)      NOT NULL,
        Label       NVARCHAR(128)     NOT NULL,
        Recipients  NVARCHAR(MAX)     NOT NULL,
        Subject     NVARCHAR(MAX)     NOT NULL,
        Body        NVARCHAR(MAX)     NOT NULL,
        CreatedAt   DATETIME2         NOT NULL,
        CreatedBy   INT               NULL,
        UpdatedAt   DATETIME2         NULL,
        UpdatedBy   INT               NULL,
        RowVersion  ROWVERSION        NOT NULL,
        CONSTRAINT PK_EmailTemplates PRIMARY KEY (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_EmailTemplates_01' AND object_id = OBJECT_ID('dbo.EmailTemplates'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_EmailTemplates_01 ON dbo.EmailTemplates ([Key]);
GO

IF OBJECT_ID('dbo.DocumentTypes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentTypes
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        DocumentCode  NVARCHAR(20)      NOT NULL,
        DocumentName  NVARCHAR(MAX)     NOT NULL,
        DocumentNameHash AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', DocumentName)) PERSISTED NOT NULL,
        IsActive      BIT               NOT NULL,
        CreatedAt     DATETIME2         NOT NULL,
        CreatedBy     INT               NULL,
        UpdatedAt     DATETIME2         NULL,
        UpdatedBy     INT               NULL,
        RowVersion    ROWVERSION        NOT NULL,
        CONSTRAINT PK_DocumentTypes PRIMARY KEY (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DocumentTypes_01' AND object_id = OBJECT_ID('dbo.DocumentTypes'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_DocumentTypes_01 ON dbo.DocumentTypes (DocumentCode);
-- Indexed on a SHA2_256 hash of DocumentName, not the raw column:
-- DocumentName is NVARCHAR(MAX) (column-size guideline), which cannot be an
-- index key. This table has no entity class in the current application
-- code - not queried here at all.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DocumentTypes_02' AND object_id = OBJECT_ID('dbo.DocumentTypes'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_DocumentTypes_02 ON dbo.DocumentTypes (DocumentNameHash);
GO

/* =============================================================================
   SECTION 19 - Column-size review (>300 bytes)

   No bounded nvarchar column in this script exceeds 300 bytes. Text columns
   that legitimately need more room (folder/image paths, JSON payloads, CSV
   lists, free-text remarks, etc.) use nvarchar(max), which is exempt from
   the 300-byte limit (Unitext) rather than a bounded oversized column.

   Where a column needed to remain indexed for uniqueness (e.g. FolderPath,
   ImagePath, FileName, DocumentName, Name), the raw column is nvarchar(max)
   and a persisted SHA2_256 hash of it carries the unique index instead,
   since nvarchar(max) cannot itself be an index key. This preserves the
   original uniqueness guarantee with no application code changes.

   Users.Username is the one case kept bounded (nvarchar(150), still 300
   bytes exactly) rather than widened, since it is indexed and queried on
   every login - hashing it would remove that index's benefit from the live
   query path.
   ============================================================================= */

/* =============================================================================
   SECTION 20 - AuditLog triggers (DEFERRED to a future release)

   Not included in this release. AuditLog triggers on tables EF Core actively
   writes to (Users, RefreshTokens, AppSettings, CodeLookups, FetchRuns,
   OcrExtractionRecords, ManualValidationRequests, MigrationRecords,
   MigrationDocuments, EmptyFolderRecords, FailedExtractionRecords) conflict
   with EF Core's default SaveChanges behavior: SQL Server disallows the
   OUTPUT clause EF Core uses to read back generated values on any table
   with an enabled trigger, so adding them breaks every insert/update on
   those tables until EF Core's model is told about the triggers (a C#
   change). Deferred to the next release, to ship together with the
   planned Hist/Log work rather than as a partial, app-breaking change here.

   AuditLog and RecordHistory tables themselves are still created (Sections
   13-14) for schema readiness; AuditLog simply stays unpopulated until the
   triggers and their EF Core compatibility fix ship together.
   ============================================================================= */

/* =============================================================================
   SECTION 21 - Permissions

   DCTSDUser is created manually by CMRE (see instructions at the top of
   this script) and used for both the AI tool and the application - a
   single technical user, not split.

   Tables granted are the 14 tables current application code reads/writes.
   The 4 legacy tables (BrandingSettings, EmailTemplates, DocumentTypes,
   OcrExtractionEntries) are not granted, since no application code uses
   them.

   __EFMigrationsHistory (Section 22) is also not granted: this script
   baselines its rows directly as part of deployment, and the application
   does not apply migrations at runtime, so DCTSDUser never needs to
   read or write this table.

   RecordHistory and AuditLog follow the Hist/Log permission model instead
   of the blanket CRUD given to the other 12 tables: RecordHistory gets
   SELECT + INSERT only; AuditLog gets SELECT only (Section 20's triggers
   populate it as the table owner, independent of the caller's own grants).

   Run this section only after CMRE has created DCTSDUser (per the
   instructions at the top) - these GRANT statements will error otherwise.
   ============================================================================= */
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Users                      TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.RefreshTokens              TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.FetchRuns                  TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.OcrExtractionRecords       TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.CodeLookups                TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.ManualValidationRequests   TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.MigrationRecords           TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.MigrationDocuments         TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.EmptyFolderRecords         TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.FailedExtractionRecords    TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.AppSettings                TO [DCTSDUser];
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.ImageExtractionStaging     TO [DCTSDUser];
GRANT SELECT, INSERT                 ON dbo.RecordHistory              TO [DCTSDUser];
GRANT SELECT                         ON dbo.AuditLog                  TO [DCTSDUser];
GO


/* =============================================================================
   SECTION 22 - __EFMigrationsHistory (EF Core's migration bookkeeping table)

   Creates the table only. The 5 baseline rows that mark this schema's
   migrations as already applied are data, not schema, and are populated by
   the separate dct_sd_main_data.sql script - run that immediately after
   this script, before the application starts.
   ============================================================================= */
IF OBJECT_ID('dbo.__EFMigrationsHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.__EFMigrationsHistory (
        MigrationId    NVARCHAR(150) NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
        ProductVersion NVARCHAR(32)  NOT NULL
    );
END
GO

/* =============================================================================
   SECTION 23 - Per-table History (_Hist) and Log (_Log) tables, and their
   triggers, per DB_Coding Standards & Guidelines 6.d.5-d.9, 6.e.1 and 6.e.3.

   Replaces the earlier shared-AuditLog approach (Section 20, deferred) with
   one dedicated _Log and _Hist table per applicable main table, as required.
   Columns are adapted from the guideline's literal Sybase-ASE-replication
   wording (Replicated_RoD_ID, univarchar, numeric(30) identity) to this
   single-instance SQL Server app - Replicated_RoD_ID is dropped entirely,
   consistent with the guideline's own "N/A" annotation for non-replicated
   systems; univarchar/numeric(30) become nvarchar/int identity. Log/Hist
   column order follows the guideline's literal d.5/d.6 sequence (AppName,
   IPAddr, HostName, Date, Activity/RefNo).

   Per guideline 6.e.1 ("trg_<tablename>_<DMLType>"), each table gets THREE
   separate triggers - trg_<Table>_I, trg_<Table>_U, trg_<Table>_D - instead
   of one combined AFTER INSERT,UPDATE,DELETE trigger, so trigger names and
   the DML type they fire on always match exactly.

   Log is a lightweight "who/when/what operation" ledger - one row per
   INSERT/UPDATE/DELETE, no payload. Hist carries the actual before/after
   value snapshot and only fires on UPDATE/DELETE: an INSERT's "after" state
   is just the row itself, so an insert-time Hist row would duplicate Log
   and the main table for no benefit - trg_<Table>_I therefore only writes
   to Log, never to Hist.

   Covers the 11 tables with live EF Core entities the application actually
   writes to (Users, RefreshTokens, AppSettings, CodeLookups, FetchRuns,
   OcrExtractionRecords, ManualValidationRequests, MigrationRecords,
   MigrationDocuments, EmptyFolderRecords, FailedExtractionRecords). Legacy
   tables with no EF entity (BrandingSettings, EmailTemplates, DocumentTypes,
   ImageExtractionStaging, OcrExtractionEntries) are out of scope, consistent
   with how Section 21 already treats them.

   EF Core compatibility: SQL Server disallows the OUTPUT clause EF Core
   uses by default on any table with an enabled trigger. All 11 tables'
   entity configurations now have UseSqlOutputClause(false) set (see each
   entity's own Configuration.cs file under Configuration) - this is
   required for these triggers to coexist with the application, not optional.
   ============================================================================= */
IF OBJECT_ID('dbo.Users_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_Users_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_Users_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_Users_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Log_01' AND object_id = OBJECT_ID('dbo.Users_Log'))
    CREATE NONCLUSTERED INDEX IX_Users_Log_01 ON dbo.Users_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.Users_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_Users_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_Users_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_Users_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_Users_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_Users_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Hist_01' AND object_id = OBJECT_ID('dbo.Users_Hist'))
    CREATE NONCLUSTERED INDEX IX_Users_Hist_01 ON dbo.Users_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Hist_02' AND object_id = OBJECT_ID('dbo.Users_Hist'))
    CREATE NONCLUSTERED INDEX IX_Users_Hist_02 ON dbo.Users_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_Users_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_Users_I ON dbo.Users AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.Users_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_Users_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_Users_U ON dbo.Users AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.Users_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.Users_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, i.Username,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_Users_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_Users_D ON dbo.Users AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.Users_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.Users_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, d.Username,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

IF OBJECT_ID('dbo.RefreshTokens_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshTokens_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_RefreshTokens_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_RefreshTokens_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_RefreshTokens_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Log_01' AND object_id = OBJECT_ID('dbo.RefreshTokens_Log'))
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_Log_01 ON dbo.RefreshTokens_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.RefreshTokens_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshTokens_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_RefreshTokens_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_RefreshTokens_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_RefreshTokens_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_RefreshTokens_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_RefreshTokens_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Hist_01' AND object_id = OBJECT_ID('dbo.RefreshTokens_Hist'))
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_Hist_01 ON dbo.RefreshTokens_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Hist_02' AND object_id = OBJECT_ID('dbo.RefreshTokens_Hist'))
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_Hist_02 ON dbo.RefreshTokens_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_RefreshTokens_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_RefreshTokens_I ON dbo.RefreshTokens AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.RefreshTokens_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_RefreshTokens_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_RefreshTokens_U ON dbo.RefreshTokens AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.RefreshTokens_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.RefreshTokens_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, i.TokenHash,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_RefreshTokens_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_RefreshTokens_D ON dbo.RefreshTokens AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.RefreshTokens_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.RefreshTokens_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, d.TokenHash,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

IF OBJECT_ID('dbo.AppSettings_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSettings_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppSettings_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_AppSettings_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_AppSettings_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_AppSettings_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppSettings_Log_01' AND object_id = OBJECT_ID('dbo.AppSettings_Log'))
    CREATE NONCLUSTERED INDEX IX_AppSettings_Log_01 ON dbo.AppSettings_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.AppSettings_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSettings_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppSettings_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_AppSettings_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_AppSettings_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_AppSettings_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_AppSettings_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_AppSettings_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppSettings_Hist_01' AND object_id = OBJECT_ID('dbo.AppSettings_Hist'))
    CREATE NONCLUSTERED INDEX IX_AppSettings_Hist_01 ON dbo.AppSettings_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppSettings_Hist_02' AND object_id = OBJECT_ID('dbo.AppSettings_Hist'))
    CREATE NONCLUSTERED INDEX IX_AppSettings_Hist_02 ON dbo.AppSettings_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_AppSettings_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_AppSettings_I ON dbo.AppSettings AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.AppSettings_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_AppSettings_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_AppSettings_U ON dbo.AppSettings AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.AppSettings_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.AppSettings_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, CONCAT(i.Category, N'':'', i.[Key]),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_AppSettings_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_AppSettings_D ON dbo.AppSettings AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.AppSettings_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.AppSettings_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, CONCAT(d.Category, N'':'', d.[Key]),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

IF OBJECT_ID('dbo.CodeLookups_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CodeLookups_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CodeLookups_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_CodeLookups_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_CodeLookups_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_CodeLookups_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CodeLookups_Log_01' AND object_id = OBJECT_ID('dbo.CodeLookups_Log'))
    CREATE NONCLUSTERED INDEX IX_CodeLookups_Log_01 ON dbo.CodeLookups_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.CodeLookups_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CodeLookups_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CodeLookups_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_CodeLookups_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_CodeLookups_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_CodeLookups_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_CodeLookups_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_CodeLookups_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CodeLookups_Hist_01' AND object_id = OBJECT_ID('dbo.CodeLookups_Hist'))
    CREATE NONCLUSTERED INDEX IX_CodeLookups_Hist_01 ON dbo.CodeLookups_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CodeLookups_Hist_02' AND object_id = OBJECT_ID('dbo.CodeLookups_Hist'))
    CREATE NONCLUSTERED INDEX IX_CodeLookups_Hist_02 ON dbo.CodeLookups_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_CodeLookups_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_CodeLookups_I ON dbo.CodeLookups AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.CodeLookups_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_CodeLookups_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_CodeLookups_U ON dbo.CodeLookups AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.CodeLookups_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.CodeLookups_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, CONCAT(i.LookupType, N'':'', i.Code),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_CodeLookups_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_CodeLookups_D ON dbo.CodeLookups AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.CodeLookups_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.CodeLookups_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, CONCAT(d.LookupType, N'':'', d.Code),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

IF OBJECT_ID('dbo.FetchRuns_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FetchRuns_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FetchRuns_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_FetchRuns_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_FetchRuns_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_FetchRuns_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FetchRuns_Log_01' AND object_id = OBJECT_ID('dbo.FetchRuns_Log'))
    CREATE NONCLUSTERED INDEX IX_FetchRuns_Log_01 ON dbo.FetchRuns_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.FetchRuns_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FetchRuns_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FetchRuns_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_FetchRuns_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_FetchRuns_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_FetchRuns_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_FetchRuns_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_FetchRuns_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FetchRuns_Hist_01' AND object_id = OBJECT_ID('dbo.FetchRuns_Hist'))
    CREATE NONCLUSTERED INDEX IX_FetchRuns_Hist_01 ON dbo.FetchRuns_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FetchRuns_Hist_02' AND object_id = OBJECT_ID('dbo.FetchRuns_Hist'))
    CREATE NONCLUSTERED INDEX IX_FetchRuns_Hist_02 ON dbo.FetchRuns_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_FetchRuns_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FetchRuns_I ON dbo.FetchRuns AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FetchRuns_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_FetchRuns_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FetchRuns_U ON dbo.FetchRuns AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FetchRuns_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.FetchRuns_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, CONCAT(i.RecordKind, N'':'', CONVERT(NVARCHAR(30), i.StartedAt, 126)),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_FetchRuns_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FetchRuns_D ON dbo.FetchRuns AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FetchRuns_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.FetchRuns_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, CONCAT(d.RecordKind, N'':'', CONVERT(NVARCHAR(30), d.StartedAt, 126)),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

IF OBJECT_ID('dbo.OcrExtractionRecords_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OcrExtractionRecords_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OcrExtractionRecords_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionRecords_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionRecords_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_OcrExtractionRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_Log_01' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_Log_01 ON dbo.OcrExtractionRecords_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.OcrExtractionRecords_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OcrExtractionRecords_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OcrExtractionRecords_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionRecords_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionRecords_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_OcrExtractionRecords_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_OcrExtractionRecords_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_OcrExtractionRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_Hist_01' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_Hist_01 ON dbo.OcrExtractionRecords_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_Hist_02' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_Hist_02 ON dbo.OcrExtractionRecords_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_OcrExtractionRecords_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_OcrExtractionRecords_I ON dbo.OcrExtractionRecords AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.OcrExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_OcrExtractionRecords_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_OcrExtractionRecords_U ON dbo.OcrExtractionRecords AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.OcrExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.OcrExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, i.RequestNumber,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_OcrExtractionRecords_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_OcrExtractionRecords_D ON dbo.OcrExtractionRecords AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.OcrExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.OcrExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, d.RequestNumber,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

IF OBJECT_ID('dbo.ManualValidationRequests_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManualValidationRequests_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ManualValidationRequests_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_ManualValidationRequests_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_ManualValidationRequests_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_ManualValidationRequests_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_Log_01' AND object_id = OBJECT_ID('dbo.ManualValidationRequests_Log'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_Log_01 ON dbo.ManualValidationRequests_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.ManualValidationRequests_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManualValidationRequests_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ManualValidationRequests_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_ManualValidationRequests_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_ManualValidationRequests_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_ManualValidationRequests_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_ManualValidationRequests_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_ManualValidationRequests_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_Hist_01' AND object_id = OBJECT_ID('dbo.ManualValidationRequests_Hist'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_Hist_01 ON dbo.ManualValidationRequests_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_Hist_02' AND object_id = OBJECT_ID('dbo.ManualValidationRequests_Hist'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_Hist_02 ON dbo.ManualValidationRequests_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_ManualValidationRequests_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_ManualValidationRequests_I ON dbo.ManualValidationRequests AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.ManualValidationRequests_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_ManualValidationRequests_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_ManualValidationRequests_U ON dbo.ManualValidationRequests AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.ManualValidationRequests_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.ManualValidationRequests_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, i.RequestNumber,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_ManualValidationRequests_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_ManualValidationRequests_D ON dbo.ManualValidationRequests AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.ManualValidationRequests_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.ManualValidationRequests_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, d.RequestNumber,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

IF OBJECT_ID('dbo.MigrationRecords_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationRecords_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MigrationRecords_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_MigrationRecords_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_MigrationRecords_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_MigrationRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_Log_01' AND object_id = OBJECT_ID('dbo.MigrationRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_Log_01 ON dbo.MigrationRecords_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.MigrationRecords_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationRecords_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MigrationRecords_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_MigrationRecords_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_MigrationRecords_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_MigrationRecords_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_MigrationRecords_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_MigrationRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_Hist_01' AND object_id = OBJECT_ID('dbo.MigrationRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_Hist_01 ON dbo.MigrationRecords_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_Hist_02' AND object_id = OBJECT_ID('dbo.MigrationRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_Hist_02 ON dbo.MigrationRecords_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_MigrationRecords_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationRecords_I ON dbo.MigrationRecords AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_MigrationRecords_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationRecords_U ON dbo.MigrationRecords AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.MigrationRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, i.RequestNumber,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_MigrationRecords_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationRecords_D ON dbo.MigrationRecords AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.MigrationRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, d.RequestNumber,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

IF OBJECT_ID('dbo.MigrationDocuments_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationDocuments_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MigrationDocuments_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_MigrationDocuments_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_MigrationDocuments_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_MigrationDocuments_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationDocuments_Log_01' AND object_id = OBJECT_ID('dbo.MigrationDocuments_Log'))
    CREATE NONCLUSTERED INDEX IX_MigrationDocuments_Log_01 ON dbo.MigrationDocuments_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.MigrationDocuments_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationDocuments_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MigrationDocuments_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_MigrationDocuments_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_MigrationDocuments_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_MigrationDocuments_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_MigrationDocuments_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_MigrationDocuments_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationDocuments_Hist_01' AND object_id = OBJECT_ID('dbo.MigrationDocuments_Hist'))
    CREATE NONCLUSTERED INDEX IX_MigrationDocuments_Hist_01 ON dbo.MigrationDocuments_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationDocuments_Hist_02' AND object_id = OBJECT_ID('dbo.MigrationDocuments_Hist'))
    CREATE NONCLUSTERED INDEX IX_MigrationDocuments_Hist_02 ON dbo.MigrationDocuments_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_MigrationDocuments_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationDocuments_I ON dbo.MigrationDocuments AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationDocuments_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_MigrationDocuments_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationDocuments_U ON dbo.MigrationDocuments AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationDocuments_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.MigrationDocuments_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, i.FileName,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_MigrationDocuments_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationDocuments_D ON dbo.MigrationDocuments AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationDocuments_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.MigrationDocuments_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, d.FileName,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

IF OBJECT_ID('dbo.EmptyFolderRecords_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmptyFolderRecords_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmptyFolderRecords_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_EmptyFolderRecords_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_EmptyFolderRecords_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_EmptyFolderRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmptyFolderRecords_Log_01' AND object_id = OBJECT_ID('dbo.EmptyFolderRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_EmptyFolderRecords_Log_01 ON dbo.EmptyFolderRecords_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.EmptyFolderRecords_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmptyFolderRecords_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmptyFolderRecords_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_EmptyFolderRecords_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_EmptyFolderRecords_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_EmptyFolderRecords_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_EmptyFolderRecords_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_EmptyFolderRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmptyFolderRecords_Hist_01' AND object_id = OBJECT_ID('dbo.EmptyFolderRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_EmptyFolderRecords_Hist_01 ON dbo.EmptyFolderRecords_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmptyFolderRecords_Hist_02' AND object_id = OBJECT_ID('dbo.EmptyFolderRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_EmptyFolderRecords_Hist_02 ON dbo.EmptyFolderRecords_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_EmptyFolderRecords_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_EmptyFolderRecords_I ON dbo.EmptyFolderRecords AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.EmptyFolderRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_EmptyFolderRecords_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_EmptyFolderRecords_U ON dbo.EmptyFolderRecords AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.EmptyFolderRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.EmptyFolderRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, i.FolderPath,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_EmptyFolderRecords_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_EmptyFolderRecords_D ON dbo.EmptyFolderRecords AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.EmptyFolderRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.EmptyFolderRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, d.FolderPath,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

IF OBJECT_ID('dbo.FailedExtractionRecords_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FailedExtractionRecords_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FailedExtractionRecords_Log PRIMARY KEY,
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_FailedExtractionRecords_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_FailedExtractionRecords_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_FailedExtractionRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FailedExtractionRecords_Log_01' AND object_id = OBJECT_ID('dbo.FailedExtractionRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_FailedExtractionRecords_Log_01 ON dbo.FailedExtractionRecords_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.FailedExtractionRecords_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FailedExtractionRecords_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FailedExtractionRecords_Hist PRIMARY KEY,
        Hist_UserID    INT           NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_FailedExtractionRecords_Hist_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Hist_IPAddr    NVARCHAR(64)  NULL,
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_FailedExtractionRecords_Hist_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_FailedExtractionRecords_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(150) NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_FailedExtractionRecords_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_FailedExtractionRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FailedExtractionRecords_Hist_01' AND object_id = OBJECT_ID('dbo.FailedExtractionRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_FailedExtractionRecords_Hist_01 ON dbo.FailedExtractionRecords_Hist (Hist_RefNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FailedExtractionRecords_Hist_02' AND object_id = OBJECT_ID('dbo.FailedExtractionRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_FailedExtractionRecords_Hist_02 ON dbo.FailedExtractionRecords_Hist (Hist_Date, Hist_UserID);
GO

IF OBJECT_ID('dbo.trg_FailedExtractionRecords_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FailedExtractionRecords_I ON dbo.FailedExtractionRecords AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FailedExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''I'' FROM inserted;
END
');
GO

IF OBJECT_ID('dbo.trg_FailedExtractionRecords_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FailedExtractionRecords_U ON dbo.FailedExtractionRecords AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FailedExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''U'' FROM inserted;

    INSERT INTO dbo.FailedExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, i.FolderPath,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO)
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_FailedExtractionRecords_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FailedExtractionRecords_D ON dbo.FailedExtractionRecords AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FailedExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity)
    SELECT @UserId, @IPAddr, ''D'' FROM deleted;

    INSERT INTO dbo.FailedExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson)
    SELECT @UserId, @IPAddr, d.FolderPath,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL
    FROM deleted d;
END
');
GO

-- Per guideline 6.b.3/6.b.4 (Hist/Log permission model), applied to the
-- Section 23 per-table Hist/Log tables: Hist gets SELECT+INSERT only, Log
-- gets SELECT only - the trigger writes as the table owner regardless.
GRANT SELECT, INSERT ON dbo.Users_Hist              TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.RefreshTokens_Hist      TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.AppSettings_Hist        TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.CodeLookups_Hist        TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.FetchRuns_Hist          TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.OcrExtractionRecords_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.ManualValidationRequests_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.MigrationRecords_Hist   TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.MigrationDocuments_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.EmptyFolderRecords_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.FailedExtractionRecords_Hist TO [DCTSDUser];
GRANT SELECT         ON dbo.Users_Log               TO [DCTSDUser];
GRANT SELECT         ON dbo.RefreshTokens_Log       TO [DCTSDUser];
GRANT SELECT         ON dbo.AppSettings_Log         TO [DCTSDUser];
GRANT SELECT         ON dbo.CodeLookups_Log         TO [DCTSDUser];
GRANT SELECT         ON dbo.FetchRuns_Log           TO [DCTSDUser];
GRANT SELECT         ON dbo.OcrExtractionRecords_Log TO [DCTSDUser];
GRANT SELECT         ON dbo.ManualValidationRequests_Log TO [DCTSDUser];
GRANT SELECT         ON dbo.MigrationRecords_Log    TO [DCTSDUser];
GRANT SELECT         ON dbo.MigrationDocuments_Log  TO [DCTSDUser];
GRANT SELECT         ON dbo.EmptyFolderRecords_Log  TO [DCTSDUser];
GRANT SELECT         ON dbo.FailedExtractionRecords_Log TO [DCTSDUser];
GO

PRINT 'dct_sd_main.sql completed.';
GO
