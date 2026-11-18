# HRMS Database Standards
Version: 1.0 | Date: 2026-11-17 | Applies to: HRBTK (SQL Server 2022) | Related: decisions.md, db_changes.md, db.md

Status: **Rules S1-S23 answered 2026-11-17; applied in db_changes.md v3.0-draft.** Each rule has a **Suggested key** (★ = recommended) and an **Answer** line. When a rule is answered, `Status` becomes `Decided`; once all are decided this file becomes the standard that db_changes.md is checked against, table by table.

## Current state (db.md v1.9): what is inconsistent today
Primary keys have six different names and three casings:

| Pattern | Tables |
|---|---|
| `uid` | Employee, Users |
| `UID` (and no PK) | Configuration, LeaveQuota |
| `uid` (and no PK) | EmployeeLeaves |
| `Id` | Allowances, Deductions, Payslips, PayslipDetails, LeaveRequests, EmployeeDocuments, AttendanceUploadLogs, TaxRules |
| `<Table>ID` | AttendanceID, DepartmentID |
| `LogId` | AuditLogs |
| Composite / natural | CarryforwardLeaves (EmployeeID, Year), GazettedHolidays (HolidayDate), __EFMigrationsHistory |

Other drift: table names are plural except `Employee` and `Attendance`; string types mix varchar(250), nvarchar(MAX) and nvarchar(50); dates are stored as text in several columns; timestamps mix `datetime`, `datetime2(7)` and `time`; `Status` columns are unconstrained text; several tables have no PK or no FK index.

---

## Rules

### S1. Primary key name (Status: Closed)
**Suggested key**
- A ★ Every table uses **`Id`**, `int IDENTITY`, except `Employee` and `Users`, which keep `uid` (decided D1, and `Users.uid` is referenced in the draft). Descriptive names such as `AttendanceID`, `DepartmentID`, `LogId` become `Id`.
- B Every table uses **`<Table>Id`** (`EmployeeId`, `AttendanceId`...). Employee's `uid` would be renamed too. Rejected by D1 unless reopened.
- C Keep each table's current name. No cleanup; inconsistency stays.

Note: renaming a PK column touches EF models and every query. Under A the change per table is small, but it should be done per table in its own migration step, not all at once.

**Answer:** A **Decided by / date:** Rayder / 17-11-2026

### S2. Primary key type (Status: Closed)
**Suggested key**
- A ★ `int IDENTITY(1,1)` clustered PK for all tables. `bigint` only where growth demands it (`AttendancePunches`, `AuditLogs`).
- B `bigint` everywhere.
- C GUIDs (`uniqueidentifier`). Larger, fragmenting, no benefit for a single-server app.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S3. Table names (Status: Closed)
**Suggested key**
- A Plural PascalCase everywhere (`Employees`, `Attendances`). Renames `Employee` and `Attendance`, which breaks every query for no functional gain.
- B ★ **Keep existing names; new tables are plural PascalCase.** `Employee` and `Attendance` stay as documented exceptions.
- C Singular everywhere. Renames 17 tables.

Under B: PascalCase, no prefixes (`tbl`), no underscores, `dbo` schema for everything. `Attendance` keeps its name even though it becomes one row per shift day.

**Answer:** B, but we should change tables that do not follow PascalCase to follow PascalCase, we'll work on fixing them in the application.  **Decided by / date:** Rayder / 17-11-2026

### S4. Column names (Status: Closed)
**Suggested key**
- A ★ PascalCase, no underscores, no table-name prefix (`Name`, not `DepartmentName`), except where the plain word is ambiguous or already in use. Foreign keys are named `<ReferencedEntity>Id`: `EmployeeId`, `ProjectId`, `LeaveTypeId`. Booleans start with `Is`/`Has`/`Requires`. Dates: `...Date` for `date`, `...At` for datetime2. Existing exceptions (`DOB`, `CNIC`, `MobileNo`, `GenStatus`) stay.
- B Same, but also rename existing columns that break the rule (`EmployeeID` -> `EmployeeCode`, `DepartmentName` -> `Name`). More churn.

