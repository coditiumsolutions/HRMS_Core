# HRMS Database Schema
Version: 1.10 | Last Updated: 2026-11-23
Database: HRBTK
Server: Teezhost 16GB (production; hr.coditium.com, SQL Server 2022 16.0.1000.6 on port 1433, same machine as the web app). Its connection string is in the git-ignored `src/HRMBT.Web/appsettings.Production.json`. See `docs/deployment.md`.

## MAINTENANCE RULES:
    1. Bump Version and Last Updated above whenever this file changes
    2. Every table's dated inline notes explain WHY a column/table exists or changed - keep new notes dated (YYYY-MM-DD) and specific, don't just describe the column type
    3. A table marked [REMOVED] stays in this file as a historical/legacy record - do not delete those sections, they document what old code may still (incorrectly) reference
    4. Add an entry to Change History for every schema change, and keep the matching EF migration / SQL script named in it
---

> **MIGRATION PENDING (2026-11-23): production `HRBTK` must be migrated to the redesigned schema.** This file describes the schema production has **today** (the old one). The redesign (typed columns, foreign keys, managed lists for projects/departments/designations/leave types, new attendance/schedule tables, `UserRoles` for several roles per user) is finished and tested on a local copy only; production has not been changed. The new app build only works on the new schema and the old build only on the old one, so database and app are switched together in one maintenance window.
> - What changes: `docs/db/db_changes.md`. How to apply it (scripts in `scripts/database/redesign/`, order, checks, rollback): `docs/db/prod_rollout.md`.
> - After the migration, rewrite this file to the new schema as v2.0 and add a Change History entry. Until then, do not use this file as the model for new code.

Source of truth for the structure: the live database, verified read-only on 2026-11-16 (19 tables, 175 columns; types and nullability match the earlier 2026-09-23 export exactly). EF Core migrations in `src/HRMBT.Web/Migrations/` are only partly recorded in `__EFMigrationsHistory` (see Known issues), so do not rely on migrations alone to rebuild the schema. Reference SQL: `scripts/database/schema/` and `scripts/database/fixes/`. EF entities are in `src/HRMBT.Web/Models/`, mapped in `Data/ApplicationDbContext.cs`.

## Relationships
Foreign keys that exist in the database:
- `Allowances.EmployeeId` -> `Employee.uid` (`FK_Allowances_Employee_EmployeeId`, no action)
- `Deductions.EmployeeId` -> `Employee.uid` (`FK_Deductions_Employee`, no action)
- `PayslipDetails.PayslipId` -> `Payslips.Id` (`FK_PayslipDetails_Payslips_PayslipId`, cascade)
- `Payslips.EmployeeId` -> `Employee.uid` (`FK_Payslips_Employee_EmployeeId`, no action)

Linked by convention only, no FK in the database:
- `EmployeeDocuments.EmployeeId` -> `Employee.uid` (the EF model declares this FK, the database does not have it)
- `LeaveRequests.EmployeeId` -> `Employee.uid`
- `Attendance.EmployeeID`, `EmployeeLeaves.EmployeeID`, `CarryforwardLeaves.EmployeeId`, `Users.EmployeeId` -> `Employee.EmployeeID` (string)

## Table: dbo.__EFMigrationsHistory
EF Core bookkeeping: which migrations have been applied.
Rows on 2026-11-16: 13.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| MigrationId | nvarchar(150) | NOT NULL |  | PK. |
| ProductVersion | nvarchar(32) | NOT NULL |  |  |

Keys and indexes
- `PK___EFMigrationsHistory` - primary key, clustered, (MigrationId).

