# Production rollout: applying the database redesign to HRBTK
Version: 1.0 | Date: 2026-11-23
Applies to: production database `HRBTK` (SQL Server 2022, server "Teezhost 16GB", https://hr.coditium.com)
Status: **Not run on production.** Everything below has been run only against a local copy of production (`TAZ\SQLEXPRESS`, test data): the full sequence once, then each script re-run to confirm it changes nothing the second time.

Source of truth for *what* changes: `db_changes.md`. This file is *how* to apply it. Scripts live in `scripts/database/redesign/`.

## 1. What has been tested, and what has not
| Tested on the local copy | Not tested |
|---|---|
| Scripts 01-04 and 06 run clean in order; each re-runs with no changes (01-04 before 06; 06 again after itself). | Production data volume and timing (the copy was 144 MB; expect a short run). |
| The app (Development environment) loads all pages and the create/edit/upload flows against the new schema. | The IIS deployment of the new build, and `appsettings.Production.json` on the server. |
| Tax-year logic, unique/duplicate guards, concurrency tokens, file-hash checks. | Anything that depends on the **real** data (see section 3: the copy is test data). |

## 2. Rules for this rollout
1. **One maintenance window, database and app together.** The old app cannot run on the new schema: from script 02 on, columns it reads as text (`ApplyTax`, `DOB`, `Users.CreatedOn`, leave status, payslip month...) are typed columns, and script 06 removes the old columns. There is no "database first, app later" option.
2. **Back up first, and test the restore.** Take a full backup of `HRBTK` (`BACKUP DATABASE ... WITH COPY_ONLY, CHECKSUM`) and keep the current IIS publish folder. Rollback is: restore the backup + redeploy the old build (section 6). Scripts do not have undo scripts.
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
- Open items that do not block the rollout: T4, T7, B1, B5-B10, B12-B18 (see decisions.md).

**Checks that must be clean (or consciously handled)**
| Check | Why / what the script does |
|---|---|
| `Users.Role` values other than `Admin`, `HR` or empty | Script 03 stops; the new CHECK allows only Admin and HR. |
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
2. Full backup of `HRBTK` (+ copy the current publish folder, and `wwwroot/uploads`: **`DeployToIIS.bat` wipes the deploy folder including uploads**, so back them up and restore them after publishing).
3. Run, one at a time, checking the output and exit code after each:
   ```
   sqlcmd -S <server> -d HRBTK -U <user> -P <pwd> -C -b -I -i scripts\database\redesign\01_lookups_and_keys.sql
   ... 02_employee_org.sql      (review mig.EmployeeOrgMap if HR asked to; see section 3)
   ... 03_leave_and_users.sql
   ... 04_attendance_payroll.sql
   ... 06_contract.sql
   ```
   Script 06 is the point of no return for the old app (it drops the old columns). Take a second backup between 04 and 06 if you want a fallback that keeps the old columns.
4. Verify the database (section 5), then publish the new build with `scripts\deploy\DeployToIIS.bat`, restore `wwwroot/uploads`, start the site.
5. Smoke test as an Admin: login, employee list and edit, payroll for one employee, leave request, attendance upload, Setup -> Lists.
6. Open the site to users.

There is no `05_*` script: step 5 in `db_changes.md` ("App switch") is the deployment above.

## 5. Verify after the scripts
Run read-only; all should hold:
```sql
SELECT COUNT(*) FROM dbo.Employee;                                   -- same as before the window
SELECT COUNT(*) FROM dbo.Employee WHERE ProjectId IS NULL OR DepartmentId IS NULL;   -- 0
SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1;     -- 0
SELECT MigrationId FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE '202611230000%' ORDER BY 1;  -- phases 1-4 and 6
```
Also compare row counts of the tables that existed before with the backup. Keep `mig.EmployeeOrgMap` and `mig.LeaveRequests_Orphans` until HR has signed off, then drop the `mig` schema.

## 6. Rollback
- **Before script 06, or after it:** restore the backup taken in step 2 (or the one between 04 and 06), redeploy the old publish folder and uploads, start the site. The scripts hold no state outside the database except the `mig` tables, which the restore removes.
- Do not try to reverse individual scripts by hand.

## 7. After the rollout
- Update `db.md` to the new schema (v2.0, with a Change History entry). `db.md` describes production, so do this only once production has the new schema. The `update-db-docs` skill can regenerate it from a connection string.
- Mark the applied items in `db_changes.md` as applied on production and move answered decisions as the maintenance rules in that file say.
- The two remaining production-security items from AGENTS.md are separate from this rollout: the anonymous `SetupController` and the old `sa` password in git history.
- Not part of this rollout (see `db_changes.md` and the app): attendance processing from punches, half-day / correction / roster / notes screens (tables exist, no pages yet), payroll use of the paid/unpaid flag.
