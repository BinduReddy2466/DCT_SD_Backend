/* ============================================================================
   Release Notes No.        : <TO BE ASSIGNED BY DBA / RELEASE MGMT>
   Package                  : Lares_DCT_SD_qa - Schema (DBChanges/Lares_DCT_SD_qa/)
   Instance                 : 172.16.1.68,1433  (SQL Server 2022 Developer Edition)
   Purpose                  : Stand up a QA-compatibility database for the
                               DCT - Supporting Documents (DCT-SD) module that
                               matches the current DCT_SD ASP.NET Core / EF Core
                               code-first model exactly (DCT_SD/Models/Entities,
                               DCT_SD/Configuration/*Configuration.cs and
                               DCT_SD/Migrations/ApplicationDbContextModelSnapshot.cs
                               as of the AddOcrExtractionAndDocumentTypes migration).
   Revision history           : v2 (2026-08-17) - added the OCR Examination
                               feature's DocumentTypes, OcrExtractionRecords and
                               OcrExtractionEntries tables, the OcrExtractionRecordId
                               lineage link on ManualValidationRequests/MigrationRecords,
                               the DocumentTypeId link on ManualValidationDocuments/
                               MigrationDocuments, the FetchRuns checkpoint-and-resume
                               columns (LastProcessedFolderPath/LastProcessedAt), and a
                               UNIQUE(FolderPath) constraint on EmptyFolderRecords.
                               Entry stays named Entry (not renamed to
                               EntryNumbersCsv) but is widened from NVARCHAR(50) to
                               NVARCHAR(200) to hold comma-separated entry numbers -
                               this was scoped as a schema-only change, and a rename
                               would have rippled into Services/Views/DTOs/JS that
                               already bind to the Entry name. All of the above was
                               scaffolded as EF Core migration
                               20260817095014_AddOcrExtractionAndDocumentTypes and
                               applied to DCT_SD/Models/Entities +
                               DCT_SD/Configuration/*Configuration.cs, so this script
                               and the application code are in sync.
   Object inventory          : 1 database, 21 tables, see per-table headers
                               below for constraint inventory (PK/FK/UK/IX/DF).
   Idempotency                : Every CREATE is guarded by an existence check so
                               this script can be re-run against a partially
                               applied target without a prior rollback
                               (Guideline A.3.c).
   Rollback instruction      : DROP DATABASE Lares_DCT_SD_qa;  (Guideline A.2.a /
                               A.4.d.1 - "Rollback thru instructions: dropping
                               of the newly created database.")
   Test data                 : NONE in this script (Guideline A.6.h.1). This
                               also excludes the EF Core HasData seed rows for
                               Roles/Menus/RegistryOffices/TitleSequenceLookups
                               and the DbInitializer-seeded SessionSettings/
                               BrandingSettings/EmailTemplates rows - those are
                               reference/config data, not QA sample data, and
                               ship separately in
                               02_Lares_DCT_SD_qa_SeedData_QA_ONLY.sql, which
                               must never be run against a production target.
   Naming conventions applied (per DB_Coding Standards & Guidelines):
       Tables      PascalCase, descriptive, no TBL prefix/suffix
       Primary Key PK_<TableName>
       Foreign Key FK_<TableName>_<NN>   (NN increments per child table)
       Unique Key  UK_<TableName>_<NN>
       Index       IX_<TableName>_<NN>   (purpose noted in a trailing comment)
       Default     DF_<TableName>_<ColumnName>
   Requires                  : CREATE DATABASE requires a login with the
                               dbcreator or sysadmin server role. The
                               lares_sd_user login used for Lares_SD_DB has
                               neither (verified 2026-08-14) - see companion
                               README for the unblock options.
   ============================================================================ */

-------------------------------------------------------------------------------
-- STEP 0: DATABASE
-------------------------------------------------------------------------------
IF DB_ID(N'Lares_DCT_SD_qa') IS NULL
BEGIN
    CREATE DATABASE Lares_DCT_SD_qa;
END
GO

USE Lares_DCT_SD_qa;
GO

