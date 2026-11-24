# Production rollout: applying the database redesign to HRBTK
Version: 1.3 | Date: 2026-11-24
Applies to: production database `HRBTK` (SQL Server 2022, server "Teezhost 16GB", https://hr.coditium.com)
Status: **Run on production on 2026-11-24** (all ten scripts, in one session, after a verified full backup). Below sections 1-7 stay as the plan that was followed; section 8 records what happened. Before that, everything had been run only against a local copy (`TAZ\SQLEXPRESS`, test data).

Source of truth for *what* changes: `db_changes.md`. This file is *how* to apply it. Scripts live in `scripts/database/redesign/`.

## 1. What has been tested, and what has not
| Tested on the local copy | Not tested |
|---|---|
| Scripts 01-04, 06, 07, 08, 09, 10 and 11 run clean in order; each re-runs with no changes (01-04 before 06; 06 to 11 again after themselves). | Production data volume and timing (the copy was 144 MB; expect a short run). |
| The app (Development environment) loads all pages and the create/edit/upload flows against the new schema. | The IIS deployment of the new build, and `appsettings.Production.json` on the server. |
| Tax-year logic, unique/duplicate guards, concurrency tokens, file-hash checks. | Anything that depends on the **real** data (see section 3: the copy is test data). |

## 2. Rules for this rollout
1. **One maintenance window, database and app together.** The old app cannot run on the new schema: from script 02 on, columns it reads as text (`ApplyTax`, `DOB`, `Users.CreatedOn`, leave status, payslip month...) are typed columns, and script 06 removes the old columns. There is no "database first, app later" option.
2. **Back up first, and test the restore.** Take a full backup of `HRBTK` (`BACKUP DATABASE ... WITH COPY_ONLY, CHECKSUM`) and keep the current IIS publish folder. Rollback is: restore the backup + redeploy the old build (section 6). The old build cannot run after script 07 either (it reads `Users.Role`). Scripts do not have undo scripts.
3. **Run with `sqlcmd -b -I`** (`-I` = QUOTED_IDENTIFIER ON, required for filtered indexes; `-b` = stop at the first error). Each script is a single transaction, so a failure leaves the database as it was before that script.
4. **Production is changed only with your approval before each run** (db_standards S19, AGENTS.md). `sa` credentials come from `appsettings.Production.json` / the server owner, never from a committed file.
5. **Never run the app against production while testing.** Without `ASPNETCORE_ENVIRONMENT=Development` it reads `appsettings.Production.json`.

## 3. Before the window: decisions and checks on the real data
The copy used test data. Re-run these checks read-only on production and resolve each finding first. Scripts 02, 03 and 06 deliberately stop (`THROW`) on most of them.

**HR decisions needed (decisions.md)**
- The **real project list** (B3). Script 02 seeds `Projects` from the distinct `Employee.Project` values; HR edits the list in Setup afterwards, but the names/spellings in the data become the starting rows.
- Department mapping (B2): the 58 distinct `Employee.Department` strings are split at the first ` - ` into department + sub-department, and `Admin` becomes `Administration`. Review `mig.EmployeeOrgMap` (created by script 02) with HR and correct it **before** the Employee update, or fix names in Setup afterwards. Open at go-live: whether `Administrator`, `Mess` and `Transportation` belong under Administration.
- Designations (B4): rank defaults to 100; HR ranks them later in Setup.
- Leave types (B11): seeded from the data; `Unpaid Leave` is marked unpaid, the rest paid. Payroll does not apply it yet.
- Leave (scripts 09 to 11): script 09 rounds leave days to one decimal (no value is changed by it when all are whole or half days; check `SELECT COUNT(*) FROM dbo.LeaveRequests WHERE TotalDays <> ROUND(TotalDays, 1)` first) and adds `CK_LeaveRequests_SingleYear`, which **stops** if a request crosses 31 December: split such requests first (`SELECT Id, EmployeeId, FromDate, ToDate FROM dbo.LeaveRequests WHERE YEAR(FromDate) <> YEAR(ToDate)`). Script 10 drops seven `Employee` columns after archiving them; **the old build reads them, so 10 only runs together with the new build** (as with 06 and 07). The Leave Balance tab needs `LeaveQuota` rows for the current year, otherwise it says no quota is set (Setup them in Leave -> Leaves Quota before opening to users). HR sets carry-forward eligibility and cap per leave type in Setup -> Lists -> Leave types (default: everything carries forward except Unpaid and Compensation, no cap).
- Employee history (script 08): the backfill writes the **current** project, department and designation into each `Joined` row because the original position was never recorded. Nothing to decide; HR can correct history later by voiding and re-recording. Employees without a `DateOfJoining` get no `Joined` row; employees with status `Resigned` / `Terminated` but no `DateOfLeaving` get no leaving row (list them before the window with `SELECT uid, EmployeeID FROM dbo.Employee WHERE EmployeeStatus IN ('Resigned','Terminated') AND DateOfLeaving IS NULL`, and set the dates in the app afterwards through Edit Employee).
- Open items that do not block the rollout: T4, T7, T8, B1, B5-B10, B12-B17, B19 (see decisions.md; B18 is closed).

**Checks that must be clean (or consciously handled)**
| Check | Why / what the script does |
|---|---|
| `Users.Role` values other than `Admin`, `HR` or empty | Script 03 stops; the new CHECK allows only Admin and HR. Script 07 then copies each role into `UserRoles` and drops `Users.Role`; grant extra roles (for example Admin to an HR user) afterwards in Setup → Users, where each user has a tick box per role. |
| `Employee.EmployeeStatus` values outside `Active, Inactive, Resigned, Terminated` | Script 02 stops. |
| `Employee.DOB` / `ModifiedOn` / `Users.CreatedOn` not valid dates | Scripts 02/03 stop and list nothing silently; fix the values first. |
| Designation longer than 100 chars; names/CNIC/mobile longer than the new lengths | Scripts 02 stop. |
| Any employee whose Project or Department text does not map | Script 02 stops (nothing is guessed). |
| `LeaveRequests` rows pointing at a missing employee | Script 03 **moves them to `mig.LeaveRequests_Orphans` and deletes them** from `LeaveRequests` (the FK needs it). Decide with HR first. |
| Overlapping leave requests (copy had 21 and 22) | Not blocked by the database; the app only blocks exact duplicates. Review with HR. |
| Two active allowances of the same name per employee (copy: Fuel Allowance for 325, 376) | No unique key is added; payroll decides. |
| `Users` whose employee ID matches no employee | They keep `EmployeeId = NULL` (no login is lost). |
| Payslip month names that are not English month names | Script 04 stops. |
| `Configuration` duplicate or NULL keys, `Departments` blank/duplicate names | Script 01 stops. |
| `Attendance`, `EmployeeLeaves`, `CarryforwardLeaves`, `GazettedHolidays` not empty | Scripts rebuild/drop these tables only when empty and stop otherwise. If production now has rows, the scripts need a data-migration step first. |

## 4. The window (suggested order)
1. Announce the downtime; stop the IIS site `HRMS` (or put up `app_offline.htm`).
2. Full backup of `HRBTK` (+ copy the current publish folder, and `wwwroot/uploads`: `DeployToIIS.bat` now backs up and restores the uploads itself (check it prints both `[OK]` lines); copy them by hand as well before the window). After the schema changes run `scripts/database/tools/backfill-document-hashes.ps1` so the 8 existing documents get a `FileHash` (the unique per-employee hash index and the duplicate check ignore rows without one).
3. Run, one at a time, checking the output and exit code after each:
   ```
   sqlcmd -S <server> -d HRBTK -U <user> -P <pwd> -C -b -I -i scripts\database\redesign\01_lookups_and_keys.sql
   ... 02_employee_org.sql      (review mig.EmployeeOrgMap if HR asked to; see section 3)
   ... 03_leave_and_users.sql
   ... 04_attendance_payroll.sql
   ... 06_contract.sql
   ... 07_user_roles.sql      (several roles per user; needs 03, independent of 06)
   ... 08_employee_events.sql (employee history table + backfill; needs 02, independent of 06 and 07)
   ... 09_leave_days_one_decimal.sql (leave days decimal(5,1), single-year requests; needs 03)
   ... 10_employee_legacy_leave_archive.sql (archive then drop the 7 Employee leave columns; with the new build)
   ... 11_leave_balances_view_v2.sql (vLeaveBalances v2, LeaveTypes carry-forward flags; needs 09)
   ```
   Script 06 is the point of no return for the old app (it drops the old columns). Take a second backup between 04 and 06 if you want a fallback that keeps the old columns.
4. Verify the database (section 5), then publish the new build with `scripts\deploy\DeployToIIS.bat`, restore `wwwroot/uploads`, start the site.
5. Smoke test as an Admin: login, employee list and edit, **Edit Employee > Employee History (record a test promotion on a throw-away employee, then void it)**, payroll for one employee, leave request, attendance upload, Setup -> Lists.
6. Open the site to users.

There is no `05_*` script: step 5 in `db_changes.md` ("App switch") is the deployment above.

## 5. Verify after the scripts
Run read-only; all should hold:
```sql
SELECT COUNT(*) FROM dbo.Employee;                                   -- same as before the window
SELECT COUNT(*) FROM dbo.Employee WHERE ProjectId IS NULL OR DepartmentId IS NULL;   -- 0
SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1;     -- 0
SELECT MigrationId FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE '2026112%' ORDER BY 1;  -- phases 1-4, 6, 7, 8, 9, 10 and 11
SELECT COUNT(*) FROM dbo.UserRoles;                                 -- one row per user that had a role (6 users, 5 with a role on 2026-11-16)
SELECT COUNT(*) FROM dbo.Users u WHERE NOT EXISTS (SELECT 1 FROM dbo.UserRoles r WHERE r.UserId = u.uid AND r.[Role] = 'Admin');  -- at least one Admin must exist: total users minus this must be >= 1
SELECT COUNT(*) FROM dbo.Employee e WHERE e.DateOfJoining IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.EmployeeEvents v WHERE v.EmployeeId = e.uid AND v.EventType = 'Joined' AND v.IsVoided = 0);  -- 0
SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Employee') AND name IN ('Year2022','Year2023','Year2023New','Year2024','CarryForwardLeaves','CarryForwardLeaves1','AdjustedAjusted');  -- 0 (columns dropped)
SELECT COUNT(*) FROM dbo.EmployeeLegacyLeaveBalances;  -- the number of employees that had any of those values (827 on the test copy)
SELECT COUNT(*) FROM sys.columns WHERE name IN ('TotalDays','CarryForwardDays','AdjustmentDays','OverrideTotalDays') AND OBJECT_NAME(object_id) IN ('LeaveRequests','LeaveQuota','CarryforwardLeaves','LeaveEntitlementOverrides') AND scale <> 1;  -- 0
SELECT COUNT(*) FROM dbo.Employee e WHERE e.DateOfLeaving IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.EmployeeEvents v WHERE v.EmployeeId = e.uid AND v.EventType IN ('Resignation','Termination') AND v.IsVoided = 0);  -- 0
```
Also compare row counts of the tables that existed before with the backup. Keep `mig.EmployeeOrgMap` and `mig.LeaveRequests_Orphans` until HR has signed off, then drop the `mig` schema.

## 6. Rollback
- **Before script 06, or after it:** restore the backup taken in step 2 (or the one between 04 and 06), redeploy the old publish folder and uploads, start the site. The scripts hold no state outside the database except the `mig` tables, which the restore removes.
- Do not try to reverse individual scripts by hand.

## 7. After the rollout
- Roles: script 07 copies each user's single role. Wanted after the rollout (not done by the script): give `farhan` HR as well as Admin in Setup -> Users (already done on the local copy); give any other HR user Admin the same way.
- Update `db.md` to the new schema (v2.0, with a Change History entry). `db.md` describes production, so do this only once production has the new schema. The `update-db-docs` skill can regenerate it from a connection string.
- Mark the applied items in `db_changes.md` as applied on production and move answered decisions as the maintenance rules in that file say.
- Security: the build deployed in this window includes the security fixes (Setup requires Admin, neutral login, `Secure` cookie, logout POST, `/uploads` blocked). After deploying, check: anonymous `/Setup/Users` redirects to login; login works over https; documents still download. Optionally set `EmployeeDocuments:StorageBasePath` and copy `wwwroot/uploads` there. The old `sa` password in git history is **deferred** to the project owner and is separate from this rollout.
- Leave Balance (employee page) is now live: open an employee, check the cards match Leave -> Leave Balance for the same year, and run Leave -> Leave Balance -> Year-end rollover as a **preview only** (do not Apply until HR confirms the rules).
- Employee History is new for HR: show them Edit Employee > Employee History. Recording a promotion, transfer, resignation, termination or rejoin there also updates the employee; voiding the latest entry undoes that change, and only an Admin can delete a voided entry (decision T8).
- Not part of this rollout (see `db_changes.md` and the app): attendance processing from punches, half-day / correction / roster / notes screens (tables exist, no pages yet), payroll use of the paid/unpaid flag.

## 8. Result of the production run (2026-11-24)
- **Pre-flight (read-only):** the production data matched the test copy exactly (848 employees, 6 users, 24 leave requests, 99 payslips, 58 distinct department strings, 8 project values). No blocking finding; the one expected item was 1 leave request pointing at a missing employee. Express edition: backups cannot use `COMPRESSION`.
- **Backups (on the server, default folder `C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup`):** `HRBTK_pre_redesign_20261124.bak` (`COPY_ONLY, CHECKSUM`, `RESTORE VERIFYONLY` passed) and `HRBTK_after04_20261124.bak` (between 04 and 06). Copy them off the server and keep them until HR signs off.
- **Scripts:** 01, 02, 03, 04, 06, 07, 08, 09, 10, 11 each finished without error (`sqlcmd -b -I`, remote server, credentials from `appsettings.Production.json`, never written to a file). Re-running 01 and 06-11 changed nothing. Re-running 02-04 after 06 fails with "Invalid column name" (the old columns are gone); that is expected and harmless, so those three are only re-runnable before 06.
- **Section 5 checks:** 848 employees; no employee without project or department; 0 untrusted foreign keys; 10 redesign rows in `__EFMigrationsHistory`; 5 `UserRoles` (6 users, 4 Admin rows); 0 employees with a join date but no `Joined` event; legacy leave columns gone; archive 828 rows; leave day scale 1 everywhere; 23 leave requests (1 moved to `mig.LeaveRequests_Orphans`); 99 payslips, 132 lines, 23 allowances, 11 deductions, 8 documents unchanged; `vLeaveBalances` 1,582 rows; 8 projects, 48 departments, 10 sub-departments, 209 designations.
- **App test:** the new build (Development environment, connection string for production passed through the environment, no files changed) logged in and loaded employees, employee edit/new, history and leave tabs, Setup (users, config, five lists), payroll (home, run, search, status, dashboard), payslips, allowances, deductions, leave, leave balances, quotas, attendance and salary reports with no errors in the log. Nothing was written to production during the test.
- **Still to do by hand:** deploy the new build with `scripts\deploy\DeployToIIS.bat` and restore `wwwroot/uploads`; run `scripts/database/tools/backfill-document-hashes.ps1` for the 8 documents; enter `LeaveQuota` rows for new years; HR to review `mig.EmployeeOrgMap` and the department/sub-department split, then drop the `mig` schema; copy the backup files off the server.
- **Done after the run (2026-11-24):** `farhan` now holds Admin and HR (row added to `UserRoles`).
