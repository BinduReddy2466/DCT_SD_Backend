/* =============================================================================
   SECTION 23 - Per-table History (_Hist) and Log (_Log) tables, and their
   triggers, per DB_Coding Standards & Guidelines 6.d.5-d.9, 6.e.1, 6.e.3 and
   6.e.4, and the established earlier-project Hist/Log pattern (full
   main-table column mirror + a plain-int copy of the main table's own PK),
   as reviewed and approved on 2026-11-02.

   Replaces the earlier shared-AuditLog approach (Section 20, deferred) with
   one dedicated _Log and _Hist table per applicable main table.

   For every applicable main table T, T_Log and T_Hist each have:
     - The standard audit columns (Log_ID/Hist_ID, UserID, AppName, IPAddr,
       HostName, Date, Activity/RefNo), per guideline d.5/d.6/d.7/d.8.
     - RecordId INT NULL - a plain copy of T's own PK, NOT FK-constrained,
       so Hist/Log rows survive deletion of the main record.
     - One column per T business column, using T's own data type but always
       NULL (no defaults) - populated only by the trigger below. ROWVERSION
       columns and PERSISTED computed hash columns (used only to index an
       NVARCHAR(MAX) column) are excluded: ROWVERSION is meaningless to
       snapshot, and the hash is redundant once its source column is already
       mirrored - this project's equivalent of guideline e.4's own "except
       for columns that have Image DataType" exception.
     - T_Hist additionally keeps OldValuesJson/NewValuesJson (a JSON
       before/after snapshot pair), in addition to the discrete columns.

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
   MigrationDocuments, EmptyFolderRecords, FailedExtractionRecords).

   EF Core compatibility: SQL Server disallows the OUTPUT clause EF Core
   uses by default on any table with an enabled trigger. All 11 tables'
   entity configurations must have UseSqlOutputClause(false) set (see each
   entity's own Configuration.cs file under Configuration) - this is
   required for these triggers to coexist with the application, not
   optional.

   Idempotent: every CREATE is guarded (IF OBJECT_ID IS NULL), so this
   script can be re-run safely. Purely additive - no DROP, no data loss.
   ============================================================================= */

USE DCT_SD;
GO