## Table: dbo.Allowances
Per-employee allowances (fixed, quantity-based or percentage of basic). Only active rows are used by payroll.
Rows on 2026-11-16: 23.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid (`FK_Allowances_Employee_EmployeeId`, no action). |
| AllowanceType | nvarchar(50) | NOT NULL |  | e.g. `Fixed Amount`, `Quantity`, `Fuel Allowance` (legacy). |
| Name | nvarchar(200) | NOT NULL |  | `Fuel Allowance` triggers FuelPrice x Quantity logic when type is `Quantity`. |
| Amount | decimal(18,2) | NOT NULL |  | Fixed amount; multiplied by Quantity for non-fuel rows. |
| IsPercentage | bit | NOT NULL |  | When true, amount = basic x PercentageValue / 100. |
| PercentageValue | decimal(5,2) | NULL |  |  |
| EffectiveDate | datetime2(7) | NOT NULL |  | Stored but not applied by payroll (only `IsActive` is). |
| EndDate | datetime2(7) | NULL |  |  |
| IsActive | bit | NOT NULL |  |  |
| CreatedDate | datetime2(7) | NOT NULL |  |  |
| CreatedBy | nvarchar(100) | NOT NULL |  |  |
| ModifiedDate | datetime2(7) | NULL |  |  |
| ModifiedBy | nvarchar(MAX) | NULL |  | nvarchar(MAX) in DB; model has no length limit either. |
| Quantity | int | NOT NULL | 1 | Added 2026-09-18 (migration 20260918120000, not recorded in __EFMigrationsHistory). |
| Remarks | nvarchar(500) | NULL |  | Added 2026-09-18 (migration 20260918130000). |
| Frequency | nvarchar(30) | NOT NULL | 'Monthly' | Stored, not honoured by payroll. |

Keys and indexes
- `PK_Allowances` - primary key, clustered, (Id).
- `IX_Allowances_EmployeeId` - nonclustered, (EmployeeId).

## Table: dbo.Attendance
One row per employee per calendar day. Empty in production at the time of the 2026-11-16 check.
Rows on 2026-11-16: 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| AttendanceID | int | NOT NULL |  | PK, identity. |
| EmployeeID | nvarchar(50) | NOT NULL |  | Employee.EmployeeID (string business key); no FK. |
| EmployeeName | nvarchar(100) | NOT NULL |  |  |
| DepartmentName | nvarchar(50) | NOT NULL |  | Free text copy; not linked to Departments. |
| AttendanceDate | date | NOT NULL |  |  |
| TimeIn | time(7) | NULL |  |  |
| TimeOut | time(7) | NULL |  |  |
| Status | nvarchar(10) | NULL |  | P Present, A Absent, L Late, H Holiday. |
| Comments | nvarchar(255) | NULL |  |  |

Keys and indexes
- `PK__Attendan__8B69263C67096255` - primary key, clustered, (AttendanceID).
- `IX_Attendance_EmployeeID_Date` - unique, nonclustered, (EmployeeID, AttendanceDate).

## Table: dbo.AttendanceUploadLogs
Audit trail of Excel/CSV attendance imports (counts and error details).
Rows on 2026-11-16: 3.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| FileName | nvarchar(255) | NULL |  |  |
| UploadedBy | nvarchar(100) | NULL |  |  |
| UploadDate | datetime | NULL | getdate() |  |
| Status | nvarchar(50) | NULL |  |  |
| Message | nvarchar(MAX) | NULL |  |  |
| ErrorDetails | nvarchar(MAX) | NULL |  |  |
| SuccessCount | int | NULL |  |  |
| FailureCount | int | NULL |  |  |
| TotalRows | int | NULL |  |  |

Keys and indexes
- `PK__Attendan__3214EC07799B6BDB` - primary key, clustered, (Id).

## Table: dbo.CarryforwardLeaves
Carry-forward leave balances per employee per year. Empty in production on 2026-11-16.
Rows on 2026-11-16: 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| EmployeeId | nvarchar(50) | NOT NULL |  | PK. |
| Year | int | NOT NULL |  | PK. |
| TotalLeaves | int | NOT NULL |  |  |
| LeavesUsed | int | NULL | 0 |  |
| LeavesRemaining | int | NULL | 0 |  |

Keys and indexes
- `PK__Carryfor__379B99141E7FEFEB` - primary key, clustered, (EmployeeId, Year).

## Table: dbo.Configuration
Key/value application settings edited under Setup. Also holds the dropdown lists (Department, Designation, Project, LeaveTypes, AllowanceNames...), `FuelPrice` and `CurrentTaxYear`. Has no primary key in the database.
Rows on 2026-11-16: 19.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| UID | int | NOT NULL |  | Identity. |
| ConfigKey | nvarchar(50) | NULL |  | e.g. `FuelPrice`. Not unique-constrained. |
| ConfigValue | nvarchar(MAX) | NULL |  |  |

Keys and indexes
- No primary key in the database.