Note: `Employee.EmployeeID` stays as is under A (it is the business key and the name HR uses). A rename to `EmployeeCode` is option B.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S5. Foreign keys: referencing and naming (Status: Closed)
**Suggested key**
- A ★ Every relationship is a real FK constraint. All employee references use `Employee.uid` as `EmployeeId int` (D1). Constraint name `FK_<Child>_<Parent>` (add the column when there are two, e.g. `FK_RosterOverrides_Employee_Swap`). No more string-key links between tables.
- B FKs only where the app benefits; keep convention-only links for imports.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S6. Delete behavior (Status: Closed)
**Suggested key**
- A ★ `NO ACTION` by default. Cascade only for owned detail rows (`PayslipDetails` under `Payslips`). Nothing that carries money or history is ever hard-deleted: employees are set `Inactive` with a leaving date (T1 in decisions.md), lookups get `IsActive = 0`.
- B Cascade employee deletes to all children.

**Answer:** A, soft deletes everywhere, especially users. Some table rows maybe be detelable but those will be decided while implementation and on special case by case basis.  **Decided by / date:** Rayder / 17-11-2026

### S7. Strings (Status: Closed)
**Suggested key**
- A ★ `nvarchar(n)` for any text a person types or may need Urdu/Unicode (names, reasons, comments, departments). `varchar(n)` only for codes and ASCII keys (EmployeeID, CNIC, phone, status codes). Always an explicit length sized to the data (50/100/200/500/1000). `nvarchar(MAX)` only for genuinely unbounded text or JSON (`Message`, `OldData`, `CalculationDetails`). Never MAX for a column that is filtered, sorted or joined.
- B Everything `nvarchar(n)`.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S8. Numbers and money (Status: Closed)
**Suggested key**
- A ★ Money `decimal(18,2)`; rates and percentages `decimal(5,2)` (percent 0-100) or `decimal(9,4)` where precision matters; days `decimal(5,2)` (half days); minutes `int`; counters `int`. No `float`, no `money`. Currency is PKR; no currency column.
- B Add a currency column for future use.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S9. Dates and times (Status: Closed)
**Suggested key**
- A ★ Whole days (join date, DOB, holiday, leave dates, shift date) are `date`. Moments are `datetime2(0)` (`CheckInAt`, `CreatedAt`, `ApprovedAt`). Times of day on a shift are `time(0)`. Never `datetime`, never text. Stored in **server local time (Pakistan, PKT)**; the app does no timezone conversion. Defaults use `sysdatetime()`.
- B Store UTC and convert in the app. More correct, but adds conversion everywhere in a single-timezone app.

**Answer:** A, app is currenty deployed on a PKT machine, if ever changed then this timing would need to be retified. Do not store in UTC as some attendance data will be uploaded, that will be in PKT.  **Decided by / date:** Rayder / 17-11-2026

### S10. Booleans (Status: Closed)
**Suggested key**
- A ★ `bit NOT NULL` with a default. `NULL` is not a third state; where "unset" has meaning (e.g. `LeaveManagedOverride`), that is documented in the column note. No `'Yes'/'No'` text (see `Employee.ApplyTax`).
- B Allow nullable bits.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S11. Status and type values (Status: Closed)
**Suggested key**
- A ★ Three kinds of list, three homes:
  1. **Code-driven value sets** (Status, DayStatus, HalfType, OverrideType): `varchar(n)` + `CHECK`. The app's logic branches on these values, so they are not data anyone should edit.
  2. **Lists with attributes or relationships** (Projects, Departments, Designations, LeaveTypes): own tables with an `Id`, FK from the user of the list.
  3. **Scalar settings** (`FuelPrice`, `CurrentTaxYear`): stay in `Configuration`.
  Simple name-only lists (AllowanceTypes, AllowanceNames, DocumentType) may stay in `Configuration` or move to option B.
- B One generic `LookupValues (Id, Category, Code, Name, SortOrder, IsActive)` table for all simple lists, with real FKs to `Id`. Cleaner than CSV in `Configuration`, but see the cross-category weakness below.
- C Keep everything in `Configuration` (today).

