---
status: retired
retired_on: 2026-11-26
superseded_by: ../../db.md
reason: This was the design draft for the database redesign, scoped until the redesign was applied to production (2026-11-24). db.md now tracks the schema and every change to it. Do not update this file.
---
# HRMS Database Schema - Proposed Changes (final draft)

> **Retired. Do not update.** The redesign is on production; `../../db.md` tracks the schema and all later changes (including ones not yet run on production). This file is kept only as the record of why the redesign looked the way it did. Its "maintenance rules" no longer apply. Phase 12 to 14 notes added after 2026-11-24 are also in db.md, which is authoritative.
Version: 4.1 | Based on: db.md v1.9 (2026-11-16) | Date: 2026-11-26
Database: HRBTK
Server: Teezhost 16GB (production)
Status: **APPLIED TO PRODUCTION on 2026-11-24 (scripts 01-04, 06-11; see db.md v2.0 and prod_rollout.md section 8). The text below is kept as the design record; db.md is the current schema.** Former status: **FINAL DRAFT. Applied to the local development copy only (2026-11-23; script 08 `EmployeeEvents`, 09 leave days one decimal, 10 legacy leave archive and 11 leave balances view v2 on 2026-11-24); production `HRBTK` is untouched and stays read-only until each script is reviewed and approved (db_standards.md S19).** The re-runnable scripts are in `scripts/database/redesign/` (01-04 expand, 06 contract); the development app is already adapted (value converters keep the legacy text shapes in code, Setup pages manage the new lists). Deviations found while applying: `UX_PayslipDetails_Payslip_Item` is created in phase 4 (after the `ItemType` type change); filtered indexes block `ALTER COLUMN` and column renames (SQL Server error 1934), so the scripts drop and recreate them around those steps; `UX_Attendance_Employee_ShiftDate` was filtered during the expand phase and is unfiltered after the contract phase. This draft implements the rules in db_standards.md (S1-S23) and the answered decisions in decisions.md (T1, T2, T3, T5, T6). Items that wait for an open decision are tagged ⏸ and name the decision (T4, T7, B1-B18). **Data status (2026-11-18):** the data in `HRBTK` is treated as test data. Row counts, "live checks" and findings in this file describe it for design purposes; they are not requirements (for example, the number of projects is not fixed, B3), and the checks are re-run on the real data before a script is applied.

## MAINTENANCE RULES:
    1. Bump Version and Date above whenever this file changes; when a change is applied, move it into db.md and add a Change History entry there
    2. Keep db.md's dated-notes style: every inline note says WHY (YYYY-MM-DD), not just the type
    3. Tables marked [TO BE REMOVED] keep their section, per db.md rule 3 - they document what old code may still reference
    4. Every change ships as an EF migration or a re-runnable SQL script (S19, S23) that is recorded in `__EFMigrationsHistory` (see Migration order)
    5. Answered decisions move out of decisions.md into this file; a ⏸ tag is removed when its decision is answered
---

## Legend
Table heading tags:
- **[NEW]** - table does not exist in HRBTK today.
- **[CHANGED]** - existing table with added, altered, renamed or deprecated columns/keys.
- **[UNCHANGED]** - no schema change proposed (notes may still change).
- **[TO BE REMOVED]** - legacy/obsolete; dropped only after the app no longer uses it. Section stays (db.md rule 3).

Column and key markers (first column of every column table, and at the start of each key/index line):
- 🟢 **NEW** - added. Column name in **bold**.
- 🟡 **CHANGED** - type, nullability, name, default, constraint or meaning changes. Column name in **bold**; the old definition is in Notes as `was ...`.
- 🔴 **DROP** - deprecated. Kept until the app has switched to the replacement, then dropped in the last phase.
- ⏸ **HELD** - depends on an open decision (named in Notes). Shown as drafted; do not build until answered.
- blank - unchanged from db.md.