## Table: dbo.Deductions
Per-employee payroll deductions (fixed amount or percentage of gross).
Rows on 2026-11-16: 11.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid (`FK_Deductions_Employee`, no action). |
| DeductionType | nvarchar(50) | NOT NULL |  |  |
| DeductionName | nvarchar(100) | NOT NULL |  |  |
| Frequency | nvarchar(50) | NOT NULL |  |  |
| CalculationMethod | nvarchar(50) | NOT NULL |  | `Percentage` (of gross) or fixed. |
| PercentageValue | decimal(5,2) | NULL |  | Overloaded: percentage when method is Percentage, otherwise the fixed amount (there is no Amount column). |
| IsMandatory | bit | NOT NULL | 0 |  |
| EffectiveDate | date | NOT NULL |  |  |
| EndDate | date | NULL |  |  |
| IsActive | bit | NOT NULL | 1 |  |
| CreatedDate | datetime2(7) | NOT NULL | getdate() |  |
| CreatedBy | nvarchar(100) | NOT NULL |  |  |
| ModifiedDate | datetime2(7) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK__Deductio__3214EC07F7E8E4F4` - primary key, clustered, (Id).

## Table: dbo.Departments
Department lookup. Not linked to `Employee.Department` / `Attendance.DepartmentName` (those are free text).
Rows on 2026-11-16: 4.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| DepartmentID | int | NOT NULL |  | PK, identity. |
| DepartmentName | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK__Departme__B2079BCDAEBFDBAB` - primary key, clustered, (DepartmentID).

## Table: dbo.Employee
Employee master. `uid` is the FK used by payroll tables; `EmployeeID` (unique) is the business key used by attendance, leave and users.
Rows on 2026-11-16: 848.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| uid | int | NOT NULL |  | PK, identity. |
| EmployeeID | varchar(250) | NOT NULL |  | Business key. Unique constraint `UQ__Employee__7AD04FF0F2154625`. |
| EmployeeName | varchar(250) | NULL |  |  |
| CNIC | varchar(250) | NULL |  |  |
| FatherName | varchar(250) | NULL |  |  |
| DOB | varchar(250) | NULL |  | Stored as text. |
| MobileNo | varchar(250) | NULL |  |  |
| Department | varchar(250) | NULL |  | Free text (58 distinct values live); only 4 rows exist in Departments. |
| Designation | varchar(250) | NULL |  |  |
| DateOfJoining | date | NULL |  |  |
| EmployeeStatus | varchar(250) | NULL |  | All 848 live rows are `Active`. |
| ModifiedBy | varchar(MAX) | NULL |  |  |
| ModifiedOn | nvarchar(MAX) | NULL |  | Stored as text. |
| Details | nvarchar(MAX) | NULL |  |  |
| Project | nvarchar(50) | NULL |  | Free text. 8 distinct values live (see Employee notes below). |
| CarryForwardLeaves | float | NULL |  |  |
| Year2022 | float | NULL |  |  |
| Year2023 | float | NULL |  |  |
| AdjustedAjusted | int | NULL |  |  |
| Year2024 | int | NULL |  |  |
| CarryForwardLeaves1 | float | NULL |  |  |
| Year2023New | decimal(18,2) | NULL |  |  |
| BasicSalary | decimal(18,2) | NULL |  | Payroll basis for gross and tax. |
| ApplyTax | nvarchar(10) | NULL |  | Live: `1` (827 rows), `Yes` (1), `0` (1), empty (19). Tax is deducted for yes / y / true / 1. |
| GenStatus | nvarchar(50) | NULL |  |  |

Keys and indexes
- `PK__Employee__DD701264105802DC` - primary key, clustered, (uid).
- `UQ__Employee__7AD04FF0F2154625` - unique, nonclustered, (EmployeeID).

## Table: dbo.EmployeeDocuments
Metadata for files uploaded per employee (files live under `wwwroot/uploads/employees/{uid}/`). Soft-deleted via `IsDeleted`. No FK to Employee in the database.
Rows on 2026-11-16: 8.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | Employee.uid. The EF model configures a FK, but the database has none. |
| FileName | nvarchar(300) | NOT NULL |  | GUID-based stored name. |
| OriginalFileName | nvarchar(300) | NULL |  |  |
| FilePath | nvarchar(500) | NOT NULL |  |  |
| FileExtension | nvarchar(50) | NULL |  |  |
| FileSize | bigint | NULL |  |  |
| UploadedOn | datetime | NULL | getdate() |  |
| IsDeleted | bit | NULL | 0 |  |

Keys and indexes
- `PK__Employee__3214EC070955CA59` - primary key, clustered, (Id).