**Why not put these in `Configuration`?** Today each list is one row whose `ConfigValue` is a comma-separated string (`GetConfigValuesAsync` splits it). That means:
- **No foreign key is possible.** A value inside a string cannot be referenced, so `Employee.Department`/`Project` are free text and drift from the list (in the test data, project names on employees did not match the Setup list). This drift is the reason for the change.
- **No attributes.** A project needs `SortOrder` and `HasLeaveManagement`; a department needs its project; a designation needs a rank. A CSV item has only a name.
- **Renaming or removing an item silently orphans every row that uses the text,** and an item cannot be deactivated while its history stays valid.
- **Status values are logic, not data.** If `Approved` lives in `Configuration`, an edit under Setup can break leave processing.
- `Configuration` also has no PK and no unique key on `ConfigKey` today.

`Configuration` is the right place for single values, not for lists the rest of the schema depends on. A one-table `LookupValues` (option B) fixes the FK problem for simple lists, but any FK can then point at a value from the wrong category unless a composite key is added, so it suits only name-only lists.

**Answer:** A, let's keep `Configuration` scoped to lists and single values that maybe used for dropdowns and such  **Decided by / date:** Rayder / 17-11-2026

### S12. Nullability and defaults (Status: Closed)
**Suggested key**
- A ★ Columns are `NOT NULL` unless "unknown/not applicable" is a real state, and then the note says which. Defaults are set in the database (not only in C#). Empty strings are not a substitute for NULL. Optional text columns are NULL.
- B Leave nullable by default, enforce in the app (today).

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S13. Audit columns (Status: Closed)
**Suggested key**
- A ★ Tables whose rows people create/edit carry `CreatedAt datetime2(0) NOT NULL DEFAULT sysdatetime()` and `CreatedBy nvarchar(100) NULL` (username). Editable tables also carry `ModifiedAt datetime2(0) NULL` and `ModifiedBy nvarchar(100) NULL`. Lookup tables with fixed seed data and pure child rows (`PayslipDetails`) can skip them. Existing `CreatedDate`/`ModifiedDate` columns (Allowances, Deductions, Payslips) keep their names to avoid churn.
- B Same, plus `RowVersion` on every table.
- C Only on tables that already have them.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S14. Soft delete (Status: Closed)
**Suggested key**
- A ★ `IsActive bit` for lookups and people-like master data; `IsDeleted bit` only where a document or record is hidden but kept (`EmployeeDocuments`); transactional rows (attendance, payslips, leave) are never deleted, they are cancelled/locked via `Status`/`IsLocked`. Queries must filter these flags; lists should be views or a documented filter.
- B Hard delete everywhere except payroll.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S15. Concurrency (Status: Closed)
**Suggested key**
- A ★ `rowversion` on tables that two people can edit at once and where a lost update is costly: `Employee`, `LeaveRequests`, `HalfDayRequests`, `Attendance`, `Payslips`. EF `IsRowVersion()`.
- B None (today).
- C On every table.

**Answer:** A, focus on Idempotency  **Decided by / date:** Rayder / 17-11-2026

### S16. Indexes (Status: Closed)
**Suggested key**
- A ★ Every FK column has an index (leading column), created in the same migration as the FK. Natural/business keys (`EmployeeID`, `Username`, `ConfigKey`, `LeaveTypes.Code`) have a unique index. Names: `PK_<Table>`, `FK_<Child>_<Parent>`, `IX_<Table>_<Cols>`, `UX_<Table>_<Cols>` (unique), `CK_<Table>_<Rule>`, `DF_<Table>_<Column>` for defaults. Index only for queries that exist; no speculative indexes. Filtered indexes where a soft-delete or NULL filter is the common case.
- B Index on demand only, after slow queries appear.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S17. Constraints (Status: Closed)
**Suggested key**
- A ★ Rules the database can check live in the database: `CHECK` for ranges and status sets, unique keys for business rules (one payslip per employee per month). (2026-11-18: a composite "department belongs to project" key was dropped; HR confirmed they are not tightly linked, decision B2.) The app validates too, but never alone.
- B App-level only.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S18. Derived values (Status: Closed)
**Suggested key**
- A ★ Do not store a value that can be calculated from other columns (leave used/remaining, `IsLate`). If it must be stored for history (payslip totals, `ScheduledStart` snapshots), the note says it is a snapshot and why. Reporting queries use views.
- B Store derived values for speed.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S19. Migrations (Status: Closed)
**Suggested key**
- A ★ EF migrations are the only way schema changes reach production, each with a hand-reviewed SQL script. Phase 0 first records the 6 hand-applied migrations and resolves the missing `LMSModule`. Every migration is **expand then contract**: add, backfill, switch the app, drop later. Back up `HRBTK` before each production run. `db.md` is updated in the same commit as the migration.
- B Hand-written SQL scripts under `scripts/database/` plus a numbered log.

**Answer:** A, not necessary to use EF mirgrations, tho it is cleaner. sqlmd through commadline is allowed. Given you have user permission to make the changes.  **Decided by / date:** Rayder / 17-11-2026

### S20. Documentation (Status: Closed)
**Suggested key**
- A ★ `db.md` stays the single source for the current schema, with dated notes saying why. Proposed changes live in `db_changes.md` until applied. Every standard that is exception-tolerated (e.g. `Employee`, `Attendance` names) is listed in db.md.
- B Generate docs from the live DB only.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S21. Idempotency: natural keys and replay-safe writes (Status: Closed)
Idempotent = doing the same operation twice (double click, browser retry, re-uploaded file, re-run script, EF connection retry) leaves the same result as doing it once. Note that `rowversion` (S15) detects *concurrent edits*; it does not stop a *replayed insert*. Only a unique key does.

**Suggested key**
- A ★ Every table that people or imports insert into has a **natural unique key** that makes the second insert fail or update instead of duplicating (list in the review below). Tables with no natural key (`Allowances`, `Deductions`, `LeaveEntitlementOverrides`, `AttendanceCorrections`) get `RequestId uniqueidentifier NULL` with a unique filtered index (`WHERE RequestId IS NOT NULL`); the form generates the GUID when it renders, so a replayed POST is rejected. Imports and processing use upsert (`MERGE` or update-then-insert in a transaction), never blind insert.
- B Natural keys only; accept duplicates on tables without one.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S22. Overlap rules (Status: Closed)
SQL Server has no exclusion constraint, so "no two overlapping leave requests / shift assignments" cannot be a plain unique index.

**Suggested key**
- A ★ Exact duplicates are blocked by a unique index (`LeaveRequests (EmployeeId, FromDate, ToDate, LeaveTypeId)` where Status is Pending or Approved). True overlaps are checked inside the save transaction after taking `sp_getapplock` on the employee, so two simultaneous requests cannot both pass the check.
- B Same, plus an `INSTEAD OF` trigger doing the overlap check. Stronger, but hidden logic that bypasses EF.
- C App check only.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S23. Re-runnable schema scripts (Status: Closed)
**Suggested key**
- A ★ Every SQL change script can be run twice with no error and no effect the second time: `IF COL_LENGTH(...) IS NULL`, `IF OBJECT_ID(...) IS NULL`, `IF NOT EXISTS` on index/constraint names, seed data via `MERGE` on the natural key, wrapped in a transaction, logged in `__EFMigrationsHistory` (or a `SchemaChangeLog` row). Needed because six past migrations were applied by hand and never recorded.
- B One-shot scripts, tracked by a numbered log.

**Answer:** A  **Decided by / date:** Rayder / 17-11-2026

### S24. Notes and remarks (Status: Open)
HR records edge cases (forgotten check-outs, late arrivals with a reason, special arrangements) in free-text notes on the record they concern, for example on that day's attendance. The names for these columns differ today (`Comments`, `Remarks`, `Notes`, `Details`, `Description`), most tables have none, and a single column per row is overwritten by the next note and loses who wrote it and when.

**Suggested key**
- A ★ Two layers. (1) Every table HR works in has one standard column `Notes nvarchar(1000) NULL` for the current short note (existing `Attendance.Comments` and `Allowances.Remarks` become `Notes`; `Payslips.Notes` already exists). (2) A `RecordNotes` table holds dated, signed notes (who, when, text), append-only, linked by a real FK to the record (S5), starting with attendance, employee and leave. Notes are context only: they never feed payroll or processing; anything that changes a status or pay goes in a structured table (`AttendanceCorrections`, `HalfDayRequests`).
- B Only the `Notes` column (layer 1). Simple, but a day with two notes keeps only the last one and has no author or time.
- C Only `RecordNotes`. Full history, but a quick one-line note needs a second table and screen.

**Answer:** ______  **Decided by / date:** ______

---

## Idempotency review of the data structure
**Data status (2026-11-18):** the data in `HRBTK` is treated as test data. The "Today" column describes the schema, and counts and findings come from the test data; re-run the checks on the real data before applying anything.

Reviewed 2026-11-17 against the live schema (db.md v1.9), re-checked against the final draft (db_changes.md v3.0-draft). Nothing here is applied yet: the "After db_changes" column is the state once that draft is built.

| Area | Replay risk | Today (live HRBTK) | After db_changes v3.0 |
|---|---|---|---|
| Employee create | Double submit duplicates a person | `EmployeeID` unique: **safe** | unchanged (`UX_Employee_EmployeeID`); `RowVersion` added for concurrent edits |
| Users create | Double submit | Username not unique: **unsafe** (6 distinct today, so no cleanup needed) | `UX_Users_Username`, `UX_Users_EmployeeId`; **safe** |
| Configuration | Same key saved twice | No PK, no unique key: **unsafe** (19 distinct keys today) | PK `Id` + `UX_Configuration_ConfigKey`; **safe** |
| Lookups (Projects, Designations, LeaveTypes, Shifts, Departments) | Same name added twice | Free text or CSV: **unsafe** | unique `Name` (and `Code` for LeaveTypes); **safe** |
| Tax slabs | Same slab saved twice | No key: **unsafe** (14 rows, no duplicates) | `UX_TaxRules_Year_MinSalary`; **safe** |
| Payslip generation | Double click or re-run makes a second payslip | No unique key (0 duplicates today by luck): **unsafe** | `UX_Payslips_Employee_Year_Month`; regenerate in one transaction (decisions T4, open); **safe** |
| Payslip lines | Regenerate doubles the lines | No uniqueness: **unsafe** (0 duplicates among 132) | `UX_PayslipDetails_Payslip_Item`; delete-and-insert in the same transaction; **safe** |
| Payslip lock | Repeated lock writes another `AuditLogs` row | State idempotent, audit row is not: **partly safe** | audit row only when `IsLocked` changes (app rule); **safe** |
| Allowances | Double submit duplicates the item | No key: **unsafe**; two active Fuel Allowance rows already exist for employees 325 and 376 | `RequestId` unique filtered index; **safe** against replay. The two existing rows need a payroll ruling (no unique key on active name) |
| Deductions | Double submit | No key: **unsafe** (no duplicates today) | `RequestId` unique filtered index; **safe** |
| Leave request | Double submit | No keys, no FK: **unsafe** (no exact duplicates; requests 21 and 22 overlap) | `UX_LeaveRequests_Open_Duplicate` + overlap check under `sp_getapplock` (S22); **safe**. Pair 21/22 reviewed first |
| Half-day request | Double submit | n/a | `UX_HalfDayRequests_Employee_Date` for Pending/Approved; **safe** |
| Leave adjustments | "+5 days" posted twice gives +10 | no table yet | `RequestId` unique filtered index; each adjustment is a row, so it is auditable; **safe** |
| Leave quota | Same type and year saved twice | No PK: **unsafe** | `UX_LeaveQuota_Type_Year`; **safe** |
| Carry-forward run | Year-end job run twice | key `(EmployeeID, Year)`: **safe** | `UX_CarryforwardLeaves_Employee_Type_FromYear` + upsert; **safe** |
| Holidays | Same date entered twice | PK on date: **safe** | `UX_GazettedHolidays_Date_Project` (allows project-specific rows); **safe** |
| Attendance import | Same file twice | `(EmployeeID, AttendanceDate)` unique: **safe** for rows; log written each time (same file imported 3 times in 7 minutes) | `UX_AttendanceUploadLogs_FileHash`; **safe** |
| Attendance punches | Re-uploaded rows, unmatched codes | n/a | `UX_AttendancePunches_Dedup` on `(RawEmployeeCode, PunchAt, Direction)`; **safe** (the key does not depend on a matched employee) |
| Attendance processing | Re-running recalculates | n/a (empty) | deterministic from punches + shift snapshot; `UX_Attendance_Employee_ShiftDate`; locked rows skipped; **safe** |
| Attendance corrections | Double submit | n/a | `RequestId` unique filtered index; **safe** |
| Record notes | Double click writes the same note twice | n/a | `RequestId` unique filtered index; **safe** |
| Roster and shift assignment | Double entry, overlaps | n/a | `UX_RosterOverrides_Employee_Date`, `UX_ShiftAssignments_Employee_From_Priority`; overlap check (S22); **safe** |
| Employee documents | Same file twice | none: **unsafe** (no duplicates among the 8 today) | `UX_EmployeeDocuments_Employee_FileHash`; **safe** |
| AuditLogs | Append-only | n/a | intentionally not idempotent |
| Schema scripts | Run twice | hand-applied, unrecorded: **unsafe** | re-runnable, recorded scripts (S23); `Migration order` in db_changes.md |

After db_changes v3.0 every area above is replay-safe by a database key (AuditLogs excepted, by design). Two items still depend on a person and not on the schema:
- **Allowances:** employees 325 and 376 each have two active `Fuel Allowance` rows. `RequestId` stops a replay but cannot say whether a second row is valid; payroll must decide.
- **Leave requests:** requests 21 and 22 (employee uid 143) overlap and must be reviewed before the overlap rule is enforced.

What the keys do not cover (app work, listed in db_changes.md "Idempotency"):
- **Connection retry.** `Program.cs` enables `EnableRetryOnFailure(5)`. If a connection drops during a commit, EF can run the work again. Every multi-step write (payslip generation, attendance processing, carry-forward) must be one transaction wrapped in the execution strategy, and the keys above make a repeated insert fail instead of duplicating.
- **`RequestId` handling.** Forms render the GUID; a duplicate-key error on it means "already saved", shown to the user as the saved row, not an error.
- **Seed and backfill.** Backfills (`MonthNo`, `LeaveTypeId`, `TaxYearStart`, department mapping) are written as "set where NULL" so a second run changes nothing.
- **`rowversion` (S15).** It detects two people editing the same row; it does not stop a replayed insert.

---

## Carried into the table walkthrough from the answers
- S3: any table not in PascalCase is renamed (the app is fixed afterwards). Only `__EFMigrationsHistory` is not PascalCase; EF manages it, so it stays.
- S6/S14: soft delete everywhere, especially users. `Users` has no `IsActive` today and needs one; login must reject inactive users. Deletable rows are decided case by case at implementation.
- S9: all timestamps stay PKT; no UTC conversion.
- S19: `sqlcmd` scripts are allowed against production, but only with your explicit approval before each run.

---

## Table walkthrough (after the rules are decided)
Order of review, grouped so decisions on a parent table apply to its children:
1. Lookups: Projects, Departments, Designations, LeaveTypes, Configuration, TaxRules
2. People: Employee, Users, EmployeeDocuments
3. Payroll: Allowances, Deductions, Payslips, PayslipDetails
4. Leave: LeaveQuota, LeaveEntitlementOverrides, LeaveRequests, CarryforwardLeaves, GazettedHolidays, (EmployeeLeaves removal)
5. Schedule and attendance: Shifts, ShiftAssignments, RosterOverrides, AttendancePunches, Attendance, AttendanceCorrections, HalfDayRequests, AttendanceUploadLogs
6. System: AuditLogs, __EFMigrationsHistory

For each table: check name, PK, FK naming, types, nullability, audit columns, indexes and constraints against S1-S20; record exceptions in db_changes.md.
