/* =============================================================================
   DCT_SD - Database Upgrade Script (Incremental Release)
   File: DCT_SD_Database_Upgrade.sql
   Companion rollback: dct_sd_main_rollback_section23.sql

   ---------------------------------------------------------------------------
   SCOPE OF THIS RELEASE
   ---------------------------------------------------------------------------
   This script contains ONLY the database changes introduced since the
   client's database was last updated. It does NOT recreate, alter, or touch
   any of the application's existing main tables or their data.

   What this release adds (purely additive, no existing object is changed):
     - 11 new "_Log" tables  (one per main table) - a lightweight ledger of
       every INSERT/UPDATE/DELETE: who, when, from where.
     - 11 new "_Hist" tables (one per main table) - before/after JSON
       snapshots, written only on UPDATE/DELETE (an INSERT's "after" state
       is just the row itself, so no history row is needed for inserts).
     - 33 new triggers (3 per main table: trg_<Table>_I, trg_<Table>_U,
       trg_<Table>_D) that populate the tables above automatically.
     - 33 new indexes supporting the tables above (1 per _Log table, 2 per
       _Hist table).
     - 22 new GRANT statements giving the application's technical user
       (DCTSDUser) the minimum required access to the new tables
       (SELECT+INSERT on _Hist, SELECT only on _Log - triggers write to
       _Log as the table owner, independent of the caller's own grants).

   The 11 main tables covered are: Users, RefreshTokens, AppSettings,
   CodeLookups, FetchRuns, OcrExtractionRecords, ManualValidationRequests,
   MigrationRecords, MigrationDocuments, EmptyFolderRecords,
   FailedExtractionRecords. These are the only tables the running
   application's EF Core model actively writes to; the schema and naming
   conventions here (column order AppName/IPAddr/HostName/Date/Activity,
   Hist/Log suffixing, trg_<Table>_<DMLType> naming) follow the DB_Coding
   Standards & Guidelines document sections 6.d.5-d.9, 6.e.1 and 6.e.3,
   adapted from the guideline's literal Sybase-replication wording
   (Replicated_RoD_ID dropped - N/A for this single-instance SQL Server
   app; univarchar/numeric(30) identity become nvarchar/int identity).

   NOT included in this release (nothing to do - already present):
     - No new columns, tables, or indexes on any of the 11 main tables
       themselves. (EntryNumberRange on OcrExtractionRecords and
       ManualValidationRequests was already added to the database in an
       earlier release and already holds live data - this release only
       changed how the C# application code reads that already-existing
       column; there is nothing for a DBA to apply for that change.)
     - No stored procedures were added or changed.

   ---------------------------------------------------------------------------
   PREREQUISITES
   ---------------------------------------------------------------------------
   1. The 11 main tables listed above must already exist with their current
      production schema (this script checks for them below and will tell
      you plainly if one is missing - do not proceed past a failed check).
   2. The [DCTSDUser] database user must already exist (created by your DBA
      team per your existing process - this script does not create logins
      or users, only grants permissions to an existing one).
   3. Update the USE statement immediately below to your database's actual
      name before running.

   ---------------------------------------------------------------------------
   SAFETY / IDEMPOTENCY
   ---------------------------------------------------------------------------
   Every CREATE below is guarded (IF OBJECT_ID(...) IS NULL), so this script
   can be re-run safely if it is interrupted partway through - objects
   already created on a prior run are simply skipped, nothing is duplicated
   or overwritten. Purely additive: no DROP, no ALTER, no UPDATE/DELETE
   against any table, so no existing row in any table (old or new) is ever
   at risk of being lost by running this script.

   A note on transactions: SQL Server does not allow a single explicit
   transaction (or a single TRY/CATCH block) to span multiple GO-separated
   batches combined with dynamic SQL the way CREATE TRIGGER requires here
   (CREATE TRIGGER must be the only statement in its batch, which is why
   each trigger below is created via EXEC('...') instead of a literal
   CREATE TRIGGER statement). Each individual CREATE TABLE / CREATE INDEX /
   trigger-creating EXEC is already atomic on its own (a single DDL
   statement cannot partially apply in SQL Server), and the guard on every
   one of them is what makes a partial, interrupted run safe to simply
   re-run to completion - this is the same pattern the rest of this
   project's deployment scripts already use. Take the backup recommended
   below as your safety net for anything outside what per-statement
   atomicity and idempotency already cover.
   ============================================================================= */

USE [DCT_SD];  -- <-- UPDATE this to your actual database name before running
GO

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =============================================================================
   PRE-DEPLOYMENT CHECKS
   Run this block first and review the results before continuing. This does
   not change anything - it only reports what it finds.
   ============================================================================= */
PRINT '--- Pre-deployment checks ---';

DECLARE @MissingMainTables NVARCHAR(MAX) = '';
SELECT @MissingMainTables = @MissingMainTables + t.TableName + ', '
FROM (VALUES ('Users'),('RefreshTokens'),('AppSettings'),('CodeLookups'),
             ('FetchRuns'),('OcrExtractionRecords'),('ManualValidationRequests'),
             ('MigrationRecords'),('MigrationDocuments'),('EmptyFolderRecords'),
             ('FailedExtractionRecords')) AS t(TableName)