## Table: dbo.EmployeeLeaves
Detailed leave records per employee (one of two parallel leave models; see also `LeaveRequests`). Empty in production on 2026-11-16 and has no primary key.
Rows on 2026-11-16: 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| uid | int | NOT NULL |  | Identity. |
| EmployeeID | varchar(50) | NOT NULL |  | Employee.EmployeeID (string); no FK. |
| LeaveTypeName | varchar(250) | NOT NULL |  |  |
| StartDate | date | NOT NULL |  |  |
| EndDate | date | NOT NULL |  |  |
| TotalDays | float | NULL |  |  |
| AddDays | int | NULL |  |  |
| ExcludeDays | int | NULL |  |  |
| Short_Adj | varchar(MAX) | NULL |  |  |
| DepSupervisorComments | varchar(MAX) | NULL |  | Model property is named `Comments`. |
| Year | varchar(50) | NULL |  |  |
| Status | nvarchar(50) | NULL |  |  |
| ApprovedBy | varchar(50) | NULL |  |  |
| ApprovedOn | date | NULL |  |  |
| AppliedDate | date | NULL |  |  |

Keys and indexes
- No primary key in the database.

## Table: dbo.GazettedHolidays
Public (gazetted) holidays, keyed by date. Empty in production on 2026-11-16.
Rows on 2026-11-16: 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| HolidayDate | date | NOT NULL |  | PK. |
| Description | nvarchar(255) | NULL |  |  |

Keys and indexes
- `PK__Gazetted__F1ADD9D04D937075` - primary key, clustered, (HolidayDate).

## Table: dbo.LeaveQuota
Yearly quota per leave type (global, not per employee). Has no primary key in the database.
Rows on 2026-11-16: 3.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| UID | int | NOT NULL |  | Identity. |
| LeaveTypeName | varchar(50) | NOT NULL |  |  |
| TotalLeaves | int | NOT NULL |  |  |
| Year | varchar(50) | NOT NULL |  |  |

Keys and indexes
- No primary key in the database.

## Table: dbo.LeaveRequests
Simple leave requests (second leave model; keyed by `Employee.uid`).
Rows on 2026-11-16: 24.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | Employee.uid (int), unlike EmployeeLeaves. |
| LeaveType | nvarchar(MAX) | NOT NULL |  |  |
| FromDate | datetime2(7) | NOT NULL |  |  |
| ToDate | datetime2(7) | NOT NULL |  |  |
| Status | nvarchar(MAX) | NULL |  |  |

Keys and indexes
- `PK_LeaveRequests` - primary key, clustered, (Id).

## Table: dbo.PayslipDetails
Line items of a payslip (Allowance / Deduction / Tax).
Rows on 2026-11-16: 132.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| PayslipId | int | NOT NULL |  | FK -> Payslips.Id (`FK_PayslipDetails_Payslips_PayslipId`, ON DELETE CASCADE). |
| ItemType | nvarchar(20) | NOT NULL |  | `Allowance`, `Deduction` or `Tax`. |
| ItemName | nvarchar(200) | NOT NULL |  |  |
| ItemCategory | nvarchar(50) | NOT NULL |  |  |
| Amount | decimal(18,2) | NOT NULL |  |  |
| SortOrder | int | NULL |  |  |

Keys and indexes
- `PK_PayslipDetails` - primary key, clustered, (Id).

## Table: dbo.Payslips
Payslip header: one per employee per month/year, with totals and lock state.
Rows on 2026-11-16: 99.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid (`FK_Payslips_Employee_EmployeeId`, no action). |
| Month | nvarchar(20) | NOT NULL |  | English month name (January ... December). |
| Year | int | NOT NULL |  |  |
| BasicSalary | decimal(18,2) | NOT NULL |  |  |
| GrossSalary | decimal(18,2) | NOT NULL |  |  |
| TotalDeductions | decimal(18,2) | NULL |  | Sum of Deductions lines only; excludes income tax (see TaxAmount). |
| TotalAllowances | decimal(18,2) | NULL |  | Added by migration 20260512100000 (not recorded in __EFMigrationsHistory). |
| NetSalary | decimal(18,2) | NOT NULL |  | Gross - TotalDeductions - TaxAmount. |
| WorkingDays | int | NULL |  | Not populated by the app. |
| LeaveDays | decimal(5,2) | NULL |  | Not populated by the app. |
| LeaveBalance | nvarchar(MAX) | NULL |  | Not populated by the app. |
| CalculationDetails | nvarchar(MAX) | NOT NULL |  |  |
| GeneratedDate | datetime2(7) | NOT NULL |  |  |
| GeneratedBy | nvarchar(100) | NOT NULL |  |  |
| IsLocked | bit | NOT NULL |  | Set to 1 for all of an employee's payslips when their allowances/deductions change. |
| LockedDate | datetime2(7) | NULL |  |  |
| Notes | nvarchar(1000) | NULL |  |  |
| TaxPercentage | decimal(18,2) | NOT NULL | 0 |  |
| TaxAmount | decimal(18,2) | NOT NULL | 0 |  |