-------------------------------------------------------------------------------
-- 1. Roles  (AuditableEntity; RoleConfiguration.cs)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        Id                  INT IDENTITY(1,1)      NOT NULL,
        Name                NVARCHAR(50)           NOT NULL,
        Description         NVARCHAR(200)          NULL,
        IsSystemDefined     BIT                    NOT NULL CONSTRAINT DF_Roles_IsSystemDefined DEFAULT (0),
        CreatedAt           DATETIME2              NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT                    NULL,
        UpdatedAt           DATETIME2              NULL,
        UpdatedBy           INT                    NULL,
        RowVersion          ROWVERSION             NOT NULL,
        CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_Roles_01 UNIQUE (Name)
    );
END
GO

-------------------------------------------------------------------------------
-- 2. Menus  (AuditableEntity; MenuConfiguration.cs)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Menus', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Menus
    (
        Id                  INT IDENTITY(1,1)      NOT NULL,
        [Key]               NVARCHAR(50)           NOT NULL,
        Label               NVARCHAR(100)          NOT NULL,
        DisplayOrder        INT                    NOT NULL CONSTRAINT DF_Menus_DisplayOrder DEFAULT (0),
        IsBaseMenu          BIT                    NOT NULL CONSTRAINT DF_Menus_IsBaseMenu DEFAULT (0),
        CreatedAt           DATETIME2              NOT NULL CONSTRAINT DF_Menus_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT                    NULL,
        UpdatedAt           DATETIME2              NULL,
        UpdatedBy           INT                    NULL,
        RowVersion          ROWVERSION             NOT NULL,
        CONSTRAINT PK_Menus PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_Menus_01 UNIQUE ([Key])
    );
END
GO

-------------------------------------------------------------------------------
-- 3. Users  (AuditableEntity; UserConfiguration.cs - soft delete via IsDeleted
--    is enforced by an EF global query filter, not representable as DDL)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id                      INT IDENTITY(1,1)      NOT NULL,
        FirstName               NVARCHAR(100)          NOT NULL,
        LastName                NVARCHAR(100)          NOT NULL,
        Username                NVARCHAR(256)          NOT NULL,
        PasswordHash            NVARCHAR(512)          NOT NULL,
        RoleId                  INT                    NOT NULL,
        Status                  INT                    NOT NULL CONSTRAINT DF_Users_Status DEFAULT (1),
        FailedLoginAttempts     INT                    NOT NULL CONSTRAINT DF_Users_FailedLoginAttempts DEFAULT (0),
        LastLoginAt             DATETIME2              NULL,
        IsDeleted               BIT                    NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
        DeletedAt               DATETIME2              NULL,
        CreatedAt               DATETIME2              NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy               INT                    NULL,
        UpdatedAt               DATETIME2              NULL,
        UpdatedBy               INT                    NULL,
        RowVersion              ROWVERSION             NOT NULL,
        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_Users_01 UNIQUE (Username),
        -- DeleteBehavior.Restrict in UserConfiguration.cs: SQL Server's default
        -- NO ACTION on a plain FK already matches this, no ON DELETE clause needed.
        CONSTRAINT FK_Users_01 FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_01' AND object_id = OBJECT_ID(N'dbo.Users'))
    CREATE INDEX IX_Users_01 ON dbo.Users (RoleId); -- role lookup / role-based menu gating
GO