WHERE OBJECT_ID('dbo.' + t.TableName, 'U') IS NULL;

IF LEN(@MissingMainTables) > 0
BEGIN
    PRINT 'FAIL: the following prerequisite main table(s) do not exist: ' + LEFT(@MissingMainTables, LEN(@MissingMainTables) - 1);
    PRINT 'STOP - do not run the rest of this script until this is resolved. This upgrade only adds Log/History tables on top of these main tables; it cannot run without them.';
END
ELSE
BEGIN
    PRINT 'OK: all 11 prerequisite main tables exist.';
END;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'DCTSDUser')
    PRINT 'WARNING: database user [DCTSDUser] does not exist yet in this database - the GRANT statements near the end of this script will fail until it is created. Create it first, per your existing process, then re-run this script (it is safe to re-run).';
ELSE
    PRINT 'OK: database user [DCTSDUser] exists.';

-- Informational only: lists which of the 22 new tables/33 new triggers this
-- release adds are already present (e.g. from a prior partial run). Any
-- rows returned here will simply be skipped (not recreated) below.
SELECT 'Already exists (will be skipped)' AS Note, name AS ObjectName, type_desc
FROM sys.objects
WHERE type IN ('U','TR')
  AND (name LIKE '%\_Log' ESCAPE '\' OR name LIKE '%\_Hist' ESCAPE '\' OR name LIKE 'trg\_%\_I' ESCAPE '\' OR name LIKE 'trg\_%\_U' ESCAPE '\' OR name LIKE 'trg\_%\_D' ESCAPE '\')
  AND name IN (
      'Users_Log','Users_Hist','RefreshTokens_Log','RefreshTokens_Hist',
      'AppSettings_Log','AppSettings_Hist','CodeLookups_Log','CodeLookups_Hist',
      'FetchRuns_Log','FetchRuns_Hist','OcrExtractionRecords_Log','OcrExtractionRecords_Hist',
      'ManualValidationRequests_Log','ManualValidationRequests_Hist','MigrationRecords_Log','MigrationRecords_Hist',
      'MigrationDocuments_Log','MigrationDocuments_Hist','EmptyFolderRecords_Log','EmptyFolderRecords_Hist',
      'FailedExtractionRecords_Log','FailedExtractionRecords_Hist',
      'trg_Users_I','trg_Users_U','trg_Users_D','trg_RefreshTokens_I','trg_RefreshTokens_U','trg_RefreshTokens_D',
      'trg_AppSettings_I','trg_AppSettings_U','trg_AppSettings_D','trg_CodeLookups_I','trg_CodeLookups_U','trg_CodeLookups_D',
      'trg_FetchRuns_I','trg_FetchRuns_U','trg_FetchRuns_D','trg_OcrExtractionRecords_I','trg_OcrExtractionRecords_U','trg_OcrExtractionRecords_D',
      'trg_ManualValidationRequests_I','trg_ManualValidationRequests_U','trg_ManualValidationRequests_D',
      'trg_MigrationRecords_I','trg_MigrationRecords_U','trg_MigrationRecords_D',
      'trg_MigrationDocuments_I','trg_MigrationDocuments_U','trg_MigrationDocuments_D',
      'trg_EmptyFolderRecords_I','trg_EmptyFolderRecords_U','trg_EmptyFolderRecords_D',
      'trg_FailedExtractionRecords_I','trg_FailedExtractionRecords_U','trg_FailedExtractionRecords_D'
  );

-- Pre-upgrade row counts for the 11 main tables - keep this output. Compare
-- it against the same query's output after the upgrade (Post-Deployment
-- Validation, below) to confirm no existing data was touched.
SELECT 'PRE-UPGRADE ROW COUNTS' AS Note, t.name AS TableName, SUM(p.rows) AS [RowCount]
FROM sys.tables t
JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0,1)
WHERE t.name IN ('Users','RefreshTokens','AppSettings','CodeLookups','FetchRuns',
                  'OcrExtractionRecords','ManualValidationRequests','MigrationRecords',
                  'MigrationDocuments','EmptyFolderRecords','FailedExtractionRecords')
GROUP BY t.name
ORDER BY t.name;
GO
-- NEW OBJECTS below: reproduced verbatim from Section 23 of dct_sd_main.sql.

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

/* =============================================================================
   POST-DEPLOYMENT VALIDATION
   Run this block after the script above completes and review the results.
   ============================================================================= */
PRINT '--- Post-deployment validation ---';