## Conventions (from db_standards.md)
- **Primary keys (S1, S2):** `Id int IDENTITY(1,1)` on every table; `bigint` for `AttendancePunches` and `AuditLogs`. `Employee` and `Users` keep `uid`. Descriptive PK names (`AttendanceID`, `DepartmentID`, `LogId`, `UID`) become `Id` in the last phase.
- **Names (S3, S4):** tables are plural PascalCase; `Employee` and `Attendance` stay as documented exceptions; no other table breaks PascalCase (`__EFMigrationsHistory` is EF's). New columns are PascalCase, no table prefix. Existing columns are **not** renamed except where this file says so (so `DepartmentName`, `ApplyTax`, `CreatedDate`, `ModifiedOn`, `DOB`... stay).
- **Foreign keys (S5):** every relationship is a real FK constraint; employee references are `EmployeeId int` -> `Employee.uid`; other FKs are `<Entity>Id`; references to `Users.uid` are `<Action>ByUserId` (`ApprovedByUserId`, `RecordedByUserId`). Constraint name `FK_<Child>_<Parent>`, plus the role when there are two (`FK_RosterOverrides_Employee_Swap`).
- **Booleans (S4, S10):** `bit NOT NULL` with a default, named `Is...`/`Has...`/`Requires...`. The one exception is `Employee.IsLeaveManagedOverride`, where NULL means "follow the project".
- **Dates (S9):** whole days are `date` named `...Date`; moments are `datetime2(0)` named `...At`; times of day are `time(0)`. Server local time (PKT), defaults use `sysdatetime()`. No `datetime`, no text.
- **Strings (S7):** `nvarchar(n)` for text people type; `varchar(n)` for codes and ASCII keys; `nvarchar(MAX)` only for unbounded text/JSON, never for a filtered, sorted or joined column.
- **Numbers (S8):** money `decimal(18,2)`; percentages `decimal(5,2)`; leave days `decimal(5,1)` (one decimal: half days are 0.5, 2026-11-24); other days `decimal(5,2)`; minutes `int`; no `float`, no `money`.
- **Status and type values (S11):** fixed sets are `varchar(n)` + `CHECK`. Lists with attributes are tables (Projects, Departments, Designations, LeaveTypes). `Configuration` holds single values and name-only dropdown lists.
- **Audit columns (S13):** **set C** = `CreatedAt datetime2(0) NOT NULL DEFAULT sysdatetime()`, `CreatedBy nvarchar(100) NULL`. **set M** = set C + `ModifiedAt datetime2(0) NULL`, `ModifiedBy nvarchar(100) NULL`. Tables with existing audit columns (`CreatedDate`, `ModifiedOn`, `UploadedOn`...) keep them.
- **Soft delete (S6, S14):** `IsActive` for lookups, master data and logins; `IsDeleted` for documents; transactional rows are cancelled or locked, never deleted. Rows that may really be deleted are decided case by case when the screen is built.
- **Notes (S24, ⏸ T7):** HR-operated tables carry one standard `Notes nvarchar(1000) NULL` for the current short note; dated, signed notes go in the append-only `RecordNotes` table. Notes are context only and never feed payroll or processing. Existing `Attendance.Comments` and `Allowances.Remarks` become `Notes`.
- **Concurrency and idempotency (S15, S21-S23):** `RowVersion rowversion` on Employee, LeaveRequests, HalfDayRequests, Attendance, Payslips; natural unique keys or a `RequestId` on every table that is written to; scripts are re-runnable.
- **Names of keys and indexes (S16):** `PK_<Table>`, `FK_<Child>_<Parent>`, `IX_<Table>_<Cols>`, `UX_<Table>_<Cols>`, `CK_<Table>_<Rule>`, `DF_<Table>_<Column>` (every default is named; not repeated per column below). Every FK column leads at least one index. Existing auto-named constraints are renamed (Appendix A).
- **Derived values (S18):** a stored value that could be calculated is marked "snapshot" with the reason.
- **Case-insensitive collation:** SQL Server treats `EmployeeID` and `EmployeeId` as the same name, so a column can't be added next to its old name. During the expand phase the new column gets a temporary name (`MonthNo`, `EmployeeUid`) and is renamed in the contract phase (see Migration order).

## Summary
19 existing tables + 12 new = 31 sections; 30 tables after `EmployeeLeaves` is dropped, plus 2 views.

| Table | Status | Why (one line) |
|---|---|---|
| __EFMigrationsHistory | UNCHANGED | EF bookkeeping; only the 6 unrecorded migrations need fixing (data, not schema). |
| Allowances | CHANGED | `RequestId`; dates to `date`/`datetime2(0)`; `ModifiedBy` tightened. |
| Attendance | CHANGED | Becomes one row per employee per shift day: int employee key, overnight support, minutes, statuses, rowversion. |
| AttendanceCorrections | NEW | HR's notes on forgotten check-outs and other fixes; raw punches are never overwritten. |
| AttendancePunches | NEW | Raw check-in/out events from device, upload or manual entry. |
| AttendanceUploadLogs | CHANGED | `FileHash`, constrained `Status`, `datetime2(0)`, counts NOT NULL. |
| AuditLogs | CHANGED | PK renamed `Id`, `datetime2(0)`, real FK to Employee. |
| CarryforwardLeaves | CHANGED | Rebuilt (empty): `Id`, int employee key, `LeaveTypeId`, `FromYear`/`ToYear`. |
| Configuration | CHANGED | Real PK `Id`, unique `ConfigKey`, audit columns; entity lists and tax/year rows move out. |
| Deductions | CHANGED | `Amount` for fixed values, `RequestId`, `datetime2(0)`, FK index. |
| Departments | CHANGED | One shared department list, independent of Project (B2 decided 2026-11-18): `SortOrder`, `IsActive`, audit. The main department (Administration, Operations...); the function inside it is a sub-department. |
| Designations | NEW | Designation lookup with `Rank` (⏸ B4). |
| Employee | CHANGED | Org FKs, leaving/return dates, pay basis, proper types, rowversion. |
| EmployeeDocuments | CHANGED | Real FK, `DocumentType`, `FileHash`, `IsDeleted` NOT NULL. |
| EmployeeEvents | NEW | Append-only timeline of key employment events: joined, promotion, transfer, resignation, termination, rejoin, other (B14, T1, T8, B19). |
| EmployeeLegacyLeaveBalances | NEW | Read-only archive of the 7 per-year leave figures that used to sit on `Employee` (B18, 2026-11-24). |
| EmployeeLeaves | TO BE REMOVED | Empty parallel leave model with no PK; folded into LeaveRequests. |
| GazettedHolidays | CHANGED | Rebuilt (empty): `Id`, `HolidayName`, optional `ProjectId`, `IsOptional`. |
| HalfDayRequests | NEW | Half-day approval with documentation and a per-case "must check out" flag (⏸ B1, B7). |
| LeaveEntitlementOverrides | NEW | Per-person, per-case quota changes on top of the standard policy. |
| LeaveQuota | CHANGED | Standard policy per leave type per year: PK, `LeaveTypeId`, int `Year`, decimal days. |
| LeaveRequests | CHANGED | The single leave model: FK to employee and leave type, `date`s, half-day, approval, rowversion. |
| LeaveTypes | NEW | Leave type lookup. |
| PayslipDetails | CHANGED | Unique line key, constrained `ItemType`. |
| Payslips | CHANGED | Numeric `Month`, unique per employee/month, hourly columns, rowversion. |
| Projects | NEW | The top of Employee -> Department -> Project, with SortOrder and HasLeaveManagement (⏸ B3). |
| RecordNotes | NEW | Dated, signed, append-only notes HR writes against an attendance day, employee or leave request (⏸ T7). |
| RosterOverrides | NEW | One-off schedule edge cases: shift change, off day, swap, extra duty (⏸ B8). |
| ShiftAssignments | NEW | Which shift an employee works, on which weekdays, for which date range (⏸ B8). |
| Shifts | NEW | Shift times, overnight flag, grace minutes, 7-hour full-day rule, half-day cutoff. |
| SubDepartments | NEW | Optional detail under a department (the function: Mess, Transportation, Record...); information only (B2). |
| TaxRules | CHANGED | `TaxYearStart` replaces the text label (T6); percentage `decimal(5,2)`; audit columns. |
| UserRoles | NEW | One row per role a login holds (`Admin`, `HR`); replaces the single `Users.Role` column so one user can have several roles (added 2026-11-23). |
| Users | CHANGED | `IsActive`, int FK to Employee, unique Username, audit columns; `Role` dropped in favour of `UserRoles`. |

Count by status: NEW 13, CHANGED 17, UNCHANGED 1, TO BE REMOVED 1.

## How the HR requirements map to the schema
- 2026-11-16: **Employee, Department and Project, sorted by Project, then Department, then Designation.** `Projects.SortOrder`, `Departments.SortOrder`, `Designations.Rank`; `Employee.ProjectId/DepartmentId/DesignationId`; view `vEmployeeDirectory` and indexes `IX_Employee_Org`.
- 2026-11-18: **Departments and projects are linked but not tightly (HR, decision B2).** Some departments exist in only one project (Galaxy has Maintenance), Admin and Security are in every project, and there is one HR department for all projects (under Bahria Town Karachi). So `Departments` is one shared list and `Employee` holds `ProjectId` and `DepartmentId` independently, with no rule tying them together. HR can sort department first (all Admin staff in every project), then by project, or the other way round: `IX_Employee_Department_Project` and `IX_Employee_Org`. 2026-11-18 (project owner): a function inside a department (`Admin - Mess`, `Operations - Record`) is the optional `Employee.SubDepartmentId`, for information only.
- 2026-11-16: **Dastarkhuwans have no leave management.** `Projects.HasLeaveManagement = 0`; `Employee.IsLeaveManagedOverride` (NULL = follow project). Effective rule: `COALESCE(e.IsLeaveManagedOverride, p.HasLeaveManagement)`.
- 2026-11-16: **Hourly pay, 7 hours = full day, less = hourly.** `Shifts.MinFullDayMinutes` (420); `Attendance.WorkedMinutes/PayableMinutes/DayStatus`; `Employee.PayBasis/HourlyRate`; `Payslips.HourlyMinutes/HourlyAmount` (⏸ B6).
- 2026-11-16: **1 minute late/early grace.** `Shifts.GraceLateMinutes`, `Shifts.GraceEarlyMinutes`; `Attendance.LateMinutes/EarlyLeaveMinutes/IsLate`.
- 2026-11-16: **Half days need approval and documentation.** `HalfDayRequests`; `Shifts.HalfDayCutoffTime/HalfDayMinMinutes`; `Attendance.HalfDayRequestId` (⏸ B1, B7).
- 2026-11-16: **Forgotten check-outs are told to HR, who note them.** `AttendanceCorrections`; `Attendance.CheckOutSource = 'HRCorrection'`; `DayStatus = 'MissingCheckout'` (⏸ B16).
- 2026-11-16: **Attendance follows the schedule; some check in and out on two different days.** `Attendance.ShiftDate` + full `CheckInAt/CheckOutAt`; `Shifts.IsOvernight`; `ShiftAssignments`, `RosterOverrides`.
- 2026-11-16: **Same leave quota for everyone, changeable per person per case.** `LeaveQuota` + `LeaveEntitlementOverrides`; view `vLeaveBalances` (⏸ B5).
- 2026-11-24: **Leave Balance on the employee page is linked to the Leave Management System.** Remaining and carry-forward per leave type for the current year, plus one row per earlier year, all read from `vLeaveBalances` (v2) through one service that the LMS Balances page also uses. Leave is tracked in one decimal (half days = 0.5). The seven per-year columns that were on `Employee` are archived and dropped (B18).
- 2026-11-17: **Employees can leave and return (T1).** `Employee.DateOfLeaving`, `Employee.DateOfReturn`, both nullable.
- 2026-11-24: **Employee history: joining, promotion, transfer, resignation, termination, rejoin (HR, decision B14 in part).** `EmployeeEvents`, one row per event with the position before and after; the app records an event from Edit Employee and applies its effect to `Employee` in the same transaction. This also lets one person leave and return more than once (T1 limit removed).
- 2026-11-17: **HR records edge cases (forgotten check-outs and the like) in notes on that day's attendance (S24, ⏸ T7).** `Notes` column on the HR tables for the current short note; `RecordNotes` for dated, signed notes with history. A note that changes a status or pay is a structured `AttendanceCorrections` row, not a note.

## Relationships (proposed)
Existing foreign keys (kept; renamed to the S5 pattern, Appendix A):
- `Allowances.EmployeeId` -> `Employee.uid` (`FK_Allowances_Employee`, no action)
- `Deductions.EmployeeId` -> `Employee.uid` (`FK_Deductions_Employee`, no action)
- `PayslipDetails.PayslipId` -> `Payslips.Id` (`FK_PayslipDetails_Payslips`, cascade)
- `Payslips.EmployeeId` -> `Employee.uid` (`FK_Payslips_Employee`, no action)

New foreign keys (all no action unless stated; every FK column leads an index, see each table):
- 🟢 `SubDepartments.DepartmentId` -> `Departments.Id`
- 🟢 `Employee.(DepartmentId, SubDepartmentId)` -> `SubDepartments.(DepartmentId, Id)` (composite, so a sub-department can only be one of the chosen department's; optional: NULL `SubDepartmentId` skips the check)
- 🟢 `Employee.ProjectId` -> `Projects.Id`
- 🟢 `Employee.DepartmentId` -> `Departments.Id` (2026-11-18, B2: independent of `ProjectId`; no composite key and no rule that the department belongs to the project)
- 🟢 `Employee.DesignationId` -> `Designations.Id`
- 🟢 `EmployeeDocuments.EmployeeId` -> `Employee.uid` (was convention only; EF already declares it)
- 🟢 `Users.EmployeeId` -> `Employee.uid` (replaces the string `Users.EmployeeId`)
- 🟢 `Attendance.EmployeeId` -> `Employee.uid` (replaces the string `Attendance.EmployeeID`); `Attendance.ShiftId` -> `Shifts.Id`; `Attendance.HalfDayRequestId` -> `HalfDayRequests.Id`; `Attendance.LeaveRequestId` -> `LeaveRequests.Id`
- 🟢 `AttendancePunches.EmployeeId` -> `Employee.uid`; `.UploadLogId` -> `AttendanceUploadLogs.Id`; `.AttendanceId` -> `Attendance.Id`
- 🟢 `AttendanceCorrections.AttendanceId` -> `Attendance.Id`; `.RecordedByUserId` -> `Users.uid`
- 🟢 `HalfDayRequests.EmployeeId` -> `Employee.uid`; `.DocumentId` -> `EmployeeDocuments.Id`; `.ApprovedByUserId` -> `Users.uid`; `.LeaveRequestId` -> `LeaveRequests.Id` (⏸ B1)
- 🟢 `EmployeeEvents.EmployeeId` -> `Employee.uid`; `.FromProjectId` / `.ToProjectId` -> `Projects.Id`; `.FromDepartmentId` / `.ToDepartmentId` -> `Departments.Id`; `.FromDesignationId` / `.ToDesignationId` -> `Designations.Id` (all `NO ACTION`: lookups used by history are never hard-deleted, S6)
- 🟢 `ShiftAssignments.EmployeeId` -> `Employee.uid`; `.ShiftId` -> `Shifts.Id`
- 🟢 `RosterOverrides.EmployeeId` -> `Employee.uid`; `.ShiftId` -> `Shifts.Id`; `.SwapWithEmployeeId` -> `Employee.uid` (`FK_RosterOverrides_Employee_Swap`); `.ApprovedByUserId` -> `Users.uid`
- 🟢 `GazettedHolidays.ProjectId` -> `Projects.Id`
- 🟢 `LeaveQuota.LeaveTypeId` -> `LeaveTypes.Id`
- 🟢 `LeaveEntitlementOverrides.EmployeeId` -> `Employee.uid`; `.LeaveTypeId` -> `LeaveTypes.Id`; `.ApprovedByUserId` -> `Users.uid`
- 🟢 `LeaveRequests.EmployeeId` -> `Employee.uid` (was convention only; 1 orphan row must be resolved first); `.LeaveTypeId` -> `LeaveTypes.Id`; `.DocumentId` -> `EmployeeDocuments.Id`; `.ApprovedByUserId` -> `Users.uid`
- 🟢 `CarryforwardLeaves.EmployeeId` -> `Employee.uid`; `.LeaveTypeId` -> `LeaveTypes.Id`
- 🟢 `AuditLogs.EmployeeId` -> `Employee.uid` (2026-11-17: safe because employees are never deleted, S6; check the 4 existing rows first)
- 🟢 `RecordNotes.AttendanceId` -> `Attendance.Id`; `.EmployeeId` -> `Employee.uid`; `.LeaveRequestId` -> `LeaveRequests.Id` (⏸ T7; exactly one is set per note)

Removed convention-only links: 🔴 `Attendance.EmployeeID`, `EmployeeLeaves.EmployeeID`, `CarryforwardLeaves.EmployeeId` (string), `Users.EmployeeId` (string) -> `Employee.EmployeeID`. After migration nothing joins on `Employee.EmployeeID`; it remains the unique business key for display and for matching device/upload codes (decision D1).

## Table: dbo.__EFMigrationsHistory [UNCHANGED]
EF Core bookkeeping: which migrations have been applied.
Rows on 2026-11-16: 13.
- 2026-11-16: No schema change. Before the first new migration, insert rows for the six hand-applied migrations (see Known issues in db.md) so `dotnet ef database update` does not try to re-apply them. Table name and PK name are EF's and stay as they are.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | MigrationId | nvarchar(150) | NOT NULL |  | PK. |
|  | ProductVersion | nvarchar(32) | NOT NULL |  |  |

Keys and indexes
- `PK___EFMigrationsHistory` - primary key, clustered, (MigrationId).

## Table: dbo.Allowances [CHANGED]
Per-employee allowances (fixed, quantity-based or percentage of basic). Only active rows are used by payroll.
Rows on 2026-11-16: 23.
- 2026-11-16: `EffectiveDate`, `EndDate` and `Frequency` are stored but payroll only checks `IsActive`. That is an app fix (filter by the payslip month), not a schema change.
- 2026-11-17: Live check: no `EffectiveDate`/`EndDate` has a time part, so the conversion to `date` is lossless. Employees 325 and 376 each have two active `Fuel Allowance` rows, so there is **no** unique key on active `(EmployeeId, Name)`; the idempotency key is `RequestId`. Open question for payroll: is a second row of the same allowance valid (e.g. two vehicles), or a duplicate to clean up?
- Audit columns: existing `CreatedDate/CreatedBy/ModifiedDate/ModifiedBy` kept (S13).

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | Id | int | NOT NULL |  | PK, identity. |
|  | EmployeeId | int | NOT NULL |  | FK -> Employee.uid. |
|  | AllowanceType | nvarchar(50) | NOT NULL |  | e.g. `Fixed Amount`, `Quantity`, `Fuel Allowance` (legacy). Values come from Configuration `AllowanceTypes`. |
|  | Name | nvarchar(200) | NOT NULL |  | `Fuel Allowance` triggers FuelPrice x Quantity logic when type is `Quantity`. Values come from Configuration `AllowanceNames`. |
|  | Amount | decimal(18,2) | NOT NULL |  | Fixed amount; multiplied by Quantity for non-fuel rows. |
|  | IsPercentage | bit | NOT NULL | 0 | When true, amount = basic x PercentageValue / 100. |
|  | PercentageValue | decimal(5,2) | NULL |  | CHECK 0-100 when set. |
| 🟡 CHANGED | **EffectiveDate** | date | NOT NULL |  | was datetime2(7). 2026-11-17: whole day (S9). Should be honoured by payroll (app fix). |
| 🟡 CHANGED | **EndDate** | date | NULL |  | was datetime2(7) NULL. Same. |
|  | IsActive | bit | NOT NULL | 1 |  |
| 🟡 CHANGED | **CreatedDate** | datetime2(0) | NOT NULL | sysdatetime() | was datetime2(7) with no default (S9, S12). |
|  | CreatedBy | nvarchar(100) | NOT NULL |  |  |
| 🟡 CHANGED | **ModifiedDate** | datetime2(0) | NULL |  | was datetime2(7). |
| 🟡 CHANGED | **ModifiedBy** | nvarchar(100) | NULL |  | was nvarchar(MAX) NULL. Same length as `CreatedBy` (S7); check `MAX(LEN(ModifiedBy))` first. |
|  | Quantity | int | NOT NULL | 1 | Added 2026-09-18 (migration 20260918120000, not recorded in __EFMigrationsHistory). |
| 🟡 CHANGED | **Notes** | nvarchar(1000) | NULL |  | was `Remarks` nvarchar(500) NULL, added 2026-09-18 (migration 20260918130000). 2026-11-17 (S24, ⏸ T7): standard note column name and length; renamed at contract. |
|  | Frequency | nvarchar(30) | NOT NULL | 'Monthly' | Stored, not honoured by payroll (app issue). |
| 🟢 NEW | **RequestId** | uniqueidentifier | NULL |  | 2026-11-17 (S21): idempotency key; the form generates the GUID when it renders, so a replayed POST is rejected. NULL for existing rows. |

Keys and indexes
- `PK_Allowances` - primary key, clustered, (Id).
- `IX_Allowances_EmployeeId` - nonclustered, (EmployeeId).
- 🟢 `UX_Allowances_RequestId` - unique, nonclustered, (RequestId) WHERE RequestId IS NOT NULL.
- 🟢 `CK_Allowances_Percentage` - PercentageValue IS NULL OR PercentageValue BETWEEN 0 AND 100.
- 🟡 `FK_Allowances_Employee` - renamed from `FK_Allowances_Employee_EmployeeId`.

## Table: dbo.Attendance [CHANGED]
Processed attendance: one row per employee per **shift date** (the schedule day the shift starts on), so a 22:00-06:00 shift stays on one row. Built from `AttendancePunches` + the schedule (`ShiftAssignments` / `RosterOverrides`) + approved `HalfDayRequests`, `LeaveRequests`, `GazettedHolidays` and HR's `AttendanceCorrections`. Table name kept (S3 exception).
Rows on 2026-11-16: 0.
- 2026-11-16: The table is empty, so there is no data migration. The running app still writes the old columns (`EmployeeID`, `EmployeeName`, `DepartmentName`, `AttendanceDate`, `TimeIn`, `TimeOut`, `Status`), so the change is **expand then contract** (S19): the new columns are added beside them (the new employee key temporarily as `EmployeeUid`), and the old ones are dropped after the app switch.
- 2026-11-16: Processing rules (draft, ⏸ B6, B7, B16): late if `CheckInAt > ScheduledStartAt + GraceLateMinutes`; early leave if `CheckOutAt < ScheduledEndAt - GraceEarlyMinutes`; `WorkedMinutes >= MinFullDayMinutes` (420) -> `Present`; approved half day -> `HalfDay`; otherwise worked time under 420 minutes -> `Hourly` with `PayableMinutes` = worked minutes; no check-out -> `MissingCheckout` until HR records a correction, except when an approved half day has `RequiresCheckout = 0`.
- 2026-11-17 (S18): `LateMinutes`, `EarlyLeaveMinutes`, `WorkedMinutes`, `PayableMinutes`, `DayStatus`, `ScheduledStartAt/EndAt` are **snapshots** of processing, stored so history survives later shift or rule edits; reprocessing is idempotent (updates the row keyed by `(EmployeeId, ShiftDate)`; locked rows are skipped).
- 2026-11-16: Once a payslip for the month is generated, rows get `IsLocked = 1`; later changes go through `AttendanceCorrections` and need the payslip unlocked (⏸ B15).

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟡 CHANGED | **Id** | int | NOT NULL |  | PK, identity. was `AttendanceID` (S1); renamed in the contract phase. |
| 🟡 CHANGED | **EmployeeId** | int | NOT NULL |  | was `EmployeeID` nvarchar(50) NOT NULL (string, no FK). FK -> Employee.uid. Added as `EmployeeUid` NULL, renamed `EmployeeId` NOT NULL at contract. |
| 🔴 DROP | EmployeeName | nvarchar(100) | NOT NULL |  | Copy of Employee data; read via the FK instead. |
| 🔴 DROP | DepartmentName | nvarchar(50) | NOT NULL |  | Free-text copy; read via Employee.DepartmentId instead. |
| 🟡 CHANGED | **ShiftDate** | date | NOT NULL |  | was `AttendanceDate` date NOT NULL. The day the shift starts, which differs from the check-out day for overnight shifts. |
| 🟢 NEW | **ShiftId** | int | NULL |  | FK -> Shifts.Id. Shift that applied that day after overrides; NULL = no schedule found (flag for HR). |
| 🟢 NEW | **ScheduledStartAt** | datetime2(0) | NULL |  | ShiftDate + Shifts.StartTime (snapshot). |
| 🟢 NEW | **ScheduledEndAt** | datetime2(0) | NULL |  | ShiftDate (+1 day if IsOvernight) + Shifts.EndTime (snapshot). |
| 🟡 CHANGED | **CheckInAt** | datetime2(0) | NULL |  | was `TimeIn` time(7) NULL. A time alone can't express a check-out on the next day. |
| 🟡 CHANGED | **CheckOutAt** | datetime2(0) | NULL |  | was `TimeOut` time(7) NULL. |
| 🟢 NEW | **CheckOutSource** | varchar(15) | NOT NULL | 'None' | `Punch`, `HRCorrection`, `Auto`, `None`. |
| 🟢 NEW | **LateMinutes** | int | NOT NULL | 0 | Minutes after ScheduledStartAt beyond grace (snapshot). |
| 🟢 NEW | **EarlyLeaveMinutes** | int | NOT NULL | 0 | Minutes before ScheduledEndAt beyond grace (snapshot). |
| 🟢 NEW | **WorkedMinutes** | int | NOT NULL | 0 | CheckOutAt - CheckInAt - Shifts.BreakMinutes (snapshot). |
| 🟢 NEW | **PayableMinutes** | int | NOT NULL | 0 | Minutes payroll pays; basis for hourly pay (snapshot). |
| 🟡 CHANGED | **DayStatus** | varchar(20) | NOT NULL | 'Absent' | was `Status` nvarchar(10) NULL (P/A/L/H). `Present`, `HalfDay`, `Hourly`, `Absent`, `Leave`, `Holiday`, `WeeklyOff`, `MissingCheckout`. Old L (Late) becomes `Present` + `IsLate = 1`. |
| 🟢 NEW | **IsLate** | bit | NOT NULL |  | Computed, persisted: `CASE WHEN LateMinutes > 0 THEN 1 ELSE 0 END`. |
| 🟢 NEW | **HalfDayRequestId** | int | NULL |  | FK -> HalfDayRequests.Id. Set when an approved half day applied. |
| 🟢 NEW | **LeaveRequestId** | int | NULL |  | FK -> LeaveRequests.Id. Set when `DayStatus = 'Leave'`. |
| 🟢 NEW | **IsLocked** | bit | NOT NULL | 0 | Set after payroll for the month. |
| 🟢 NEW | **ProcessedAt** | datetime2(0) | NULL |  | Last time the row was (re)calculated. |
| 🟡 CHANGED | **Notes** | nvarchar(1000) | NULL |  | was `Comments` nvarchar(255) NULL. 2026-11-17 (S24, ⏸ T7): HR's short note on that day (e.g. a forgotten check-out told to HR); renamed at contract. Context only, never read by processing or payroll; dated, signed notes go in `RecordNotes`. |
| 🟢 NEW | **CreatedAt** | datetime2(0) | NOT NULL | sysdatetime() | Set M. |
| 🟢 NEW | **CreatedBy** | nvarchar(100) | NULL |  |  |
| 🟢 NEW | **ModifiedAt** | datetime2(0) | NULL |  |  |
| 🟢 NEW | **ModifiedBy** | nvarchar(100) | NULL |  |  |
| 🟢 NEW | **RowVersion** | rowversion | NOT NULL |  | S15: processing and an HR correction can run at the same time; the later writer must re-read. |

Keys and indexes
- 🟡 `PK_Attendance` - primary key, clustered, (Id). was `PK__Attendan__8B69263C67096255`.
- 🔴 `IX_Attendance_EmployeeID_Date` - unique, nonclustered, (EmployeeID, AttendanceDate). Replaced by the index below.
- 🟢 `UX_Attendance_Employee_ShiftDate` - unique, nonclustered, (EmployeeId, ShiftDate). Idempotency key for processing and imports.
- 🟢 `IX_Attendance_ShiftDate` - nonclustered, (ShiftDate) INCLUDE (EmployeeId, DayStatus, PayableMinutes). Daily and monthly reports.
- 🟢 `IX_Attendance_ShiftId` - nonclustered, (ShiftId) WHERE ShiftId IS NOT NULL; 🟢 `IX_Attendance_HalfDayRequestId` - (HalfDayRequestId) WHERE HalfDayRequestId IS NOT NULL; 🟢 `IX_Attendance_LeaveRequestId` - (LeaveRequestId) WHERE LeaveRequestId IS NOT NULL. FK indexes (S16).
- 🟢 `FK_Attendance_Employee`, `FK_Attendance_Shifts`, `FK_Attendance_HalfDayRequests`, `FK_Attendance_LeaveRequests`.
- 🟢 `CK_Attendance_DayStatus` - DayStatus IN (the 8 values above).
- 🟢 `CK_Attendance_CheckOutSource` - CheckOutSource IN ('Punch','HRCorrection','Auto','None').
- 🟢 `CK_Attendance_CheckTimes` - CheckOutAt IS NULL OR CheckInAt IS NULL OR CheckOutAt > CheckInAt.

## Table: dbo.AttendanceCorrections [NEW]
HR's record of attendance fixes, mainly forgotten check-outs reported by the employee. The correction is applied to `Attendance`; the raw `AttendancePunches` are never overwritten. Append-only (set C only).
Rows: new table.
- 2026-11-17: No `EmployeeId` column: it would duplicate `Attendance.EmployeeId` and could contradict it. Per-employee history joins through `AttendanceId`. No `CreatedBy` either: `RecordedByUserId` is the user.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **AttendanceId** | int | NOT NULL |  | FK -> Attendance.Id. Processing creates a row for every scheduled day (Absent included), so there is always a row to correct. |
| 🟢 NEW | **CorrectionType** | varchar(20) | NOT NULL |  | `MissedCheckOut`, `MissedCheckIn`, `TimeChange`, `StatusChange`. |
| 🟢 NEW | **CorrectedCheckInAt** | datetime2(0) | NULL |  | New check-in, when corrected. |
| 🟢 NEW | **CorrectedCheckOutAt** | datetime2(0) | NULL |  | New check-out, e.g. the time the employee tells HR. |
| 🟢 NEW | **OldDayStatus** | varchar(20) | NULL |  | For `StatusChange`. |
| 🟢 NEW | **NewDayStatus** | varchar(20) | NULL |  | For `StatusChange`. |
| 🟢 NEW | **ReportedByEmployeeAt** | datetime2(0) | NULL |  | When the employee told HR. |
| 🟢 NEW | **Reason** | nvarchar(500) | NOT NULL |  |  |
| 🟢 NEW | **RecordedByUserId** | int | NOT NULL |  | FK -> Users.uid (the HR user). |
| 🟢 NEW | **CreatedAt** | datetime2(0) | NOT NULL | sysdatetime() | When HR recorded it. |
| 🟢 NEW | **RequestId** | uniqueidentifier | NULL |  | S21 idempotency key, generated by the form. |

Keys and indexes
- 🟢 `PK_AttendanceCorrections` - primary key, clustered, (Id).
- 🟢 `IX_AttendanceCorrections_AttendanceId` - nonclustered, (AttendanceId).
- 🟢 `IX_AttendanceCorrections_RecordedByUserId` - nonclustered, (RecordedByUserId).
- 🟢 `UX_AttendanceCorrections_RequestId` - unique, nonclustered, (RequestId) WHERE RequestId IS NOT NULL.
- 🟢 `CK_AttendanceCorrections_Type` - CorrectionType IN ('MissedCheckOut','MissedCheckIn','TimeChange','StatusChange').
- 🟢 `CK_AttendanceCorrections_Values` - MissedCheckOut requires CorrectedCheckOutAt; MissedCheckIn requires CorrectedCheckInAt; StatusChange requires NewDayStatus.

## Table: dbo.AttendancePunches [NEW]
Raw check-in / check-out events exactly as received from a device, an Excel/CSV upload or manual entry, so processing (and re-processing after a schedule change) always starts from untouched source data. Append-only (set C only).
Rows: new table. Expected volume: ~850 employees x a few punches a day, so `bigint` (S2).
- 2026-11-17 (S21): re-uploading a file cannot duplicate punches: the unique key is on the **raw code** (always present), not on `EmployeeId` (NULL for unmatched codes, which would otherwise duplicate on every re-upload).

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | bigint | NOT NULL |  | PK, identity. |
| 🟢 NEW | **EmployeeId** | int | NULL |  | FK -> Employee.uid. NULL when `RawEmployeeCode` could not be matched (shown to HR). |
| 🟢 NEW | **RawEmployeeCode** | varchar(50) | NOT NULL |  | Code as it came from the device/file; matched against Employee.EmployeeID. Manual punches use the employee's `EmployeeID`. |
| 🟢 NEW | **PunchAt** | datetime2(0) | NOT NULL |  | Full date and time (overnight shifts). PKT, as uploaded (S9). |
| 🟢 NEW | **Direction** | varchar(7) | NOT NULL | 'Unknown' | `In`, `Out`, `Unknown` (devices that don't send direction). |
| 🟢 NEW | **Source** | varchar(10) | NOT NULL |  | `Device`, `Upload`, `Manual`. |
| 🟢 NEW | **DeviceId** | nvarchar(50) | NULL |  | Device serial/name for `Device` punches. |
| 🟢 NEW | **UploadLogId** | int | NULL |  | FK -> AttendanceUploadLogs.Id for `Upload` punches. |
| 🟢 NEW | **AttendanceId** | int | NULL |  | FK -> Attendance.Id: the shift day this punch was assigned to by processing. |
| 🟢 NEW | **IsIgnored** | bit | NOT NULL | 0 | Duplicate/accidental punch excluded by HR or processing. |
| 🟢 NEW | **IgnoreReason** | nvarchar(200) | NULL |  |  |
| 🟢 NEW | **CreatedAt** | datetime2(0) | NOT NULL | sysdatetime() |  |
| 🟢 NEW | **CreatedBy** | nvarchar(100) | NULL |  |  |

Keys and indexes
- 🟢 `PK_AttendancePunches` - primary key, clustered, (Id).
- 🟢 `IX_AttendancePunches_Employee_PunchAt` - nonclustered, (EmployeeId, PunchAt) INCLUDE (Direction, IsIgnored).
- 🟢 `UX_AttendancePunches_Dedup` - unique, nonclustered, (RawEmployeeCode, PunchAt, Direction).
- 🟢 `IX_AttendancePunches_UploadLogId` - nonclustered, (UploadLogId) WHERE UploadLogId IS NOT NULL.
- 🟢 `IX_AttendancePunches_AttendanceId` - nonclustered, (AttendanceId) WHERE AttendanceId IS NOT NULL.
- 🟢 `CK_AttendancePunches_Direction` - Direction IN ('In','Out','Unknown'); `CK_AttendancePunches_Source` - Source IN ('Device','Upload','Manual').

## Table: dbo.AttendanceUploadLogs [CHANGED]
Audit trail of Excel/CSV attendance imports (counts and error details). Existing `UploadedBy/UploadDate` serve as the S13 audit columns.
Rows on 2026-11-16: 3.
- 2026-11-17: Live check: all 3 rows are `LeaveRecords II.xlsx`, uploaded 3 times within 7 minutes on 2026-01-05, with a NULL `Status`. That is the replay problem `FileHash` (T3) fixes. Their `Status` is backfilled from `Message`/counts before the CHECK is added (proposed `Failed` when `SuccessCount` is 0 or NULL, else `Completed`; review the 3 rows by hand).
- 2026-11-16: Uploads now write `AttendancePunches` rows that point back here (`AttendancePunches.UploadLogId`).

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | Id | int | NOT NULL |  | PK, identity. |
| 🟡 CHANGED | **FileName** | nvarchar(255) | NOT NULL |  | was NULL. S12. |
|  | UploadedBy | nvarchar(100) | NULL |  |  |
| 🟡 CHANGED | **UploadDate** | datetime2(0) | NOT NULL | sysdatetime() | was datetime NULL, default getdate() (S9). Existing name kept (S4). |
| 🟡 CHANGED | **Status** | varchar(20) | NOT NULL | 'Processing' | was nvarchar(50) NULL, unconstrained. `Processing`, `Completed`, `CompletedWithErrors`, `Failed` (verify against what the upload code writes). |
|  | Message | nvarchar(MAX) | NULL |  |  |
|  | ErrorDetails | nvarchar(MAX) | NULL |  |  |
| 🟡 CHANGED | **SuccessCount** | int | NOT NULL | 0 | was NULL. S12. NULLs set to 0 first. |
| 🟡 CHANGED | **FailureCount** | int | NOT NULL | 0 | was NULL. |
| 🟡 CHANGED | **TotalRows** | int | NOT NULL | 0 | was NULL. |
| 🟢 NEW | **FileHash** | char(64) | NULL |  | T3: SHA-256 of the uploaded file. NULL for the 3 existing rows. |

Keys and indexes
- 🟡 `PK_AttendanceUploadLogs` - primary key, clustered, (Id). was `PK__Attendan__3214EC07799B6BDB`.
- 🟢 `CK_AttendanceUploadLogs_Status` - Status IN ('Processing','Completed','CompletedWithErrors','Failed').
- 🟢 `UX_AttendanceUploadLogs_FileHash` - unique, nonclustered, (FileHash) WHERE FileHash IS NOT NULL AND Status <> 'Failed'. Re-uploading the same file is answered with "already imported on <date>"; a failed import can be retried.

## Table: dbo.AuditLogs [CHANGED]
Generic change audit (who, what, old/new JSON, IP). Append-only, so it is intentionally not idempotent (S21).
Rows on 2026-11-16: 4.
- 2026-11-16: Currently written only by payslip locking; should also be written by leave/half-day approvals, attendance corrections, roster overrides, entitlement overrides, Employee org changes and payslip unlocking (⏸ B15). It should write a row only when state actually changes.
- 2026-11-17: `EmployeeId` gets a real FK (S5); employees are never deleted (S6), so audit rows cannot be orphaned. Verify the 4 existing rows first.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟡 CHANGED | **Id** | bigint | NOT NULL |  | PK, identity. was `LogId` int (S1, S2: a log grows without limit). Renamed at contract; the type change is cheap on 4 rows. |
|  | TableName | nvarchar(128) | NOT NULL |  |  |
|  | Operation | nvarchar(20) | NOT NULL |  |  |
|  | RecordId | nvarchar(100) | NULL |  |  |
|  | EmployeeId | int | NULL |  | FK -> Employee.uid. |
|  | OldData | nvarchar(MAX) | NULL |  | JSON. |
|  | NewData | nvarchar(MAX) | NULL |  | JSON. |
|  | ChangedBy | nvarchar(128) | NULL | suser_sname() |  |
| 🟡 CHANGED | **ChangedAt** | datetime2(0) | NOT NULL | sysdatetime() | was datetime2(7) (S9). Ordering uses `Id`. |
|  | ModuleName | nvarchar(100) | NULL |  |  |
|  | IPAddress | nvarchar(50) | NULL |  |  |

Keys and indexes
- 🟡 `PK_AuditLogs` - primary key, clustered, (Id).
- 🟢 `IX_AuditLogs_EmployeeId` - nonclustered, (EmployeeId) WHERE EmployeeId IS NOT NULL; 🟢 `FK_AuditLogs_Employee`.

## Table: dbo.CarryforwardLeaves [CHANGED]
Leave days carried from one leave year to the next, per employee per leave type.
Rows on 2026-11-16: 0.
- 2026-11-16: Empty, so the table is dropped and re-created in the new shape. This resolves the db.md mismatch with the EF entity (`Id`, `FromYear`, `ToYear`, `CarryForwardDays`, `Description`), now with `EmployeeId` int and `LeaveTypeId`. Used/remaining days are derived in `vLeaveBalances`, not stored (S18).
- ⏸ B5: carry-forward cap and leave year (calendar assumed) are open; a `CK` for a cap is added once answered.
- 2026-11-24 (B18): the legacy per-year figures were archived in `EmployeeLegacyLeaveBalances`, not converted into rows here. New rows come from the yearly rollover (see `LeaveTypes`).

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟡 CHANGED | **EmployeeId** | int | NOT NULL |  | was nvarchar(50) (Employee.EmployeeID string, part of PK). FK -> Employee.uid. |
| 🟢 NEW | **LeaveTypeId** | int | NOT NULL |  | FK -> LeaveTypes.Id. Replaces the entity's `LeaveTypeName`. |
| 🟡 CHANGED | **FromYear** | int | NOT NULL |  | was `Year` int (part of PK). Leave year the days come from. |
| 🟢 NEW | **ToYear** | int | NOT NULL |  | Leave year the days are added to. |
| 🟡 CHANGED | **CarryForwardDays** | decimal(5,1) | NOT NULL |  | was `TotalLeaves` int. Half days; name matches the EF entity. |
| 🟢 NEW | **Description** | nvarchar(500) | NULL |  |  |
| 🔴 DROP | LeavesUsed | int | NULL | 0 | Derived (S18); computed in vLeaveBalances. |
| 🔴 DROP | LeavesRemaining | int | NULL | 0 | Derived (S18). |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🔴 `PK__Carryfor__379B99141E7FEFEB` - primary key, clustered, (EmployeeId, Year). Replaced.
- 🟢 `PK_CarryforwardLeaves` - primary key, clustered, (Id).
- 🟢 `UX_CarryforwardLeaves_Employee_Type_FromYear` - unique, nonclustered, (EmployeeId, LeaveTypeId, FromYear). Idempotency key for the year-end run (upsert).
- 🟢 `IX_CarryforwardLeaves_LeaveTypeId` - nonclustered, (LeaveTypeId).
- 🟢 `CK_CarryforwardLeaves_Years` - ToYear = FromYear + 1; `CK_CarryforwardLeaves_Days` - CarryForwardDays >= 0.

## Table: dbo.Configuration [CHANGED]
Key/value application settings edited under Setup: single values (`FuelPrice`) and name-only dropdown lists (`AllowanceTypes`, `AllowanceNames`), stored as comma-separated text (S11).
Rows on 2026-11-16: 19.
- 2026-11-17 (S11, T5, T6): Rows for lists that become tables or derived values are deleted in the last phase, after the app reads the new tables: `Project`, `Department`, `Designation`, `LeaveTypes`, `Year`, `TaxSlabs`, `CurrentTaxYear`. Rows not named here stay. Live check: no duplicate or NULL `ConfigKey` among the 19 rows.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟡 CHANGED | **Id** | int | NOT NULL |  | Identity. was `UID`, not a key. Becomes the PK (EF already declares it); renamed at contract (S1). |
| 🟡 CHANGED | **ConfigKey** | nvarchar(50) | NOT NULL |  | was NULL, not unique. Unique. |
|  | ConfigValue | nvarchar(MAX) | NULL |  | Not filtered or joined, so MAX is allowed (S7). |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M: who changed `FuelPrice` matters for payroll. |

Keys and indexes
- 🟢 `PK_Configuration` - primary key, clustered, (Id). (was: no primary key)
- 🟢 `UX_Configuration_ConfigKey` - unique, nonclustered, (ConfigKey). Idempotency key (S21): a key saved twice updates one row.

## Table: dbo.Deductions [CHANGED]
Per-employee payroll deductions (fixed amount or percentage of gross).
Rows on 2026-11-16: 11.
- 2026-11-17: Live check: all 11 rows are `Fixed`, with values up to 800.00 stored in `PercentageValue decimal(5,2)` (cap 999.99). They move to `Amount` and `PercentageValue` becomes percentage-only.
- 2026-11-16: `EffectiveDate`, `EndDate` and `Frequency` are stored but not honoured by payroll (app issue).
- 2026-11-17: Live check: no duplicate active `(EmployeeId, DeductionName)`, so a unique key on it can be added later if wanted; the idempotency key is `RequestId`.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | Id | int | NOT NULL |  | PK, identity. |
|  | EmployeeId | int | NOT NULL |  | FK -> Employee.uid. |
|  | DeductionType | nvarchar(50) | NOT NULL |  |  |
|  | DeductionName | nvarchar(100) | NOT NULL |  |  |
|  | Frequency | nvarchar(50) | NOT NULL |  | Not honoured by payroll (app issue). |
| 🟡 CHANGED | **CalculationMethod** | varchar(20) | NOT NULL |  | was nvarchar(50). `Percentage` or `Fixed` (live: only `Fixed`). |
| 🟡 CHANGED | **PercentageValue** | decimal(5,2) | NULL |  | Percentage only, 0-100. Fixed amounts move to `Amount`. |
| 🟢 NEW | **Amount** | decimal(18,2) | NULL |  | Fixed amount when CalculationMethod = `Fixed`. Backfilled from PercentageValue for those rows. |
|  | IsMandatory | bit | NOT NULL | 0 |  |
|  | EffectiveDate | date | NOT NULL |  | Not honoured by payroll (app issue). |
|  | EndDate | date | NULL |  |  |
|  | IsActive | bit | NOT NULL | 1 |  |
| 🟡 CHANGED | **CreatedDate** | datetime2(0) | NOT NULL | sysdatetime() | was datetime2(7), default getdate(). |
|  | CreatedBy | nvarchar(100) | NOT NULL |  |  |
| 🟡 CHANGED | **ModifiedDate** | datetime2(0) | NULL |  | was datetime2(7). |
|  | ModifiedBy | nvarchar(100) | NULL |  |  |
| 🟢 NEW | **Notes** | nvarchar(1000) | NULL |  | S24, ⏸ T7: HR's short note. |
| 🟢 NEW | **RequestId** | uniqueidentifier | NULL |  | S21 idempotency key, generated by the form. |

Keys and indexes
- 🟡 `PK_Deductions` - primary key, clustered, (Id). was `PK__Deductio__3214EC07F7E8E4F4`.
- 🟢 `IX_Deductions_EmployeeId` - nonclustered, (EmployeeId). The FK has no supporting index today.
- 🟢 `UX_Deductions_RequestId` - unique, nonclustered, (RequestId) WHERE RequestId IS NOT NULL.
- 🟢 `CK_Deductions_Method` - CalculationMethod IN ('Percentage','Fixed').
- 🟢 `CK_Deductions_Value` - (CalculationMethod = 'Percentage' AND PercentageValue BETWEEN 0 AND 100) OR (CalculationMethod = 'Fixed' AND Amount IS NOT NULL AND Amount >= 0). Added after the backfill.

## Table: dbo.Departments [CHANGED]
One shared department list, the FK target of `Employee.DepartmentId`. 2026-11-18 (decision B2, from HR): projects and departments are linked but not tightly. A department is not owned by a project, so this table has no `ProjectId` and there is no link table.
Rows on 2026-11-16: 4.
- 2026-11-18: HR's examples: Galaxy Construction has a Maintenance department that other projects do not have; every project has Admin and Security; there is one HR department for all projects and its staff sit under Bahria Town Karachi. HR sorts department first (all Admin staff in every project) and then by project, or the reverse. This matches the live data: 14 department names are used in more than one project (Security in 4, Accounts & Finance in 4, Admin - Transportation, Operations, House Keeping and Information Technology in 3 each, and 8 more in 2).
- 2026-11-18: Employee holds `ProjectId` and `DepartmentId` independently; the database does not check that a department "belongs" to the project. A project's department dropdown can be built from the departments already used in that project, or show them all.
- 2026-11-16: The 4 existing rows (Software Development, Human Resource, Civil Staff, Azizabad Dastarkhuwan) are kept. The list is seeded from the 58 distinct `Employee.Department` strings; the mapping of those strings to departments still needs HR (B2).
- 2026-11-18 (project owner, B2): this table holds the **main** department. The test data has names that are a base plus a function (`Admin - Mess`, `Operations - Record`); the base goes here and the function goes to `SubDepartments`. `Admin` is the Administration department, so that row is named `Administration`. Split rule: at the first ` - ` (see Migration order). Only Administration and Operations have sub-departments in the test data. For HR at go-live: whether `Administrator` (BT Tower, Icon) is the Administration department, and whether `Mess` and `Transportation` (no `Admin - ` prefix) are its functions.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟡 CHANGED | **Id** | int | NOT NULL |  | PK, identity. was `DepartmentID` (S1); renamed at contract. |
| 🟡 CHANGED | **DepartmentName** | nvarchar(100) | NOT NULL |  | was nvarchar(100) NULL. Existing name kept (S4). Unique. |
| 🟢 NEW | **SortOrder** | int | NOT NULL | 0 | Default order of departments in lists (a global order now, not per project). |
| 🟢 NEW | **IsActive** | bit | NOT NULL | 1 | S14. |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🟡 `PK_Departments` - primary key, clustered, (Id). was `PK__Departme__B2079BCDAEBFDBAB`.
- 🟢 `UX_Departments_DepartmentName` - unique, nonclustered, (DepartmentName). Idempotency key for the department seed and Setup.
## Table: dbo.Designations [NEW]
Designation lookup, replacing the free-text `Employee.Designation` and the Configuration dropdown list. Shared by all projects. Lets employee lists sort by seniority (`Rank`) instead of alphabetically.
Rows: new table (seeded from distinct `Employee.Designation` values; 141 distinct in Bahria Town Karachi alone, so expect a long list to review).

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **Name** | nvarchar(100) | NOT NULL |  |  |
| ⏸ HELD | **Rank** | int | NOT NULL | 100 | Lower = more senior. Ranking to come from HR (B4). |
| 🟢 NEW | **IsActive** | bit | NOT NULL | 1 |  |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🟢 `PK_Designations` - primary key, clustered, (Id).
- 🟢 `UX_Designations_Name` - unique, nonclustered, (Name).

## Table: dbo.Employee [CHANGED]
Employee master. `uid` is the FK used by every table after this change; `EmployeeID` (unique) stays the business key for display and for matching device/upload codes (decision D1). Table name kept (S3 exception).
Rows on 2026-11-16: 848.
- 2026-11-17: Live check: `EmployeeID` is numeric for all 848 rows and at most 6 characters; `EmployeeName` at most 33, `CNIC` 15, `MobileNo` 12; `FatherName` is empty on every row. The proposed lengths below are therefore safe. Names are text people type (S7), so they become `nvarchar`.
- 2026-11-16: Org structure becomes FKs (Project -> Department -> Designation). The free-text columns stay until the app has switched, then are dropped. Backfilling needs the HR mappings (the 58 department strings from B2, and ⏸ B3, B4); `ProjectId` and `DepartmentId` become NOT NULL once every employee is mapped.
- 2026-11-17 (T1): `DateOfLeaving` and `DateOfReturn` record leaving and returning. 2026-11-24: `Employee` always describes the **latest stint** only. A resignation or termination sets `DateOfLeaving` and **clears `DateOfReturn`** (so `CK_Employee_Dates` still holds when someone leaves a second time); the earlier leaving and return stay in `EmployeeEvents`, which carries every cycle.
- 2026-11-24 (B18, decided by Rayder): the 7 legacy per-year leave columns (`CarryForwardLeaves`, `CarryForwardLeaves1`, `Year2022`, `Year2023`, `Year2023New`, `Year2024`, `AdjustedAjusted`) are copied into `EmployeeLegacyLeaveBalances` and dropped from `Employee` (script 10). In the test data they held values for 827 of 848 employees; Year2023 was always CarryForwardLeaves minus Year2022 and Year2024 always CarryForwardLeaves1 minus Year2023New, i.e. results typed in as numbers. The live balance is derived (below), never stored on `Employee` (S18). The Employees grid no longer filters or sorts by Year 2024.
- 2026-11-16: All length/type tightenings need a `MAX(LEN(...))` / `TRY_CONVERT` check on the 848 rows first.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | uid | int | NOT NULL |  | PK, identity. |
| 🟡 CHANGED | **EmployeeID** | varchar(50) | NOT NULL |  | was varchar(250). Business key, unique. |
| 🟡 CHANGED | **EmployeeName** | nvarchar(100) | NOT NULL |  | was varchar(250) NULL. Check for NULLs first. |
| 🟡 CHANGED | **CNIC** | varchar(15) | NULL |  | was varchar(250). 13 digits + 2 dashes. |
| 🟡 CHANGED | **FatherName** | nvarchar(100) | NULL |  | was varchar(250). |
| 🟡 CHANGED | **DOB** | date | NULL |  | was varchar(250) (text). Converted with TRY_CONVERT; unparseable values listed for HR first. |
| 🟡 CHANGED | **MobileNo** | varchar(20) | NULL |  | was varchar(250). |
| 🔴 DROP | Department | varchar(250) | NULL |  | Free text (58 distinct values in the test data, some of the form `Base - Function`). Replaced by DepartmentId (base) and SubDepartmentId (function). |
| 🔴 DROP | Designation | varchar(250) | NULL |  | Replaced by DesignationId. |
|  | DateOfJoining | date | NULL |  |  |
| 🟢 NEW | **DateOfLeaving** | date | NULL |  | T1. Last working day; processing skips attendance/payroll after it. |
| 🟢 NEW | **DateOfReturn** | date | NULL |  | T1. Set when someone returns after leaving. |
| 🟡 CHANGED | **EmployeeStatus** | varchar(20) | NOT NULL | 'Active' | was varchar(250) NULL, unconstrained (all 848 rows `Active`). CHECK list: ⏸ B9 (proposed `Active`, `Inactive`, `Resigned`, `Terminated`). |
| 🟡 CHANGED | **ModifiedBy** | nvarchar(100) | NULL |  | was varchar(MAX) NULL. Existing name kept (S13). |
| 🟡 CHANGED | **ModifiedOn** | datetime2(0) | NULL |  | was nvarchar(MAX) NULL (text). Converted with TRY_CONVERT. Existing name kept. |
|  | Details | nvarchar(MAX) | NULL |  |  |
| 🔴 DROP | Project | nvarchar(50) | NULL |  | Free text (8 distinct values in the test data). Replaced by ProjectId. |
| 🟢 NEW | **ProjectId** | int | NULL |  | FK -> Projects.Id. NOT NULL once every employee is mapped (⏸ B3). |
| 🟢 NEW | **DepartmentId** | int | NULL |  | FK -> Departments.Id. Independent of `ProjectId` (B2, 2026-11-18): the same department can appear in many projects and a project can use any department. NOT NULL once every employee is mapped. |
| 🟢 NEW | **SubDepartmentId** | int | NULL |  | 2026-11-18 (B2): the function inside the department (e.g. Mess under Administration). Optional and for information only: no rule, payroll or approval reads it. Composite FK (DepartmentId, SubDepartmentId) -> SubDepartments(DepartmentId, Id). |
| 🟢 NEW | **DesignationId** | int | NULL |  | FK -> Designations.Id (⏸ B4). |
| 🟢 NEW | **IsLeaveManagedOverride** | bit | NULL |  | NULL = follow Projects.HasLeaveManagement; 1/0 = exception for this person (S10 exception). |
| 🟢 NEW | **PayBasis** | varchar(10) | NOT NULL | 'Monthly' | `Monthly` or `Hourly` (⏸ B6). |
| 🟢 NEW | **HourlyRate** | decimal(18,2) | NULL |  | Optional per-person rate. NULL = derived from BasicSalary (⏸ B6). |
|  | BasicSalary | decimal(18,2) | NULL |  | Payroll basis for gross and tax. |
| 🟡 CHANGED | **ApplyTax** | bit | NOT NULL | 1 | was nvarchar(10) NULL. Live `1` (827), `Yes` (1), `0` (1), empty (19). `1`/`Yes` -> 1; `0`/empty -> 0 (matches today's payroll rule). Existing name kept (S4). |
|  | GenStatus | nvarchar(50) | NULL |  |  |
| 🟢 NEW | **Notes** | nvarchar(1000) | NULL |  | S24, ⏸ T7: HR's short note on the employee. `Details` (purpose undocumented) is left as it is. |
| 🟢 NEW | **CreatedAt** | datetime2(0) | NOT NULL | sysdatetime() | S13. Existing 848 rows get the migration time (noted). |
| 🟢 NEW | **CreatedBy** | nvarchar(100) | NULL |  |  |
| 🟢 NEW | **RowVersion** | rowversion | NOT NULL |  | S15: two HR users editing the same employee no longer overwrite each other silently. |

Keys and indexes
- 🟡 `PK_Employee` - primary key, clustered, (uid). was `PK__Employee__DD701264105802DC`.
- 🟡 `UX_Employee_EmployeeID` - unique, nonclustered, (EmployeeID). was `UQ__Employee__7AD04FF0F2154625`. Idempotency key for creating and importing employees.
- 🟢 `IX_Employee_Org` - nonclustered, (ProjectId, DepartmentId, DesignationId) INCLUDE (EmployeeID, EmployeeName, EmployeeStatus). Supports the Project, then Department, then Designation listing; also leads the `FK_Employee_Projects` FK.
- 🟢 `IX_Employee_Department_Project` - nonclustered, (DepartmentId, ProjectId, DesignationId) INCLUDE (EmployeeID, EmployeeName, EmployeeStatus). 2026-11-18 (B2): HR also sorts department first (all Admin staff, then by project); leads the `FK_Employee_Departments` FK.
- 🟢 `IX_Employee_Department_SubDepartment` - nonclustered, (DepartmentId, SubDepartmentId) WHERE SubDepartmentId IS NOT NULL. Leads the composite `FK_Employee_SubDepartments` and serves "everyone in Administration / Mess".
- 🟢 `IX_Employee_DesignationId` - nonclustered, (DesignationId) WHERE DesignationId IS NOT NULL.
- 🟢 `FK_Employee_Projects`, `FK_Employee_Departments`, `FK_Employee_SubDepartments` (composite), `FK_Employee_Designations`.
- 🟢 `CK_Employee_PayBasis` - PayBasis IN ('Monthly','Hourly'); `CK_Employee_EmployeeStatus` - EmployeeStatus IN (confirmed list, ⏸ B9); `CK_Employee_HourlyRate` - HourlyRate IS NULL OR HourlyRate > 0.
- 🟢 `CK_Employee_Dates` - (DateOfLeaving IS NULL OR DateOfJoining IS NULL OR DateOfLeaving >= DateOfJoining) AND (DateOfReturn IS NULL OR (DateOfLeaving IS NOT NULL AND DateOfReturn > DateOfLeaving)).

## Table: dbo.EmployeeDocuments [CHANGED]
Metadata for files uploaded per employee (files live under `wwwroot/uploads/employees/{uid}/`). Soft-deleted via `IsDeleted`. Existing `UploadedOn/UploadedBy` serve as the S13 audit columns.
Rows on 2026-11-16: 8.
- 2026-11-16: Also stores the evidence for half days and leave (`HalfDayRequests.DocumentId`, `LeaveRequests.DocumentId`). Live check: no employee has the same file name and size twice.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | Id | int | NOT NULL |  | PK, identity. |
|  | EmployeeId | int | NOT NULL |  | FK -> Employee.uid (EF already declares it; no orphans today). |
| 🟢 NEW | **DocumentType** | varchar(30) | NULL |  | e.g. `CNIC`, `HalfDayProof`, `LeaveProof`, `MedicalCertificate`, `Other`. Code list: ⏸ B9. |
|  | FileName | nvarchar(300) | NOT NULL |  | GUID-based stored name. |
|  | OriginalFileName | nvarchar(300) | NULL |  |  |
|  | FilePath | nvarchar(500) | NOT NULL |  |  |
|  | FileExtension | nvarchar(50) | NULL |  |  |
|  | FileSize | bigint | NULL |  |  |
| 🟢 NEW | **FileHash** | char(64) | NULL |  | T3: SHA-256 of the file content; NULL for the 8 existing rows (a backfill script can hash the files on disk). |
| 🟡 CHANGED | **UploadedOn** | datetime2(0) | NOT NULL | sysdatetime() | was datetime NULL, default getdate(). |
| 🟢 NEW | **UploadedBy** | nvarchar(100) | NULL |  |  |
| 🟡 CHANGED | **IsDeleted** | bit | NOT NULL | 0 | was bit NULL. NULLs set to 0 first. |

Keys and indexes
- 🟡 `PK_EmployeeDocuments` - primary key, clustered, (Id). was `PK__Employee__3214EC070955CA59`.
- 🟢 `FK_EmployeeDocuments_Employee` - EmployeeId -> Employee.uid.
- 🟢 `IX_EmployeeDocuments_EmployeeId` - nonclustered, (EmployeeId) WHERE IsDeleted = 0.
- 🟢 `UX_EmployeeDocuments_Employee_FileHash` - unique, nonclustered, (EmployeeId, FileHash) WHERE IsDeleted = 0 AND FileHash IS NOT NULL. The same file can't be attached twice to one employee; a deleted document can be uploaded again.

## Table: dbo.EmployeeEvents [NEW]
Key employment events per employee, shown as the Employee History tab and recorded from Edit Employee. An entry is never edited; a wrong one is **voided** with a reason (kept, shown struck through) and can then be deleted by an Admin. Script `08_employee_events.sql`.
Rows after the backfill on the development copy: 843 `Joined` (848 employees, 5 without a joining date); no leavers in the test data.
- 2026-11-24 (B14, T1): decided by Rayder. History is a timeline of events, each with the position before and after, not a table of effective-dated positions. `Employee` still holds the current position; the events explain how it got there. "Where was this person on a past date" is answered by walking the events back from the current position (B14 stays open for whether reports must store a snapshot).
- 2026-11-24: **Recording an event also updates `Employee`, in the same transaction** (Rayder): `Joined` sets `DateOfJoining`; `Promotion` sets `DesignationId`; `Transfer` sets any of `ProjectId`, `DepartmentId`, `DesignationId` (a department change clears `SubDepartmentId`); `Resignation` / `Termination` set `DateOfLeaving`, clear `DateOfReturn` and set `EmployeeStatus` to `Resigned` / `Terminated`; `Rejoin` sets `DateOfReturn` and `EmployeeStatus = Active`; `Other` changes nothing. The service refuses combinations that cannot happen: a promotion or transfer for someone who has left, a rejoin for someone who has not, a second resignation without a rejoin, a date before the joining date, and (except `Other`) a date earlier than the latest live entry, so the timeline and the employee record cannot disagree.
- 2026-11-24 (T8, decided): only the **latest** live entry can be voided, and voiding it **reverts** its effect on `Employee` (promotion/transfer restore the From position; resignation, termination and rejoin replay the remaining entries; fields changed by hand since are left alone). A voided entry stays visible, marked voided, until an **Admin deletes it** (hard delete, only for voided rows, the full row is written to `AuditLogs`); then it no longer appears in the history.
- 2026-11-24: `Joined` is written when an employee is created (Employee Create) and by the backfill; HR records the other six types from Edit Employee. The backfill gives each employee with a `DateOfJoining` one `Joined` row carrying the **current** project, department and designation (the original position was never recorded; the remark says so), a `Resignation` / `Termination` row where `DateOfLeaving` is set (type from `EmployeeStatus`) and a `Rejoin` row where `DateOfReturn` is set; `CreatedBy = Migration`.
- 2026-11-24: Not part of payroll or attendance processing yet. Processing keeps reading `Employee.DateOfJoining/DateOfLeaving/DateOfReturn` (B10); the events are the audit trail behind them.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **EmployeeId** | int | NOT NULL |  | FK -> Employee.uid. |
| 🟢 NEW | **EventType** | varchar(20) | NOT NULL |  | `Joined`, `Promotion`, `Transfer`, `Resignation`, `Termination`, `Rejoin`, `Other` (CHECK; more types are a CHECK change, B19). |
| 🟢 NEW | **EventDate** | date | NOT NULL |  | The day the event took effect (last working day for a resignation or termination). |
| 🟢 NEW | **FromProjectId** / **ToProjectId** | int | NULL |  | FK -> Projects.Id. From = the position when the event was recorded; To = set only when the event changes it. |
| 🟢 NEW | **FromDepartmentId** / **ToDepartmentId** | int | NULL |  | FK -> Departments.Id. |
| 🟢 NEW | **FromDesignationId** / **ToDesignationId** | int | NULL |  | FK -> Designations.Id. |
| 🟢 NEW | **Remarks** | nvarchar(1000) | NULL |  | Reason, letter reference. Required for `Other`. |
| 🟢 NEW | **IsVoided** | bit | NOT NULL | 0 | Voided entries stay in the table and on screen (S14). |
| 🟢 NEW | **VoidReason** | nvarchar(500) | NULL |  | Required when voided (CK). |
| 🟢 NEW | **VoidedAt** | datetime2(0) | NULL |  | Set with `IsVoided`. |
| 🟢 NEW | **VoidedBy** | nvarchar(100) | NULL |  | Username. |
| 🟢 NEW | **CreatedAt** | datetime2(0) | NOT NULL | sysdatetime() | When it was recorded. |
| 🟢 NEW | **CreatedBy** | nvarchar(100) | NULL |  | Username of the recorder (`Migration` for backfilled rows). |
| 🟢 NEW | **RequestId** | uniqueidentifier | NULL |  | S21 idempotency key from the form: a double click records one event. |

Keys and indexes
- 🟢 `PK_EmployeeEvents` - primary key, clustered, (Id).
- 🟢 `IX_EmployeeEvents_Employee_Date` - nonclustered, (EmployeeId, EventDate, Id) INCLUDE (EventType, IsVoided). FK index (S16) that serves "this person's timeline".
- 🟢 `UX_EmployeeEvents_Employee_Joined` - unique, nonclustered, (EmployeeId) WHERE EventType = 'Joined' AND IsVoided = 0. One live joining per person; a second stint is a `Rejoin`.
- 🟢 `UX_EmployeeEvents_RequestId` - unique, nonclustered, (RequestId) WHERE RequestId IS NOT NULL.
- 🟢 `FK_EmployeeEvents_Employee`, `_FromProject`, `_ToProject`, `_FromDepartment`, `_ToDepartment`, `_FromDesignation`, `_ToDesignation`.
- 🟢 `CK_EmployeeEvents_Type` - EventType IN the seven values above.
- 🟢 `CK_EmployeeEvents_Void` - IsVoided = 0 OR (VoidReason IS NOT NULL AND VoidedAt IS NOT NULL).

## Table: dbo.EmployeeLegacyLeaveBalances [NEW]
Read-only archive of the seven per-year leave figures that lived on `Employee` (decision B18, script 10, 2026-11-24). Nothing in the app reads or writes it; it exists so the old spreadsheet figures can still be looked up. One row per employee that had any value.
Rows after the archive on the development copy: 827.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **EmployeeId** | int | NOT NULL |  | PK and FK -> Employee.uid. |
| 🟢 NEW | **CarryForwardLeaves**, **CarryForwardLeaves1**, **Year2022**, **Year2023** | float | NULL |  | Original names and values, unchanged. |
| 🟢 NEW | **Year2023New** | decimal(18,2) | NULL |  | Original. |
| 🟢 NEW | **Year2024**, **AdjustedAjusted** | int | NULL |  | Original. |
| 🟢 NEW | **ArchivedAt** | datetime2(0) | NOT NULL | sysdatetime() | When the row was archived. |

Keys and indexes
- 🟢 `PK_EmployeeLegacyLeaveBalances` - primary key, clustered, (EmployeeId); `FK_EmployeeLegacyLeaveBalances_Employee`.

## Table: dbo.EmployeeLeaves [TO BE REMOVED]
Detailed leave records per employee (one of two parallel leave models). Empty in production and has no primary key.
Rows on 2026-11-16: 0.
- 2026-11-16: Replaced by `LeaveRequests`, which the app actually uses. Nothing to migrate (empty). Dropped after any code referencing it is removed; section kept per rule 3. Where a field lives now is shown in Notes.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🔴 DROP | uid | int | NOT NULL |  | Identity. -> LeaveRequests.Id. |
| 🔴 DROP | EmployeeID | varchar(50) | NOT NULL |  | String key. -> LeaveRequests.EmployeeId (int). |
| 🔴 DROP | LeaveTypeName | varchar(250) | NOT NULL |  | -> LeaveRequests.LeaveTypeId. |
| 🔴 DROP | StartDate | date | NOT NULL |  | -> LeaveRequests.FromDate. |
| 🔴 DROP | EndDate | date | NOT NULL |  | -> LeaveRequests.ToDate. |
| 🔴 DROP | TotalDays | float | NULL |  | -> LeaveRequests.TotalDays decimal(5,1). |
| 🔴 DROP | AddDays | int | NULL |  | Not carried over; meaning unclear. Use LeaveEntitlementOverrides for adjustments. |
| 🔴 DROP | ExcludeDays | int | NULL |  | Not carried over; weekly offs/holidays are excluded when TotalDays is calculated. |
| 🔴 DROP | Short_Adj | varchar(MAX) | NULL |  | Not carried over; meaning unclear. |
| 🔴 DROP | DepSupervisorComments | varchar(MAX) | NULL |  | -> LeaveRequests.SupervisorComments. |
| 🔴 DROP | Year | varchar(50) | NULL |  | Derived from FromDate. |
| 🔴 DROP | Status | nvarchar(50) | NULL |  | -> LeaveRequests.Status (constrained). |
| 🔴 DROP | ApprovedBy | varchar(50) | NULL |  | -> LeaveRequests.ApprovedByUserId (Users.uid). |
| 🔴 DROP | ApprovedOn | date | NULL |  | -> LeaveRequests.ApprovedAt. |
| 🔴 DROP | AppliedDate | date | NULL |  | -> LeaveRequests.AppliedAt. |

Keys and indexes
- No primary key in the database. 🔴 Whole table dropped in the last phase.

## Table: dbo.GazettedHolidays [CHANGED]
Public (gazetted) holidays. Empty in production on 2026-11-16.
Rows on 2026-11-16: 0.
- 2026-11-16: Resolves the EF mismatch: the EF entity (`GazettedHoliday`, with `Id`, `HolidayName`) is mapped to this plural table with `ToTable("GazettedHolidays")`; `database-schema.sql` should create the plural name. Empty, so it is rebuilt.
- 2026-11-16: `ProjectId` NULL = holiday for everyone; a value = project-specific. SQL Server unique indexes treat NULLs as equal, so the key below allows one company-wide row plus one row per project for the same date.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity (EF entity key). |
| 🟡 CHANGED | **HolidayDate** | date | NOT NULL |  | Type unchanged; was the PK. Now part of a unique key with ProjectId. |
| 🟢 NEW | **HolidayName** | nvarchar(100) | NOT NULL |  | In the EF entity. |
|  | Description | nvarchar(255) | NULL |  |  |
| 🟢 NEW | **ProjectId** | int | NULL |  | FK -> Projects.Id. NULL = all projects. |
| 🟢 NEW | **IsOptional** | bit | NOT NULL | 0 | Optional holidays (employees may work); not auto-applied by processing. |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🔴 `PK__Gazetted__F1ADD9D04D937075` - primary key, clustered, (HolidayDate). Replaced.
- 🟢 `PK_GazettedHolidays` - primary key, clustered, (Id).
- 🟢 `UX_GazettedHolidays_Date_Project` - unique, nonclustered, (HolidayDate, ProjectId). Idempotency key: the same holiday entered twice is rejected.
- 🟢 `IX_GazettedHolidays_ProjectId` - nonclustered, (ProjectId) WHERE ProjectId IS NOT NULL.

## Table: dbo.HalfDayRequests [NEW]
Half-day requests with approval and documentation. HR rule: half days (until at least 12-1pm) need approval with documentation; whether the employee must still check out depends on the approval and the case, so it is a per-request flag.
Rows: new table.
- ⏸ B1: whether an approved half day is charged to leave balance is open. Drafted with an optional link (`LeaveRequestId NULL`); if HR says "always charged" this table merges into `LeaveRequests`, if "never" the column is dropped.
- ⏸ B7: the exact half-day rule (minimum hours, cutoff, whether a document is mandatory) is open; `DocumentId` is nullable and has no CHECK until answered.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **EmployeeId** | int | NOT NULL |  | FK -> Employee.uid. |
| 🟢 NEW | **HalfDayDate** | date | NOT NULL |  | Shift date the half day applies to. |
| 🟢 NEW | **HalfType** | varchar(10) | NOT NULL |  | `FirstHalf` (arrives late) or `SecondHalf` (leaves early). |
| 🟢 NEW | **ExpectedTime** | time(0) | NULL |  | SecondHalf: expected leave time (not before Shifts.HalfDayCutoffTime). FirstHalf: expected arrival. |
| 🟢 NEW | **Reason** | nvarchar(500) | NOT NULL |  |  |
| 🟢 NEW | **DocumentId** | int | NULL |  | FK -> EmployeeDocuments.Id (supporting document). |
| 🟢 NEW | **RequiresCheckout** | bit | NOT NULL | 1 | 0 = approver accepts no check-out for this case; processing then doesn't mark MissingCheckout. |
| ⏸ HELD | **LeaveRequestId** | int | NULL |  | B1. FK -> LeaveRequests.Id when the half day is charged to leave. |
| 🟢 NEW | **Status** | varchar(10) | NOT NULL | 'Pending' | `Pending`, `Approved`, `Rejected`, `Cancelled`. |
| 🟢 NEW | **ApprovedByUserId** | int | NULL |  | FK -> Users.uid. Approver or rejecter. |
| 🟢 NEW | **ApprovedAt** | datetime2(0) | NULL |  |  |
| 🟢 NEW | **ReviewerComments** | nvarchar(500) | NULL |  |  |
| 🟢 NEW | **Notes** | nvarchar(1000) | NULL |  | S24, ⏸ T7: HR's short note. |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |
| 🟢 NEW | **RowVersion** | rowversion | NOT NULL |  | S15. |

Keys and indexes
- 🟢 `PK_HalfDayRequests` - primary key, clustered, (Id).
- 🟢 `UX_HalfDayRequests_Employee_Date` - unique, nonclustered, (EmployeeId, HalfDayDate) WHERE Status IN ('Pending','Approved'). Idempotency key.
- 🟢 `IX_HalfDayRequests_DocumentId` - (DocumentId) WHERE DocumentId IS NOT NULL; `IX_HalfDayRequests_ApprovedByUserId` - (ApprovedByUserId) WHERE ApprovedByUserId IS NOT NULL; `IX_HalfDayRequests_LeaveRequestId` - (LeaveRequestId) WHERE LeaveRequestId IS NOT NULL (⏸ B1).
- 🟢 `CK_HalfDayRequests_HalfType` - HalfType IN ('FirstHalf','SecondHalf'); `CK_HalfDayRequests_Status` - Status IN ('Pending','Approved','Rejected','Cancelled').
- 🟢 `CK_HalfDayRequests_Approval` - Status IN ('Pending','Cancelled') OR (ApprovedByUserId IS NOT NULL AND ApprovedAt IS NOT NULL).

## Table: dbo.LeaveEntitlementOverrides [NEW]
Per-person, per-case changes to the standard leave quota. HR rule: everyone has the same quota, but HR may change it for one person in a specific case; each change keeps its reason and approver instead of editing a shared number. Append-only (set C only).
Rows: new table.
- ⏸ B5: leave year (calendar assumed), pro-rating and carry-forward cap are open.
- 2026-11-24 (B18): the legacy per-year figures were archived, not converted into overrides.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **EmployeeId** | int | NOT NULL |  | FK -> Employee.uid. |
| 🟢 NEW | **LeaveTypeId** | int | NOT NULL |  | FK -> LeaveTypes.Id. |
| 🟢 NEW | **Year** | int | NOT NULL |  | Leave year (T5: int). |
| 🟢 NEW | **AdjustmentDays** | decimal(5,1) | NULL |  | + adds days, - removes days. Several adjustments per year allowed (one per case). |
| 🟢 NEW | **OverrideTotalDays** | decimal(5,1) | NULL |  | Replaces the standard quota entirely for this person/type/year. |
| 🟢 NEW | **Reason** | nvarchar(500) | NOT NULL |  |  |
| 🟢 NEW | **ApprovedByUserId** | int | NOT NULL |  | FK -> Users.uid. |
| 🟢 NEW | **CreatedAt** | datetime2(0) | NOT NULL | sysdatetime() |  |
| 🟢 NEW | **CreatedBy** | nvarchar(100) | NULL |  |  |
| 🟢 NEW | **RequestId** | uniqueidentifier | NULL |  | S21: no natural key (several adjustments per year are allowed), so this is the idempotency key. A "+5 days" posted twice is rejected instead of giving +10. |

Keys and indexes
- 🟢 `PK_LeaveEntitlementOverrides` - primary key, clustered, (Id).
- 🟢 `IX_LeaveEntitlementOverrides_Employee_Year` - nonclustered, (EmployeeId, Year, LeaveTypeId).
- 🟢 `IX_LeaveEntitlementOverrides_LeaveTypeId` - (LeaveTypeId); `IX_LeaveEntitlementOverrides_ApprovedByUserId` - (ApprovedByUserId).
- 🟢 `UX_LeaveEntitlementOverrides_Total` - unique, nonclustered, (EmployeeId, LeaveTypeId, Year) WHERE OverrideTotalDays IS NOT NULL. Only one full override per person/type/year.
- 🟢 `UX_LeaveEntitlementOverrides_RequestId` - unique, nonclustered, (RequestId) WHERE RequestId IS NOT NULL.
- 🟢 `CK_LeaveEntitlementOverrides_OneValue` - exactly one of AdjustmentDays / OverrideTotalDays is NOT NULL.

## Table: dbo.LeaveQuota [CHANGED]
The **standard** leave policy: days per leave type per leave year, the same for every employee. Per-person changes go to `LeaveEntitlementOverrides`.
Rows on 2026-11-16: 3 (Annual Leave 10, Casual Leave 10, Sick Leave 15, all for 2026).
- 2026-11-16: Kept under its current name to limit app changes. The 3 live rows are mapped to `LeaveTypes` by `LeaveTypeName`.
- 2026-11-16: Does not apply to employees whose leave is not managed (`COALESCE(Employee.IsLeaveManagedOverride, Projects.HasLeaveManagement) = 0`, e.g. Dastarkhuwans).
- ⏸ B5: standard days per type and the leave year are open; the 3 rows are the current figures.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟡 CHANGED | **Id** | int | NOT NULL |  | Identity. was `UID`, not a key. Becomes the PK (EF already declares it); renamed at contract. |
| 🟢 NEW | **LeaveTypeId** | int | NOT NULL |  | FK -> LeaveTypes.Id. Added NULL, backfilled from LeaveTypeName, then NOT NULL. |
| 🔴 DROP | LeaveTypeName | varchar(50) | NOT NULL |  | Replaced by LeaveTypeId. |
| 🟡 CHANGED | **TotalDays** | decimal(5,1) | NOT NULL |  | was `TotalLeaves` int NOT NULL. Half days; one decimal (script 09). |
| 🟡 CHANGED | **Year** | int | NOT NULL |  | was varchar(50) (T5). Live values convert. |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🟢 `PK_LeaveQuota` - primary key, clustered, (Id). (was: no primary key)
- 🟢 `UX_LeaveQuota_Type_Year` - unique, nonclustered, (LeaveTypeId, Year). Idempotency key; also leads the `LeaveTypeId` FK.
- 🟢 `CK_LeaveQuota_TotalDays` - TotalDays >= 0.

## Table: dbo.LeaveRequests [CHANGED]
The single leave model (replaces the `EmployeeLeaves` / `LeaveRequests` split). Request -> approval, linked to employee, leave type and an optional document. Existing `AppliedAt`/`CreatedBy` serve as the creation audit.
Rows on 2026-11-16: 24 (live statuses: `Applied` 7, `Approved` 17).
- 2026-11-16: The 24 live rows are migrated in place: `LeaveType` text -> `LeaveTypeId`, dates truncated to `date`, `Status` mapped (`Applied` -> `Pending`, `Approved` stays), `TotalDays` calculated. The 1 row pointing at a missing employee must be fixed or archived before the FK is added.
- 2026-11-17: The 17 `Approved` rows have no approver, so no CHECK ties `Approved` to `ApprovedByUserId` here (unlike `HalfDayRequests`); add it once the approver is known or on new rows only.
- 2026-11-17: Live check: no exact duplicate requests, but **requests 21 and 22 (employee uid 143) overlap**. Review with HR and fix or cancel before overlap enforcement.
- 2026-11-16: A request may not cross a leave-year boundary (split it), so balances per year stay simple (⏸ B5).

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | Id | int | NOT NULL |  | PK, identity. |
| 🟡 CHANGED | **EmployeeId** | int | NOT NULL |  | Type unchanged. Real FK -> Employee.uid (was convention only). |
| 🟢 NEW | **LeaveTypeId** | int | NOT NULL |  | FK -> LeaveTypes.Id. Added NULL, backfilled from LeaveType, then NOT NULL. |
| 🔴 DROP | LeaveType | nvarchar(MAX) | NOT NULL |  | Replaced by LeaveTypeId. |
| 🟡 CHANGED | **FromDate** | date | NOT NULL |  | was datetime2(7). Leave is whole days; check no live row has a time part first. |
| 🟡 CHANGED | **ToDate** | date | NOT NULL |  | was datetime2(7). |
| 🟢 NEW | **IsHalfDay** | bit | NOT NULL | 0 | Half-day leave (only for LeaveTypes.IsHalfDayAllowed = 1). ⏸ B1. |
| 🟢 NEW | **TotalDays** | decimal(5,1) | NOT NULL |  | Days charged to the balance, after excluding weekly offs and holidays (⏸ B5). |
| 🟢 NEW | **Reason** | nvarchar(500) | NULL |  |  |
| 🟢 NEW | **DocumentId** | int | NULL |  | FK -> EmployeeDocuments.Id. |
| 🟡 CHANGED | **Status** | varchar(10) | NOT NULL | 'Pending' | was nvarchar(MAX) NULL. `Pending`, `Approved`, `Rejected`, `Cancelled`. |
| 🟢 NEW | **ApprovedByUserId** | int | NULL |  | FK -> Users.uid. |
| 🟢 NEW | **ApprovedAt** | datetime2(0) | NULL |  |  |
| 🟢 NEW | **SupervisorComments** | nvarchar(1000) | NULL |  | From EmployeeLeaves.DepSupervisorComments. |
| 🟢 NEW | **Notes** | nvarchar(1000) | NULL |  | S24, ⏸ T7: HR's short note (`Reason` is the applicant's). |
| 🟢 NEW | **AppliedAt** | datetime2(0) | NOT NULL | sysdatetime() | The 24 existing rows have no original applied date; they get the migration time (noted in the migration). |
| 🟢 NEW | **CreatedBy** | nvarchar(100) | NULL |  |  |
| 🟢 NEW | **ModifiedAt** | datetime2(0) | NULL |  |  |
| 🟢 NEW | **ModifiedBy** | nvarchar(100) | NULL |  |  |
| 🟢 NEW | **RowVersion** | rowversion | NOT NULL |  | S15: two approvers cannot silently overwrite each other. |

Keys and indexes
- `PK_LeaveRequests` - primary key, clustered, (Id).
- 🟢 `IX_LeaveRequests_Employee_FromDate` - nonclustered, (EmployeeId, FromDate) INCLUDE (ToDate, Status, LeaveTypeId, TotalDays).
- 🟢 `IX_LeaveRequests_LeaveTypeId` - (LeaveTypeId); `IX_LeaveRequests_DocumentId` - (DocumentId) WHERE DocumentId IS NOT NULL; `IX_LeaveRequests_ApprovedByUserId` - (ApprovedByUserId) WHERE ApprovedByUserId IS NOT NULL.
- 🟢 `FK_LeaveRequests_Employee`, `FK_LeaveRequests_LeaveTypes`, `FK_LeaveRequests_EmployeeDocuments`, `FK_LeaveRequests_Users`.
- 🟢 `CK_LeaveRequests_Dates` - ToDate >= FromDate; `CK_LeaveRequests_HalfDay` - IsHalfDay = 0 OR (FromDate = ToDate AND TotalDays = 0.5).
- 🟢 `CK_LeaveRequests_SingleYear` - YEAR(FromDate) = YEAR(ToDate) (2026-11-24, script 09). One request lies in one calendar year, so each request belongs to exactly one balance year; a leave over 31 December is entered as two requests. The LMS screens say so.
- 🟢 `CK_LeaveRequests_Status` - Status IN ('Pending','Approved','Rejected','Cancelled').
- 🟢 `UX_LeaveRequests_Open_Duplicate` - unique, nonclustered, (EmployeeId, FromDate, ToDate, LeaveTypeId) WHERE Status IN ('Pending','Approved'). S22: blocks an exact double submit. True overlaps are checked in the save transaction under `sp_getapplock` on the employee.

## Table: dbo.LeaveTypes [NEW]
Leave type lookup, replacing the leave type names spread over `Configuration` (LeaveTypes list), `LeaveQuota.LeaveTypeName`, `LeaveRequests.LeaveType` and `EmployeeLeaves.LeaveTypeName`.
Rows: new table (seeded from those sources, de-duplicated and reviewed; the live quota has Annual, Casual and Sick Leave).

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **Code** | varchar(10) | NOT NULL |  | Short code for reports, e.g. `CL`, `SL`, `AL`. |
| 🟢 NEW | **Name** | nvarchar(50) | NOT NULL |  |  |
| 🟢 NEW | **IsPaid** | bit | NOT NULL | 1 | ⏸ B11 (effect on payroll). |
| 🟢 NEW | **IsHalfDayAllowed** | bit | NOT NULL | 1 |  |
| 🟢 NEW | **IsActive** | bit | NOT NULL | 1 |  |
| 🟢 NEW | **SortOrder** | int | NOT NULL | 0 |  |
| 🟢 NEW | **AllowsCarryForward** | bit | NOT NULL | 1 | 2026-11-24 (script 11): false = the year-end rollover skips this type. Unpaid and Compensation are seeded as 0. HR edits it in Setup -> Lists. Holds the carry-forward eligibility part of B5 in data. |
| 🟢 NEW | **MaxCarryForwardDays** | decimal(5,1) | NULL |  | 2026-11-24 (script 11): most days the rollover carries into the next year; NULL = no cap (`CK_LeaveTypes_MaxCarryForward`: NULL or >= 0). |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🟢 `PK_LeaveTypes` - primary key, clustered, (Id).
- 🟢 `UX_LeaveTypes_Code` - unique, nonclustered, (Code); 🟢 `UX_LeaveTypes_Name` - unique, nonclustered, (Name).

## Table: dbo.PayslipDetails [CHANGED]
Line items of a payslip (Allowance / Deduction / Tax). Pure child rows of `Payslips`, so no audit columns (S13).
Rows on 2026-11-16: 132.
- 2026-11-17 (T4 pending): regeneration of an unlocked payslip deletes and re-inserts its lines in one transaction; the unique key makes a double insert fail instead of doubling. Live check: 0 duplicate groups among the 132 rows.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | Id | int | NOT NULL |  | PK, identity. |
|  | PayslipId | int | NOT NULL |  | FK -> Payslips.Id (`FK_PayslipDetails_Payslips`, ON DELETE CASCADE). |
| 🟡 CHANGED | **ItemType** | varchar(20) | NOT NULL |  | was nvarchar(20). Code (S11): `Allowance`, `Deduction` or `Tax`, with a CHECK. |
|  | ItemName | nvarchar(200) | NOT NULL |  |  |
|  | ItemCategory | nvarchar(50) | NOT NULL |  |  |
|  | Amount | decimal(18,2) | NOT NULL |  |  |
|  | SortOrder | int | NULL |  |  |

Keys and indexes
- `PK_PayslipDetails` - primary key, clustered, (Id).
- 🟢 `UX_PayslipDetails_Payslip_Item` - unique, nonclustered, (PayslipId, ItemType, ItemName). Serves the `PayslipId` FK lookups (leading column), so no separate `IX_PayslipDetails_PayslipId`.
- 🟢 `CK_PayslipDetails_ItemType` - ItemType IN ('Allowance','Deduction','Tax').

## Table: dbo.Payslips [CHANGED]
Payslip header: one per employee per month/year, with totals and lock state. Existing `GeneratedDate/GeneratedBy` serve as the audit columns. Table name stays.
Rows on 2026-11-16: 99 (test data; 8 locked; 11 month/year combinations, a few dated Jan/Feb 2027, i.e. in the future).
- 2026-11-17 (T2): the period is `Year` (int) + `Month` (tinyint 1-12); the text month name is shown by the app. Expand/contract: `MonthNo` is added, backfilled from the text name, the app switches, the text `Month` is dropped, then `MonthNo` is renamed `Month` (collation is case-insensitive, so the old and new `Month` can't coexist).
- 2026-11-17 (T6): payroll finds the tax year from the payslip: `Month >= 7 ? Year : Year - 1`, matched to `TaxRules.TaxYearStart`.
- 2026-11-17 (S18): `TotalAllowances`, `TotalDeductions`, `NetSalary`, `WorkingDays`, `LeaveDays`, `LeaveBalance` and the hourly columns are **snapshots** of the generated slip (they must not change when attendance or rules change later).
- 2026-11-16: `WorkingDays` and `LeaveDays` are now filled from `Attendance` / `LeaveRequests`; hourly pay is a separate figure so it shows on the slip.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | Id | int | NOT NULL |  | PK, identity. |
|  | EmployeeId | int | NOT NULL |  | FK -> Employee.uid (`FK_Payslips_Employee`, no action). |
| 🟡 CHANGED | **Month** | tinyint | NOT NULL |  | was nvarchar(20) English month name. 1-12. Temporary name `MonthNo` until contract. |
|  | Year | int | NOT NULL |  | Calendar year of the payslip period. |
|  | BasicSalary | decimal(18,2) | NOT NULL |  |  |
|  | GrossSalary | decimal(18,2) | NOT NULL |  |  |
|  | TotalDeductions | decimal(18,2) | NULL |  | Sum of Deductions lines only; excludes income tax (see TaxAmount). Snapshot. |
|  | TotalAllowances | decimal(18,2) | NULL |  | Added by migration 20260512100000 (not recorded in __EFMigrationsHistory). Snapshot. |
|  | NetSalary | decimal(18,2) | NOT NULL |  | Gross - TotalDeductions - TaxAmount. Snapshot. |
| 🟡 CHANGED | **WorkingDays** | int | NULL |  | Type unchanged; was not populated. Filled from Attendance (exact rule per HR). Snapshot. |
| 🟡 CHANGED | **LeaveDays** | decimal(5,2) | NULL |  | Type unchanged; was not populated. Filled from approved LeaveRequests in the month. Snapshot. |
|  | LeaveBalance | nvarchar(MAX) | NULL |  | Not populated by the app. Could be filled from vLeaveBalances (snapshot). |
| 🟢 NEW | **HourlyMinutes** | int | NULL |  | Sum of PayableMinutes on `Hourly` days (⏸ B6). |
| 🟢 NEW | **HourlyAmount** | decimal(18,2) | NULL |  | HourlyMinutes / 60 x hourly rate (⏸ B6). |
|  | CalculationDetails | nvarchar(MAX) | NOT NULL |  | Snapshot of how the slip was calculated. |
| 🟡 CHANGED | **GeneratedDate** | datetime2(0) | NOT NULL | sysdatetime() | was datetime2(7) with no default. |
|  | GeneratedBy | nvarchar(100) | NOT NULL |  |  |
| 🟡 CHANGED | **IsLocked** | bit | NOT NULL | 0 | Same type; default added (S12). Set to 1 for all of an employee's payslips when their allowances/deductions change. |
| 🟡 CHANGED | **LockedDate** | datetime2(0) | NULL |  | was datetime2(7). |
|  | Notes | nvarchar(1000) | NULL |  |  |
| 🟡 CHANGED | **TaxPercentage** | decimal(5,2) | NOT NULL | 0 | was decimal(18,2). Live max is 10.00 (S8). |
|  | TaxAmount | decimal(18,2) | NOT NULL | 0 |  |
| 🟢 NEW | **RowVersion** | rowversion | NOT NULL |  | S15. |

Keys and indexes
- `PK_Payslips` - primary key, clustered, (Id).
- 🟢 `UX_Payslips_Employee_Year_Month` - unique, nonclustered, (EmployeeId, Year, Month). S21 idempotency key: a double click or re-run cannot create a second payslip for the same month. Live check: 0 duplicate groups. (Named `..._MonthNo` until the contract rename.) Also leads the `EmployeeId` FK.
- 🟢 `CK_Payslips_Month` - Month BETWEEN 1 AND 12; `CK_Payslips_Year` - Year BETWEEN 2000 AND 2100.

## Table: dbo.Projects [NEW]
Project lookup: one of the two org dimensions of an employee, next to Department. **A dynamic, HR-managed list** (decision B3, 2026-11-18): no number of projects and no project name is built into the schema or the app. HR adds, renames, reorders and deactivates projects from Setup.
Rows: new table, seeded from the distinct `Employee.Project` values of the test data for development; HR supplies the real list at go-live.
- 2026-11-18 (B3): `Employee.Project` is free text and the Configuration project list is a comma-separated string, so the two had drifted apart (the earlier "6 or 8 projects" question is dropped: the count is whatever HR keeps in this table). The data in the database is test data and is not a source of truth for the list.
- 2026-11-18 (S6, S14): a project that has employees or history is never hard-deleted; `IsActive = 0` hides it from new-employee dropdowns and keeps old records valid. The FK from `Employee.ProjectId` is `NO ACTION`, so the database refuses to delete a project that is still used. A project nobody uses may be deleted.
- 2026-11-18: `HasLeaveManagement` and `SortOrder` are per project and edited by HR; nothing in code assumes a particular project (the Dastarkhuwans rule is the value `0` on that project's row, not a name check).

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **Name** | nvarchar(100) | NOT NULL |  | e.g. Bahria Town Karachi, Bahria Dastarkhuwans. |
| 🟢 NEW | **Code** | varchar(20) | NULL |  | Short code for reports. |
| 🟢 NEW | **SortOrder** | int | NOT NULL | 0 | First sort key of employee lists. |
| 🟢 NEW | **HasLeaveManagement** | bit | NOT NULL | 1 | 0 for Bahria Dastarkhuwans (no leave management). |
| 🟢 NEW | **IsActive** | bit | NOT NULL | 1 |  |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🟢 `PK_Projects` - primary key, clustered, (Id).
- 🟢 `UX_Projects_Name` - unique, nonclustered, (Name).

## Table: dbo.RecordNotes [NEW]
Dated, signed notes HR writes against a record, for the edge cases HR tracks: a forgotten check-out told to HR, a late arrival with its reason, a special arrangement. Append-only: a note is never edited; a wrong one is retracted with `IsDeleted` (S14), kept in the table and hidden from the screen. ⏸ T7.
Rows: new table.
- 2026-11-17 (S5): there is no generic `TableName`/`RecordId` pair, because that cannot be a foreign key. Each target is its own nullable FK column and exactly one is set per note. The targets drafted are the attendance day, the employee and the leave request; another target later is one more nullable FK column, its index and a term in the CHECK.
- 2026-11-17: Context only. A note never feeds processing or payroll; a change to a day's status or pay is an `AttendanceCorrections` row (or a half-day or leave request) with the note as extra explanation.
- 2026-11-17 (S24): the `Notes` column on each HR table holds the current short note; this table keeps the history with who wrote it and when.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **AttendanceId** | int | NULL |  | FK -> Attendance.Id. Set when the note is about one shift day (the main use). |
| 🟢 NEW | **EmployeeId** | int | NULL |  | FK -> Employee.uid. Set when the note is about the person, not a day. |
| 🟢 NEW | **LeaveRequestId** | int | NULL |  | FK -> LeaveRequests.Id. |
| 🟢 NEW | **Note** | nvarchar(1000) | NOT NULL |  | The text. |
| 🟢 NEW | **IsDeleted** | bit | NOT NULL | 0 | Retracted note: kept, hidden. Who retracted it is in `AuditLogs`. |
| 🟢 NEW | **CreatedAt** | datetime2(0) | NOT NULL | sysdatetime() | When the note was written. |
| 🟢 NEW | **CreatedBy** | nvarchar(100) | NULL |  | Username of the author. |
| 🟢 NEW | **RequestId** | uniqueidentifier | NULL |  | S21 idempotency key, generated by the form: a double click does not write the same note twice. |

Keys and indexes
- 🟢 `PK_RecordNotes` - primary key, clustered, (Id).
- 🟢 `IX_RecordNotes_AttendanceId` - nonclustered, (AttendanceId, CreatedAt) WHERE AttendanceId IS NOT NULL; `IX_RecordNotes_EmployeeId` - (EmployeeId, CreatedAt) WHERE EmployeeId IS NOT NULL; `IX_RecordNotes_LeaveRequestId` - (LeaveRequestId, CreatedAt) WHERE LeaveRequestId IS NOT NULL. FK indexes (S16) that also serve "notes of this day/person, newest first".
- 🟢 `UX_RecordNotes_RequestId` - unique, nonclustered, (RequestId) WHERE RequestId IS NOT NULL.
- 🟢 `FK_RecordNotes_Attendance`, `FK_RecordNotes_Employee`, `FK_RecordNotes_LeaveRequests`.
- 🟢 `CK_RecordNotes_OneTarget` - (CASE WHEN AttendanceId IS NULL THEN 0 ELSE 1 END + CASE WHEN EmployeeId IS NULL THEN 0 ELSE 1 END + CASE WHEN LeaveRequestId IS NULL THEN 0 ELSE 1 END) = 1.
- 🟢 `CK_RecordNotes_Note` - LEN(LTRIM(RTRIM(Note))) > 0.

## Table: dbo.RosterOverrides [NEW]
One-off changes to an employee's schedule on a single date: shift change, extra off day, swap with a colleague, extra duty on a weekly off, without editing the regular assignment. Overrides win over `ShiftAssignments`. ⏸ B8: the full edge-case list from HR is still open.
Rows: new table.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **EmployeeId** | int | NOT NULL |  | FK -> Employee.uid. |
| 🟢 NEW | **OverrideDate** | date | NOT NULL |  | Shift date affected. |
| 🟢 NEW | **OverrideType** | varchar(15) | NOT NULL |  | `ShiftChange`, `Off`, `Swap`, `ExtraDuty`. |
| 🟢 NEW | **ShiftId** | int | NULL |  | FK -> Shifts.Id. Required for ShiftChange, ExtraDuty and Swap; NULL for Off. |
| 🟢 NEW | **SwapWithEmployeeId** | int | NULL |  | FK -> Employee.uid. Colleague for a Swap (each side gets a row). |
| 🟢 NEW | **Reason** | nvarchar(500) | NULL |  |  |
| 🟢 NEW | **Notes** | nvarchar(1000) | NULL |  | S24, ⏸ T7: HR's short note. |
| 🟢 NEW | **ApprovedByUserId** | int | NULL |  | FK -> Users.uid. |
| 🟢 NEW | **ApprovedAt** | datetime2(0) | NULL |  |  |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🟢 `PK_RosterOverrides` - primary key, clustered, (Id).
- 🟢 `UX_RosterOverrides_Employee_Date` - unique, nonclustered, (EmployeeId, OverrideDate). Idempotency key; leads the `EmployeeId` FK.
- 🟢 `IX_RosterOverrides_ShiftId` - (ShiftId) WHERE ShiftId IS NOT NULL; `IX_RosterOverrides_SwapWithEmployeeId` - (SwapWithEmployeeId) WHERE SwapWithEmployeeId IS NOT NULL; `IX_RosterOverrides_ApprovedByUserId` - (ApprovedByUserId) WHERE ApprovedByUserId IS NOT NULL.
- 🟢 `CK_RosterOverrides_Type` - OverrideType IN ('ShiftChange','Off','Swap','ExtraDuty').
- 🟢 `CK_RosterOverrides_Shift` - (OverrideType = 'Off' AND ShiftId IS NULL) OR (OverrideType <> 'Off' AND ShiftId IS NOT NULL); `CK_RosterOverrides_Swap` - OverrideType <> 'Swap' OR SwapWithEmployeeId IS NOT NULL.

## Table: dbo.ShiftAssignments [NEW]
Which shift an employee works, on which weekdays, over which date range. Attendance follows the schedule; weekly offs differ per person. ⏸ B8.
Rows: new table.
- 2026-11-16: Day bitmask Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64 (Mon-Fri = 31, Mon-Sat = 63). Day bit for a date `d` = `POWER(2, DATEDIFF(day, '19000101', d) % 7)` (1900-01-01 was a Monday; independent of `SET DATEFIRST`).
- 2026-11-16: When two assignments cover the same date, the higher `Priority` wins (e.g. a Ramadan timing over the regular shift). Same start date and same priority is blocked by the unique key below; other overlaps at the same priority are blocked in the save transaction under `sp_getapplock` (S22).

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **EmployeeId** | int | NOT NULL |  | FK -> Employee.uid. |
| 🟢 NEW | **ShiftId** | int | NOT NULL |  | FK -> Shifts.Id. |
| 🟢 NEW | **EffectiveFromDate** | date | NOT NULL |  |  |
| 🟢 NEW | **EffectiveToDate** | date | NULL |  | NULL = open-ended. |
| 🟢 NEW | **DaysOfWeekMask** | tinyint | NOT NULL |  | Days this shift is worked. |
| 🟢 NEW | **WeeklyOffMask** | tinyint | NOT NULL | 0 | Rest days (become `WeeklyOff` in Attendance). |
| 🟢 NEW | **Priority** | tinyint | NOT NULL | 0 | Higher wins on overlap. |
| 🟢 NEW | **Notes** | nvarchar(1000) | NULL |  | S24, ⏸ T7: HR's short note (e.g. why a Ramadan timing applies). |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🟢 `PK_ShiftAssignments` - primary key, clustered, (Id).
- 🟢 `IX_ShiftAssignments_Employee_From` - nonclustered, (EmployeeId, EffectiveFromDate) INCLUDE (EffectiveToDate, ShiftId, DaysOfWeekMask, WeeklyOffMask, Priority).
- 🟢 `UX_ShiftAssignments_Employee_From_Priority` - unique, nonclustered, (EmployeeId, EffectiveFromDate, Priority). Idempotency key.
- 🟢 `IX_ShiftAssignments_ShiftId` - nonclustered, (ShiftId).
- 🟢 `CK_ShiftAssignments_Range` - EffectiveToDate IS NULL OR EffectiveToDate >= EffectiveFromDate.
- 🟢 `CK_ShiftAssignments_Masks` - both masks BETWEEN 0 AND 127 AND (DaysOfWeekMask & WeeklyOffMask) = 0 AND DaysOfWeekMask > 0.

## Table: dbo.Shifts [NEW]
Shift definitions and the attendance rules that go with them. HR rules live here as data, not code - 7 hours (420 minutes) for a full day, 1 minute late/early grace, half-day cutoff around 12-1pm - so a shift with different rules needs no code change (⏸ B6, B7, B8 for the exact values).
Rows: new table.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **Name** | nvarchar(100) | NOT NULL |  | e.g. Morning 09:00-17:00, Night 22:00-06:00. |
| 🟢 NEW | **StartTime** | time(0) | NOT NULL |  |  |
| 🟢 NEW | **EndTime** | time(0) | NOT NULL |  |  |
| 🟢 NEW | **IsOvernight** | bit | NOT NULL |  | Computed, persisted: `CASE WHEN EndTime <= StartTime THEN 1 ELSE 0 END`. Overnight shifts check out on the next calendar day (S18, S10 naming). |
| 🟢 NEW | **BreakMinutes** | smallint | NOT NULL | 0 | Unpaid break subtracted from worked time (confirm with HR whether breaks are unpaid). |
| 🟢 NEW | **MinFullDayMinutes** | smallint | NOT NULL | 420 | 7 hours; below this the day is Hourly. |
| 🟢 NEW | **GraceLateMinutes** | tinyint | NOT NULL | 1 | Official policy: 1 minute. |
| 🟢 NEW | **GraceEarlyMinutes** | tinyint | NOT NULL | 1 | Official policy: 1 minute. |
| 🟢 NEW | **HalfDayMinMinutes** | smallint | NULL |  | Minimum worked minutes for a half day (⏸ B7, e.g. 240). |
| 🟢 NEW | **HalfDayCutoffTime** | time(0) | NULL |  | Earliest leave time for a second-half half day (HR: "till at least 12-1pm", ⏸ B7). |
| 🟢 NEW | **IsActive** | bit | NOT NULL | 1 |  |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🟢 `PK_Shifts` - primary key, clustered, (Id).
- 🟢 `UX_Shifts_Name` - unique, nonclustered, (Name).
- 🟢 `CK_Shifts_Times` - StartTime <> EndTime (a 24-hour shift would need a separate flag; not expected).
- 🟢 `CK_Shifts_Minutes` - MinFullDayMinutes > 0 AND (HalfDayMinMinutes IS NULL OR HalfDayMinMinutes < MinFullDayMinutes).

## Table: dbo.SubDepartments [NEW]
Optional detail under a department: the function inside it (Mess, Transportation and Sweeper under Administration; Record, Transfer, Dispatch and others under Operations). 2026-11-18 (decision B2, project owner): **information and detail only**. No attendance, payroll, leave or approval rule reads it, and a department with no sub-departments simply has none. Each sub-department belongs to exactly one department.
Rows: new table, seeded by splitting the test-data department names at the first ` - `: 10 sub-departments under 2 departments (Administration: Mess, Sweeper, Transportation; Operations: Audit & Verification, Data Center Q.C, Dispatch, Lahore / Rawalpindi, Nawabshah, Record, Transfer).
- 2026-11-18 (S11): a table and not free text on `Employee`, because free text drifts (the test data already has `Mess` and `Admin - Mess` for the same function). The simpler alternative is a free-text `SubDepartment nvarchar(100)` column on `Employee`.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **Id** | int | NOT NULL |  | PK, identity. |
| 🟢 NEW | **DepartmentId** | int | NOT NULL |  | FK -> Departments.Id. |
| 🟢 NEW | **Name** | nvarchar(100) | NOT NULL |  | The function, without the department prefix (`Mess`, not `Admin - Mess`). |
| 🟢 NEW | **SortOrder** | int | NOT NULL | 0 | Order inside the department. |
| 🟢 NEW | **IsActive** | bit | NOT NULL | 1 | S14. |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M. |

Keys and indexes
- 🟢 `PK_SubDepartments` - primary key, clustered, (Id).
- 🟢 `UX_SubDepartments_Department_Name` - unique, nonclustered, (DepartmentId, Name). Idempotency key; leads the `DepartmentId` FK.
- 🟢 `UX_SubDepartments_Department_Id` - unique, nonclustered, (DepartmentId, Id). Target of the composite FK from `Employee`.
- 🟢 `FK_SubDepartments_Departments`.

## Table: dbo.TaxRules [CHANGED]
Income-tax slabs per tax year, on **annual** income (2026-11-25): annual tax = `FixedAmount` + `TaxPercentage` % of (annual income - `MinSalary`) for the slab with the highest `MinSalary` not above the income; monthly tax = annual tax / 12, rounded to the rupee. Method, source and slab figures: `docs/reference/pk-income-tax.md`. The taxable base (basic or gross) is still B17.
- 2026-11-25 (script `12_tax_slabs_annual.sql`, after the production run of 01-11): the flat "basic x percentage" method was wrong. `MinSalary`/`MaxSalary` are now annual income; new `FixedAmount decimal(18,2) NOT NULL DEFAULT 0` with `CK_TaxRules_Fixed`; the old test slabs of 2025 and 2026 are replaced by the real slabs (2025-26 fixed amounts derived by continuity, to be verified).
Rows on 2026-11-16: 14 (7 for 2025-2026, 7 for 2026-2027).
- 2026-11-17 (T6): the text label `TaxYear` (`2025-2026`) is replaced by `TaxYearStart smallint` (2025 = 1 Jul 2025 to 30 Jun 2026); the label is derived in the app. This fixes the tax-year bug at its cause: payroll derives the tax year from the payslip (`Month >= 7 ? Year : Year - 1`) instead of comparing the label with `2026`. Expand/contract: add `TaxYearStart`, backfill `LEFT(TaxYear, 4)`, switch the app, drop `TaxYear`. `Configuration` rows `CurrentTaxYear` and `TaxSlabs` are removed afterwards.
- 2026-11-17: Live check: no duplicate `(TaxYear, MinSalary)`; max percentage is 20.00, so `decimal(5,2)` is safe.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | Id | int | NOT NULL |  | PK, identity. |
|  | MinSalary | decimal(18,2) | NOT NULL |  |  |
|  | MaxSalary | decimal(18,2) | NULL |  | NULL or 0 = no upper limit. 2026-11-25: annual income; informational. |
| 🟢 NEW 2026-11-25 | **FixedAmount** | decimal(18,2) | NOT NULL | 0 | Tax already due at the start of the slab (script 12). `MinSalary` is now the annual income where the slab starts. |
| 🟡 CHANGED | **TaxPercentage** | decimal(5,2) | NOT NULL |  | was decimal(18,2). S8. |
| 🔴 DROP | TaxYear | nvarchar(20) | NOT NULL | '2025-2026' | Replaced by TaxYearStart; dropped after the app switch. |
| 🟢 NEW | **TaxYearStart** | smallint | NOT NULL |  | T6. Calendar year in which the July-June tax year starts. |
| 🟢 NEW | **CreatedAt / CreatedBy / ModifiedAt / ModifiedBy** | datetime2(0) / nvarchar(100) | per set M |  | Set M: who changed a slab matters for payroll. |

Keys and indexes
- `PK_TaxRules` - primary key, clustered, (Id).
- 🟢 `UX_TaxRules_Year_MinSalary` - unique, nonclustered, (TaxYearStart, MinSalary). Idempotency key: a slab saved twice is rejected.
- 🟢 `CK_TaxRules_Percentage` - TaxPercentage BETWEEN 0 AND 100; `CK_TaxRules_Year` - TaxYearStart BETWEEN 2000 AND 2100.

## Table: dbo.Users [CHANGED]
Application logins (cookie auth). Existing `CreatedBy/CreatedOn` serve as the creation audit.
Rows on 2026-11-16: 6.
- 2026-11-17 (S6, S14): logins are never deleted; `IsActive = 0` disables one, and login must reject inactive users.
- 2026-11-16: Linked to employees by `uid` like everything else. Roles must be constrained values so they can be enforced for authorization (approvals for leave, half days, corrections; ⏸ B13, B9 for the list of roles).
- 2026-11-23: One login can hold several roles (for example an HR user with Admin privileges), so the single `Role` column is replaced by the child table `UserRoles` (script `07_user_roles.sql`). The app issues one role claim per row, so `[Authorize(Roles = "Admin")]` passes for anyone who has the Admin row. A login with no rows has no role.
- 2026-11-17: Expand/contract: the new int key is added as `EmployeeUid`, backfilled by `EmployeeID` match, the app switches, the string `EmployeeId` is dropped and `EmployeeUid` is renamed `EmployeeId`.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
|  | uid | int | NOT NULL |  | PK, identity. |
| 🟡 CHANGED | **EmployeeId** | int | NULL |  | was varchar(50) NULL: Employee.EmployeeID (string), no FK; 1 row points at a missing employee. Now FK -> Employee.uid, optional (admin accounts may not be employees). The orphan stays NULL. Temporary name `EmployeeUid`. |
|  | Username | nvarchar(100) | NOT NULL |  | Now unique. |
|  | PasswordHash | nvarchar(255) | NOT NULL |  | PBKDF2 `iterations.salt.hash`. Login code should stop accepting legacy SHA-256/plain text (app change). |
| 🔴 DROP | Role | nvarchar(30) | NULL |  | was nvarchar(MAX) NULL, free text (Admin 4, HR 1, empty 1). In script 03 it became nvarchar(30) with CHECK Admin/HR; script 07 copies it to `UserRoles` and drops the column (and `CK_Users_Role`). |
| 🟢 NEW | **IsActive** | bit | NOT NULL | 1 | S6/S14: soft delete. |
| 🟡 CHANGED | **CreatedBy** | nvarchar(100) | NULL |  | was varchar(MAX) NULL. |
| 🟡 CHANGED | **CreatedOn** | datetime2(0) | NULL |  | was varchar(MAX) NULL (text). Converted with TRY_CONVERT. Existing name kept. |
| 🟢 NEW | **ModifiedAt** | datetime2(0) | NULL |  | Set M. |
| 🟢 NEW | **ModifiedBy** | nvarchar(100) | NULL |  |  |
|  | History | varchar(MAX) | NULL |  |  |

Keys and indexes
- 🟡 `PK_Users` - primary key, clustered, (uid). was `PK__Users__DD70126471BA34B8`.
- 🟢 `UX_Users_Username` - unique, nonclustered, (Username). Not enforced today; live data has 6 distinct usernames, so it can be created without cleanup.
- 🟢 `UX_Users_EmployeeId` - unique, nonclustered, (EmployeeId) WHERE EmployeeId IS NOT NULL. One login per employee; also the FK index.
- 🔴 `CK_Users_Role` - existed between scripts 03 and 07 only; replaced by `CK_UserRoles_Role`.

## Table: dbo.UserRoles [NEW]
The roles each login holds. A user can have any number of rows (Admin, HR, or both); no rows = no role. Added 2026-11-23 so certain HR users can also have Admin privileges.
Rows after script 07 on the development copy: 5 (shahid, sindhu, farhan, ryyan = Admin; Zulfiqar = HR; shahzad has none).
- 2026-11-23: A composite key, not an `Id` column: the row has no identity of its own and is never referenced (S1 exception, like a pure link table).
- 2026-11-23: The role list lives in the CHECK for now (B9, B13). Adding a role such as Supervisor means changing `CK_UserRoles_Role` and `AppRoles` in the app.

| Change | Column | Type | Null | Default | Notes |
|---|---|---|---|---|---|
| 🟢 NEW | **UserId** | int | NOT NULL |  | FK -> Users.uid, ON DELETE CASCADE (logins are normally disabled, never deleted, S6). |
| 🟢 NEW | **Role** | nvarchar(30) | NOT NULL |  | `Admin` or `HR`. |

Keys and indexes
- 🟢 `PK_UserRoles` - primary key, clustered, (UserId, Role). One row per user and role; also the FK index.
- 🟢 `FK_UserRoles_Users` - UserId -> Users.uid, cascade.
- 🟢 `CK_UserRoles_Role` - Role IN ('Admin','HR').

## Views (proposed)
- 🟢 `dbo.vEmployeeDirectory` - Employee joined to Projects, Departments, Designations. Columns: uid, EmployeeID, EmployeeName, ProjectName, DepartmentName, SubDepartmentName, DesignationName, EmployeeStatus, plus the sort keys `Projects.SortOrder`, `Departments.SortOrder`, `Designations.Rank`. Lists use either `ORDER BY p.SortOrder, d.SortOrder, g.Rank, e.EmployeeName` or, for department-first (HR's "all Admin staff, then by project"), `ORDER BY d.SortOrder, p.SortOrder, g.Rank, e.EmployeeName` (a view can't carry ORDER BY). Backed by `IX_Employee_Org` and `IX_Employee_Department_Project`.
- 🟢 `dbo.vLeaveBalances` (v2, script 11, 2026-11-24) - per employee, leave type and year. The rows are the **union** of every year that has any leave data for that employee and type: a `LeaveQuota` row, a `CarryforwardLeaves.ToYear`, a `LeaveEntitlementOverrides.Year`, or a Pending/Approved `LeaveRequests` year (so a year with leave taken but no quota still shows, with `BaseDays` 0 and a negative balance; history is never lost because a quota row is missing). Columns: `BaseDays = COALESCE(OverrideTotalDays, LeaveQuota.TotalDays, 0)`, `Adjustment = SUM(AdjustmentDays)`, `CarryForward = SUM(CarryForwardDays)` where `ToYear` = year, `Entitled = BaseDays + Adjustment + CarryForward`, `Taken = SUM(TotalDays)` of `Approved` requests whose `FromDate` is in the year, `Pending` likewise, `Remaining = Entitled - Taken`. Only employees with `COALESCE(e.IsLeaveManagedOverride, p.HasLeaveManagement) = 1`. Derived, never stored (S18). Read by one service (`LeaveBalanceService`) for both the employee Leave Balance tab and the LMS Balances page. The **year-end rollover** (LMS -> Leave Balance -> Year-end rollover) writes next year's `CarryforwardLeaves` rows from `Remaining` (never below zero, capped by `LeaveTypes.MaxCarryForwardDays`, skipping types with `AllowsCarryForward = 0`), marks them `Description = 'Year-end rollover'`, updates only such rows on a re-run, never touches hand-entered rows, and writes one `AuditLogs` row.

## Idempotency (standards S21-S23)
Every table people or imports write to is replay-safe. The guard that makes a second identical write fail or update instead of duplicate:

| Table | Guard |
|---|---|
| Employee | `UX_Employee_EmployeeID` (live: 848 distinct) |
| Users | `UX_Users_Username`; `UX_Users_EmployeeId` |
| UserRoles | `PK_UserRoles` (UserId, Role) |
| Configuration | `UX_Configuration_ConfigKey` |
| Projects / Designations / LeaveTypes / Shifts / Departments | unique `Name` (and `Code` for LeaveTypes) |
| SubDepartments | `UX_SubDepartments_Department_Name` |
| TaxRules | `UX_TaxRules_Year_MinSalary` |
| Payslips | `UX_Payslips_Employee_Year_Month` |
| PayslipDetails | `UX_PayslipDetails_Payslip_Item` |
| Allowances, Deductions | `RequestId` unique filtered index (no natural key: two active Fuel Allowance rows exist for employees 325 and 376) |
| LeaveEntitlementOverrides, AttendanceCorrections | `RequestId` unique filtered index |
| LeaveRequests | `UX_LeaveRequests_Open_Duplicate` + overlap check (S22) |
| HalfDayRequests | `UX_HalfDayRequests_Employee_Date` |
| CarryforwardLeaves | `UX_CarryforwardLeaves_Employee_Type_FromYear` |
| LeaveQuota | `UX_LeaveQuota_Type_Year` |
| GazettedHolidays | `UX_GazettedHolidays_Date_Project` |
| Attendance | `UX_Attendance_Employee_ShiftDate` |
| AttendancePunches | `UX_AttendancePunches_Dedup` (RawEmployeeCode, PunchAt, Direction) |
| AttendanceUploadLogs | `UX_AttendanceUploadLogs_FileHash` |
| EmployeeDocuments | `UX_EmployeeDocuments_Employee_FileHash` |
| RosterOverrides | `UX_RosterOverrides_Employee_Date` |
| ShiftAssignments | `UX_ShiftAssignments_Employee_From_Priority` + overlap check (S22) |
| RecordNotes | `RequestId` unique filtered index (a note has no natural key) |
| EmployeeEvents | `UX_EmployeeEvents_Employee_Joined` (one live joining per person) + `RequestId` unique filtered index |
| AuditLogs | none, append-only by design |

Concurrency (S15, `rowversion`): Employee, LeaveRequests, HalfDayRequests, Attendance, Payslips. A `rowversion` detects two people editing at once; it does **not** stop a replayed insert, which is what the keys above are for.

App-side requirements these keys depend on (not schema, listed so they are not forgotten):
- Forms that insert into Allowances, Deductions, LeaveEntitlementOverrides or AttendanceCorrections render a hidden `RequestId` GUID; the controller treats a duplicate-key error on it as "already saved" and shows the saved row instead of an error.
- Multi-step writes (payslip generation and regeneration, attendance processing, carry-forward) run as one transaction inside the EF execution strategy, because `Program.cs` enables `EnableRetryOnFailure(5)` and a retried commit would otherwise repeat the work.
- Imports and processing upsert (`MERGE`, or update then insert in a transaction) and skip rows where `IsLocked = 1`.
- The payslip lock service writes its `AuditLogs` row only when `IsLocked` actually changes.
- Upload code computes the SHA-256 file hash and answers "already imported on <date>" on a duplicate.

Findings from the current database that affect the keys (read-only checks, 2026-11-17). **This is test data (2026-11-18)**, so these are design evidence, not cleanup work; re-run the checks on the real data before creating the unique indexes:
- No duplicates exist for Payslips, PayslipDetails, Users.Username, Configuration.ConfigKey, TaxRules `(TaxYear, MinSalary)`, active Deductions, exact LeaveRequests, or EmployeeDocuments, so those indexes can be created without cleanup.
- Active Allowances: employees 325 and 376 each have two active `Fuel Allowance` rows. No unique key is added there until payroll says whether that is valid.
- LeaveRequests: requests 21 and 22 (employee uid 143) overlap. Review before overlap enforcement.
- AttendanceUploadLogs: the same file was imported 3 times in 7 minutes. This is the replay problem `FileHash` fixes.

## Other notes
- 2026-11-16: `database-schema.sql` still contains obsolete tables (`Payrolls`, `Employees`, `Attendances`) that do not exist in HRBTK; remove them when the script is regenerated from this design. They are not database tables, so they have no section here.
- 2026-11-16: Not changed on purpose: no Gender column (no HR requirement); `Employee.Details`, `Employee.GenStatus` and `Users.History` kept as they are (purpose not documented).
- 2026-11-17: EF model changes go with each phase: entities for the new tables, `ToTable("GazettedHolidays")`, `HasMaxLength` matching the tightened columns, `IsRowVersion()` on the S15 tables, and int keys replacing the string `EmployeeID` navigation. PascalCase and the `Id` primary keys mean property and `HasColumnName` changes in the app (S1, S3).
- 2026-11-17 (S20): documented exceptions to the standards, to be copied into db.md when applied: tables `Employee` and `Attendance` are singular; `Employee` and `Users` use `uid`; existing columns keep their names (`DepartmentName`, `ApplyTax`, `DOB`, `CNIC`, `MobileNo`, `GenStatus`, `ModifiedOn`, `CreatedDate`, `CreatedOn`, `UploadDate`); `Employee.IsLeaveManagedOverride` is a nullable bit; `AuditLogs` and `AttendancePunches` use `bigint` keys.

## Pending decisions this draft depends on
All live in decisions.md. The draft shows the assumption it made; nothing below blocks writing the schema, but each ⏸ is built only after its answer.

| Decision | Assumption in this draft | Tables affected |
|---|---|---|
| T4 payslip regeneration | decided 2026-11-26: delete and re-insert lines in one transaction (built) | Payslips, PayslipDetails |
| T7 notes and remarks | `Notes` column on HR tables + append-only `RecordNotes` (attendance, employee, leave request) | Attendance, Allowances, Deductions, Employee, LeaveRequests, HalfDayRequests, RosterOverrides, ShiftAssignments, RecordNotes |
| B1 half day vs leave | optional `LeaveRequestId` link on HalfDayRequests | HalfDayRequests, LeaveRequests |
| B2 departments | decided 2026-11-18: one shared main-department list, independent of project, plus optional `SubDepartments`. Still open at go-live: HR's mapping of the names (`Administrator`, `Mess`, `Transportation`) | Departments, SubDepartments, Employee |
| B3 projects | decided 2026-11-18: a dynamic HR-managed list, no fixed count; test-data seed for development, HR's list at go-live | Projects, Employee |
| B4 designation rank | default 100, alphabetical until HR ranks | Designations |
| B5 leave year, quotas | calendar year; carry-forward eligibility and cap are data on `LeaveTypes` (`AllowsCarryForward`, `MaxCarryForwardDays`, default no cap) | LeaveQuota, CarryforwardLeaves, LeaveEntitlementOverrides, vLeaveBalances |
| B6 hourly pay | exact minutes; `HourlyRate` optional | Employee, Attendance, Payslips |
| B7 half-day rule | cutoff/minimum columns nullable | Shifts, HalfDayRequests |
| B8 schedule edge cases | generic base only | Shifts, ShiftAssignments, RosterOverrides |
| B9 value lists, roles | proposed lists in the CHECKs | Employee, EmployeeDocuments, Users |
| B13 approvals | Admin and HR only; a user may hold both (`UserRoles`) | UserRoles, all approval columns |
| B14 transfer history | partly decided 2026-11-24: `EmployeeEvents` timeline with before/after positions; whether reports must snapshot the position stays open | Employee, EmployeeEvents |
| T8 voiding an event | decided 2026-11-24: latest entry only, reverts its effect; Admin may delete a voided entry | EmployeeEvents |
| B19 event types | the seven in `CK_EmployeeEvents_Type`; HR may want more (suspension, probation confirmed, contract renewed) | EmployeeEvents |
| B15 payslip unlock | no schema change | Payslips, AuditLogs |
| B17 tax base | basic salary | TaxRules |
| B18 legacy leave columns | decided 2026-11-24: archived in `EmployeeLegacyLeaveBalances` and dropped from Employee | Employee, EmployeeLegacyLeaveBalances |

## Phase 13: payslip line rates and fuel prices (2026-11-26, T9)
Script `13_payslip_line_rates_fuel_prices.sql`, additive and re-runnable: `PayslipDetails.Quantity decimal(9,2)`, `UnitAmount decimal(18,2)`, `Note nvarchar(200)` (all NULL), and `FuelPrices (Id, EffectiveFrom date unique, UnitPrice decimal(18,2) > 0, CreatedAt, CreatedBy)` seeded from the `Configuration` `FuelPrice` value. Applied to the development copy and, after backup `HRBTK_pre_script13_20261126.bak`, to production on 2026-11-26. Column notes are in db.md.

## Phase 14: work weeks (2026-11-26)
Script `14_work_weeks.sql`, re-runnable, only relaxes constraints: `ShiftAssignments.ShiftId` nullable (a row records working weekdays before shift times exist) and `CK_RosterOverrides_Shift` allows `ExtraDuty` without a shift. Leave `TotalDays` is now counted on work days (B5 question 5, weekly offs). Applied to the development copy only; run on production (after a backup) before deploying the build.

## Migration order
**Script map (applied to the local development database on 2026-11-23; production steps in `prod_rollout.md`):**

| Step below | Script in `scripts/database/redesign/` | Notes |
|---|---|---|
| 0 and 1 | `01_lookups_and_keys.sql` | `UX_PayslipDetails_Payslip_Item` moved to script 04 (needs the `ItemType` type change first). |
| 2 | `02_employee_org.sql` | Creates `mig.EmployeeOrgMap` for HR review; stops on any unmapped project/department text. |
| 3 | `03_leave_and_users.sql` | Leave requests pointing at a missing employee are archived to `mig.LeaveRequests_Orphans` and removed. |
| 4 | `04_attendance_payroll.sql` | Expand only; `Attendance.EmployeeUid`, `ShiftDate`, `Payslips.MonthNo`, `TaxRules.TaxYearStart` sit beside the old columns. |
| 5 | (deployment) | The development app already reads only the new schema; see `prod_rollout.md` for the production deployment. |
| 7 | `07_user_roles.sql` | Added 2026-11-23: creates `UserRoles`, copies `Users.Role`, drops `CK_Users_Role` and `Users.Role`. Independent of 06; needs 03. The app needs the new build (the old one reads `Users.Role`). |
| 8 | `08_employee_events.sql` | Added 2026-11-24: creates `EmployeeEvents` and backfills one row per employee from `DateOfJoining` / `DateOfLeaving` / `DateOfReturn`. Additive and independent of 06 and 07; needs 02 (org FKs) and the `Designations`/`Projects`/`Departments` tables. The new app build needs it (Details, Edit and Create read and write the table); the old build ignores it. |
| 9 | `09_leave_days_one_decimal.sql` | Added 2026-11-24: `decimal(5,1)` for the five leave-day columns (drops and recreates the dependent checks and indexes unchanged), plus `CK_LeaveRequests_SingleYear`. Needs 03. |
| 10 | `10_employee_legacy_leave_archive.sql` | Added 2026-11-24: archives the 7 legacy leave columns into `EmployeeLegacyLeaveBalances`, checks the count, drops them from `Employee`. Run in the same window as the new build (the build no longer reads them). |
| 11 | `11_leave_balances_view_v2.sql` | Added 2026-11-24: `vLeaveBalances` v2 (union of years, separate carry-forward) and `LeaveTypes.AllowsCarryForward` / `MaxCarryForwardDays`. Needs 03 and 09. |
| 6 | `06_contract.sql` | Drops the legacy columns, renames the temporary ones, makes `ProjectId`/`DepartmentId` NOT NULL, removes the moved Configuration rows, drops `EmployeeLeaves`. |

Production stays untouched until each script below has been reviewed and approved. Each phase is an EF migration or a **re-runnable** SQL script (S23: guard every change with `IF COL_LENGTH(...) IS NULL`, `IF OBJECT_ID(...) IS NULL` or `IF NOT EXISTS` on the index/constraint name; write backfills as "update where the new column IS NULL"; seed with `MERGE` on the natural key; run in a transaction with a rollback script; running twice changes nothing), **recorded in `__EFMigrationsHistory`** (or a `SchemaChangeLog` row), and preceded by a backup of `HRBTK` (S19). Create unique indexes **after** the backfill and after checking for duplicates (live checks are listed in the Idempotency section). `sqlcmd` runs against production only with your approval before each run.

0. **Bookkeeping.** Back up. Record the six hand-applied migrations (`20260105120000`, `20260512100000`, `20260515120000`, `20260515140000`, `20260520100000`, `20260918120000`) and decide what to do with `20260110114250_LMSModule` (recorded but missing from the repo).
1. **Lookups and keys (additive, safe).** Create `Projects`, `Designations`, `LeaveTypes`, `SubDepartments`; change `Departments` (unique name, `SortOrder`, `IsActive`, audit); add PKs/unique keys to `Configuration` (`Id` PK, unique `ConfigKey`) and `LeaveQuota` after checking for NULL/duplicate keys; add the missing indexes (`IX_Deductions_EmployeeId`, `UX_PayslipDetails_Payslip_Item`), the `EmployeeDocuments` and `AuditLogs` FKs, and the `Users.Username` unique key; rename the auto-named constraints (Appendix A, `sp_rename`, no data change).
2. **Backfill Employee org FKs.** Add `Employee.ProjectId/DepartmentId/SubDepartmentId/DesignationId` (NULL), `DateOfLeaving`, `DateOfReturn`, `PayBasis`, `HourlyRate`, `IsLeaveManagedOverride`, `CreatedAt/CreatedBy`, `RowVersion`. Build a temporary mapping table (e.g. `mig.EmployeeOrgMap`: raw Department string -> Department, raw Project string -> Project) for the **58 department strings** and 8 project values, reviewed by HR, then update Employee from it (set only where NULL). Seed Designations from distinct values. Department names are split at the first ` - ` (re-runnable, set only where NULL): the left side is the department (`Admin` is named `Administration`), the right side is the sub-department, created under that department (test data: `Admin - Mess` -> Administration / Mess; `Operations - Record` -> Operations / Record; names without ` - ` get no sub-department; `Mess`, `Transportation` and `Administrator` wait for HR). Unmapped rows are reported, not guessed. Then the Employee type conversions (DOB, ModifiedOn, ApplyTax, lengths, `nvarchar` names) after `TRY_CONVERT` / `MAX(LEN)` checks. The 7 legacy leave columns stay (⏸ B18).
3. **Leave consolidation.** Seed `LeaveTypes`; map `LeaveQuota` (3 rows) to `LeaveTypeId` and int `Year`; add the new `LeaveRequests` columns and migrate the **24 rows** (type, dates, status `Applied` -> `Pending`, TotalDays), resolving the **1 orphan** (fix its EmployeeId or archive the row) and reviewing the overlapping requests 21/22 before adding the FK and unique index; create `LeaveEntitlementOverrides`; rebuild `CarryforwardLeaves` (empty); create `vLeaveBalances`. Add `Users.EmployeeUid` (backfilled by EmployeeID match; the orphan stays NULL) and `Users.IsActive`.
4. **Attendance, schedule and payroll columns (all empty or additive).** Create `Shifts`, `ShiftAssignments`, `RosterOverrides`, `AttendancePunches`, `HalfDayRequests`, `AttendanceCorrections`, `RecordNotes` (and add the `Notes` columns, widening `Attendance.Comments` and `Allowances.Remarks` to 1000 first); **expand** `Attendance` (add the new columns beside the old ones; the app still writes the old ones until step 5); rebuild `GazettedHolidays` (empty); `AttendanceUploadLogs` types, backfilled `Status`, `FileHash`; `EmployeeDocuments.FileHash`; `Payslips.MonthNo` (backfill from the month name), `TaxPercentage` type, hourly columns, `RowVersion`, unique key; `TaxRules.TaxYearStart` (backfill `LEFT(TaxYear, 4)`); `Deductions.Amount` and `RequestId` (backfill Amount from the 11 `Fixed` rows); `Allowances.RequestId` and date types.
5. **App switch.** Release the app version that reads and writes only the new columns and tables (org FKs, LeaveTypeId, int employee keys, `Id` primary keys, `MonthNo`/`TaxYearStart`, attendance processing, dropdowns from the new tables, `RequestId` hidden fields, file hashing, the tax-year lookup fix). Verify in production for an agreed period.
6. **Contract (last, after a backup).** Drop the 🔴 columns (`Employee.Department/Designation/Project`, `Attendance.EmployeeID/EmployeeName/DepartmentName/AttendanceDate/TimeIn/TimeOut/Status`, `LeaveQuota.LeaveTypeName`, `LeaveRequests.LeaveType`, `Users.EmployeeId` (string), `CarryforwardLeaves.LeavesUsed/LeavesRemaining`, `Payslips.Month` (text), `TaxRules.TaxYear`), the moved Configuration rows and the `EmployeeLeaves` table; **then rename** the temporary columns (`EmployeeUid` -> `EmployeeId` in Users and Attendance, `MonthNo` -> `Month`, `Attendance.Comments` and `Allowances.Remarks` -> `Notes`) and the PKs (`AttendanceID`, `DepartmentID`, `LogId`, `UID` -> `Id`); make `Employee.ProjectId` and `Employee.DepartmentId` NOT NULL. 7. **User roles (2026-11-23).** Create `UserRoles`, copy `Users.Role` into it, drop `CK_Users_Role` and `Users.Role` (`07_user_roles.sql`). Run it after 03; it can run before or after 06. Then update db.md (v2.0) and its Change History.
9. **Leave days in one decimal (2026-11-24).** `09_leave_days_one_decimal.sql`. After 03; before 11.
10. **Legacy leave archive (2026-11-24).** `10_employee_legacy_leave_archive.sql`: archive, verify, drop the 7 `Employee` columns.
11. **Leave balances v2 (2026-11-24).** `11_leave_balances_view_v2.sql`: the history-safe view and the carry-forward flags on `LeaveTypes`.
8. **Employee history (2026-11-24).** Create `EmployeeEvents` and backfill it from the employee record (`08_employee_events.sql`). Run it after 02; it can run before or after 06 and 07, and must be in place before the new build is deployed.

## Appendix A. Constraint renames (S5, S16)
Renamed with `sp_rename` in step 1; no data change.

| Table | Old name | New name |
|---|---|---|
| Allowances | `FK_Allowances_Employee_EmployeeId` | `FK_Allowances_Employee` |
| Payslips | `FK_Payslips_Employee_EmployeeId` | `FK_Payslips_Employee` |
| PayslipDetails | `FK_PayslipDetails_Payslips_PayslipId` | `FK_PayslipDetails_Payslips` |
| Attendance | `PK__Attendan__8B69263C67096255` | `PK_Attendance` |
| AttendanceUploadLogs | `PK__Attendan__3214EC07799B6BDB` | `PK_AttendanceUploadLogs` |
| Deductions | `PK__Deductio__3214EC07F7E8E4F4` | `PK_Deductions` |
| Departments | `PK__Departme__B2079BCDAEBFDBAB` | `PK_Departments` |
| Employee | `PK__Employee__DD701264105802DC` | `PK_Employee` |
| Employee | `UQ__Employee__7AD04FF0F2154625` | `UX_Employee_EmployeeID` |
| EmployeeDocuments | `PK__Employee__3214EC070955CA59` | `PK_EmployeeDocuments` |
| Users | `PK__Users__DD70126471BA34B8` | `PK_Users` |
| (rebuilt tables) | `PK__Carryfor__379B99141E7FEFEB`, `PK__Gazetted__F1ADD9D04D937075` | replaced by `PK_CarryforwardLeaves`, `PK_GazettedHolidays` |