Keys and indexes
- `PK_Payslips` - primary key, clustered, (Id).

## Table: dbo.TaxRules
Income-tax slabs per tax year. Tax = BasicSalary x TaxPercentage / 100 for the slab containing the basic salary.
Rows on 2026-11-16: 14.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| MinSalary | decimal(18,2) | NOT NULL |  |  |
| MaxSalary | decimal(18,2) | NULL |  | NULL or 0 = no upper limit. |
| TaxPercentage | decimal(18,2) | NOT NULL |  |  |
| TaxYear | nvarchar(20) | NOT NULL | '2025-2026' | Live values are `2025-2026` and `2026-2027`. Payroll compares it with the payslip year as `2026`, so it never matches (see Known issues). |

Keys and indexes
- `PK_TaxRules` - primary key, clustered, (Id).

## Table: dbo.Users
Application logins (cookie auth). `Role` is free text and is not yet used for authorization.
Rows on 2026-11-16: 6.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| uid | int | NOT NULL |  | PK, identity. |
| EmployeeId | varchar(50) | NULL |  | Employee.EmployeeID (string), optional; no FK. |
| Username | nvarchar(100) | NOT NULL |  |  |
| PasswordHash | nvarchar(255) | NOT NULL |  | PBKDF2 `iterations.salt.hash`. All 6 live rows use PBKDF2; the login code still accepts legacy SHA-256 hex and plain text. |
| Role | nvarchar(MAX) | NULL |  | Free text, copied to the auth cookie; not enforced anywhere. Live values: Admin (4), HR (1), empty (1). |
| CreatedBy | varchar(MAX) | NULL |  |  |
| CreatedOn | varchar(MAX) | NULL |  |  |
| History | varchar(MAX) | NULL |  |  |

Keys and indexes
- `PK__Users__DD70126471BA34B8` - primary key, clustered, (uid).

## Table: dbo.AuditLogs
Generic change audit (who, what, old/new JSON, IP). Currently written only by payslip locking.
Rows on 2026-11-16: 4.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| LogId | int | NOT NULL |  | PK, identity. |
| TableName | nvarchar(128) | NOT NULL |  |  |
| Operation | nvarchar(20) | NOT NULL |  |  |
| RecordId | nvarchar(100) | NULL |  |  |
| EmployeeId | int | NULL |  |  |
| OldData | nvarchar(MAX) | NULL |  | JSON. |
| NewData | nvarchar(MAX) | NULL |  | JSON. |
| ChangedBy | nvarchar(128) | NULL | suser_sname() |  |
| ChangedAt | datetime2(7) | NOT NULL | sysdatetime() |  |
| ModuleName | nvarchar(100) | NULL |  |  |
| IPAddress | nvarchar(50) | NULL |  |  |

Keys and indexes
- `PK_AuditLogs` - primary key, clustered, (LogId).