-- Expect exactly 22 (11 tables x _Log + _Hist).
SELECT 'New tables created' AS Check_, COUNT(*) AS ActualCount, 22 AS ExpectedCount
FROM sys.tables
WHERE name IN (
    'Users_Log','Users_Hist','RefreshTokens_Log','RefreshTokens_Hist',
    'AppSettings_Log','AppSettings_Hist','CodeLookups_Log','CodeLookups_Hist',
    'FetchRuns_Log','FetchRuns_Hist','OcrExtractionRecords_Log','OcrExtractionRecords_Hist',
    'ManualValidationRequests_Log','ManualValidationRequests_Hist','MigrationRecords_Log','MigrationRecords_Hist',
    'MigrationDocuments_Log','MigrationDocuments_Hist','EmptyFolderRecords_Log','EmptyFolderRecords_Hist',
    'FailedExtractionRecords_Log','FailedExtractionRecords_Hist'
);

-- Expect exactly 33 (11 tables x 3 triggers: _I, _U, _D).
SELECT 'New triggers created' AS Check_, COUNT(*) AS ActualCount, 33 AS ExpectedCount
FROM sys.triggers
WHERE name LIKE 'trg\_%\_I' ESCAPE '\' OR name LIKE 'trg\_%\_U' ESCAPE '\' OR name LIKE 'trg\_%\_D' ESCAPE '\';

-- Expect exactly 55: each of the 22 new tables gets 1 PRIMARY KEY (clustered)
-- index automatically, plus the 33 explicit nonclustered indexes created above
-- (11 x 1 on _Log tables, 11 x 2 on _Hist tables) = 22 + 33 = 55.
SELECT 'New indexes created' AS Check_, COUNT(*) AS ActualCount, 55 AS ExpectedCount
FROM sys.indexes i
JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name IS NOT NULL
  AND t.name IN (
    'Users_Log','Users_Hist','RefreshTokens_Log','RefreshTokens_Hist',
    'AppSettings_Log','AppSettings_Hist','CodeLookups_Log','CodeLookups_Hist',
    'FetchRuns_Log','FetchRuns_Hist','OcrExtractionRecords_Log','OcrExtractionRecords_Hist',
    'ManualValidationRequests_Log','ManualValidationRequests_Hist','MigrationRecords_Log','MigrationRecords_Hist',
    'MigrationDocuments_Log','MigrationDocuments_Hist','EmptyFolderRecords_Log','EmptyFolderRecords_Hist',
    'FailedExtractionRecords_Log','FailedExtractionRecords_Hist'
  );

-- Expect exactly 33: SQL Server records GRANT SELECT, INSERT as two separate
-- permission rows, not one - 11 _Hist tables x 2 rows (SELECT + INSERT) = 22,
-- plus 11 _Log tables x 1 row (SELECT) = 11. Total 22 + 11 = 33.
SELECT 'New grants to DCTSDUser' AS Check_, COUNT(*) AS ActualCount, 33 AS ExpectedCount
FROM sys.database_permissions p
JOIN sys.objects o ON p.major_id = o.object_id
JOIN sys.database_principals dp ON p.grantee_principal_id = dp.principal_id
WHERE dp.name = 'DCTSDUser'
  AND o.name IN (
    'Users_Log','Users_Hist','RefreshTokens_Log','RefreshTokens_Hist',
    'AppSettings_Log','AppSettings_Hist','CodeLookups_Log','CodeLookups_Hist',
    'FetchRuns_Log','FetchRuns_Hist','OcrExtractionRecords_Log','OcrExtractionRecords_Hist',
    'ManualValidationRequests_Log','ManualValidationRequests_Hist','MigrationRecords_Log','MigrationRecords_Hist',
    'MigrationDocuments_Log','MigrationDocuments_Hist','EmptyFolderRecords_Log','EmptyFolderRecords_Hist',
    'FailedExtractionRecords_Log','FailedExtractionRecords_Hist'
  );

-- Post-upgrade row counts for the 11 main tables - compare this output,
-- table by table, against the "PRE-UPGRADE ROW COUNTS" output captured
-- during Pre-Deployment Checks above. Every number must match exactly;
-- any difference means something other than this script changed data
-- during the deployment window and should be investigated before sign-off.
SELECT 'POST-UPGRADE ROW COUNTS' AS Note, t.name AS TableName, SUM(p.rows) AS [RowCount]
FROM sys.tables t
JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0,1)
WHERE t.name IN ('Users','RefreshTokens','AppSettings','CodeLookups','FetchRuns',
                  'OcrExtractionRecords','ManualValidationRequests','MigrationRecords',
                  'MigrationDocuments','EmptyFolderRecords','FailedExtractionRecords')
GROUP BY t.name
ORDER BY t.name;

-- Smoke test: confirms the new tables are queryable (expect 0 rows on a
-- freshly-created table, which is correct - they only fill up as the
-- application does normal inserts/updates/deletes afterwards).
SELECT TOP 1 'Users_Log is queryable' AS Smoke_Test, COUNT(*) AS [RowCount] FROM dbo.Users_Log;
SELECT TOP 1 'Users_Hist is queryable' AS Smoke_Test, COUNT(*) AS [RowCount] FROM dbo.Users_Hist;

PRINT '--- DCT_SD_Database_Upgrade.sql completed. Review all Check_/ActualCount rows above: ActualCount must equal ExpectedCount on every row, and PRE/POST row counts must match exactly per table. ---';
GO