-------------------------------------------------------------------------------
-- Users
-------------------------------------------------------------------------------
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
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_Users_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
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

    INSERT INTO dbo.Users_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, FirstName, LastName, Username, PasswordHash, RoleName, MenuPermissionsCsv, Status, FailedLoginAttempts, LastLoginAt, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, LockoutEndUtc)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.FirstName, i.LastName, i.Username, i.PasswordHash, i.RoleName, i.MenuPermissionsCsv, i.Status, i.FailedLoginAttempts, i.LastLoginAt, i.IsDeleted, i.DeletedAt, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy, i.LockoutEndUtc FROM inserted i;
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

    INSERT INTO dbo.Users_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, FirstName, LastName, Username, PasswordHash, RoleName, MenuPermissionsCsv, Status, FailedLoginAttempts, LastLoginAt, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, LockoutEndUtc)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.FirstName, i.LastName, i.Username, i.PasswordHash, i.RoleName, i.MenuPermissionsCsv, i.Status, i.FailedLoginAttempts, i.LastLoginAt, i.IsDeleted, i.DeletedAt, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy, i.LockoutEndUtc FROM inserted i;

    INSERT INTO dbo.Users_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, FirstName, LastName, Username, PasswordHash, RoleName, MenuPermissionsCsv, Status, FailedLoginAttempts, LastLoginAt, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, LockoutEndUtc)
    SELECT @UserId, @IPAddr, i.Username,
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
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.Users_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, FirstName, LastName, Username, PasswordHash, RoleName, MenuPermissionsCsv, Status, FailedLoginAttempts, LastLoginAt, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, LockoutEndUtc)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.FirstName, d.LastName, d.Username, d.PasswordHash, d.RoleName, d.MenuPermissionsCsv, d.Status, d.FailedLoginAttempts, d.LastLoginAt, d.IsDeleted, d.DeletedAt, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy, d.LockoutEndUtc FROM deleted d;

    INSERT INTO dbo.Users_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, FirstName, LastName, Username, PasswordHash, RoleName, MenuPermissionsCsv, Status, FailedLoginAttempts, LastLoginAt, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, LockoutEndUtc)
    SELECT @UserId, @IPAddr, d.Username,
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
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_RefreshTokens_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_RefreshTokens_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_RefreshTokens_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_RefreshTokens_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
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

    INSERT INTO dbo.RefreshTokens_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.UserId, i.TokenHash, i.CreatedAt, i.ExpiresAt, i.RevokedAt, i.ReplacedByTokenHash, i.CreatedByIp FROM inserted i;
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

    INSERT INTO dbo.RefreshTokens_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.UserId, i.TokenHash, i.CreatedAt, i.ExpiresAt, i.RevokedAt, i.ReplacedByTokenHash, i.CreatedByIp FROM inserted i;

    INSERT INTO dbo.RefreshTokens_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp)
    SELECT @UserId, @IPAddr, i.TokenHash,
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
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.RefreshTokens_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.UserId, d.TokenHash, d.CreatedAt, d.ExpiresAt, d.RevokedAt, d.ReplacedByTokenHash, d.CreatedByIp FROM deleted d;

    INSERT INTO dbo.RefreshTokens_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp)
    SELECT @UserId, @IPAddr, d.TokenHash,
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
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_AppSettings_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_AppSettings_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_AppSettings_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_AppSettings_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
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

    INSERT INTO dbo.AppSettings_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, Category, [Key], Label, DataJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.Category, i.[Key], i.Label, i.DataJson, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;
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

    INSERT INTO dbo.AppSettings_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, Category, [Key], Label, DataJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.Category, i.[Key], i.Label, i.DataJson, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;

    INSERT INTO dbo.AppSettings_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, Category, [Key], Label, DataJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, CONCAT(i.Category, N'':'', i.[Key]),
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
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.AppSettings_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, Category, [Key], Label, DataJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.Category, d.[Key], d.Label, d.DataJson, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy FROM deleted d;

    INSERT INTO dbo.AppSettings_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, Category, [Key], Label, DataJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, CONCAT(d.Category, N'':'', d.[Key]),
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
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_CodeLookups_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_CodeLookups_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_CodeLookups_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_CodeLookups_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
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

    INSERT INTO dbo.CodeLookups_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, LookupType, Code, Name, DataJson, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.LookupType, i.Code, i.Name, i.DataJson, i.IsActive, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;
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

    INSERT INTO dbo.CodeLookups_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, LookupType, Code, Name, DataJson, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.LookupType, i.Code, i.Name, i.DataJson, i.IsActive, i.CreatedAt, i.CreatedBy, i.UpdatedAt, i.UpdatedBy FROM inserted i;

    INSERT INTO dbo.CodeLookups_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, LookupType, Code, Name, DataJson, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, CONCAT(i.LookupType, N'':'', i.Code),
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
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.CodeLookups_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, LookupType, Code, Name, DataJson, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.LookupType, d.Code, d.Name, d.DataJson, d.IsActive, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy FROM deleted d;

    INSERT INTO dbo.CodeLookups_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, LookupType, Code, Name, DataJson, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @UserId, @IPAddr, CONCAT(d.LookupType, N'':'', d.Code),
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
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_FetchRuns_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_FetchRuns_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_FetchRuns_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_FetchRuns_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
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

    INSERT INTO dbo.FetchRuns_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, SourcePath, Status, TotalCount, ProcessedCount, LastProcessedFolderPath, LastProcessedAt, ExecutedByUserId, ExecutedByUsername, StartedAt, CompletedAt, RecordKind, FromPath, Remarks, SummaryMessage, RootPath)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.SourcePath, i.Status, i.TotalCount, i.ProcessedCount, i.LastProcessedFolderPath, i.LastProcessedAt, i.ExecutedByUserId, i.ExecutedByUsername, i.StartedAt, i.CompletedAt, i.RecordKind, i.FromPath, i.Remarks, i.SummaryMessage, i.RootPath FROM inserted i;
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

    INSERT INTO dbo.FetchRuns_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, SourcePath, Status, TotalCount, ProcessedCount, LastProcessedFolderPath, LastProcessedAt, ExecutedByUserId, ExecutedByUsername, StartedAt, CompletedAt, RecordKind, FromPath, Remarks, SummaryMessage, RootPath)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.SourcePath, i.Status, i.TotalCount, i.ProcessedCount, i.LastProcessedFolderPath, i.LastProcessedAt, i.ExecutedByUserId, i.ExecutedByUsername, i.StartedAt, i.CompletedAt, i.RecordKind, i.FromPath, i.Remarks, i.SummaryMessage, i.RootPath FROM inserted i;

    INSERT INTO dbo.FetchRuns_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, SourcePath, Status, TotalCount, ProcessedCount, LastProcessedFolderPath, LastProcessedAt, ExecutedByUserId, ExecutedByUsername, StartedAt, CompletedAt, RecordKind, FromPath, Remarks, SummaryMessage, RootPath)
    SELECT @UserId, @IPAddr, CONCAT(i.RecordKind, N'':'', CONVERT(NVARCHAR(30), i.StartedAt, 126)),
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
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FetchRuns_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, SourcePath, Status, TotalCount, ProcessedCount, LastProcessedFolderPath, LastProcessedAt, ExecutedByUserId, ExecutedByUsername, StartedAt, CompletedAt, RecordKind, FromPath, Remarks, SummaryMessage, RootPath)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.SourcePath, d.Status, d.TotalCount, d.ProcessedCount, d.LastProcessedFolderPath, d.LastProcessedAt, d.ExecutedByUserId, d.ExecutedByUsername, d.StartedAt, d.CompletedAt, d.RecordKind, d.FromPath, d.Remarks, d.SummaryMessage, d.RootPath FROM deleted d;

    INSERT INTO dbo.FetchRuns_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, SourcePath, Status, TotalCount, ProcessedCount, LastProcessedFolderPath, LastProcessedAt, ExecutedByUserId, ExecutedByUsername, StartedAt, CompletedAt, RecordKind, FromPath, Remarks, SummaryMessage, RootPath)
    SELECT @UserId, @IPAddr, CONCAT(d.RecordKind, N'':'', CONVERT(NVARCHAR(30), d.StartedAt, 126)),
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
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionRecords_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_OcrExtractionRecords_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_OcrExtractionRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
        RequestNumber NVARCHAR(30) NULL,
        FetchRunId INT NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        FolderPath NVARCHAR(MAX) NULL,
        EntryNumbersCsv NVARCHAR(MAX) NULL,
        TitleNumber NVARCHAR(50) NULL,
        TitleType INT NULL,
        DocumentCount INT NULL,
        ExtractionStatus INT NULL,
        ExtractionDateTime DATETIME2 NULL,
        DocumentsJson NVARCHAR(MAX) NULL,
        EntryNumberRange NVARCHAR(MAX) NULL
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_OcrExtractionRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
        RequestNumber NVARCHAR(30) NULL,
        FetchRunId INT NULL,
        RdCode NVARCHAR(20) NULL,
        RdName NVARCHAR(150) NULL,
        FolderPath NVARCHAR(MAX) NULL,
        EntryNumbersCsv NVARCHAR(MAX) NULL,
        TitleNumber NVARCHAR(50) NULL,
        TitleType INT NULL,
        DocumentCount INT NULL,
        ExtractionStatus INT NULL,
        ExtractionDateTime DATETIME2 NULL,
        DocumentsJson NVARCHAR(MAX) NULL,
        EntryNumberRange NVARCHAR(MAX) NULL
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

    INSERT INTO dbo.OcrExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RequestNumber, FetchRunId, RdCode, RdName, FolderPath, EntryNumbersCsv, TitleNumber, TitleType, DocumentCount, ExtractionStatus, ExtractionDateTime, DocumentsJson, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.RequestNumber, i.FetchRunId, i.RdCode, i.RdName, i.FolderPath, i.EntryNumbersCsv, i.TitleNumber, i.TitleType, i.DocumentCount, i.ExtractionStatus, i.ExtractionDateTime, i.DocumentsJson, i.EntryNumberRange FROM inserted i;
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

    INSERT INTO dbo.OcrExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RequestNumber, FetchRunId, RdCode, RdName, FolderPath, EntryNumbersCsv, TitleNumber, TitleType, DocumentCount, ExtractionStatus, ExtractionDateTime, DocumentsJson, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.RequestNumber, i.FetchRunId, i.RdCode, i.RdName, i.FolderPath, i.EntryNumbersCsv, i.TitleNumber, i.TitleType, i.DocumentCount, i.ExtractionStatus, i.ExtractionDateTime, i.DocumentsJson, i.EntryNumberRange FROM inserted i;

    INSERT INTO dbo.OcrExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, RequestNumber, FetchRunId, RdCode, RdName, FolderPath, EntryNumbersCsv, TitleNumber, TitleType, DocumentCount, ExtractionStatus, ExtractionDateTime, DocumentsJson, EntryNumberRange)
    SELECT @UserId, @IPAddr, i.RequestNumber,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.RequestNumber, d.FetchRunId, d.RdCode, d.RdName, d.FolderPath, d.EntryNumbersCsv, d.TitleNumber, d.TitleType, d.DocumentCount, d.ExtractionStatus, d.ExtractionDateTime, d.DocumentsJson, d.EntryNumberRange
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

    INSERT INTO dbo.OcrExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RequestNumber, FetchRunId, RdCode, RdName, FolderPath, EntryNumbersCsv, TitleNumber, TitleType, DocumentCount, ExtractionStatus, ExtractionDateTime, DocumentsJson, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.RequestNumber, d.FetchRunId, d.RdCode, d.RdName, d.FolderPath, d.EntryNumbersCsv, d.TitleNumber, d.TitleType, d.DocumentCount, d.ExtractionStatus, d.ExtractionDateTime, d.DocumentsJson, d.EntryNumberRange FROM deleted d;

    INSERT INTO dbo.OcrExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, RequestNumber, FetchRunId, RdCode, RdName, FolderPath, EntryNumbersCsv, TitleNumber, TitleType, DocumentCount, ExtractionStatus, ExtractionDateTime, DocumentsJson, EntryNumberRange)
    SELECT @UserId, @IPAddr, d.RequestNumber,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.RequestNumber, d.FetchRunId, d.RdCode, d.RdName, d.FolderPath, d.EntryNumbersCsv, d.TitleNumber, d.TitleType, d.DocumentCount, d.ExtractionStatus, d.ExtractionDateTime, d.DocumentsJson, d.EntryNumberRange
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
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_ManualValidationRequests_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_ManualValidationRequests_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_ManualValidationRequests_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_ManualValidationRequests_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
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

    INSERT INTO dbo.ManualValidationRequests_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RequestNumber, OcrExtractionRecordId, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MissingFieldsCsv, DocumentsJson, ExtractionDate, UpdatedByUserId, UpdatedByUsername, UpdatedAt, LockedByUserId, LockedByUsername, LockedAt, MigratedAt, Status, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.RequestNumber, i.OcrExtractionRecordId, i.RdCode, i.RdName, i.EntryNumbersCsv, i.Title, i.TitleType, i.[Plan], i.Block, i.Lot, i.TitleSequence, i.MissingFieldsCsv, i.DocumentsJson, i.ExtractionDate, i.UpdatedByUserId, i.UpdatedByUsername, i.UpdatedAt, i.LockedByUserId, i.LockedByUsername, i.LockedAt, i.MigratedAt, i.Status, i.EntryNumberRange FROM inserted i;
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

    INSERT INTO dbo.ManualValidationRequests_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RequestNumber, OcrExtractionRecordId, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MissingFieldsCsv, DocumentsJson, ExtractionDate, UpdatedByUserId, UpdatedByUsername, UpdatedAt, LockedByUserId, LockedByUsername, LockedAt, MigratedAt, Status, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.RequestNumber, i.OcrExtractionRecordId, i.RdCode, i.RdName, i.EntryNumbersCsv, i.Title, i.TitleType, i.[Plan], i.Block, i.Lot, i.TitleSequence, i.MissingFieldsCsv, i.DocumentsJson, i.ExtractionDate, i.UpdatedByUserId, i.UpdatedByUsername, i.UpdatedAt, i.LockedByUserId, i.LockedByUsername, i.LockedAt, i.MigratedAt, i.Status, i.EntryNumberRange FROM inserted i;

    INSERT INTO dbo.ManualValidationRequests_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, RequestNumber, OcrExtractionRecordId, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MissingFieldsCsv, DocumentsJson, ExtractionDate, UpdatedByUserId, UpdatedByUsername, UpdatedAt, LockedByUserId, LockedByUsername, LockedAt, MigratedAt, Status, EntryNumberRange)
    SELECT @UserId, @IPAddr, i.RequestNumber,
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
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.ManualValidationRequests_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RequestNumber, OcrExtractionRecordId, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MissingFieldsCsv, DocumentsJson, ExtractionDate, UpdatedByUserId, UpdatedByUsername, UpdatedAt, LockedByUserId, LockedByUsername, LockedAt, MigratedAt, Status, EntryNumberRange)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.RequestNumber, d.OcrExtractionRecordId, d.RdCode, d.RdName, d.EntryNumbersCsv, d.Title, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.MissingFieldsCsv, d.DocumentsJson, d.ExtractionDate, d.UpdatedByUserId, d.UpdatedByUsername, d.UpdatedAt, d.LockedByUserId, d.LockedByUsername, d.LockedAt, d.MigratedAt, d.Status, d.EntryNumberRange FROM deleted d;

    INSERT INTO dbo.ManualValidationRequests_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, RequestNumber, OcrExtractionRecordId, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MissingFieldsCsv, DocumentsJson, ExtractionDate, UpdatedByUserId, UpdatedByUsername, UpdatedAt, LockedByUserId, LockedByUsername, LockedAt, MigratedAt, Status, EntryNumberRange)
    SELECT @UserId, @IPAddr, d.RequestNumber,
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
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_MigrationRecords_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_MigrationRecords_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_MigrationRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
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
        SdStatus NVARCHAR(50) NULL
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_MigrationRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
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
        SdStatus NVARCHAR(50) NULL
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

    INSERT INTO dbo.MigrationRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RequestNumber, OcrExtractionRecordId, MigrationDate, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MigratedToRdName, MigrationStatus, SdStatus)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.RequestNumber, i.OcrExtractionRecordId, i.MigrationDate, i.RdCode, i.RdName, i.EntryNumbersCsv, i.Title, i.TitleType, i.[Plan], i.Block, i.Lot, i.TitleSequence, i.MigratedToRdName, i.MigrationStatus, i.SdStatus FROM inserted i;
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

    INSERT INTO dbo.MigrationRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RequestNumber, OcrExtractionRecordId, MigrationDate, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MigratedToRdName, MigrationStatus, SdStatus)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.RequestNumber, i.OcrExtractionRecordId, i.MigrationDate, i.RdCode, i.RdName, i.EntryNumbersCsv, i.Title, i.TitleType, i.[Plan], i.Block, i.Lot, i.TitleSequence, i.MigratedToRdName, i.MigrationStatus, i.SdStatus FROM inserted i;

    INSERT INTO dbo.MigrationRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, RequestNumber, OcrExtractionRecordId, MigrationDate, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MigratedToRdName, MigrationStatus, SdStatus)
    SELECT @UserId, @IPAddr, i.RequestNumber,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.RequestNumber, d.OcrExtractionRecordId, d.MigrationDate, d.RdCode, d.RdName, d.EntryNumbersCsv, d.Title, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.MigratedToRdName, d.MigrationStatus, d.SdStatus
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

    INSERT INTO dbo.MigrationRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RequestNumber, OcrExtractionRecordId, MigrationDate, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MigratedToRdName, MigrationStatus, SdStatus)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.RequestNumber, d.OcrExtractionRecordId, d.MigrationDate, d.RdCode, d.RdName, d.EntryNumbersCsv, d.Title, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.MigratedToRdName, d.MigrationStatus, d.SdStatus FROM deleted d;

    INSERT INTO dbo.MigrationRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, RequestNumber, OcrExtractionRecordId, MigrationDate, RdCode, RdName, EntryNumbersCsv, Title, TitleType, [Plan], Block, Lot, TitleSequence, MigratedToRdName, MigrationStatus, SdStatus)
    SELECT @UserId, @IPAddr, d.RequestNumber,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.RequestNumber, d.OcrExtractionRecordId, d.MigrationDate, d.RdCode, d.RdName, d.EntryNumbersCsv, d.Title, d.TitleType, d.[Plan], d.Block, d.Lot, d.TitleSequence, d.MigratedToRdName, d.MigrationStatus, d.SdStatus
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
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_MigrationDocuments_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_MigrationDocuments_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_MigrationDocuments_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
        MigrationRecordId INT NULL,
        CodeLookupId INT NULL,
        DocumentName NVARCHAR(MAX) NULL,
        FileName NVARCHAR(MAX) NULL,
        Status INT NULL,
        ExistingFileName NVARCHAR(MAX) NULL,
        PerformedByUserId INT NULL,
        PerformedByUsername NVARCHAR(MAX) NULL,
        ActionDate DATETIME2 NULL
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_MigrationDocuments_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
        MigrationRecordId INT NULL,
        CodeLookupId INT NULL,
        DocumentName NVARCHAR(MAX) NULL,
        FileName NVARCHAR(MAX) NULL,
        Status INT NULL,
        ExistingFileName NVARCHAR(MAX) NULL,
        PerformedByUserId INT NULL,
        PerformedByUsername NVARCHAR(MAX) NULL,
        ActionDate DATETIME2 NULL
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

    INSERT INTO dbo.MigrationDocuments_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, MigrationRecordId, CodeLookupId, DocumentName, FileName, Status, ExistingFileName, PerformedByUserId, PerformedByUsername, ActionDate)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.MigrationRecordId, i.CodeLookupId, i.DocumentName, i.FileName, i.Status, i.ExistingFileName, i.PerformedByUserId, i.PerformedByUsername, i.ActionDate FROM inserted i;
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

    INSERT INTO dbo.MigrationDocuments_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, MigrationRecordId, CodeLookupId, DocumentName, FileName, Status, ExistingFileName, PerformedByUserId, PerformedByUsername, ActionDate)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.MigrationRecordId, i.CodeLookupId, i.DocumentName, i.FileName, i.Status, i.ExistingFileName, i.PerformedByUserId, i.PerformedByUsername, i.ActionDate FROM inserted i;

    INSERT INTO dbo.MigrationDocuments_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, MigrationRecordId, CodeLookupId, DocumentName, FileName, Status, ExistingFileName, PerformedByUserId, PerformedByUsername, ActionDate)
    SELECT @UserId, @IPAddr, i.FileName,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           (SELECT i2.* FROM inserted i2 WHERE i2.Id = i.Id FOR JSON AUTO),
           d.Id, d.MigrationRecordId, d.CodeLookupId, d.DocumentName, d.FileName, d.Status, d.ExistingFileName, d.PerformedByUserId, d.PerformedByUsername, d.ActionDate
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

    INSERT INTO dbo.MigrationDocuments_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, MigrationRecordId, CodeLookupId, DocumentName, FileName, Status, ExistingFileName, PerformedByUserId, PerformedByUsername, ActionDate)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.MigrationRecordId, d.CodeLookupId, d.DocumentName, d.FileName, d.Status, d.ExistingFileName, d.PerformedByUserId, d.PerformedByUsername, d.ActionDate FROM deleted d;

    INSERT INTO dbo.MigrationDocuments_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, MigrationRecordId, CodeLookupId, DocumentName, FileName, Status, ExistingFileName, PerformedByUserId, PerformedByUsername, ActionDate)
    SELECT @UserId, @IPAddr, d.FileName,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.MigrationRecordId, d.CodeLookupId, d.DocumentName, d.FileName, d.Status, d.ExistingFileName, d.PerformedByUserId, d.PerformedByUsername, d.ActionDate
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
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_EmptyFolderRecords_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_EmptyFolderRecords_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_EmptyFolderRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_EmptyFolderRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
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

    INSERT INTO dbo.EmptyFolderRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RdCode, RdName, FolderName, FolderPath, Status, FetchDateTime)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.RdCode, i.RdName, i.FolderName, i.FolderPath, i.Status, i.FetchDateTime FROM inserted i;
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

    INSERT INTO dbo.EmptyFolderRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RdCode, RdName, FolderName, FolderPath, Status, FetchDateTime)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.RdCode, i.RdName, i.FolderName, i.FolderPath, i.Status, i.FetchDateTime FROM inserted i;

    INSERT INTO dbo.EmptyFolderRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, RdCode, RdName, FolderName, FolderPath, Status, FetchDateTime)
    SELECT @UserId, @IPAddr, i.FolderPath,
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
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.EmptyFolderRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RdCode, RdName, FolderName, FolderPath, Status, FetchDateTime)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.RdCode, d.RdName, d.FolderName, d.FolderPath, d.Status, d.FetchDateTime FROM deleted d;

    INSERT INTO dbo.EmptyFolderRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, RdCode, RdName, FolderName, FolderPath, Status, FetchDateTime)
    SELECT @UserId, @IPAddr, d.FolderPath,
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
        Log_UserID    INT           NULL,
        Log_AppName   NVARCHAR(30)  NULL CONSTRAINT DF_FailedExtractionRecords_Log_AppName DEFAULT (LEFT(APP_NAME(), 30)),
        Log_IPAddr    NVARCHAR(64)  NULL,
        Log_HostName  NVARCHAR(30)  NULL CONSTRAINT DF_FailedExtractionRecords_Log_HostName DEFAULT (LEFT(HOST_NAME(), 30)),
        Log_Date      DATETIME2     NOT NULL CONSTRAINT DF_FailedExtractionRecords_Log_Date DEFAULT (SYSUTCDATETIME()),
        Log_Activity  CHAR(1)       NOT NULL,
        RecordId      INT           NULL,
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
        NewValuesJson  NVARCHAR(MAX) NULL CONSTRAINT CK_FailedExtractionRecords_Hist_New CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        RecordId       INT           NULL,
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

    INSERT INTO dbo.FailedExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RdCode, RdName, FolderName, FolderPath, FailureReason, ExtractionDateTime)
    SELECT @UserId, @IPAddr, ''I'', i.Id, i.RdCode, i.RdName, i.FolderName, i.FolderPath, i.FailureReason, i.ExtractionDateTime FROM inserted i;
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

    INSERT INTO dbo.FailedExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RdCode, RdName, FolderName, FolderPath, FailureReason, ExtractionDateTime)
    SELECT @UserId, @IPAddr, ''U'', i.Id, i.RdCode, i.RdName, i.FolderName, i.FolderPath, i.FailureReason, i.ExtractionDateTime FROM inserted i;

    INSERT INTO dbo.FailedExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, RdCode, RdName, FolderName, FolderPath, FailureReason, ExtractionDateTime)
    SELECT @UserId, @IPAddr, i.FolderPath,
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
    DECLARE @UserId INT = TRY_CAST(SESSION_CONTEXT(N''AppUserId'') AS INT);
    DECLARE @IPAddr NVARCHAR(64) = CAST(SESSION_CONTEXT(N''AppClientIp'') AS NVARCHAR(64));

    INSERT INTO dbo.FailedExtractionRecords_Log (Log_UserID, Log_IPAddr, Log_Activity, RecordId, RdCode, RdName, FolderName, FolderPath, FailureReason, ExtractionDateTime)
    SELECT @UserId, @IPAddr, ''D'', d.Id, d.RdCode, d.RdName, d.FolderName, d.FolderPath, d.FailureReason, d.ExtractionDateTime FROM deleted d;

    INSERT INTO dbo.FailedExtractionRecords_Hist (Hist_UserID, Hist_IPAddr, Hist_RefNo, OldValuesJson, NewValuesJson, RecordId, RdCode, RdName, FolderName, FolderPath, FailureReason, ExtractionDateTime)
    SELECT @UserId, @IPAddr, d.FolderPath,
           (SELECT d2.* FROM deleted d2 WHERE d2.Id = d.Id FOR JSON AUTO),
           NULL,
           d.Id, d.RdCode, d.RdName, d.FolderName, d.FolderPath, d.FailureReason, d.ExtractionDateTime
    FROM deleted d;
END
');
GO


