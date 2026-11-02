# Rollback Mapping and Validation

Companion to the two rollback scripts in this folder. Explains exactly what
each one reverses and how to validate it safely in QA before either is ever
run against Production.

## 0. Which script to use

There are two rollback scripts because they answer two different
situations - using the wrong one either does nothing (safety gate aborts)
or is simply the wrong tool for what you're trying to undo:

| Situation | Use | Touches main app tables (Users, FetchRuns, etc.)? | Safe after real users have started using the app? |
|---|---|---|---|
| Deployment just ran, **nobody has used the app on this database yet** (same release/validation window), and you want to undo the *entire* deployment | `dct_sd_main_rollback.sql` | Yes - drops all 18 | No - its safety gate will correctly abort with no changes once those tables hold real rows |
| App has **already gone live with real data**, and you need to remove only the Section 23 audit additions (Hist/Log tables + triggers) - e.g. a trigger is causing problems | `dct_sd_main_rollback_section23.sql` | **No - never reads, writes, or drops any of the 18 main tables** | **Yes** - this is the one built specifically for this case |

If you're not sure which applies: if the application has ever been pointed
at this database with real users, use `dct_sd_main_rollback_section23.sql`.

## 1. What the forward deployment does

Two scripts make up this release, run in order:

| Script | Type | What it does |
|---|---|---|
| `dct_sd_main.sql` | Schema only | Creates 18 main/legacy tables (Sections 1-18), `__EFMigrationsHistory` (Section 22), 22 `_Hist`/`_Log` tables and 11 triggers (Section 23), and issues `GRANT`s to `[DCTSDUser]` (Section 21 + Section 23's grants). No `INSERT`/`UPDATE`/`DELETE` of any kind. |
| `dct_sd_main_data.sql` | Data only | 5 guarded `INSERT`s into `__EFMigrationsHistory`, baselining EF Core's migration history so a future `dotnet ef database update` doesn't try to re-apply migrations against tables that already exist. |

Every object either script creates is **new** - this deploys onto an empty
database. Neither script contains an `ALTER` of a pre-existing object, a
`DROP`, or any write to a table outside `__EFMigrationsHistory`. That's why
"restore to the previous state" is unambiguous here: previous state = these
objects did not exist, and these 5 specific rows did not exist.

## 2. What the rollback script reverses, section by section

`dct_sd_main_rollback.sql` reverses the two scripts above in strict
reverse-dependency order:

| Step | Reverses | Action |
|---|---|---|
| 0 | - | Safety gate (see §3) - aborts with no changes if anything looks like it wasn't part of this deployment's own baseline. |
| 1 | Section 23's grants, then Section 21's grants | `REVOKE` every permission `dct_sd_main.sql` granted, in the same order it granted them (Hist/Log tables first, then the 14 main tables). |
| 2 | Section 23 triggers | `DROP TRIGGER IF EXISTS` for all 33 `trg_<Table>_I`/`_U`/`_D` triggers (3 per table, per guideline 6.e.1). |
| 3 | Section 23 tables | `DROP TABLE IF EXISTS` for all 22 `_Hist`/`_Log` tables. |
| 4 | Section 22 + `dct_sd_main_data.sql`'s 5 rows | `DROP TABLE IF EXISTS dbo.__EFMigrationsHistory` - dropping the table removes the baseline rows with it; there's no separate `DELETE` step because the whole table was created by this deployment. |
| 5 | Sections 1-18 | `DROP TABLE IF EXISTS` for all 18 main/legacy tables, children before parents (FK-safe order: e.g. `MigrationDocuments` before `MigrationRecords` before `OcrExtractionRecords`). Dropping a table removes its PK, FKs, indexes, defaults, and check constraints with it - none of those need a separate statement. |

Nothing here restores a previous *version* of an object, because nothing
this deployment touches had a previous version - every object was newly
created. "Reversal" and "removal" are the same operation for every item in
this table.

## 3. Why running this is safe (the safety gate)

Step 0 runs first, inside the same transaction as everything else, and
checks every table `dct_sd_main.sql` created for rows:

- The 18 main/legacy tables and the 22 `_Hist`/`_Log` tables: any row at all
  trips the gate (they should be empty if nothing has used this deployment
  yet).
- `__EFMigrationsHistory`: not a blanket check - this table is *supposed* to
  hold this release's 5 baseline migration IDs. The gate only trips if a
  migration ID is found that **isn't** one of those 5, meaning EF Core has
  applied something further since deployment.

If any check trips, the script raises an error listing exactly which
objects contain unexpected data and **makes no changes at all** - no
partial rollback, no silent data loss. That satisfies "if the previous data
cannot be reconstructed, identify what is required rather than assuming" -
the script never assumes a table is safe to drop, it verifies first and
refuses to guess.

If the gate trips, the two legitimate next steps are:
1. Export/back up the data the gate flagged (a `SELECT * INTO` or a QA
   backup/restore, outside the scope of this script), then re-run once
   it's handled, or
2. Confirm with whoever owns that environment that the data is disposable
   test data and can be cleared, then re-run.

Either way, this requires a human decision - the script will not make it
for you.

## 4. Validation steps - QA environment (run before Production)

Run these against a QA/test database only, never against `DCT_SD` or any
database the application is live against.

1. **Create or pick an empty test database.** Confirm it has zero tables
   (`SELECT COUNT(*) FROM sys.tables;` should return 0) so you're starting
   from the same "empty database" state this deployment targets.
2. **Point the session at that database** (`USE [YourTestDb];`) - do this
   explicitly before running any of the three scripts; none of them contain
   their own `USE` (deliberate, see each script's header), so the wrong
   target is only ever a copy/paste mistake, not a script bug.
3. **Run `dct_sd_main.sql`.** Confirm it completes with
   `PRINT 'dct_sd_main.sql completed.'` and no errors. Spot-check:
   `SELECT COUNT(*) FROM sys.tables;` should be 41 (18 main/legacy + 1
   `__EFMigrationsHistory` + 22 Hist/Log), and
   `SELECT COUNT(*) FROM sys.triggers;` should be 11.
4. **Run `dct_sd_main_data.sql`.** Confirm
   `SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;` returns 5.
5. **(Optional but recommended) Exercise the triggers before rolling back**,
   to prove the rollback's safety gate actually works, not just that it's
   present:
   - Insert a throwaway row into any one main table covered by Section 23
     (e.g. `dbo.CodeLookups`).
   - Confirm a matching row appears in `dbo.CodeLookups_Log` and
     `dbo.CodeLookups_Hist`.
   - Run `dct_sd_main_rollback.sql` now, while that row still exists, and
     confirm it **aborts** with an error naming `CodeLookups` (and
     `CodeLookups_Log`) - this proves the safety gate actually blocks a
     real rollback attempt when data exists, rather than just existing as
     unused code.
   - Delete the throwaway row (or just drop and recreate the test database)
     before the next step, so the gate has a clean database to work with.
6. **Run `dct_sd_main_rollback.sql` against the clean (no test rows left)
   database.** Confirm it prints
   `'dct_sd_main_rollback.sql completed successfully...'` with no errors.
7. **Confirm full removal:**
   `SELECT COUNT(*) FROM sys.tables;` and `SELECT COUNT(*) FROM sys.triggers;`
   should both be 0, and
   `SELECT COUNT(*) FROM sys.database_permissions WHERE grantee_principal_id = USER_ID('DCTSDUser');`
   should be 0 (if `DCTSDUser` exists in that test database at all).
8. **Confirm idempotency:** run `dct_sd_main_rollback.sql` a second time
   against the now-empty database. It should complete with no errors (every
   `DROP`/`REVOKE` is `IF EXISTS`-guarded) - proving a retry after a partial
   failure elsewhere is safe.
9. **Re-run the full forward sequence** (`dct_sd_main.sql` then
   `dct_sd_main_data.sql`) one more time against the same now-empty
   database, and confirm it reaches the same state as step 3/4. This proves
   the rollback left the database in a state indistinguishable from "never
   deployed," not some intermediate state that would make a second forward
   deployment behave differently.

Only once all nine steps pass in QA should this rollback script be treated
as ready to hold in reserve for a Production deployment of this same
release.

## 5. Running `dct_sd_main_rollback.sql` in Production (only if actually needed)

1. Confirm with the release owner that rollback is in fact the decision
   (not a smaller fix) before running anything.
2. Take a full database backup immediately before running it, regardless of
   what the safety gate says - this is a standard pre-rollback precaution,
   not something the script itself can substitute for.
3. Connect to the exact target database explicitly (`USE [DCT_SD];` or
   whatever Production is actually named) before running
   `dct_sd_main_rollback.sql`.
4. If the safety gate aborts, stop and follow §3 above - do not modify the
   script to bypass the gate.
5. On success, confirm with the application team that the app is fully
   stopped/not pointed at this database before or during the rollback (the
   script does not stop the application for you).

## 6. `dct_sd_main_rollback_section23.sql` - the post-go-live-safe rollback

Use this one instead of the two sections above once the application has
real data in the main tables. It only reverses Section 23 (the 22 `_Hist`/
`_Log` tables, their 11 triggers, and their 22 grants) - it never issues a
single `SELECT`, `INSERT`, `UPDATE`, `DELETE`, or `DROP` against any of the
18 main/legacy tables, and never touches `__EFMigrationsHistory`. There is
no safety gate for the main tables because there's nothing to gate - this
script structurally cannot reach them.

The only data it removes is whatever audit rows the 11 triggers have
already written into the `_Hist`/`_Log` tables themselves (who changed a
row and when). That history is lost once this runs, which is the expected,
accepted cost of turning the audit feature off - it is not data belonging
to the main tables, and the main tables' current rows are completely
unaffected.

### Validation steps - QA environment

1. On a QA database that already has `dct_sd_main.sql` + `dct_sd_main_data.sql`
   applied (or a fresh empty one you deploy the same way), insert a few
   normal rows into the app the usual way (e.g. log in, create a
   `CodeLookup`, save a setting) so the main tables are **not** empty -
   this QA pass is specifically about proving the script leaves that data
   alone, so an empty database doesn't actually prove anything here.
2. Record row counts for every main table you touched
   (`SELECT COUNT(*) FROM dbo.Users;` etc.) before going further.
3. Confirm the Section 23 triggers fired as expected: the matching
   `_Log`/`_Hist` tables should have rows for what you just did.
4. Run `dct_sd_main_rollback_section23.sql`. Confirm it prints
   `'dct_sd_main_rollback_section23.sql completed successfully...'` with no
   errors.
5. **Re-check the row counts from step 2 - they must be identical.** This
   is the actual point of this script: proving nothing in the main tables
   moved.
6. Confirm removal of the Section 23 objects only:
   `SELECT COUNT(*) FROM sys.triggers;` should be 0, and none of the 22
   `_Hist`/`_Log` tables should still exist
   (`SELECT OBJECT_ID('dbo.Users_Hist');` etc. should return `NULL`).
7. Confirm the application itself still works against this database with
   Section 23 removed: log in again, create another `CodeLookup`, save a
   setting - all should succeed exactly as before (no C# change is needed
   for this; see the script's own header for why).
8. **Confirm idempotency:** run `dct_sd_main_rollback_section23.sql` again.
   It should complete with no errors.
9. If you need Section 23 back afterward, re-running `dct_sd_main.sql`
   alone recreates only what's missing (every `CREATE` is guarded with
   `IF OBJECT_ID(...) IS NULL`) - it will skip the 18 main tables (already
   present) and recreate only the Section 23 objects this script removed.

### Running it in Production

1. Confirm the actual problem is with the Section 23 additions specifically
   (e.g. a trigger error, unwanted audit volume) - if the problem is
   elsewhere in the application, this script won't help.
2. Take a full database backup first regardless (standard precaution, not
   a substitute for this script).
3. Connect to the exact target database explicitly before running it.
4. Run `dct_sd_main_rollback_section23.sql`. The application can keep
   running during and after this - it requires no restart and no config
   change, since the trigger removal is transparent to EF Core.
