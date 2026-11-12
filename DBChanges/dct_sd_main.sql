USE DCT_SD  -- DBA needs to create the Database
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
        [Plan]              NVARCHAR(50)      NULL,
        Block               NVARCHAR(50)      NULL,
        Lot                 NVARCHAR(50)      NULL,
        TitleSequence       NVARCHAR(50)      NULL,
        DocumentCount       INT               NOT NULL CONSTRAINT DF_OcrExtractionRecords_DocumentCount DEFAULT ((0)),
        ExtractionStatus    INT               NOT NULL,
        ExtractionDateTime  DATETIME2         NOT NULL CONSTRAINT DF_OcrExtractionRecords_ExtractionDateTime DEFAULT (SYSUTCDATETIME()),
        DocumentsJson       NVARCHAR(MAX)     NULL,
        EntryNumberRange    NVARCHAR(MAX)     NULL CONSTRAINT DF_OcrExtractionRecords_EntryNumberRange DEFAULT ('0'),
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
        EntryNumberRange        NVARCHAR(MAX)     NULL CONSTRAINT DF_ManualValidationRequests_EntryNumberRange DEFAULT ('0'),
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
        -- NVARCHAR(200), not MAX (unlike OcrExtractionRecords/ManualValidationRequests' own
        -- EntryNumbersCsv columns): this one needs to be a key column in UK_MigrationRecords_01
        -- below, which NVARCHAR(MAX) cannot be. Matches MigrationRecordConfiguration.cs's
        -- HasMaxLength(200), which this column always should have been.
        EntryNumbersCsv       NVARCHAR(200)     NULL,
        Title                 NVARCHAR(50)      NULL,
        TitleType             INT               NULL,
        [Plan]                NVARCHAR(50)      NULL,
        Block                 NVARCHAR(50)      NULL,
        Lot                   NVARCHAR(50)      NULL,
        TitleSequence         NVARCHAR(50)      NULL,
        -- Nullable (per 2026-11-09 System Migration batch work): only populated once a row
        -- reaches ProcessingStatus = 'Completed' - see the columns below. Manual Validation /
        -- legacy rows (populated by the external process) always have real values here, same as
        -- before this change.
        MigratedToRdName      NVARCHAR(150)     NULL,
        MigrationStatus       NVARCHAR(50)      NULL,
        SdStatus              NVARCHAR(50)      NULL,
        -- Added 2026-11-09: identifies how this row was produced, and the System Migration
        -- batch's own claim/attempt/retry tracking (independent of MigrationStatus/SdStatus
        -- above, which only describe a completed migration's outcome). NULL for rows written
        -- before this change.
        MigrationType                 NVARCHAR(30)  NULL,
        ProcessedByUserId             INT           NULL,
        ProcessedByUsername           NVARCHAR(256) NULL,
        ProcessingStatus              NVARCHAR(20)  NULL,
        AttemptCount                  INT           NOT NULL CONSTRAINT DF_MigrationRecords_AttemptCount DEFAULT ((0)),
        ProcessingStartedAt           DATETIME2     NULL,
        ProcessingCompletedAt         DATETIME2     NULL,
        LastAttemptAt                 DATETIME2     NULL,
        LastFailureReason             NVARCHAR(500) NULL,
        ApiResponseJson               NVARCHAR(MAX) NULL,
        ApiResponseAt                 DATETIME2     NULL,
        TargetSystemValidation        NVARCHAR(100) NULL,
        TargetSystemActivationStatus  NVARCHAR(20)  NULL,
        -- Added 2026-11-10 (Ready for Migration / SD Auto Conversion from Migration Monitoring):
        -- holds the Entry Record's supporting-document information end to end - the original
        -- ManualValidationRequests.DocumentsJson copied verbatim on "Ready for Migration", then
        -- merged with the SD Auto Conversion API's own response under a separate "apiResponse"
        -- property once that API has been called (see MigrationService.InitiateMigrationAsync).
        -- Distinct from ApiResponseJson above, which is the System Migration batch's own raw
        -- audit passthrough - this column is the durable combined document+response record.
        Document_Json                 NVARCHAR(MAX) NULL,
        -- Added 2026-11-11: copied directly from the originating row's own EntryNumberRange
        -- (ManualValidationRequests.EntryNumberRange for a Manual Validation-origin row,
        -- OcrExtractionRecords.EntryNumberRange for a System Migration-origin row) the moment
        -- this row is created - not looked up indirectly via OcrExtractionRecordId at display
        -- time, since a Manual Validation user's own correction to EntryNumberRange (Save) only
        -- ever updates ManualValidationRequests, never the OcrExtractionRecords row it came from.
        RangeEntryNumber              NVARCHAR(MAX) NULL,
        CONSTRAINT PK_MigrationRecords PRIMARY KEY (Id),
        CONSTRAINT FK_MigrationRecords_01 FOREIGN KEY (OcrExtractionRecordId) REFERENCES dbo.OcrExtractionRecords (Id),
        CONSTRAINT CK_MigrationRecords_ApiResponseJson CHECK (ApiResponseJson IS NULL OR ISJSON(ApiResponseJson) = 1),
        CONSTRAINT CK_MigrationRecords_DocumentJson CHECK (Document_Json IS NULL OR ISJSON(Document_Json) = 1)
    );
END;
GO