-------------------------------------------------------------------------------
-- 4. UserMenuPermissions  (join table; UserMenuPermissionConfiguration.cs -
--    both FKs are DeleteBehavior.Cascade)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.UserMenuPermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserMenuPermissions
    (
        UserId              INT             NOT NULL,
        MenuId              INT             NOT NULL,
        GrantedAt           DATETIME2       NOT NULL CONSTRAINT DF_UserMenuPermissions_GrantedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_UserMenuPermissions PRIMARY KEY CLUSTERED (UserId, MenuId),
        CONSTRAINT FK_UserMenuPermissions_01 FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserMenuPermissions_02 FOREIGN KEY (MenuId) REFERENCES dbo.Menus (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserMenuPermissions_01' AND object_id = OBJECT_ID(N'dbo.UserMenuPermissions'))
    CREATE INDEX IX_UserMenuPermissions_01 ON dbo.UserMenuPermissions (MenuId); -- reverse lookup: who can see menu X
GO

-------------------------------------------------------------------------------
-- 5. RefreshTokens  (RefreshTokenConfiguration.cs - UserId FK is
--    DeleteBehavior.Cascade)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens
    (
        Id                      INT IDENTITY(1,1)  NOT NULL,
        UserId                  INT                 NOT NULL,
        TokenHash               NVARCHAR(128)       NOT NULL,
        CreatedAt               DATETIME2           NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT (SYSUTCDATETIME()),
        ExpiresAt               DATETIME2           NOT NULL,
        RevokedAt               DATETIME2           NULL,
        ReplacedByTokenHash     NVARCHAR(128)       NULL,
        CreatedByIp             NVARCHAR(64)        NULL,
        CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_RefreshTokens_01 UNIQUE (TokenHash),
        CONSTRAINT FK_RefreshTokens_01 FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshTokens_01' AND object_id = OBJECT_ID(N'dbo.RefreshTokens'))
    CREATE INDEX IX_RefreshTokens_01 ON dbo.RefreshTokens (UserId); -- active-sessions-per-user lookup
GO

-------------------------------------------------------------------------------
-- 6. RegistryOffices  (AuditableEntity; RegistryOfficeConfiguration.cs)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.RegistryOffices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistryOffices
    (
        Id                  INT IDENTITY(1,1)      NOT NULL,
        Code                NVARCHAR(20)           NOT NULL,
        Name                NVARCHAR(150)          NOT NULL,
        IsActive            BIT                    NOT NULL CONSTRAINT DF_RegistryOffices_IsActive DEFAULT (1),
        CreatedAt           DATETIME2              NOT NULL CONSTRAINT DF_RegistryOffices_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT                    NULL,
        UpdatedAt           DATETIME2              NULL,
        UpdatedBy           INT                    NULL,
        RowVersion          ROWVERSION             NOT NULL,
        CONSTRAINT PK_RegistryOffices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_RegistryOffices_01 UNIQUE (Code)
    );
END
GO

-------------------------------------------------------------------------------
-- 7. DocumentTypes  (AuditableEntity; DocumentTypeConfiguration.cs - backs the
--    OCR Examination feature's document-type lookups referenced from
--    ManualValidationDocuments.DocumentTypeId / MigrationDocuments.DocumentTypeId)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.DocumentTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentTypes
    (
        Id                  INT IDENTITY(1,1)      NOT NULL,
        DocumentCode        NVARCHAR(20)           NOT NULL,
        DocumentName        NVARCHAR(200)          NOT NULL,
        IsActive            BIT                    NOT NULL CONSTRAINT DF_DocumentTypes_IsActive DEFAULT (1),
        CreatedAt           DATETIME2              NOT NULL CONSTRAINT DF_DocumentTypes_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT                    NULL,
        UpdatedAt           DATETIME2              NULL,
        UpdatedBy           INT                    NULL,
        RowVersion          ROWVERSION             NOT NULL,
        CONSTRAINT PK_DocumentTypes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_DocumentTypes_01 UNIQUE (DocumentCode),
        CONSTRAINT UK_DocumentTypes_02 UNIQUE (DocumentName)
    );
END
GO

-------------------------------------------------------------------------------
-- 8. RootPathHistories  (RootPathHistoryConfiguration.cs - audit-log style
--    table; ModifiedByUserId is a plain denormalized int, no FK to Users)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.RootPathHistories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RootPathHistories
    (
        Id                      INT IDENTITY(1,1)      NOT NULL,
        FromPath                NVARCHAR(1000)         NULL,
        ToPath                  NVARCHAR(1000)         NOT NULL,
        Remarks                 NVARCHAR(500)          NOT NULL,
        ModifiedByUserId        INT                    NOT NULL,
        ModifiedByUsername      NVARCHAR(256)          NOT NULL,
        ModifiedAt              DATETIME2              NOT NULL CONSTRAINT DF_RootPathHistories_ModifiedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_RootPathHistories PRIMARY KEY CLUSTERED (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RootPathHistories_01' AND object_id = OBJECT_ID(N'dbo.RootPathHistories'))
    CREATE INDEX IX_RootPathHistories_01 ON dbo.RootPathHistories (ModifiedAt DESC); -- default sort, most-recent-first
GO

-------------------------------------------------------------------------------
-- 9. FetchRuns  (FetchRunConfiguration.cs - ExecutedByUserId is a plain
--    denormalized int, no FK to Users; LastProcessedFolderPath/LastProcessedAt
--    back the fetch checkpoint-and-resume behavior)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.FetchRuns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FetchRuns
    (
        Id                          INT IDENTITY(1,1)      NOT NULL,
        SourcePath                  NVARCHAR(1000)         NOT NULL,
        Status                      INT                    NOT NULL,   -- 1=Ongoing, 2=Completed, 3=Failed (FetchRunStatus)
        TotalCount                  INT                    NULL,
        ProcessedCount               INT                    NOT NULL CONSTRAINT DF_FetchRuns_ProcessedCount DEFAULT (0),
        LastProcessedFolderPath     NVARCHAR(1000)         NULL,       -- resume checkpoint
        LastProcessedAt             DATETIME2              NULL,       -- when the checkpoint was recorded
        ExecutedByUserId            INT                    NOT NULL,
        ExecutedByUsername          NVARCHAR(256)          NOT NULL,
        StartedAt                   DATETIME2              NOT NULL CONSTRAINT DF_FetchRuns_StartedAt DEFAULT (SYSUTCDATETIME()),
        CompletedAt                 DATETIME2              NULL,
        CONSTRAINT PK_FetchRuns PRIMARY KEY CLUSTERED (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FetchRuns_01' AND object_id = OBJECT_ID(N'dbo.FetchRuns'))
    CREATE INDEX IX_FetchRuns_01 ON dbo.FetchRuns (StartedAt DESC); -- default sort, most-recent-first
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FetchRuns_02' AND object_id = OBJECT_ID(N'dbo.FetchRuns'))
    CREATE INDEX IX_FetchRuns_02 ON dbo.FetchRuns (Status); -- "resume the ongoing/failed run" lookups
GO

-------------------------------------------------------------------------------
-- 10. EmptyFolderRecords  (EmptyFolderRecordConfiguration.cs - Status is
--     intentionally free-text NVARCHAR, not an int-backed enum like every
--     other status column in this schema)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.EmptyFolderRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmptyFolderRecords
    (
        Id                  INT IDENTITY(1,1)      NOT NULL,
        RdCode              NVARCHAR(20)           NULL,
        RdName              NVARCHAR(150)          NULL,
        FolderName          NVARCHAR(260)          NOT NULL,
        FolderPath          NVARCHAR(1000)         NOT NULL,
        Status              NVARCHAR(50)           NOT NULL CONSTRAINT DF_EmptyFolderRecords_Status DEFAULT (N'Empty Entry Folder'),
        FetchDateTime       DATETIME2              NOT NULL CONSTRAINT DF_EmptyFolderRecords_FetchDateTime DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EmptyFolderRecords PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_EmptyFolderRecords_01 UNIQUE (FolderPath)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmptyFolderRecords_01' AND object_id = OBJECT_ID(N'dbo.EmptyFolderRecords'))
    CREATE INDEX IX_EmptyFolderRecords_01 ON dbo.EmptyFolderRecords (FetchDateTime DESC); -- default sort, most-recent-first
GO

-------------------------------------------------------------------------------
-- 11. OcrExtractionRecords  (OcrExtractionRecordConfiguration.cs - the one
--     place a source Entry-folder path is linked to what it turned into
--     (Manual Validation and/or Migration))
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.OcrExtractionRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OcrExtractionRecords
    (
        Id                  INT IDENTITY(1,1)      NOT NULL,
        RequestNumber       NVARCHAR(30)           NOT NULL,   -- REQ-{12 digit sequence}, generated once at extraction
        FetchRunId          INT                    NULL,       -- which fetch run produced this record
        RdCode              NVARCHAR(20)           NULL,
        RdName              NVARCHAR(150)          NULL,
        FolderPath          NVARCHAR(1000)         NOT NULL,   -- source Entry Folder - enables "already processed" lookups on recurring fetch
        TitleNumber         NVARCHAR(50)           NULL,
        TitleType           INT                    NULL,
        DocumentCount       INT                    NOT NULL CONSTRAINT DF_OcrExtractionRecords_DocumentCount DEFAULT (0),
        ExtractionStatus    INT                    NOT NULL,   -- 1=Fully Extracted, 2=Partially Extracted
        ExtractionDateTime  DATETIME2              NOT NULL CONSTRAINT DF_OcrExtractionRecords_ExtractionDateTime DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_OcrExtractionRecords PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_OcrExtractionRecords_01 UNIQUE (RequestNumber),
        -- DeleteBehavior.Restrict in OcrExtractionRecordConfiguration.cs: SQL
        -- Server's default NO ACTION on a plain FK already matches this.
        CONSTRAINT FK_OcrExtractionRecords_01 FOREIGN KEY (FetchRunId) REFERENCES dbo.FetchRuns (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OcrExtractionRecords_01' AND object_id = OBJECT_ID(N'dbo.OcrExtractionRecords'))
    CREATE INDEX IX_OcrExtractionRecords_01 ON dbo.OcrExtractionRecords (ExtractionDateTime DESC); -- OCR Examination default sort
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OcrExtractionRecords_02' AND object_id = OBJECT_ID(N'dbo.OcrExtractionRecords'))
    CREATE INDEX IX_OcrExtractionRecords_02 ON dbo.OcrExtractionRecords (ExtractionStatus); -- OCR Examination status filter
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OcrExtractionRecords_03' AND object_id = OBJECT_ID(N'dbo.OcrExtractionRecords'))
    CREATE INDEX IX_OcrExtractionRecords_03 ON dbo.OcrExtractionRecords (FolderPath); -- recurring-fetch "already processed?" lookup
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OcrExtractionRecords_04' AND object_id = OBJECT_ID(N'dbo.OcrExtractionRecords'))
    CREATE INDEX IX_OcrExtractionRecords_04 ON dbo.OcrExtractionRecords (FetchRunId);
GO

-------------------------------------------------------------------------------
-- 12. OcrExtractionEntries  (OcrExtractionEntryConfiguration.cs - expanded
--     Entry Numbers per OCR record; a folder named "8001-04" expands to 4 rows)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.OcrExtractionEntries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OcrExtractionEntries
    (
        Id                      INT IDENTITY(1,1)  NOT NULL,
        OcrExtractionRecordId   INT                 NOT NULL,
        EntryNumber             NVARCHAR(50)        NOT NULL,
        CONSTRAINT PK_OcrExtractionEntries PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_OcrExtractionEntries_01 UNIQUE (OcrExtractionRecordId, EntryNumber),
        CONSTRAINT FK_OcrExtractionEntries_01 FOREIGN KEY (OcrExtractionRecordId) REFERENCES dbo.OcrExtractionRecords (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OcrExtractionEntries_01' AND object_id = OBJECT_ID(N'dbo.OcrExtractionEntries'))
    CREATE INDEX IX_OcrExtractionEntries_01 ON dbo.OcrExtractionEntries (EntryNumber); -- LIKE-search on Entry Number
GO

-------------------------------------------------------------------------------
-- 13. TitleSequenceLookups  (TitleSequenceLookupConfiguration.cs)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.TitleSequenceLookups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TitleSequenceLookups
    (
        Id                  INT IDENTITY(1,1)      NOT NULL,
        Title               NVARCHAR(50)           NOT NULL,
        TitleType           INT                    NOT NULL,   -- TitleType enum (required here; nullable on the two tables below)
        [Plan]              NVARCHAR(50)           NOT NULL,
        Block               NVARCHAR(50)           NOT NULL,
        Lot                 NVARCHAR(50)           NOT NULL,
        Sequence            NVARCHAR(50)           NOT NULL,
        CONSTRAINT PK_TitleSequenceLookups PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_TitleSequenceLookups_01 UNIQUE (Title, TitleType, [Plan], Block, Lot)
    );
END
GO

-------------------------------------------------------------------------------
-- 14. ManualValidationRequests  (ManualValidationRequestConfiguration.cs -
--     UpdatedByUserId/LockedByUserId are plain denormalized ints, no FK)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.ManualValidationRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManualValidationRequests
    (
        Id                      INT IDENTITY(1,1)      NOT NULL,
        RequestNumber           NVARCHAR(30)           NOT NULL,
        OcrExtractionRecordId   INT                    NULL,       -- lineage back to the source OCR record / folder
        RdCode                  NVARCHAR(20)           NULL,
        RdName                  NVARCHAR(150)          NULL,
        Entry                   NVARCHAR(200)          NULL,       -- comma-separated when multiple
        Title                   NVARCHAR(50)           NULL,
        TitleType               INT                    NULL,
        [Plan]                  NVARCHAR(50)           NULL,
        Block                   NVARCHAR(50)           NULL,
        Lot                     NVARCHAR(50)           NULL,
        TitleSequence           NVARCHAR(50)           NULL,
        Status                  INT                    NOT NULL,   -- 1=Incomplete Extraction, 2=Target RD Not Identified
        MissingFieldsCsv        NVARCHAR(500)          NOT NULL,
        ExtractionDate          DATETIME2              NOT NULL CONSTRAINT DF_ManualValidationRequests_ExtractionDate DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId         INT                    NULL,
        UpdatedByUsername       NVARCHAR(256)          NULL,
        UpdatedAt               DATETIME2              NULL,
        LockedByUserId          INT                    NULL,
        LockedByUsername        NVARCHAR(256)          NULL,
        LockedAt                DATETIME2              NULL,
        MigratedAt              DATETIME2              NULL,
        CONSTRAINT PK_ManualValidationRequests PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_ManualValidationRequests_01 UNIQUE (RequestNumber),
        -- DeleteBehavior.Restrict in ManualValidationRequestConfiguration.cs:
        -- SQL Server's default NO ACTION on a plain FK already matches this.
        CONSTRAINT FK_ManualValidationRequests_01 FOREIGN KEY (OcrExtractionRecordId) REFERENCES dbo.OcrExtractionRecords (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManualValidationRequests_01' AND object_id = OBJECT_ID(N'dbo.ManualValidationRequests'))
    CREATE INDEX IX_ManualValidationRequests_01 ON dbo.ManualValidationRequests (ExtractionDate DESC); -- default sort, most-recent-first
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManualValidationRequests_02' AND object_id = OBJECT_ID(N'dbo.ManualValidationRequests'))
    CREATE INDEX IX_ManualValidationRequests_02 ON dbo.ManualValidationRequests (Status); -- status filter
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManualValidationRequests_03' AND object_id = OBJECT_ID(N'dbo.ManualValidationRequests'))
    CREATE INDEX IX_ManualValidationRequests_03 ON dbo.ManualValidationRequests (OcrExtractionRecordId);
GO

-------------------------------------------------------------------------------
-- 15. ManualValidationDocuments  (ManualValidationDocumentConfiguration.cs -
--     ManualValidationRequestId FK is DeleteBehavior.Cascade; DocumentTypeId
--     FK is DeleteBehavior.Restrict)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.ManualValidationDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManualValidationDocuments
    (
        Id                          INT IDENTITY(1,1)  NOT NULL,
        ManualValidationRequestId   INT                 NOT NULL,
        DocumentTypeId              INT                 NULL,
        DocumentName                NVARCHAR(200)       NOT NULL,
        FileName                    NVARCHAR(260)       NOT NULL,
        CONSTRAINT PK_ManualValidationDocuments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ManualValidationDocuments_01 FOREIGN KEY (ManualValidationRequestId) REFERENCES dbo.ManualValidationRequests (Id) ON DELETE CASCADE,
        CONSTRAINT FK_ManualValidationDocuments_02 FOREIGN KEY (DocumentTypeId) REFERENCES dbo.DocumentTypes (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManualValidationDocuments_01' AND object_id = OBJECT_ID(N'dbo.ManualValidationDocuments'))
    CREATE INDEX IX_ManualValidationDocuments_01 ON dbo.ManualValidationDocuments (ManualValidationRequestId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManualValidationDocuments_02' AND object_id = OBJECT_ID(N'dbo.ManualValidationDocuments'))
    CREATE INDEX IX_ManualValidationDocuments_02 ON dbo.ManualValidationDocuments (DocumentTypeId);
GO

-------------------------------------------------------------------------------
-- 16. ManualValidationRemarks  (ManualValidationRemarkConfiguration.cs -
--     ManualValidationRequestId FK is DeleteBehavior.Cascade; ByUserId is a
--     plain denormalized int, no FK)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.ManualValidationRemarks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManualValidationRemarks
    (
        Id                          INT IDENTITY(1,1)  NOT NULL,
        ManualValidationRequestId   INT                 NOT NULL,
        Action                      INT                 NOT NULL,   -- 1=Saved, 2=Closed
        Remarks                     NVARCHAR(500)       NOT NULL,
        ByUserId                    INT                 NOT NULL,
        ByUsername                  NVARCHAR(256)       NOT NULL,
        CreatedAt                   DATETIME2           NOT NULL CONSTRAINT DF_ManualValidationRemarks_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ManualValidationRemarks PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ManualValidationRemarks_01 FOREIGN KEY (ManualValidationRequestId) REFERENCES dbo.ManualValidationRequests (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManualValidationRemarks_01' AND object_id = OBJECT_ID(N'dbo.ManualValidationRemarks'))
    CREATE INDEX IX_ManualValidationRemarks_01 ON dbo.ManualValidationRemarks (ManualValidationRequestId);
GO

-------------------------------------------------------------------------------
-- 17. MigrationRecords  (MigrationRecordConfiguration.cs - RdCode/RdName are
--     required here, unlike ManualValidationRequests/EmptyFolderRecords
--     where they are nullable)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.MigrationRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationRecords
    (
        Id                      INT IDENTITY(1,1)      NOT NULL,
        RequestNumber           NVARCHAR(30)           NOT NULL,
        OcrExtractionRecordId   INT                    NULL,       -- lineage back to the source OCR record / folder
        MigrationDate           DATETIME2              NOT NULL CONSTRAINT DF_MigrationRecords_MigrationDate DEFAULT (SYSUTCDATETIME()),
        RdCode                  NVARCHAR(20)           NOT NULL,
        RdName                  NVARCHAR(150)          NOT NULL,
        Entry                   NVARCHAR(200)          NULL,       -- comma-separated when multiple
        Title                   NVARCHAR(50)           NULL,
        TitleType               INT                    NULL,
        [Plan]                  NVARCHAR(50)           NULL,
        Block                   NVARCHAR(50)           NULL,
        Lot                     NVARCHAR(50)           NULL,
        TitleSequence           NVARCHAR(50)           NULL,
        MigrationStatus         INT                    NOT NULL,   -- 1=Migrated to Existing Title/Entry Record, 2=Migrated as New Record
        SdStatus                INT                    NOT NULL,   -- 1=All Migrated, 2=Partially Duplicate SD, 3=All Duplicate SD
        MigratedToRdName        NVARCHAR(150)          NOT NULL,
        CONSTRAINT PK_MigrationRecords PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_MigrationRecords_01 UNIQUE (RequestNumber),
        -- DeleteBehavior.Restrict in MigrationRecordConfiguration.cs: SQL
        -- Server's default NO ACTION on a plain FK already matches this.
        CONSTRAINT FK_MigrationRecords_01 FOREIGN KEY (OcrExtractionRecordId) REFERENCES dbo.OcrExtractionRecords (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MigrationRecords_01' AND object_id = OBJECT_ID(N'dbo.MigrationRecords'))
    CREATE INDEX IX_MigrationRecords_01 ON dbo.MigrationRecords (MigrationDate DESC); -- default sort, most-recent-first
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MigrationRecords_02' AND object_id = OBJECT_ID(N'dbo.MigrationRecords'))
    CREATE INDEX IX_MigrationRecords_02 ON dbo.MigrationRecords (MigrationStatus); -- status filter
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MigrationRecords_03' AND object_id = OBJECT_ID(N'dbo.MigrationRecords'))
    CREATE INDEX IX_MigrationRecords_03 ON dbo.MigrationRecords (OcrExtractionRecordId);
GO

-------------------------------------------------------------------------------
-- 18. MigrationDocuments  (MigrationDocumentConfiguration.cs -
--     MigrationRecordId FK is DeleteBehavior.Cascade; PerformedByUserId is a
--     plain denormalized int, no FK; DocumentTypeId FK is DeleteBehavior.Restrict)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.MigrationDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MigrationDocuments
    (
        Id                      INT IDENTITY(1,1)  NOT NULL,
        MigrationRecordId       INT                 NOT NULL,
        DocumentTypeId          INT                 NULL,
        DocumentName            NVARCHAR(200)       NOT NULL,
        FileName                NVARCHAR(260)       NOT NULL,
        Status                  INT                 NOT NULL,   -- 1=Migrated, 2=Duplicate SD, 3=Overwritten, 4=Inserted as New
        ExistingFileName        NVARCHAR(260)       NULL,
        PerformedByUserId       INT                 NULL,
        PerformedByUsername     NVARCHAR(256)       NULL,
        ActionDate               DATETIME2           NULL,
        CONSTRAINT PK_MigrationDocuments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_MigrationDocuments_01 FOREIGN KEY (MigrationRecordId) REFERENCES dbo.MigrationRecords (Id) ON DELETE CASCADE,
        CONSTRAINT FK_MigrationDocuments_02 FOREIGN KEY (DocumentTypeId) REFERENCES dbo.DocumentTypes (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MigrationDocuments_01' AND object_id = OBJECT_ID(N'dbo.MigrationDocuments'))
    CREATE INDEX IX_MigrationDocuments_01 ON dbo.MigrationDocuments (MigrationRecordId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MigrationDocuments_02' AND object_id = OBJECT_ID(N'dbo.MigrationDocuments'))
    CREATE INDEX IX_MigrationDocuments_02 ON dbo.MigrationDocuments (DocumentTypeId);
GO

-------------------------------------------------------------------------------
-- 19. BrandingSettings  (AuditableEntity; BrandingSettingConfiguration.cs -
--     single-row config table, app-enforced, no DB constraint limits it to 1 row)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.BrandingSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BrandingSettings
    (
        Id                  INT IDENTITY(1,1)      NOT NULL,
        ImagePath           NVARCHAR(260)          NULL,
        CreatedAt           DATETIME2              NOT NULL CONSTRAINT DF_BrandingSettings_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT                    NULL,
        UpdatedAt           DATETIME2              NULL,
        UpdatedBy           INT                    NULL,
        RowVersion          ROWVERSION             NOT NULL,
        CONSTRAINT PK_BrandingSettings PRIMARY KEY CLUSTERED (Id)
    );
END
GO

-------------------------------------------------------------------------------
-- 20. EmailTemplates  (AuditableEntity; EmailTemplateConfiguration.cs)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.EmailTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailTemplates
    (
        Id                  INT IDENTITY(1,1)      NOT NULL,
        [Key]               NVARCHAR(64)           NOT NULL,
        Label               NVARCHAR(128)          NOT NULL,
        Recipients          NVARCHAR(500)          NOT NULL,
        Subject             NVARCHAR(200)          NOT NULL,
        Body                NVARCHAR(4000)         NOT NULL,
        CreatedAt           DATETIME2              NOT NULL CONSTRAINT DF_EmailTemplates_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT                    NULL,
        UpdatedAt           DATETIME2              NULL,
        UpdatedBy           INT                    NULL,
        RowVersion          ROWVERSION             NOT NULL,
        CONSTRAINT PK_EmailTemplates PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_EmailTemplates_01 UNIQUE ([Key])
    );
END
GO

-------------------------------------------------------------------------------
-- 21. SessionSettings  (AuditableEntity; SessionSettingConfiguration.cs -
--     single-row config table, app-enforced, no DB constraint limits it to 1 row)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.SessionSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SessionSettings
    (
        Id                  INT IDENTITY(1,1)      NOT NULL,
        TimeoutMinutes      INT                    NOT NULL,
        Action              INT                    NOT NULL,
        CreatedAt           DATETIME2              NOT NULL CONSTRAINT DF_SessionSettings_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT                    NULL,
        UpdatedAt           DATETIME2              NULL,
        UpdatedBy           INT                    NULL,
        RowVersion          ROWVERSION             NOT NULL,
        CONSTRAINT PK_SessionSettings PRIMARY KEY CLUSTERED (Id)
    );
END
GO

-------------------------------------------------------------------------------
-- PERMISSIONS  (Guideline A.6.b - no PUBLIC grants; explicit TechUser grant)
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'lares_sd_user')
BEGIN
    CREATE USER lares_sd_user FOR LOGIN lares_sd_user;
END
GO

IF IS_ROLEMEMBER(N'db_owner', N'lares_sd_user') = 0
BEGIN
    ALTER ROLE db_owner ADD MEMBER lares_sd_user;
END
GO

-- Guideline A.6.b.1 requires that table permissions not be granted to PUBLIC.
-- PUBLIC receives no explicit GRANT anywhere in this script, which already
-- satisfies that rule under SQL Server's default-deny model. Do NOT add an
-- explicit DENY to PUBLIC here: DENY overrides every other permission,
-- including db_owner membership (every user, db_owner members included, is
-- implicitly a member of PUBLIC), so it would silently lock the TechUser
-- grant above out of their own database. (Verified live on 2026-08-14: it did
-- exactly that on first deployment, and was reverted.)

/* ============================================================================
   END OF SCHEMA SCRIPT
   Next: 02_Lares_DCT_SD_qa_SeedData_QA_ONLY.sql (QA sample data - do not run
   against a production target).
   ============================================================================ */