## Known issues and model / database differences
- 2026-11-16: **Tax year mismatch (affects payroll results).** `TaxRules.TaxYear` holds `2025-2026` (7 slabs, max 20%) and `2026-2027` (7 slabs, max 10%), and `Configuration.CurrentTaxYear` exists, but `PayrollCalculationService` compares `TaxYear` with the payslip year as `2026`. No slab matches, there are no blank-year slabs, so it falls back to **all 14 slabs mixed**, and the slab with the highest `MinSalary` that contains the basic salary wins regardless of year. Fix the lookup to use the fiscal-year label (e.g. `CurrentTaxYear`) or store plain years.
- 2026-11-16: `CarryforwardLeaves` - the EF entity (`Id`, `EmployeeID`, `LeaveTypeName`, `FromYear`, `ToYear`, `CarryForwardDays`, `Description`) does not match the live table (`EmployeeId`, `Year`, `TotalLeaves`, `LeavesUsed`, `LeavesRemaining`, PK on EmployeeId+Year). The table is empty, so any code using the entity would fail on first use.
- 2026-11-16: `GazettedHolidays` - the live table has only `HolidayDate` (PK) and `Description`; the EF entity maps `GazettedHoliday` (singular, with `Id`, `HolidayName`). Table is empty. `database-schema.sql` creates the singular name.
- 2026-11-16: `EmployeeLeaves`, `LeaveQuota` and `Configuration` have **no primary key** in the database although the EF model declares keys (`uid` / `UID`). Duplicate key values are therefore not prevented by the database.
- 2026-11-16: Migration history drift. `__EFMigrationsHistory` has 13 rows. The repo contains six migrations that are not recorded there although their schema changes are present (applied by hand): `20260105120000_CreateAttendanceUploadLogsTable`, `20260512100000_PayslipTotalAllowancesColumn`, `20260515120000_AlignDeductionsSchemaDbTxt`, `20260515140000_CreateEmployeeDocumentsTable`, `20260520100000_AddTaxYearColumnTaxRules`, `20260918120000_AddAllowancesQuantityColumn`. The database also records `20260110114250_LMSModule`, which has no file in the repo. Running `dotnet ef database update` against this database would try to re-apply those six.
- 2026-11-16: Duplicate leave models. `LeaveRequests` holds 24 rows and is what the app actually uses; `EmployeeLeaves` (the richer model) is empty, as are `Attendance`, `CarryforwardLeaves` and `GazettedHolidays`.
- 2026-11-16: Referential gaps. `LeaveRequests`: 1 row references a missing employee; `Users`: 1 row references a missing `EmployeeID`; `EmployeeDocuments`: none orphaned. `Departments` has 4 rows while employees use 58 distinct department strings (54 not in `Departments`).
- 2026-11-16: `Employee.Project` has 8 distinct values, not 6 (Bahria Town Karachi 338, Bahria Dastarkhuwans 324, Maintenance & Services - BT Tower 83, Galaxy Construction 45, The Arena Karachi 27, Bahria Town Tower Project 11, Maintenance & Services - Icon 11, BTK - Customer Support Center (Head Office) 9). The project list in `Configuration` and the data can drift apart.
- 2026-11-16: `Payslips` has no unique index on (EmployeeId, Month, Year); no duplicates exist today (0 groups). 99 payslips: 97 for 2026 (8 locked) and 2 for 2027.
- 2026-11-16: `Allowances.ModifiedBy` is nvarchar(MAX); `Employee.ModifiedBy` is varchar(MAX); `Employee` string columns are varchar(250) while the entity limits them to 50-100 characters.
- 2026-11-16: `database-schema.sql` still contains obsolete tables (`Payrolls`, `Employees`, `Attendances`).
- 2026-11-16: Connection settings. Connection strings are no longer committed. The production string (this database, `HRBTK` on Teezhost 16GB, port 1433) lives in the git-ignored `src/HRMBT.Web/appsettings.Production.json`; local development uses `appsettings.Development.json`. Old strings (`localhost,50001` / `HRMS`, old public IP `178.105.20.255,50001`, `Payroll2`, `BT`) are outdated reference values kept only in the git-ignored `.env`. They are still present in git history.

## Change History
- 2026-09-23: Live schema exported (former `db.txt`, v1.4) with `scripts/database/tools/export-db-schema.ps1`; database `HRBTK`.
- 2026-11-16: `db.txt` converted to this Markdown file (`db.md`, v1.5) in the maintained format of the example file. No schema change.
- 2026-11-16: Server identified as **Teezhost 16GB** (v1.7); the web app is deployed on the same machine. No schema change.
- 2026-11-16: Verified against the live production database `HRBTK` with read-only catalog queries (v1.6). Result: no column, type or nullability change since 2026-09-23. Added defaults, identity flags, real primary/foreign keys and indexes, row counts and migration-history findings, and corrected earlier assumptions (Allowances/Deductions do have FKs; EmployeeDocuments has none; Employee.EmployeeID is unique). No schema change was made.
- 2026-11-16: `appsettings.json` `DefaultConnection` switched to this database; old connection strings commented out (v1.8). No schema change.
- 2026-11-16: Connection strings moved out of `appsettings.json` into git-ignored `appsettings.Development.json` / `appsettings.Production.json` (examples committed) and `.env` (v1.9). No schema change.
- 2026-11-23: Added the migration-pending note above (v1.10). Production still has the schema documented here; the redesigned schema has been applied to a local development copy only. No schema change.