-- Composite, not single-column RequestNumber: as of 2026-11-12, one MigrationRecords row is
-- created per entry number (see Helpers/EntryNumberCsvParser.cs), so several rows now legitimately
-- share one RequestNumber - this still blocks a genuine duplicate re-processing attempt (same
-- source, same entry number) while allowing that source's other entry numbers their own rows.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_MigrationRecords_01' AND object_id = OBJECT_ID('dbo.MigrationRecords'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_MigrationRecords_01 ON dbo.MigrationRecords (RequestNumber, EntryNumbersCsv);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_01' AND object_id = OBJECT_ID('dbo.MigrationRecords'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_01 ON dbo.MigrationRecords (MigrationDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_02' AND object_id = OBJECT_ID('dbo.MigrationRecords'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_02 ON dbo.MigrationRecords (MigrationStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_03' AND object_id = OBJECT_ID('dbo.MigrationRecords'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_03 ON dbo.MigrationRecords (OcrExtractionRecordId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_04' AND object_id = OBJECT_ID('dbo.MigrationRecords'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_04 ON dbo.MigrationRecords (ProcessingStatus);
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
        -- Added 2026-11-09: populated directly from the SD Auto Conversion API's per-image
        -- response (destinationPath/sizeBytes/sequence), System Migration rows only.
        DestinationPath      NVARCHAR(500)     NULL,
        SizeBytes            BIGINT            NULL,
        Sequence             INT               NULL,
        -- Added 2026-11-10 (Supporting Document Image columns AC): SourceImagePath is the
        -- submitted DCT-SD image path (the exact value sent to the SD Auto Conversion API as
        -- SupportingDocuments[n].file); ReasonForFailing is set only for a document the API
        -- never recognized (Status = Failed) - see Services/SystemMigrationService.cs and
        -- Services/MigrationService.cs.
        SourceImagePath      NVARCHAR(500)     NULL,
        ReasonForFailing     NVARCHAR(500)     NULL,
        -- Added 2026-11-12: per-document Target System Validation, distinct from
        -- MigrationRecords.TargetSystemValidation (the Entry Record-level value, also reused as-is
        -- for each sibling Title Record). Deliberately left unset by every code path today - the
        -- external SD Auto Conversion API's documented response has no field for this at either
        -- level; nothing populates it until that's confirmed with the API team.
        TargetSystemValidation NVARCHAR(100)   NULL,
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
   triggers, per DB_Coding Standards & Guidelines 6.d.5-d.9, 6.e.1, 6.e.3 and
   6.e.4, and the established earlier-project Hist/Log pattern (full
   main-table column mirror + a plain-int copy of the main table's own PK),
   as reviewed and approved on 2026-11-02 (plus 2026-11-05 client comments:
   Log_AppName/Hist_AppName, Log_HostName/Hist_HostName and Log_IPAddr/
   Hist_IPAddr now default via the client's own ufn_GetAppName/
   ufn_GetHostName/ufn_GetIPAddress functions, defined at the top of this
   section, instead of the earlier inline APP_NAME()/HOST_NAME() calls;
   Log_Activity is NVARCHAR(1), not CHAR(1); Hist_RefNo is NVARCHAR(50), not
   NVARCHAR(150) - this was consolidated into dct_sd_main.sql itself at the
   same time, per guideline 3.c, so there is one script per instance instead
   of a separate DCT_SD_HistLog_Full.sql).

   Replaces the earlier shared-AuditLog approach (Section 20, deferred) with
   one dedicated _Log and _Hist table per applicable main table.

   For every applicable main table T, T_Log and T_Hist each have:
     - The standard audit columns (Log_ID/Hist_ID, UserID, AppName, IPAddr,
       HostName, Date, Activity/RefNo), per guideline d.5/d.6/d.7/d.8.
       Log_UserID/Hist_UserID are NVARCHAR(50) NOT NULL (changed from
       INT NULL on 2026-11-03, per explicit client instruction) - the
       trigger falls back to the literal '0' (ISNULL) whenever
       SESSION_CONTEXT('AppUserId') has no value, matching this codebase's
       own existing "no authenticated user" sentinel (ICurrentUserService.
       UserId ?? 0, e.g. RdConfigService.cs). This fallback is required,
       not optional, now that the column is NOT NULL: SESSION_CONTEXT('App
       UserId') is unset on at least two real paths - the Login request
       itself (RefreshTokens is written before any authenticated
       HttpContext.User exists) and any startup/seed-data insert that runs
       with no HttpContext at all - either of those would otherwise fail
       the trigger, and with it the entire INSERT/UPDATE/DELETE on the main
       table, the instant this column stopped accepting NULL.
     - Id INT NULL - a plain copy of T's own Primary Key column, using the
       exact same column name as the main table (per the client's own
       earlier-project reference pattern, e.g. Configurations.ConfigID
       mirrored into Configurations_Hist/_Log as ConfigID too - renamed here
       from an earlier draft's generic "RecordId" to close that naming gap).
       NOT FK-constrained, so Hist/Log rows survive deletion of the main
       record.
     - One column per T business column, using T's own data type but always
       NULL (no defaults) - populated only by the trigger below. ROWVERSION
       columns and PERSISTED computed hash columns (used only to index an
       NVARCHAR(MAX) column) are excluded: ROWVERSION is meaningless to
       snapshot, and the hash is redundant once its source column is already
       mirrored - this project's equivalent of guideline e.4's own "except
       for columns that have Image DataType" exception.
     - T_Hist additionally keeps OldValuesJson/NewValuesJson (a JSON
       before/after snapshot pair), in addition to the discrete columns.

   Indexing (per the client's mandatory indexing guideline, reviewed and
   applied on 2026-11-03): every T_Log gets two non-clustered indexes -
   IX_T_Log_01 on Id (the main table's own Primary Key column, mirrored
   here under its own original name, so Log rows for a given record are
   directly seekable) and IX_T_Log_02 on (Log_Date, Log_UserID) in that
   order. Every T_Hist gets three - IX_T_Hist_01 on Id (same reasoning as
   the Log index), IX_T_Hist_02 on (Hist_Date, Hist_UserID) in that order,
   and IX_T_Hist_03 on Hist_RefNo. (Earlier drafts of this script had only
   one Log index, numbered _01, on (Log_Date, Log_UserID), and only two
   Hist indexes, with Hist_RefNo numbered _01 - renumbered here to make
   room for the new Id index as _01 on both, per the guideline's own
   numbering.)

   Permissions (per guideline b.3/b.4, applied 2026-11-03, matching the
   existing RecordHistory/AuditLog precedent in Section 21 of dct_sd_main.sql
   exactly): every T_Log gets GRANT SELECT only to [DCTSDUser] - no INSERT,
   UPDATE or DELETE to any user, since only the trigger (as table owner)
   ever writes to it. Every T_Hist gets GRANT SELECT, INSERT to [DCTSDUser]
   - still no UPDATE or DELETE to any user. [DCTSDUser] is the same single
   technical user Section 21 already grants the main tables to; these GRANT
   statements require that user to already exist (created manually by CMRE),
   same precondition as Section 21 itself. All 32 GRANT statements are
   consolidated in one block at the very end of this script (mirroring
   Section 21's own layout in dct_sd_main.sql - one permissions block after
   every table/trigger already exists - rather than interleaved per table).

   Log is a lightweight "who/when/what operation" ledger - one row per
   INSERT/UPDATE/DELETE, mirroring the resulting row state (inserted for
   I/U, deleted for D). Hist carries the prior/old row state and only fires
   on UPDATE/DELETE: an INSERT's "prior" state doesn't exist, so an
   insert-time Hist row would duplicate Log and the main table for no
   benefit.

   Per guideline 6.e.1 ("trg_<tablename>_<DMLType>"), each table gets THREE
   separate triggers - trg_<Table>_I, trg_<Table>_U, trg_<Table>_D.

   Covers the 11 tables with live EF Core entities the application actually
   writes to (Users, RefreshTokens, AppSettings, CodeLookups, FetchRuns,
   OcrExtractionRecords, ManualValidationRequests, MigrationRecords,
   MigrationDocuments, EmptyFolderRecords, FailedExtractionRecords), plus
   BrandingSettings, EmailTemplates, DocumentTypes, OcrExtractionEntries and
   ImageExtractionStaging (added 2026-11-03 per client request, for complete
   physical-table coverage regardless of current usage) - note that
   BrandingSettings, EmailTemplates and DocumentTypes are confirmed legacy/
   unused by the live application (SettingsService.cs now stores branding
   and email-template data in AppSettings, and DocumentTypeService.cs now
   reads document types from CodeLookups), and OcrExtractionEntries/
   ImageExtractionStaging have no current entity, service, or controller
   referencing them (EF migration artifacts only) - triggers on these 5
   tables are inert under today's code paths, included only so every
   physical table in the schema has audit coverage if it's ever written to
   again. Their ROWVERSION columns and PERSISTED computed hash columns
   (BrandingSettings.ImagePathHash, DocumentTypes.DocumentNameHash,
   ImageExtractionStaging.FolderPathHash/ImagePathHash) are excluded from
   the mirror for the same reason as every other table's ROWVERSION/
   computed-hash columns: meaningless to snapshot, and redundant once their
   source column is already mirrored.

   EF Core compatibility: SQL Server disallows the OUTPUT clause EF Core
   uses by default on any table with an enabled trigger. All 11 tables'
   entity configurations must have UseSqlOutputClause(false) set (see each
   entity's own Configuration.cs file under Configuration) - this is
   required for these triggers to coexist with the application, not
   optional.

   Idempotent: every CREATE is guarded (IF OBJECT_ID IS NULL), so this
   script can be re-run safely. Purely additive - no DROP, no data loss.
   ============================================================================= */

IF OBJECT_ID('dbo.ufn_GetAppName', 'FN') IS NULL
EXEC('
CREATE FUNCTION dbo.ufn_GetAppName()
RETURNS VARCHAR(100)
AS
BEGIN
    DECLARE @Log_AppName VARCHAR(30)
    SELECT @Log_AppName = program_name FROM master..sysprocesses WHERE spid = @@spid
    RETURN @Log_AppName
END
');
GO

IF OBJECT_ID('dbo.ufn_GetHostName', 'FN') IS NULL
EXEC('
CREATE FUNCTION dbo.ufn_GetHostName()
RETURNS VARCHAR(100)
AS
BEGIN
    DECLARE @Log_HostName VARCHAR(50)
    SELECT @Log_HostName = hostname FROM master..sysprocesses WHERE spid = @@spid
    RETURN @Log_HostName
END
');
GO

IF OBJECT_ID('dbo.ufn_GetIPAddress', 'FN') IS NULL
EXEC('
CREATE FUNCTION dbo.ufn_GetIPAddress()
RETURNS VARCHAR(100)
AS
BEGIN
    DECLARE @Log_IPAddr VARCHAR(64)
    SELECT @Log_IPAddr = CONVERT(VARCHAR(64), CONNECTIONPROPERTY(''client_net_address''))
    RETURN @Log_IPAddr
END
');
GO

-- Per guideline 5.b.1/5.b.2: execute permission not granted to public,
-- granted to at least one TechUser (DCTSDUser - see Section 21 of
-- dct_sd_main.sql).
GRANT EXEC ON dbo.ufn_GetAppName   TO [DCTSDUser];
GRANT EXEC ON dbo.ufn_GetHostName  TO [DCTSDUser];
GRANT EXEC ON dbo.ufn_GetIPAddress TO [DCTSDUser];
GO

-------------------------------------------------------------------------------
-- Users
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.Users_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_Users_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_Users_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_Users_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_Users_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        FirstName NVARCHAR(100) NULL,
        LastName NVARCHAR(100) NULL,
        Username NVARCHAR(150) NULL,
        PasswordHash NVARCHAR(MAX) NULL,
        RoleName NVARCHAR(50) NULL,
        MenuPermissionsCsv NVARCHAR(MAX) NULL,
        Status INT NULL,
        FailedLoginAttempts INT NULL,
        LastLoginAt DATETIME2 NULL,
        IsDeleted BIT NULL,
        DeletedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL,
        LockoutEndUtc DATETIME2 NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Log_01' AND object_id = OBJECT_ID('dbo.Users_Log'))
    CREATE NONCLUSTERED INDEX IX_Users_Log_01 ON dbo.Users_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Log_02' AND object_id = OBJECT_ID('dbo.Users_Log'))
    CREATE NONCLUSTERED INDEX IX_Users_Log_02 ON dbo.Users_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.Users_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_Users_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_Users_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_Users_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_Users_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_Users_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_Users_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        FirstName NVARCHAR(100) NULL,
        LastName NVARCHAR(100) NULL,
        Username NVARCHAR(150) NULL,
        PasswordHash NVARCHAR(MAX) NULL,
        RoleName NVARCHAR(50) NULL,
        MenuPermissionsCsv NVARCHAR(MAX) NULL,
        Status INT NULL,
        FailedLoginAttempts INT NULL,
        LastLoginAt DATETIME2 NULL,
        IsDeleted BIT NULL,
        DeletedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL,
        LockoutEndUtc DATETIME2 NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Hist_01' AND object_id = OBJECT_ID('dbo.Users_Hist'))
    CREATE NONCLUSTERED INDEX IX_Users_Hist_01 ON dbo.Users_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Hist_02' AND object_id = OBJECT_ID('dbo.Users_Hist'))
    CREATE NONCLUSTERED INDEX IX_Users_Hist_02 ON dbo.Users_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Hist_03' AND object_id = OBJECT_ID('dbo.Users_Hist'))
    CREATE NONCLUSTERED INDEX IX_Users_Hist_03 ON dbo.Users_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_Users_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_Users_I ON dbo.Users AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.Users_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, FirstName, LastName, Username, PasswordHash, RoleName, MenuPermissionsCsv, Status, FailedLoginAttempts, LastLoginAt, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, LockoutEndUtc)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.FirstName, i.LastName, i.Username, i.PasswordHash, i.RoleName, i.MenuPermissionsCsv, i.Status, i.FailedLoginAttempts, i.LastLoginAt, i.IsDeleted, i.DeletedAt, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy, i.LockoutEndUtc FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_Users_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_Users_U ON dbo.Users AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.Users_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, FirstName, LastName, Username, PasswordHash, RoleName, MenuPermissionsCsv, Status, FailedLoginAttempts, LastLoginAt, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, LockoutEndUtc)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.FirstName, i.LastName, i.Username, i.PasswordHash, i.RoleName, i.MenuPermissionsCsv, i.Status, i.FailedLoginAttempts, i.LastLoginAt, i.IsDeleted, i.DeletedAt, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy, i.LockoutEndUtc FROM inserted i;

    INSERT INTO dbo.Users_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, FirstName, LastName, Username, PasswordHash, RoleName, MenuPermissionsCsv, Status, FailedLoginAttempts, LastLoginAt, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, LockoutEndUtc)
    SELECT @UserId, @IPAddr, LEFT(i.Username, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.FirstName, d.LastName, d.Username, d.PasswordHash, d.RoleName, d.MenuPermissionsCsv, d.Status, d.FailedLoginAttempts, d.LastLoginAt, d.IsDeleted, d.DeletedAt, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy, d.LockoutEndUtc
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_Users_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_Users_D ON dbo.Users AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.Users_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, FirstName, LastName, Username, PasswordHash, RoleName, MenuPermissionsCsv, Status, FailedLoginAttempts, LastLoginAt, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, LockoutEndUtc)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.FirstName, d.LastName, d.Username, d.PasswordHash, d.RoleName, d.MenuPermissionsCsv, d.Status, d.FailedLoginAttempts, d.LastLoginAt, d.IsDeleted, d.DeletedAt, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy, d.LockoutEndUtc FROM deleted d;

    INSERT INTO dbo.Users_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, FirstName, LastName, Username, PasswordHash, RoleName, MenuPermissionsCsv, Status, FailedLoginAttempts, LastLoginAt, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, LockoutEndUtc)
    SELECT @UserId, @IPAddr, LEFT(d.Username, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.FirstName, d.LastName, d.Username, d.PasswordHash, d.RoleName, d.MenuPermissionsCsv, d.Status, d.FailedLoginAttempts, d.LastLoginAt, d.IsDeleted, d.DeletedAt, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy, d.LockoutEndUtc
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- RefreshTokens
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.RefreshTokens_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshTokens_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_RefreshTokens_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_RefreshTokens_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_RefreshTokens_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_RefreshTokens_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        UserId INT NULL,
        TokenHash NVARCHAR(128) NULL,
        CreatedAt DATETIME2 NULL,
        ExpiresAt DATETIME2 NULL,
        RevokedAt DATETIME2 NULL,
        ReplacedByTokenHash NVARCHAR(128) NULL,
        CreatedByIp NVARCHAR(64) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Log_01' AND object_id = OBJECT_ID('dbo.RefreshTokens_Log'))
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_Log_01 ON dbo.RefreshTokens_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Log_02' AND object_id = OBJECT_ID('dbo.RefreshTokens_Log'))
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_Log_02 ON dbo.RefreshTokens_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.RefreshTokens_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshTokens_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_RefreshTokens_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_RefreshTokens_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_RefreshTokens_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_RefreshTokens_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_RefreshTokens_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_RefreshTokens_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        UserId INT NULL,
        TokenHash NVARCHAR(128) NULL,
        CreatedAt DATETIME2 NULL,
        ExpiresAt DATETIME2 NULL,
        RevokedAt DATETIME2 NULL,
        ReplacedByTokenHash NVARCHAR(128) NULL,
        CreatedByIp NVARCHAR(64) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Hist_01' AND object_id = OBJECT_ID('dbo.RefreshTokens_Hist'))
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_Hist_01 ON dbo.RefreshTokens_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Hist_02' AND object_id = OBJECT_ID('dbo.RefreshTokens_Hist'))
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_Hist_02 ON dbo.RefreshTokens_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Hist_03' AND object_id = OBJECT_ID('dbo.RefreshTokens_Hist'))
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_Hist_03 ON dbo.RefreshTokens_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_RefreshTokens_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_RefreshTokens_I ON dbo.RefreshTokens AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.RefreshTokens_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.UserId, i.TokenHash, i.CreatedAt, i.ExpiresAt, i.RevokedAt, i.ReplacedByTokenHash, i.CreatedByIp FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_RefreshTokens_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_RefreshTokens_U ON dbo.RefreshTokens AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.RefreshTokens_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.UserId, i.TokenHash, i.CreatedAt, i.ExpiresAt, i.RevokedAt, i.ReplacedByTokenHash, i.CreatedByIp FROM inserted i;

    INSERT INTO dbo.RefreshTokens_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp)
    SELECT @UserId, @IPAddr, LEFT(i.TokenHash, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.UserId, d.TokenHash, d.CreatedAt, d.ExpiresAt, d.RevokedAt, d.ReplacedByTokenHash, d.CreatedByIp
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_RefreshTokens_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_RefreshTokens_D ON dbo.RefreshTokens AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.RefreshTokens_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.UserId, d.TokenHash, d.CreatedAt, d.ExpiresAt, d.RevokedAt, d.ReplacedByTokenHash, d.CreatedByIp FROM deleted d;

    INSERT INTO dbo.RefreshTokens_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp)
    SELECT @UserId, @IPAddr, LEFT(d.TokenHash, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.UserId, d.TokenHash, d.CreatedAt, d.ExpiresAt, d.RevokedAt, d.ReplacedByTokenHash, d.CreatedByIp
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- AppSettings
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.AppSettings_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSettings_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppSettings_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_AppSettings_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_AppSettings_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_AppSettings_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_AppSettings_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        Category NVARCHAR(30) NULL,
        [Key] NVARCHAR(64) NULL,
        Label NVARCHAR(128) NULL,
        DataJson NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppSettings_Log_01' AND object_id = OBJECT_ID('dbo.AppSettings_Log'))
    CREATE NONCLUSTERED INDEX IX_AppSettings_Log_01 ON dbo.AppSettings_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppSettings_Log_02' AND object_id = OBJECT_ID('dbo.AppSettings_Log'))
    CREATE NONCLUSTERED INDEX IX_AppSettings_Log_02 ON dbo.AppSettings_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.AppSettings_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSettings_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppSettings_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_AppSettings_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_AppSettings_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_AppSettings_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_AppSettings_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_AppSettings_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_AppSettings_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        Category NVARCHAR(30) NULL,
        [Key] NVARCHAR(64) NULL,
        Label NVARCHAR(128) NULL,
        DataJson NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppSettings_Hist_01' AND object_id = OBJECT_ID('dbo.AppSettings_Hist'))
    CREATE NONCLUSTERED INDEX IX_AppSettings_Hist_01 ON dbo.AppSettings_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppSettings_Hist_02' AND object_id = OBJECT_ID('dbo.AppSettings_Hist'))
    CREATE NONCLUSTERED INDEX IX_AppSettings_Hist_02 ON dbo.AppSettings_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppSettings_Hist_03' AND object_id = OBJECT_ID('dbo.AppSettings_Hist'))
    CREATE NONCLUSTERED INDEX IX_AppSettings_Hist_03 ON dbo.AppSettings_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_AppSettings_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_AppSettings_I ON dbo.AppSettings AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.AppSettings_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, Category, [Key], Label, DataJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.Category, i.[Key], i.Label, i.DataJson, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_AppSettings_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_AppSettings_U ON dbo.AppSettings AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.AppSettings_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, Category, [Key], Label, DataJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.Category, i.[Key], i.Label, i.DataJson, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;

    INSERT INTO dbo.AppSettings_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, Category, [Key], Label, DataJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, LEFT(CONCAT(i.Category, N'':'', i.[Key]), 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.Category, d.[Key], d.Label, d.DataJson, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_AppSettings_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_AppSettings_D ON dbo.AppSettings AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.AppSettings_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, Category, [Key], Label, DataJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.Category, d.[Key], d.Label, d.DataJson, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy FROM deleted d;

    INSERT INTO dbo.AppSettings_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, Category, [Key], Label, DataJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, LEFT(CONCAT(d.Category, N'':'', d.[Key]), 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.Category, d.[Key], d.Label, d.DataJson, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- CodeLookups
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.CodeLookups_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CodeLookups_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CodeLookups_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_CodeLookups_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_CodeLookups_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_CodeLookups_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_CodeLookups_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        LookupType NVARCHAR(30) NULL,
        Code NVARCHAR(150) NULL,
        Name NVARCHAR(MAX) NULL,
        DataJson NVARCHAR(MAX) NULL,
        IsActive BIT NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CodeLookups_Log_01' AND object_id = OBJECT_ID('dbo.CodeLookups_Log'))
    CREATE NONCLUSTERED INDEX IX_CodeLookups_Log_01 ON dbo.CodeLookups_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CodeLookups_Log_02' AND object_id = OBJECT_ID('dbo.CodeLookups_Log'))
    CREATE NONCLUSTERED INDEX IX_CodeLookups_Log_02 ON dbo.CodeLookups_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.CodeLookups_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CodeLookups_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CodeLookups_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_CodeLookups_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_CodeLookups_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_CodeLookups_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_CodeLookups_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_CodeLookups_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_CodeLookups_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        LookupType NVARCHAR(30) NULL,
        Code NVARCHAR(150) NULL,
        Name NVARCHAR(MAX) NULL,
        DataJson NVARCHAR(MAX) NULL,
        IsActive BIT NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CodeLookups_Hist_01' AND object_id = OBJECT_ID('dbo.CodeLookups_Hist'))
    CREATE NONCLUSTERED INDEX IX_CodeLookups_Hist_01 ON dbo.CodeLookups_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CodeLookups_Hist_02' AND object_id = OBJECT_ID('dbo.CodeLookups_Hist'))
    CREATE NONCLUSTERED INDEX IX_CodeLookups_Hist_02 ON dbo.CodeLookups_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CodeLookups_Hist_03' AND object_id = OBJECT_ID('dbo.CodeLookups_Hist'))
    CREATE NONCLUSTERED INDEX IX_CodeLookups_Hist_03 ON dbo.CodeLookups_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_CodeLookups_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_CodeLookups_I ON dbo.CodeLookups AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.CodeLookups_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, LookupType, Code, Name, DataJson, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.LookupType, i.Code, i.Name, i.DataJson, i.IsActive, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_CodeLookups_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_CodeLookups_U ON dbo.CodeLookups AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.CodeLookups_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, LookupType, Code, Name, DataJson, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.LookupType, i.Code, i.Name, i.DataJson, i.IsActive, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;

    INSERT INTO dbo.CodeLookups_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, LookupType, Code, Name, DataJson, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, LEFT(CONCAT(i.LookupType, N'':'', i.Code), 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.LookupType, d.Code, d.Name, d.DataJson, d.IsActive, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_CodeLookups_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_CodeLookups_D ON dbo.CodeLookups AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.CodeLookups_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, LookupType, Code, Name, DataJson, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.LookupType, d.Code, d.Name, d.DataJson, d.IsActive, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy FROM deleted d;

    INSERT INTO dbo.CodeLookups_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, LookupType, Code, Name, DataJson, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, LEFT(CONCAT(d.LookupType, N'':'', d.Code), 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.LookupType, d.Code, d.Name, d.DataJson, d.IsActive, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- FetchRuns
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.FetchRuns_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FetchRuns_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FetchRuns_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_FetchRuns_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_FetchRuns_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_FetchRuns_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_FetchRuns_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        SourcePath NVARCHAR(MAX) NULL,
        Status INT NULL,
        TotalCount INT NULL,
        ProcessedCount INT NULL,
        LastProcessedFolderPath NVARCHAR(MAX) NULL,
        LastProcessedAt DATETIME2 NULL,
        ExecutedByUserId INT NULL,
        ExecutedByUsername NVARCHAR(MAX) NULL,
        StartedAt DATETIME2 NULL,
        CompletedAt DATETIME2 NULL,
        RecordKind NVARCHAR(20) NULL,
        FromPath NVARCHAR(MAX) NULL,
        Remarks NVARCHAR(MAX) NULL,
        SummaryMessage NVARCHAR(MAX) NULL,
        RootPath NVARCHAR(MAX) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FetchRuns_Log_01' AND object_id = OBJECT_ID('dbo.FetchRuns_Log'))
    CREATE NONCLUSTERED INDEX IX_FetchRuns_Log_01 ON dbo.FetchRuns_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FetchRuns_Log_02' AND object_id = OBJECT_ID('dbo.FetchRuns_Log'))
    CREATE NONCLUSTERED INDEX IX_FetchRuns_Log_02 ON dbo.FetchRuns_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.FetchRuns_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FetchRuns_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FetchRuns_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_FetchRuns_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_FetchRuns_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_FetchRuns_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_FetchRuns_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_FetchRuns_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_FetchRuns_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        SourcePath NVARCHAR(MAX) NULL,
        Status INT NULL,
        TotalCount INT NULL,
        ProcessedCount INT NULL,
        LastProcessedFolderPath NVARCHAR(MAX) NULL,
        LastProcessedAt DATETIME2 NULL,
        ExecutedByUserId INT NULL,
        ExecutedByUsername NVARCHAR(MAX) NULL,
        StartedAt DATETIME2 NULL,
        CompletedAt DATETIME2 NULL,
        RecordKind NVARCHAR(20) NULL,
        FromPath NVARCHAR(MAX) NULL,
        Remarks NVARCHAR(MAX) NULL,
        SummaryMessage NVARCHAR(MAX) NULL,
        RootPath NVARCHAR(MAX) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FetchRuns_Hist_01' AND object_id = OBJECT_ID('dbo.FetchRuns_Hist'))
    CREATE NONCLUSTERED INDEX IX_FetchRuns_Hist_01 ON dbo.FetchRuns_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FetchRuns_Hist_02' AND object_id = OBJECT_ID('dbo.FetchRuns_Hist'))
    CREATE NONCLUSTERED INDEX IX_FetchRuns_Hist_02 ON dbo.FetchRuns_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FetchRuns_Hist_03' AND object_id = OBJECT_ID('dbo.FetchRuns_Hist'))
    CREATE NONCLUSTERED INDEX IX_FetchRuns_Hist_03 ON dbo.FetchRuns_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_FetchRuns_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FetchRuns_I ON dbo.FetchRuns AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FetchRuns_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, SourcePath, Status, TotalCount, ProcessedCount, LastProcessedFolderPath, LastProcessedAt, ExecutedByUserId, ExecutedByUsername, StartedAt, CompletedAt, RecordKind, FromPath, Remarks, SummaryMessage, RootPath)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.SourcePath, i.Status, i.TotalCount, i.ProcessedCount, i.LastProcessedFolderPath, i.LastProcessedAt, i.ExecutedByUserId, i.ExecutedByUsername, i.StartedAt, i.CompletedAt, i.RecordKind, i.FromPath, i.Remarks, i.SummaryMessage, i.RootPath FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_FetchRuns_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FetchRuns_U ON dbo.FetchRuns AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FetchRuns_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, SourcePath, Status, TotalCount, ProcessedCount, LastProcessedFolderPath, LastProcessedAt, ExecutedByUserId, ExecutedByUsername, StartedAt, CompletedAt, RecordKind, FromPath, Remarks, SummaryMessage, RootPath)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.SourcePath, i.Status, i.TotalCount, i.ProcessedCount, i.LastProcessedFolderPath, i.LastProcessedAt, i.ExecutedByUserId, i.ExecutedByUsername, i.StartedAt, i.CompletedAt, i.RecordKind, i.FromPath, i.Remarks, i.SummaryMessage, i.RootPath FROM inserted i;

    INSERT INTO dbo.FetchRuns_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, SourcePath, Status, TotalCount, ProcessedCount, LastProcessedFolderPath, LastProcessedAt, ExecutedByUserId, ExecutedByUsername, StartedAt, CompletedAt, RecordKind, FromPath, Remarks, SummaryMessage, RootPath)
    SELECT @UserId, @IPAddr, LEFT(CONCAT(i.RecordKind, N'':'', CONVERT(NVARCHAR(30), i.StartedAt, 126)), 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.SourcePath, d.Status, d.TotalCount, d.ProcessedCount, d.LastProcessedFolderPath, d.LastProcessedAt, d.ExecutedByUserId, d.ExecutedByUsername, d.StartedAt, d.CompletedAt, d.RecordKind, d.FromPath, d.Remarks, d.SummaryMessage, d.RootPath
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_FetchRuns_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FetchRuns_D ON dbo.FetchRuns AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FetchRuns_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, SourcePath, Status, TotalCount, ProcessedCount, LastProcessedFolderPath, LastProcessedAt, ExecutedByUserId, ExecutedByUsername, StartedAt, CompletedAt, RecordKind, FromPath, Remarks, SummaryMessage, RootPath)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.SourcePath, d.Status, d.TotalCount, d.ProcessedCount, d.LastProcessedFolderPath, d.LastProcessedAt, d.ExecutedByUserId, d.ExecutedByUsername, d.StartedAt, d.CompletedAt, d.RecordKind, d.FromPath, d.Remarks, d.SummaryMessage, d.RootPath FROM deleted d;

    INSERT INTO dbo.FetchRuns_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, SourcePath, Status, TotalCount, ProcessedCount, LastProcessedFolderPath, LastProcessedAt, ExecutedByUserId, ExecutedByUsername, StartedAt, CompletedAt, RecordKind, FromPath, Remarks, SummaryMessage, RootPath)
    SELECT @UserId, @IPAddr, LEFT(CONCAT(d.RecordKind, N'':'', CONVERT(NVARCHAR(30), d.StartedAt, 126)), 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.SourcePath, d.Status, d.TotalCount, d.ProcessedCount, d.LastProcessedFolderPath, d.LastProcessedAt, d.ExecutedByUserId, d.ExecutedByUsername, d.StartedAt, d.CompletedAt, d.RecordKind, d.FromPath, d.Remarks, d.SummaryMessage, d.RootPath
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- OcrExtractionRecords
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.OcrExtractionRecords_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OcrExtractionRecords_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OcrExtractionRecords_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionRecords_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_OcrExtractionRecords_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionRecords_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_OcrExtractionRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        RequestNumber NVARCHAR(30) NULL,
        FetchRunId INT NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        FolderPath NVARCHAR(MAX) NULL,
        EntryNumbersCsv NVARCHAR(MAX) NULL,
        TitleNumber NVARCHAR(50) NULL,
        TitleType INT NULL,
        [Plan] NVARCHAR(50) NULL,
        Block NVARCHAR(50) NULL,
        Lot NVARCHAR(50) NULL,
        TitleSequence NVARCHAR(50) NULL,
        DocumentCount INT NULL,
        ExtractionStatus INT NULL,
        ExtractionDateTime DATETIME2 NULL,
        DocumentsJson NVARCHAR(MAX) NULL,
        EntryNumberRange NVARCHAR(MAX) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_Log_01' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_Log_01 ON dbo.OcrExtractionRecords_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_Log_02' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_Log_02 ON dbo.OcrExtractionRecords_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.OcrExtractionRecords_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OcrExtractionRecords_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OcrExtractionRecords_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionRecords_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_OcrExtractionRecords_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionRecords_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_OcrExtractionRecords_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_OcrExtractionRecords_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_OcrExtractionRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        RequestNumber NVARCHAR(30) NULL,
        FetchRunId INT NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        FolderPath NVARCHAR(MAX) NULL,
        EntryNumbersCsv NVARCHAR(MAX) NULL,
        TitleNumber NVARCHAR(50) NULL,
        TitleType INT NULL,
        [Plan] NVARCHAR(50) NULL,
        Block NVARCHAR(50) NULL,
        Lot NVARCHAR(50) NULL,
        TitleSequence NVARCHAR(50) NULL,
        DocumentCount INT NULL,
        ExtractionStatus INT NULL,
        ExtractionDateTime DATETIME2 NULL,
        DocumentsJson NVARCHAR(MAX) NULL,
        EntryNumberRange NVARCHAR(MAX) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_Hist_01' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_Hist_01 ON dbo.OcrExtractionRecords_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_Hist_02' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_Hist_02 ON dbo.OcrExtractionRecords_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionRecords_Hist_03' AND object_id = OBJECT_ID('dbo.OcrExtractionRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionRecords_Hist_03 ON dbo.OcrExtractionRecords_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_OcrExtractionRecords_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_OcrExtractionRecords_I ON dbo.OcrExtractionRecords AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.OcrExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RequestNumber, FetchRunId, RdCode, RdName, FolderPath, EntryNumbersCsv, TitleNumber, TitleType, [Plan], Block, Lot, TitleSequence, DocumentCount, ExtractionStatus, ExtractionDateTime, DocumentsJson, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.RequestNumber, i.FetchRunId, i.RdCode, i.RdName, i.FolderPath, i.EntryNumbersCsv, i.TitleNumber, i.TitleType, i.[Plan], i.Block, i.Lot, i.TitleSequence, i.DocumentCount, i.ExtractionStatus, i.ExtractionDateTime, i.DocumentsJson, i.EntryNumberRange FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_OcrExtractionRecords_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_OcrExtractionRecords_U ON dbo.OcrExtractionRecords AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.OcrExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RequestNumber, FetchRunId, RdCode, RdName, FolderPath, EntryNumbersCsv, TitleNumber, TitleType, [Plan], Block, Lot, TitleSequence, DocumentCount, ExtractionStatus, ExtractionDateTime, DocumentsJson, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.RequestNumber, i.FetchRunId, i.RdCode, i.RdName, i.FolderPath, i.EntryNumbersCsv, i.TitleNumber, i.TitleType, i.[Plan], i.Block, i.Lot, i.TitleSequence, i.DocumentCount, i.ExtractionStatus, i.ExtractionDateTime, i.DocumentsJson, i.EntryNumberRange FROM inserted i;

    INSERT INTO dbo.OcrExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, RequestNumber, FetchRunId, RdCode, RdName, FolderPath, EntryNumbersCsv, TitleNumber, TitleType, [Plan], Block, Lot, TitleSequence, DocumentCount, ExtractionStatus, ExtractionDateTime, DocumentsJson, EntryNumberRange)
    SELECT @UserId, @IPAddr, LEFT(i.RequestNumber, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.RequestNumber, d.FetchRunId, d.RdCode, d.RdName, d.FolderPath, d.EntryNumbersCsv, d.TitleNumber, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.DocumentCount, d.ExtractionStatus, d.ExtractionDateTime, d.DocumentsJson, d.EntryNumberRange
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_OcrExtractionRecords_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_OcrExtractionRecords_D ON dbo.OcrExtractionRecords AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.OcrExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RequestNumber, FetchRunId, RdCode, RdName, FolderPath, EntryNumbersCsv, TitleNumber, TitleType, [Plan], Block, Lot, TitleSequence, DocumentCount, ExtractionStatus, ExtractionDateTime, DocumentsJson, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.RequestNumber, d.FetchRunId, d.RdCode, d.RdName, d.FolderPath, d.EntryNumbersCsv, d.TitleNumber, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.DocumentCount, d.ExtractionStatus, d.ExtractionDateTime, d.DocumentsJson, d.EntryNumberRange FROM deleted d;

    INSERT INTO dbo.OcrExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, RequestNumber, FetchRunId, RdCode, RdName, FolderPath, EntryNumbersCsv, TitleNumber, TitleType, [Plan], Block, Lot, TitleSequence, DocumentCount, ExtractionStatus, ExtractionDateTime, DocumentsJson, EntryNumberRange)
    SELECT @UserId, @IPAddr, LEFT(d.RequestNumber, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.RequestNumber, d.FetchRunId, d.RdCode, d.RdName, d.FolderPath, d.EntryNumbersCsv, d.TitleNumber, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.DocumentCount, d.ExtractionStatus, d.ExtractionDateTime, d.DocumentsJson, d.EntryNumberRange
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- ManualValidationRequests
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.ManualValidationRequests_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManualValidationRequests_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ManualValidationRequests_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_ManualValidationRequests_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_ManualValidationRequests_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_ManualValidationRequests_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_ManualValidationRequests_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        RequestNumber NVARCHAR(30) NULL,
        OcrExtractionRecordId INT NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        EntryNumbersCsv NVARCHAR(MAX) NULL,
        Title NVARCHAR(50) NULL,
        TitleType INT NULL,
        [Plan] NVARCHAR(50) NULL,
        Block NVARCHAR(50) NULL,
        Lot NVARCHAR(50) NULL,
        TitleSequence NVARCHAR(50) NULL,
        MissingFieldsCsv NVARCHAR(MAX) NULL,
        DocumentsJson NVARCHAR(MAX) NULL,
        ExtractionDate DATETIME2 NULL,
        UpdatedByUserId INT NULL,
        UpdatedByUsername NVARCHAR(MAX) NULL,
        UpdatedAt DATETIME2 NULL,
        LockedByUserId INT NULL,
        LockedByUsername NVARCHAR(MAX) NULL,
        LockedAt DATETIME2 NULL,
        MigratedAt DATETIME2 NULL,
        Status NVARCHAR(30) NULL,
        EntryNumberRange NVARCHAR(MAX) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_Log_01' AND object_id = OBJECT_ID('dbo.ManualValidationRequests_Log'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_Log_01 ON dbo.ManualValidationRequests_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_Log_02' AND object_id = OBJECT_ID('dbo.ManualValidationRequests_Log'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_Log_02 ON dbo.ManualValidationRequests_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.ManualValidationRequests_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManualValidationRequests_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ManualValidationRequests_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_ManualValidationRequests_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_ManualValidationRequests_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_ManualValidationRequests_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_ManualValidationRequests_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_ManualValidationRequests_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_ManualValidationRequests_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        RequestNumber NVARCHAR(30) NULL,
        OcrExtractionRecordId INT NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        EntryNumbersCsv NVARCHAR(MAX) NULL,
        Title NVARCHAR(50) NULL,
        TitleType INT NULL,
        [Plan] NVARCHAR(50) NULL,
        Block NVARCHAR(50) NULL,
        Lot NVARCHAR(50) NULL,
        TitleSequence NVARCHAR(50) NULL,
        MissingFieldsCsv NVARCHAR(MAX) NULL,
        DocumentsJson NVARCHAR(MAX) NULL,
        ExtractionDate DATETIME2 NULL,
        UpdatedByUserId INT NULL,
        UpdatedByUsername NVARCHAR(MAX) NULL,
        UpdatedAt DATETIME2 NULL,
        LockedByUserId INT NULL,
        LockedByUsername NVARCHAR(MAX) NULL,
        LockedAt DATETIME2 NULL,
        MigratedAt DATETIME2 NULL,
        Status NVARCHAR(30) NULL,
        EntryNumberRange NVARCHAR(MAX) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_Hist_01' AND object_id = OBJECT_ID('dbo.ManualValidationRequests_Hist'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_Hist_01 ON dbo.ManualValidationRequests_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_Hist_02' AND object_id = OBJECT_ID('dbo.ManualValidationRequests_Hist'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_Hist_02 ON dbo.ManualValidationRequests_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ManualValidationRequests_Hist_03' AND object_id = OBJECT_ID('dbo.ManualValidationRequests_Hist'))
    CREATE NONCLUSTERED INDEX IX_ManualValidationRequests_Hist_03 ON dbo.ManualValidationRequests_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_ManualValidationRequests_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_ManualValidationRequests_I ON dbo.ManualValidationRequests AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.ManualValidationRequests_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RequestNumber, OcrExtractionRecordId, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MissingFieldsCsv, DocumentsJson, ExtractionDate, UpdatedByUserId, UpdatedByUsername, UpdatedAt, LockedByUserId, LockedByUsername, LockedAt, MigratedAt, Status, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.RequestNumber, i.OcrExtractionRecordId, i.RdCode, i.RdName, i.EntryNumbersCsv, i.Title, i.TitleType, i.[Plan], i.Block, i.Lot, i.TitleSequence, i.MissingFieldsCsv, i.DocumentsJson, i.ExtractionDate, i.UpdatedByUserId, i.UpdatedByUsername, i.UpdatedAt, i.LockedByUserId, i.LockedByUsername, i.LockedAt, i.MigratedAt, i.Status, i.EntryNumberRange FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_ManualValidationRequests_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_ManualValidationRequests_U ON dbo.ManualValidationRequests AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.ManualValidationRequests_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RequestNumber, OcrExtractionRecordId, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MissingFieldsCsv, DocumentsJson, ExtractionDate, UpdatedByUserId, UpdatedByUsername, UpdatedAt, LockedByUserId, LockedByUsername, LockedAt, MigratedAt, Status, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.RequestNumber, i.OcrExtractionRecordId, i.RdCode, i.RdName, i.EntryNumbersCsv, i.Title, i.TitleType, i.[Plan], i.Block, i.Lot, i.TitleSequence, i.MissingFieldsCsv, i.DocumentsJson, i.ExtractionDate, i.UpdatedByUserId, i.UpdatedByUsername, i.UpdatedAt, i.LockedByUserId, i.LockedByUsername, i.LockedAt, i.MigratedAt, i.Status, i.EntryNumberRange FROM inserted i;

    INSERT INTO dbo.ManualValidationRequests_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, RequestNumber, OcrExtractionRecordId, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MissingFieldsCsv, DocumentsJson, ExtractionDate, UpdatedByUserId, UpdatedByUsername, UpdatedAt, LockedByUserId, LockedByUsername, LockedAt, MigratedAt, Status, EntryNumberRange)
    SELECT @UserId, @IPAddr, LEFT(i.RequestNumber, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.RequestNumber, d.OcrExtractionRecordId, d.RdCode, d.RdName, d.EntryNumbersCsv, d.Title, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.MissingFieldsCsv, d.DocumentsJson, d.ExtractionDate, d.UpdatedByUserId, d.UpdatedByUsername, d.UpdatedAt, d.LockedByUserId, d.LockedByUsername, d.LockedAt, d.MigratedAt, d.Status, d.EntryNumberRange
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_ManualValidationRequests_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_ManualValidationRequests_D ON dbo.ManualValidationRequests AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.ManualValidationRequests_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RequestNumber, OcrExtractionRecordId, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MissingFieldsCsv, DocumentsJson, ExtractionDate, UpdatedByUserId, UpdatedByUsername, UpdatedAt, LockedByUserId, LockedByUsername, LockedAt, MigratedAt, Status, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.RequestNumber, d.OcrExtractionRecordId, d.RdCode, d.RdName, d.EntryNumbersCsv, d.Title, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.MissingFieldsCsv, d.DocumentsJson, d.ExtractionDate, d.UpdatedByUserId, d.UpdatedByUsername, d.UpdatedAt, d.LockedByUserId, d.LockedByUsername, d.LockedAt, d.MigratedAt, d.Status, d.EntryNumberRange FROM deleted d;

    INSERT INTO dbo.ManualValidationRequests_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, RequestNumber, OcrExtractionRecordId, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MissingFieldsCsv, DocumentsJson, ExtractionDate, UpdatedByUserId, UpdatedByUsername, UpdatedAt, LockedByUserId, LockedByUsername, LockedAt, MigratedAt, Status, EntryNumberRange)
    SELECT @UserId, @IPAddr, LEFT(d.RequestNumber, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.RequestNumber, d.OcrExtractionRecordId, d.RdCode, d.RdName, d.EntryNumbersCsv, d.Title, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.MissingFieldsCsv, d.DocumentsJson, d.ExtractionDate, d.UpdatedByUserId, d.UpdatedByUsername, d.UpdatedAt, d.LockedByUserId, d.LockedByUsername, d.LockedAt, d.MigratedAt, d.Status, d.EntryNumberRange
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- MigrationRecords
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.MigrationRecords_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationRecords_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MigrationRecords_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_MigrationRecords_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_MigrationRecords_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_MigrationRecords_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_MigrationRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        RequestNumber NVARCHAR(30) NULL,
        OcrExtractionRecordId INT NULL,
        MigrationDate DATETIME2 NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        EntryNumbersCsv NVARCHAR(MAX) NULL,
        Title NVARCHAR(50) NULL,
        TitleType INT NULL,
        [Plan] NVARCHAR(50) NULL,
        Block NVARCHAR(50) NULL,
        Lot NVARCHAR(50) NULL,
        TitleSequence NVARCHAR(50) NULL,
        MigratedToRdName NVARCHAR(150) NULL,
        MigrationStatus NVARCHAR(50) NULL,
        SdStatus NVARCHAR(50) NULL,
        MigrationType NVARCHAR(30) NULL,
        ProcessedByUserId INT NULL,
        ProcessedByUsername NVARCHAR(256) NULL,
        ProcessingStatus NVARCHAR(20) NULL,
        AttemptCount INT NULL,
        ProcessingStartedAt DATETIME2 NULL,
        ProcessingCompletedAt DATETIME2 NULL,
        LastAttemptAt DATETIME2 NULL,
        LastFailureReason NVARCHAR(500) NULL,
        ApiResponseJson NVARCHAR(MAX) NULL,
        ApiResponseAt DATETIME2 NULL,
        TargetSystemValidation NVARCHAR(100) NULL,
        TargetSystemActivationStatus NVARCHAR(20) NULL,
        Document_Json NVARCHAR(MAX) NULL,
        RangeEntryNumber NVARCHAR(MAX) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_Log_01' AND object_id = OBJECT_ID('dbo.MigrationRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_Log_01 ON dbo.MigrationRecords_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_Log_02' AND object_id = OBJECT_ID('dbo.MigrationRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_Log_02 ON dbo.MigrationRecords_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.MigrationRecords_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationRecords_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MigrationRecords_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_MigrationRecords_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_MigrationRecords_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_MigrationRecords_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_MigrationRecords_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_MigrationRecords_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_MigrationRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        RequestNumber NVARCHAR(30) NULL,
        OcrExtractionRecordId INT NULL,
        MigrationDate DATETIME2 NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        EntryNumbersCsv NVARCHAR(MAX) NULL,
        Title NVARCHAR(50) NULL,
        TitleType INT NULL,
        [Plan] NVARCHAR(50) NULL,
        Block NVARCHAR(50) NULL,
        Lot NVARCHAR(50) NULL,
        TitleSequence NVARCHAR(50) NULL,
        MigratedToRdName NVARCHAR(150) NULL,
        MigrationStatus NVARCHAR(50) NULL,
        SdStatus NVARCHAR(50) NULL,
        MigrationType NVARCHAR(30) NULL,
        ProcessedByUserId INT NULL,
        ProcessedByUsername NVARCHAR(256) NULL,
        ProcessingStatus NVARCHAR(20) NULL,
        AttemptCount INT NULL,
        ProcessingStartedAt DATETIME2 NULL,
        ProcessingCompletedAt DATETIME2 NULL,
        LastAttemptAt DATETIME2 NULL,
        LastFailureReason NVARCHAR(500) NULL,
        ApiResponseJson NVARCHAR(MAX) NULL,
        ApiResponseAt DATETIME2 NULL,
        TargetSystemValidation NVARCHAR(100) NULL,
        TargetSystemActivationStatus NVARCHAR(20) NULL,
        Document_Json NVARCHAR(MAX) NULL,
        RangeEntryNumber NVARCHAR(MAX) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_Hist_01' AND object_id = OBJECT_ID('dbo.MigrationRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_Hist_01 ON dbo.MigrationRecords_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_Hist_02' AND object_id = OBJECT_ID('dbo.MigrationRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_Hist_02 ON dbo.MigrationRecords_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationRecords_Hist_03' AND object_id = OBJECT_ID('dbo.MigrationRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_MigrationRecords_Hist_03 ON dbo.MigrationRecords_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_MigrationRecords_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationRecords_I ON dbo.MigrationRecords AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RequestNumber, OcrExtractionRecordId, MigrationDate, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MigratedToRdName, MigrationStatus, SdStatus, MigrationType, ProcessedByUserId, ProcessedByUsername, ProcessingStatus, AttemptCount, ProcessingStartedAt, ProcessingCompletedAt, LastAttemptAt, LastFailureReason, ApiResponseJson, ApiResponseAt, TargetSystemValidation, TargetSystemActivationStatus, Document_Json, RangeEntryNumber)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.RequestNumber, i.OcrExtractionRecordId, i.MigrationDate, i.RdCode, i.RdName, i.EntryNumbersCsv, i.Title, i.TitleType, i.[Plan], i.Block, i.Lot, i.TitleSequence, i.MigratedToRdName, i.MigrationStatus, i.SdStatus, i.MigrationType, i.ProcessedByUserId, i.ProcessedByUsername, i.ProcessingStatus, i.AttemptCount, i.ProcessingStartedAt, i.ProcessingCompletedAt, i.LastAttemptAt, i.LastFailureReason, i.ApiResponseJson, i.ApiResponseAt, i.TargetSystemValidation, i.TargetSystemActivationStatus, i.Document_Json, i.RangeEntryNumber FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_MigrationRecords_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationRecords_U ON dbo.MigrationRecords AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RequestNumber, OcrExtractionRecordId, MigrationDate, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MigratedToRdName, MigrationStatus, SdStatus, MigrationType, ProcessedByUserId, ProcessedByUsername, ProcessingStatus, AttemptCount, ProcessingStartedAt, ProcessingCompletedAt, LastAttemptAt, LastFailureReason, ApiResponseJson, ApiResponseAt, TargetSystemValidation, TargetSystemActivationStatus, Document_Json, RangeEntryNumber)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.RequestNumber, i.OcrExtractionRecordId, i.MigrationDate, i.RdCode, i.RdName, i.EntryNumbersCsv, i.Title, i.TitleType, i.[Plan], i.Block, i.Lot, i.TitleSequence, i.MigratedToRdName, i.MigrationStatus, i.SdStatus, i.MigrationType, i.ProcessedByUserId, i.ProcessedByUsername, i.ProcessingStatus, i.AttemptCount, i.ProcessingStartedAt, i.ProcessingCompletedAt, i.LastAttemptAt, i.LastFailureReason, i.ApiResponseJson, i.ApiResponseAt, i.TargetSystemValidation, i.TargetSystemActivationStatus, i.Document_Json, i.RangeEntryNumber FROM inserted i;

    INSERT INTO dbo.MigrationRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, RequestNumber, OcrExtractionRecordId, MigrationDate, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MigratedToRdName, MigrationStatus, SdStatus, MigrationType, ProcessedByUserId, ProcessedByUsername, ProcessingStatus, AttemptCount, ProcessingStartedAt, ProcessingCompletedAt, LastAttemptAt, LastFailureReason, ApiResponseJson, ApiResponseAt, TargetSystemValidation, TargetSystemActivationStatus, Document_Json, RangeEntryNumber)
    SELECT @UserId, @IPAddr, LEFT(i.RequestNumber, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.RequestNumber, d.OcrExtractionRecordId, d.MigrationDate, d.RdCode, d.RdName, d.EntryNumbersCsv, d.Title, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.MigratedToRdName, d.MigrationStatus, d.SdStatus, d.MigrationType, d.ProcessedByUserId, d.ProcessedByUsername, d.ProcessingStatus, d.AttemptCount, d.ProcessingStartedAt, d.ProcessingCompletedAt, d.LastAttemptAt, d.LastFailureReason, d.ApiResponseJson, d.ApiResponseAt, d.TargetSystemValidation, d.TargetSystemActivationStatus, d.Document_Json, d.RangeEntryNumber
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_MigrationRecords_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationRecords_D ON dbo.MigrationRecords AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RequestNumber, OcrExtractionRecordId, MigrationDate, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MigratedToRdName, MigrationStatus, SdStatus, MigrationType, ProcessedByUserId, ProcessedByUsername, ProcessingStatus, AttemptCount, ProcessingStartedAt, ProcessingCompletedAt, LastAttemptAt, LastFailureReason, ApiResponseJson, ApiResponseAt, TargetSystemValidation, TargetSystemActivationStatus, Document_Json, RangeEntryNumber)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.RequestNumber, d.OcrExtractionRecordId, d.MigrationDate, d.RdCode, d.RdName, d.EntryNumbersCsv, d.Title, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.MigratedToRdName, d.MigrationStatus, d.SdStatus, d.MigrationType, d.ProcessedByUserId, d.ProcessedByUsername, d.ProcessingStatus, d.AttemptCount, d.ProcessingStartedAt, d.ProcessingCompletedAt, d.LastAttemptAt, d.LastFailureReason, d.ApiResponseJson, d.ApiResponseAt, d.TargetSystemValidation, d.TargetSystemActivationStatus, d.Document_Json, d.RangeEntryNumber FROM deleted d;

    INSERT INTO dbo.MigrationRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, RequestNumber, OcrExtractionRecordId, MigrationDate, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MigratedToRdName, MigrationStatus, SdStatus, MigrationType, ProcessedByUserId, ProcessedByUsername, ProcessingStatus, AttemptCount, ProcessingStartedAt, ProcessingCompletedAt, LastAttemptAt, LastFailureReason, ApiResponseJson, ApiResponseAt, TargetSystemValidation, TargetSystemActivationStatus, Document_Json, RangeEntryNumber)
    SELECT @UserId, @IPAddr, LEFT(d.RequestNumber, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.RequestNumber, d.OcrExtractionRecordId, d.MigrationDate, d.RdCode, d.RdName, d.EntryNumbersCsv, d.Title, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.MigratedToRdName, d.MigrationStatus, d.SdStatus, d.MigrationType, d.ProcessedByUserId, d.ProcessedByUsername, d.ProcessingStatus, d.AttemptCount, d.ProcessingStartedAt, d.ProcessingCompletedAt, d.LastAttemptAt, d.LastFailureReason, d.ApiResponseJson, d.ApiResponseAt, d.TargetSystemValidation, d.TargetSystemActivationStatus, d.Document_Json, d.RangeEntryNumber
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- MigrationDocuments
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.MigrationDocuments_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationDocuments_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MigrationDocuments_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_MigrationDocuments_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_MigrationDocuments_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_MigrationDocuments_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_MigrationDocuments_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        MigrationRecordId INT NULL,
        CodeLookupId INT NULL,
        DocumentName NVARCHAR(MAX) NULL,
        FileName NVARCHAR(MAX) NULL,
        Status INT NULL,
        ExistingFileName NVARCHAR(MAX) NULL,
        PerformedByUserId INT NULL,
        PerformedByUsername NVARCHAR(MAX) NULL,
        ActionDate DATETIME2 NULL,
        DestinationPath NVARCHAR(500) NULL,
        SizeBytes BIGINT NULL,
        Sequence INT NULL,
        SourceImagePath NVARCHAR(500) NULL,
        ReasonForFailing NVARCHAR(500) NULL,
        TargetSystemValidation NVARCHAR(100) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationDocuments_Log_01' AND object_id = OBJECT_ID('dbo.MigrationDocuments_Log'))
    CREATE NONCLUSTERED INDEX IX_MigrationDocuments_Log_01 ON dbo.MigrationDocuments_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationDocuments_Log_02' AND object_id = OBJECT_ID('dbo.MigrationDocuments_Log'))
    CREATE NONCLUSTERED INDEX IX_MigrationDocuments_Log_02 ON dbo.MigrationDocuments_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.MigrationDocuments_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationDocuments_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MigrationDocuments_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_MigrationDocuments_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_MigrationDocuments_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_MigrationDocuments_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_MigrationDocuments_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_MigrationDocuments_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_MigrationDocuments_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        MigrationRecordId INT NULL,
        CodeLookupId INT NULL,
        DocumentName NVARCHAR(MAX) NULL,
        FileName NVARCHAR(MAX) NULL,
        Status INT NULL,
        ExistingFileName NVARCHAR(MAX) NULL,
        PerformedByUserId INT NULL,
        PerformedByUsername NVARCHAR(MAX) NULL,
        ActionDate DATETIME2 NULL,
        DestinationPath NVARCHAR(500) NULL,
        SizeBytes BIGINT NULL,
        Sequence INT NULL,
        SourceImagePath NVARCHAR(500) NULL,
        ReasonForFailing NVARCHAR(500) NULL,
        TargetSystemValidation NVARCHAR(100) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationDocuments_Hist_01' AND object_id = OBJECT_ID('dbo.MigrationDocuments_Hist'))
    CREATE NONCLUSTERED INDEX IX_MigrationDocuments_Hist_01 ON dbo.MigrationDocuments_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationDocuments_Hist_02' AND object_id = OBJECT_ID('dbo.MigrationDocuments_Hist'))
    CREATE NONCLUSTERED INDEX IX_MigrationDocuments_Hist_02 ON dbo.MigrationDocuments_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MigrationDocuments_Hist_03' AND object_id = OBJECT_ID('dbo.MigrationDocuments_Hist'))
    CREATE NONCLUSTERED INDEX IX_MigrationDocuments_Hist_03 ON dbo.MigrationDocuments_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_MigrationDocuments_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationDocuments_I ON dbo.MigrationDocuments AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationDocuments_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, MigrationRecordId, CodeLookupId, DocumentName, FileName, Status, ExistingFileName, PerformedByUserId, PerformedByUsername, ActionDate, DestinationPath, SizeBytes, Sequence, SourceImagePath, ReasonForFailing, TargetSystemValidation)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.MigrationRecordId, i.CodeLookupId, i.DocumentName, i.FileName, i.Status, i.ExistingFileName, i.PerformedByUserId, i.PerformedByUsername, i.ActionDate, i.DestinationPath, i.SizeBytes, i.Sequence, i.SourceImagePath, i.ReasonForFailing, i.TargetSystemValidation FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_MigrationDocuments_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationDocuments_U ON dbo.MigrationDocuments AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationDocuments_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, MigrationRecordId, CodeLookupId, DocumentName, FileName, Status, ExistingFileName, PerformedByUserId, PerformedByUsername, ActionDate, DestinationPath, SizeBytes, Sequence, SourceImagePath, ReasonForFailing, TargetSystemValidation)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.MigrationRecordId, i.CodeLookupId, i.DocumentName, i.FileName, i.Status, i.ExistingFileName, i.PerformedByUserId, i.PerformedByUsername, i.ActionDate, i.DestinationPath, i.SizeBytes, i.Sequence, i.SourceImagePath, i.ReasonForFailing, i.TargetSystemValidation FROM inserted i;

    INSERT INTO dbo.MigrationDocuments_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, MigrationRecordId, CodeLookupId, DocumentName, FileName, Status, ExistingFileName, PerformedByUserId, PerformedByUsername, ActionDate, DestinationPath, SizeBytes, Sequence, SourceImagePath, ReasonForFailing, TargetSystemValidation)
    SELECT @UserId, @IPAddr, LEFT(i.FileName, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.MigrationRecordId, d.CodeLookupId, d.DocumentName, d.FileName, d.Status, d.ExistingFileName, d.PerformedByUserId, d.PerformedByUsername, d.ActionDate, d.DestinationPath, d.SizeBytes, d.Sequence, d.SourceImagePath, d.ReasonForFailing, d.TargetSystemValidation
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_MigrationDocuments_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_MigrationDocuments_D ON dbo.MigrationDocuments AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.MigrationDocuments_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, MigrationRecordId, CodeLookupId, DocumentName, FileName, Status, ExistingFileName, PerformedByUserId, PerformedByUsername, ActionDate, DestinationPath, SizeBytes, Sequence, SourceImagePath, ReasonForFailing, TargetSystemValidation)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.MigrationRecordId, d.CodeLookupId, d.DocumentName, d.FileName, d.Status, d.ExistingFileName, d.PerformedByUserId, d.PerformedByUsername, d.ActionDate, d.DestinationPath, d.SizeBytes, d.Sequence, d.SourceImagePath, d.ReasonForFailing, d.TargetSystemValidation FROM deleted d;

    INSERT INTO dbo.MigrationDocuments_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, MigrationRecordId, CodeLookupId, DocumentName, FileName, Status, ExistingFileName, PerformedByUserId, PerformedByUsername, ActionDate, DestinationPath, SizeBytes, Sequence, SourceImagePath, ReasonForFailing, TargetSystemValidation)
    SELECT @UserId, @IPAddr, LEFT(d.FileName, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.MigrationRecordId, d.CodeLookupId, d.DocumentName, d.FileName, d.Status, d.ExistingFileName, d.PerformedByUserId, d.PerformedByUsername, d.ActionDate, d.DestinationPath, d.SizeBytes, d.Sequence, d.SourceImagePath, d.ReasonForFailing, d.TargetSystemValidation
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- EmptyFolderRecords
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.EmptyFolderRecords_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmptyFolderRecords_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmptyFolderRecords_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_EmptyFolderRecords_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_EmptyFolderRecords_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_EmptyFolderRecords_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_EmptyFolderRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        FolderName NVARCHAR(MAX) NULL,
        FolderPath NVARCHAR(MAX) NULL,
        Status NVARCHAR(50) NULL,
        FetchDateTime DATETIME2 NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmptyFolderRecords_Log_01' AND object_id = OBJECT_ID('dbo.EmptyFolderRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_EmptyFolderRecords_Log_01 ON dbo.EmptyFolderRecords_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmptyFolderRecords_Log_02' AND object_id = OBJECT_ID('dbo.EmptyFolderRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_EmptyFolderRecords_Log_02 ON dbo.EmptyFolderRecords_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.EmptyFolderRecords_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmptyFolderRecords_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmptyFolderRecords_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_EmptyFolderRecords_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_EmptyFolderRecords_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_EmptyFolderRecords_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_EmptyFolderRecords_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_EmptyFolderRecords_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_EmptyFolderRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        FolderName NVARCHAR(MAX) NULL,
        FolderPath NVARCHAR(MAX) NULL,
        Status NVARCHAR(50) NULL,
        FetchDateTime DATETIME2 NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmptyFolderRecords_Hist_01' AND object_id = OBJECT_ID('dbo.EmptyFolderRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_EmptyFolderRecords_Hist_01 ON dbo.EmptyFolderRecords_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmptyFolderRecords_Hist_02' AND object_id = OBJECT_ID('dbo.EmptyFolderRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_EmptyFolderRecords_Hist_02 ON dbo.EmptyFolderRecords_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmptyFolderRecords_Hist_03' AND object_id = OBJECT_ID('dbo.EmptyFolderRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_EmptyFolderRecords_Hist_03 ON dbo.EmptyFolderRecords_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_EmptyFolderRecords_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_EmptyFolderRecords_I ON dbo.EmptyFolderRecords AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.EmptyFolderRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RdCode, RdName, FolderName, FolderPath, Status, FetchDateTime)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.RdCode, i.RdName, i.FolderName, i.FolderPath, i.Status, i.FetchDateTime FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_EmptyFolderRecords_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_EmptyFolderRecords_U ON dbo.EmptyFolderRecords AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.EmptyFolderRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RdCode, RdName, FolderName, FolderPath, Status, FetchDateTime)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.RdCode, i.RdName, i.FolderName, i.FolderPath, i.Status, i.FetchDateTime FROM inserted i;

    INSERT INTO dbo.EmptyFolderRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, RdCode, RdName, FolderName, FolderPath, Status, FetchDateTime)
    SELECT @UserId, @IPAddr, LEFT(i.FolderPath, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.RdCode, d.RdName, d.FolderName, d.FolderPath, d.Status, d.FetchDateTime
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_EmptyFolderRecords_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_EmptyFolderRecords_D ON dbo.EmptyFolderRecords AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.EmptyFolderRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RdCode, RdName, FolderName, FolderPath, Status, FetchDateTime)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.RdCode, d.RdName, d.FolderName, d.FolderPath, d.Status, d.FetchDateTime FROM deleted d;

    INSERT INTO dbo.EmptyFolderRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, RdCode, RdName, FolderName, FolderPath, Status, FetchDateTime)
    SELECT @UserId, @IPAddr, LEFT(d.FolderPath, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.RdCode, d.RdName, d.FolderName, d.FolderPath, d.Status, d.FetchDateTime
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- FailedExtractionRecords
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.FailedExtractionRecords_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FailedExtractionRecords_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FailedExtractionRecords_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_FailedExtractionRecords_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_FailedExtractionRecords_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_FailedExtractionRecords_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_FailedExtractionRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        FolderName NVARCHAR(MAX) NULL,
        FolderPath NVARCHAR(MAX) NULL,
        FailureReason NVARCHAR(MAX) NULL,
        ExtractionDateTime DATETIME2 NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FailedExtractionRecords_Log_01' AND object_id = OBJECT_ID('dbo.FailedExtractionRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_FailedExtractionRecords_Log_01 ON dbo.FailedExtractionRecords_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FailedExtractionRecords_Log_02' AND object_id = OBJECT_ID('dbo.FailedExtractionRecords_Log'))
    CREATE NONCLUSTERED INDEX IX_FailedExtractionRecords_Log_02 ON dbo.FailedExtractionRecords_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.FailedExtractionRecords_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FailedExtractionRecords_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FailedExtractionRecords_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_FailedExtractionRecords_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_FailedExtractionRecords_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_FailedExtractionRecords_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_FailedExtractionRecords_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_FailedExtractionRecords_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_FailedExtractionRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        FolderName NVARCHAR(MAX) NULL,
        FolderPath NVARCHAR(MAX) NULL,
        FailureReason NVARCHAR(MAX) NULL,
        ExtractionDateTime DATETIME2 NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FailedExtractionRecords_Hist_01' AND object_id = OBJECT_ID('dbo.FailedExtractionRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_FailedExtractionRecords_Hist_01 ON dbo.FailedExtractionRecords_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FailedExtractionRecords_Hist_02' AND object_id = OBJECT_ID('dbo.FailedExtractionRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_FailedExtractionRecords_Hist_02 ON dbo.FailedExtractionRecords_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FailedExtractionRecords_Hist_03' AND object_id = OBJECT_ID('dbo.FailedExtractionRecords_Hist'))
    CREATE NONCLUSTERED INDEX IX_FailedExtractionRecords_Hist_03 ON dbo.FailedExtractionRecords_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_FailedExtractionRecords_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FailedExtractionRecords_I ON dbo.FailedExtractionRecords AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FailedExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RdCode, RdName, FolderName, FolderPath, FailureReason, ExtractionDateTime)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.RdCode, i.RdName, i.FolderName, i.FolderPath, i.FailureReason, i.ExtractionDateTime FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_FailedExtractionRecords_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FailedExtractionRecords_U ON dbo.FailedExtractionRecords AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FailedExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RdCode, RdName, FolderName, FolderPath, FailureReason, ExtractionDateTime)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.RdCode, i.RdName, i.FolderName, i.FolderPath, i.FailureReason, i.ExtractionDateTime FROM inserted i;

    INSERT INTO dbo.FailedExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, RdCode, RdName, FolderName, FolderPath, FailureReason, ExtractionDateTime)
    SELECT @UserId, @IPAddr, LEFT(i.FolderPath, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.RdCode, d.RdName, d.FolderName, d.FolderPath, d.FailureReason, d.ExtractionDateTime
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_FailedExtractionRecords_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_FailedExtractionRecords_D ON dbo.FailedExtractionRecords AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FailedExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, RdCode, RdName, FolderName, FolderPath, FailureReason, ExtractionDateTime)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.RdCode, d.RdName, d.FolderName, d.FolderPath, d.FailureReason, d.ExtractionDateTime FROM deleted d;

    INSERT INTO dbo.FailedExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, RdCode, RdName, FolderName, FolderPath, FailureReason, ExtractionDateTime)
    SELECT @UserId, @IPAddr, LEFT(d.FolderPath, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.RdCode, d.RdName, d.FolderName, d.FolderPath, d.FailureReason, d.ExtractionDateTime
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- BrandingSettings
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.BrandingSettings_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BrandingSettings_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BrandingSettings_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_BrandingSettings_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_BrandingSettings_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_BrandingSettings_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_BrandingSettings_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        ImagePath NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BrandingSettings_Log_01' AND object_id = OBJECT_ID('dbo.BrandingSettings_Log'))
    CREATE NONCLUSTERED INDEX IX_BrandingSettings_Log_01 ON dbo.BrandingSettings_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BrandingSettings_Log_02' AND object_id = OBJECT_ID('dbo.BrandingSettings_Log'))
    CREATE NONCLUSTERED INDEX IX_BrandingSettings_Log_02 ON dbo.BrandingSettings_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.BrandingSettings_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BrandingSettings_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BrandingSettings_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_BrandingSettings_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_BrandingSettings_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_BrandingSettings_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_BrandingSettings_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_BrandingSettings_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_BrandingSettings_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        ImagePath NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BrandingSettings_Hist_01' AND object_id = OBJECT_ID('dbo.BrandingSettings_Hist'))
    CREATE NONCLUSTERED INDEX IX_BrandingSettings_Hist_01 ON dbo.BrandingSettings_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BrandingSettings_Hist_02' AND object_id = OBJECT_ID('dbo.BrandingSettings_Hist'))
    CREATE NONCLUSTERED INDEX IX_BrandingSettings_Hist_02 ON dbo.BrandingSettings_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BrandingSettings_Hist_03' AND object_id = OBJECT_ID('dbo.BrandingSettings_Hist'))
    CREATE NONCLUSTERED INDEX IX_BrandingSettings_Hist_03 ON dbo.BrandingSettings_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_BrandingSettings_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_BrandingSettings_I ON dbo.BrandingSettings AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.BrandingSettings_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, ImagePath, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.ImagePath, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_BrandingSettings_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_BrandingSettings_U ON dbo.BrandingSettings AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.BrandingSettings_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, ImagePath, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.ImagePath, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;

    INSERT INTO dbo.BrandingSettings_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, ImagePath, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, LEFT(i.ImagePath, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.ImagePath, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_BrandingSettings_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_BrandingSettings_D ON dbo.BrandingSettings AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.BrandingSettings_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, ImagePath, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.ImagePath, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy FROM deleted d;

    INSERT INTO dbo.BrandingSettings_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, ImagePath, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, LEFT(d.ImagePath, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.ImagePath, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- EmailTemplates
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.EmailTemplates_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailTemplates_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmailTemplates_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_EmailTemplates_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_EmailTemplates_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_EmailTemplates_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_EmailTemplates_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        [Key] NVARCHAR(64) NULL,
        Label NVARCHAR(128) NULL,
        Recipients NVARCHAR(MAX) NULL,
        Subject NVARCHAR(MAX) NULL,
        Body NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailTemplates_Log_01' AND object_id = OBJECT_ID('dbo.EmailTemplates_Log'))
    CREATE NONCLUSTERED INDEX IX_EmailTemplates_Log_01 ON dbo.EmailTemplates_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailTemplates_Log_02' AND object_id = OBJECT_ID('dbo.EmailTemplates_Log'))
    CREATE NONCLUSTERED INDEX IX_EmailTemplates_Log_02 ON dbo.EmailTemplates_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.EmailTemplates_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailTemplates_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmailTemplates_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_EmailTemplates_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_EmailTemplates_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_EmailTemplates_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_EmailTemplates_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_EmailTemplates_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_EmailTemplates_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        [Key] NVARCHAR(64) NULL,
        Label NVARCHAR(128) NULL,
        Recipients NVARCHAR(MAX) NULL,
        Subject NVARCHAR(MAX) NULL,
        Body NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailTemplates_Hist_01' AND object_id = OBJECT_ID('dbo.EmailTemplates_Hist'))
    CREATE NONCLUSTERED INDEX IX_EmailTemplates_Hist_01 ON dbo.EmailTemplates_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailTemplates_Hist_02' AND object_id = OBJECT_ID('dbo.EmailTemplates_Hist'))
    CREATE NONCLUSTERED INDEX IX_EmailTemplates_Hist_02 ON dbo.EmailTemplates_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailTemplates_Hist_03' AND object_id = OBJECT_ID('dbo.EmailTemplates_Hist'))
    CREATE NONCLUSTERED INDEX IX_EmailTemplates_Hist_03 ON dbo.EmailTemplates_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_EmailTemplates_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_EmailTemplates_I ON dbo.EmailTemplates AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.EmailTemplates_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, [Key], Label, Recipients, Subject, Body, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.[Key], i.Label, i.Recipients, i.Subject, i.Body, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_EmailTemplates_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_EmailTemplates_U ON dbo.EmailTemplates AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.EmailTemplates_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, [Key], Label, Recipients, Subject, Body, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.[Key], i.Label, i.Recipients, i.Subject, i.Body, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;

    INSERT INTO dbo.EmailTemplates_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, [Key], Label, Recipients, Subject, Body, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, LEFT(i.[Key], 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.[Key], d.Label, d.Recipients, d.Subject, d.Body, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_EmailTemplates_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_EmailTemplates_D ON dbo.EmailTemplates AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.EmailTemplates_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, [Key], Label, Recipients, Subject, Body, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.[Key], d.Label, d.Recipients, d.Subject, d.Body, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy FROM deleted d;

    INSERT INTO dbo.EmailTemplates_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, [Key], Label, Recipients, Subject, Body, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, LEFT(d.[Key], 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.[Key], d.Label, d.Recipients, d.Subject, d.Body, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- DocumentTypes
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.DocumentTypes_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentTypes_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentTypes_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_DocumentTypes_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_DocumentTypes_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_DocumentTypes_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_DocumentTypes_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        DocumentCode NVARCHAR(20) NULL,
        DocumentName NVARCHAR(MAX) NULL,
        IsActive BIT NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentTypes_Log_01' AND object_id = OBJECT_ID('dbo.DocumentTypes_Log'))
    CREATE NONCLUSTERED INDEX IX_DocumentTypes_Log_01 ON dbo.DocumentTypes_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentTypes_Log_02' AND object_id = OBJECT_ID('dbo.DocumentTypes_Log'))
    CREATE NONCLUSTERED INDEX IX_DocumentTypes_Log_02 ON dbo.DocumentTypes_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.DocumentTypes_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentTypes_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentTypes_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_DocumentTypes_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_DocumentTypes_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_DocumentTypes_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_DocumentTypes_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_DocumentTypes_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_DocumentTypes_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        DocumentCode NVARCHAR(20) NULL,
        DocumentName NVARCHAR(MAX) NULL,
        IsActive BIT NULL,
        CreatedAt DATETIME2 NULL,
        CreatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy INT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentTypes_Hist_01' AND object_id = OBJECT_ID('dbo.DocumentTypes_Hist'))
    CREATE NONCLUSTERED INDEX IX_DocumentTypes_Hist_01 ON dbo.DocumentTypes_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentTypes_Hist_02' AND object_id = OBJECT_ID('dbo.DocumentTypes_Hist'))
    CREATE NONCLUSTERED INDEX IX_DocumentTypes_Hist_02 ON dbo.DocumentTypes_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentTypes_Hist_03' AND object_id = OBJECT_ID('dbo.DocumentTypes_Hist'))
    CREATE NONCLUSTERED INDEX IX_DocumentTypes_Hist_03 ON dbo.DocumentTypes_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_DocumentTypes_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_DocumentTypes_I ON dbo.DocumentTypes AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.DocumentTypes_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, DocumentCode, DocumentName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.DocumentCode, i.DocumentName, i.IsActive, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_DocumentTypes_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_DocumentTypes_U ON dbo.DocumentTypes AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.DocumentTypes_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, DocumentCode, DocumentName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.DocumentCode, i.DocumentName, i.IsActive, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;

    INSERT INTO dbo.DocumentTypes_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, DocumentCode, DocumentName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, LEFT(i.DocumentCode, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.DocumentCode, d.DocumentName, d.IsActive, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_DocumentTypes_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_DocumentTypes_D ON dbo.DocumentTypes AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.DocumentTypes_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, DocumentCode, DocumentName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.DocumentCode, d.DocumentName, d.IsActive, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy FROM deleted d;

    INSERT INTO dbo.DocumentTypes_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, DocumentCode, DocumentName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, LEFT(d.DocumentCode, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.DocumentCode, d.DocumentName, d.IsActive, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- OcrExtractionEntries
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.OcrExtractionEntries_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OcrExtractionEntries_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OcrExtractionEntries_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionEntries_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_OcrExtractionEntries_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionEntries_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_OcrExtractionEntries_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        OcrExtractionRecordId INT NULL,
        EntryNumber NVARCHAR(50) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionEntries_Log_01' AND object_id = OBJECT_ID('dbo.OcrExtractionEntries_Log'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionEntries_Log_01 ON dbo.OcrExtractionEntries_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionEntries_Log_02' AND object_id = OBJECT_ID('dbo.OcrExtractionEntries_Log'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionEntries_Log_02 ON dbo.OcrExtractionEntries_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.OcrExtractionEntries_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OcrExtractionEntries_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OcrExtractionEntries_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionEntries_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_OcrExtractionEntries_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionEntries_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_OcrExtractionEntries_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_OcrExtractionEntries_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_OcrExtractionEntries_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        OcrExtractionRecordId INT NULL,
        EntryNumber NVARCHAR(50) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionEntries_Hist_01' AND object_id = OBJECT_ID('dbo.OcrExtractionEntries_Hist'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionEntries_Hist_01 ON dbo.OcrExtractionEntries_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionEntries_Hist_02' AND object_id = OBJECT_ID('dbo.OcrExtractionEntries_Hist'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionEntries_Hist_02 ON dbo.OcrExtractionEntries_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OcrExtractionEntries_Hist_03' AND object_id = OBJECT_ID('dbo.OcrExtractionEntries_Hist'))
    CREATE NONCLUSTERED INDEX IX_OcrExtractionEntries_Hist_03 ON dbo.OcrExtractionEntries_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_OcrExtractionEntries_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_OcrExtractionEntries_I ON dbo.OcrExtractionEntries AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.OcrExtractionEntries_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, OcrExtractionRecordId, EntryNumber)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.OcrExtractionRecordId, i.EntryNumber FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_OcrExtractionEntries_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_OcrExtractionEntries_U ON dbo.OcrExtractionEntries AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.OcrExtractionEntries_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, OcrExtractionRecordId, EntryNumber)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.OcrExtractionRecordId, i.EntryNumber FROM inserted i;

    INSERT INTO dbo.OcrExtractionEntries_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, OcrExtractionRecordId, EntryNumber)
    SELECT @UserId, @IPAddr, LEFT(CONCAT(i.OcrExtractionRecordId, N'':'', i.EntryNumber), 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.OcrExtractionRecordId, d.EntryNumber
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_OcrExtractionEntries_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_OcrExtractionEntries_D ON dbo.OcrExtractionEntries AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.OcrExtractionEntries_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, OcrExtractionRecordId, EntryNumber)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.OcrExtractionRecordId, d.EntryNumber FROM deleted d;

    INSERT INTO dbo.OcrExtractionEntries_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, OcrExtractionRecordId, EntryNumber)
    SELECT @UserId, @IPAddr, LEFT(CONCAT(d.OcrExtractionRecordId, N'':'', d.EntryNumber), 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.OcrExtractionRecordId, d.EntryNumber
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- ImageExtractionStaging
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.ImageExtractionStaging_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ImageExtractionStaging_Log
    (
        Log_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ImageExtractionStaging_Log PRIMARY KEY,
        Log_UserID    NVARCHAR(50)  NOT NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_ImageExtractionStaging_Log_AppName DEFAULT (dbo.ufn_GetAppName()),
        Log_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_ImageExtractionStaging_Log_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_ImageExtractionStaging_Log_HostName DEFAULT (dbo.ufn_GetHostName()),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_ImageExtractionStaging_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  NVARCHAR(1)   NOT NULL,
        Id            INT           NULL,
        FetchRunId INT NULL,
        FolderPath NVARCHAR(MAX) NULL,
        ImagePath NVARCHAR(MAX) NULL,
        DocumentTypeName NVARCHAR(MAX) NULL,
        TitleNumber NVARCHAR(50) NULL,
        TitleType INT NULL,
        [Plan] NVARCHAR(50) NULL,
        Block NVARCHAR(50) NULL,
        Lot NVARCHAR(50) NULL,
        ClassifiedAt DATETIME2 NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ImageExtractionStaging_Log_01' AND object_id = OBJECT_ID('dbo.ImageExtractionStaging_Log'))
    CREATE NONCLUSTERED INDEX IX_ImageExtractionStaging_Log_01 ON dbo.ImageExtractionStaging_Log (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ImageExtractionStaging_Log_02' AND object_id = OBJECT_ID('dbo.ImageExtractionStaging_Log'))
    CREATE NONCLUSTERED INDEX IX_ImageExtractionStaging_Log_02 ON dbo.ImageExtractionStaging_Log (Log_Date, Log_UserID);
GO

IF OBJECT_ID('dbo.ImageExtractionStaging_Hist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ImageExtractionStaging_Hist
    (
        Hist_ID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ImageExtractionStaging_Hist PRIMARY KEY,
        Hist_UserID    NVARCHAR(50)  NOT NULL,
        Hist_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_ImageExtractionStaging_Hist_AppName DEFAULT (dbo.ufn_GetAppName()),
        Hist_IPAddr    NVARCHAR(64)  NULL CONSTRAINT DF_ImageExtractionStaging_Hist_IPAddr DEFAULT (dbo.ufn_GetIPAddress()),
        Hist_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_ImageExtractionStaging_Hist_HostName DEFAULT (dbo.ufn_GetHostName()),
        Hist_Date      DATETIME2     NOT NULL CONSTRAINT DF_ImageExtractionStaging_Hist_Date DEFAULT (SYSUTCDATETIME()),
        Hist_RefNo     NVARCHAR(50)  NULL,
        OldValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_ImageExtractionStaging_Hist_Old CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_ImageExtractionStaging_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        Id             INT           NULL,
        FetchRunId INT NULL,
        FolderPath NVARCHAR(MAX) NULL,
        ImagePath NVARCHAR(MAX) NULL,
        DocumentTypeName NVARCHAR(MAX) NULL,
        TitleNumber NVARCHAR(50) NULL,
        TitleType INT NULL,
        [Plan] NVARCHAR(50) NULL,
        Block NVARCHAR(50) NULL,
        Lot NVARCHAR(50) NULL,
        ClassifiedAt DATETIME2 NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ImageExtractionStaging_Hist_01' AND object_id = OBJECT_ID('dbo.ImageExtractionStaging_Hist'))
    CREATE NONCLUSTERED INDEX IX_ImageExtractionStaging_Hist_01 ON dbo.ImageExtractionStaging_Hist (Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ImageExtractionStaging_Hist_02' AND object_id = OBJECT_ID('dbo.ImageExtractionStaging_Hist'))
    CREATE NONCLUSTERED INDEX IX_ImageExtractionStaging_Hist_02 ON dbo.ImageExtractionStaging_Hist (Hist_Date, Hist_UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ImageExtractionStaging_Hist_03' AND object_id = OBJECT_ID('dbo.ImageExtractionStaging_Hist'))
    CREATE NONCLUSTERED INDEX IX_ImageExtractionStaging_Hist_03 ON dbo.ImageExtractionStaging_Hist (Hist_RefNo);
GO

IF OBJECT_ID('dbo.trg_ImageExtractionStaging_I', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_ImageExtractionStaging_I ON dbo.ImageExtractionStaging AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.ImageExtractionStaging_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, FetchRunId, FolderPath, ImagePath, DocumentTypeName, TitleNumber, TitleType, [Plan], Block, Lot, ClassifiedAt)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.FetchRunId, i.FolderPath, i.ImagePath, i.DocumentTypeName, i.TitleNumber, i.TitleType, i.[Plan], i.Block, i.Lot, i.ClassifiedAt FROM inserted i;
END
');
GO

IF OBJECT_ID('dbo.trg_ImageExtractionStaging_U', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_ImageExtractionStaging_U ON dbo.ImageExtractionStaging AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.ImageExtractionStaging_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, FetchRunId, FolderPath, ImagePath, DocumentTypeName, TitleNumber, TitleType, [Plan], Block, Lot, ClassifiedAt)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.FetchRunId, i.FolderPath, i.ImagePath, i.DocumentTypeName, i.TitleNumber, i.TitleType, i.[Plan], i.Block, i.Lot, i.ClassifiedAt FROM inserted i;

    INSERT INTO dbo.ImageExtractionStaging_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, FetchRunId, FolderPath, ImagePath, DocumentTypeName, TitleNumber, TitleType, [Plan], Block, Lot, ClassifiedAt)
    SELECT @UserId, @IPAddr, LEFT(i.ImagePath, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.FetchRunId, d.FolderPath, d.ImagePath, d.DocumentTypeName, d.TitleNumber, d.TitleType, d.[Plan], d.Block, d.Lot, d.ClassifiedAt
    FROM inserted i JOIN deleted d ON i.Id = d.Id;
END
');
GO

IF OBJECT_ID('dbo.trg_ImageExtractionStaging_D', 'TR') IS NULL
EXEC('
CREATE TRIGGER dbo.trg_ImageExtractionStaging_D ON dbo.ImageExtractionStaging AFTER DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(50) = ISNULL(CAST(SESSION_CONTEXT(N''AppUserId'') AS NVARCHAR(50)), N''0'');
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.ImageExtractionStaging_Log (Log_UserID, Log_IPAddr, Log_Activity, Id, FetchRunId, FolderPath, ImagePath, DocumentTypeName, TitleNumber, TitleType, [Plan], Block, Lot, ClassifiedAt)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.FetchRunId, d.FolderPath, d.ImagePath, d.DocumentTypeName, d.TitleNumber, d.TitleType, d.[Plan], d.Block, d.Lot, d.ClassifiedAt FROM deleted d;

    INSERT INTO dbo.ImageExtractionStaging_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, Id, FetchRunId, FolderPath, ImagePath, DocumentTypeName, TitleNumber, TitleType, [Plan], Block, Lot, ClassifiedAt)
    SELECT @UserId, @IPAddr, LEFT(d.ImagePath, 50),
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.FetchRunId, d.FolderPath, d.ImagePath, d.DocumentTypeName, d.TitleNumber, d.TitleType, d.[Plan], d.Block, d.Lot, d.ClassifiedAt
    FROM deleted d;
END
');
GO

-------------------------------------------------------------------------------
-- Permissions (per guideline b.3/b.4 - see header) - one consolidated block
-- after every _Log/_Hist table and trigger above already exists, mirroring
-- Section 21's own layout in dct_sd_main.sql. Requires [DCTSDUser] to
-- already exist (created manually by CMRE), same precondition as Section 21.
-------------------------------------------------------------------------------
GRANT SELECT ON dbo.Users_Log TO [DCTSDUser];
GRANT SELECT ON dbo.RefreshTokens_Log TO [DCTSDUser];
GRANT SELECT ON dbo.AppSettings_Log TO [DCTSDUser];
GRANT SELECT ON dbo.CodeLookups_Log TO [DCTSDUser];
GRANT SELECT ON dbo.FetchRuns_Log TO [DCTSDUser];
GRANT SELECT ON dbo.OcrExtractionRecords_Log TO [DCTSDUser];
GRANT SELECT ON dbo.ManualValidationRequests_Log TO [DCTSDUser];
GRANT SELECT ON dbo.MigrationRecords_Log TO [DCTSDUser];
GRANT SELECT ON dbo.MigrationDocuments_Log TO [DCTSDUser];
GRANT SELECT ON dbo.EmptyFolderRecords_Log TO [DCTSDUser];
GRANT SELECT ON dbo.FailedExtractionRecords_Log TO [DCTSDUser];
GRANT SELECT ON dbo.BrandingSettings_Log TO [DCTSDUser];
GRANT SELECT ON dbo.EmailTemplates_Log TO [DCTSDUser];
GRANT SELECT ON dbo.DocumentTypes_Log TO [DCTSDUser];
GRANT SELECT ON dbo.OcrExtractionEntries_Log TO [DCTSDUser];
GRANT SELECT ON dbo.ImageExtractionStaging_Log TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.Users_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.RefreshTokens_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.AppSettings_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.CodeLookups_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.FetchRuns_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.OcrExtractionRecords_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.ManualValidationRequests_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.MigrationRecords_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.MigrationDocuments_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.EmptyFolderRecords_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.FailedExtractionRecords_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.BrandingSettings_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.EmailTemplates_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.DocumentTypes_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.OcrExtractionEntries_Hist TO [DCTSDUser];
GRANT SELECT, INSERT ON dbo.ImageExtractionStaging_Hist TO [DCTSDUser];
GO


PRINT 'dct_sd_main.sql completed.';
GO
