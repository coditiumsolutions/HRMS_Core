---
status: partially-outdated
last_verified: 2026-11-23
verified_against: main @ 7aa2685 plus the uncommitted security and user-roles changes (code), and the redesigned development database (scripts 01-04, 06, 07 applied)
outdated_since: 2026-11-23
outdated_sections: [Tables (dbo), Relationships, Data-type notes]
reason: These sections describe the production schema as of 2026-11-16, which is still what production runs. The repository and development database use the redesigned schema; the banner at the top lists the differences.
see_also: ../db/db_changes.md, ../db/prod_rollout.md, ../../db.md
prod_migration: pending   # db.md v1.10 carries the same warning
---
# 3. Data Structures

> **Read this first (2026-11-23).** The tables below describe **production as of 2026-11-16**, and production still has this schema. The repository and the local development database use the redesigned schema (`../db/db_changes.md`; how to apply it: `../db/prod_rollout.md`). Main differences:
>
> | Production (below) | Redesigned (dev) |
> |---|---|
> | `Employee.Department/Project/Designation` free text | `ProjectId`, `DepartmentId`, `SubDepartmentId`, `DesignationId` FKs to `Projects`, `Departments`, `SubDepartments`, `Designations` |
> | `Attendance`, `LeaveRequests`, `Allowances`... keyed by string `EmployeeID` or mixed | all keyed by `Employee.uid` FK; `Employee.EmployeeID` stays the unique business code |
> | `DOB`, `ModifiedOn`, `Users.CreatedOn` strings; `ApplyTax` text; payslip `Month` name | `date`, `datetime2`, `bit`, `MonthNo` tinyint (code converts to the old shapes) |
> | `TaxRules.TaxYear` `"2025-2026"` | `TaxYearStart` smallint |
> | `Deductions.PercentageValue` overloaded | `Percentage` and `Amount` |
> | `EmployeeLeaves` (empty) | removed |
> | no keys on `Configuration`, `LeaveQuota` | keys added |
> | `Users.Role` (one role) | `UserRoles` (one row per role; several per user) |
> | not present | `Projects`, `SubDepartments`, `Designations`, `LeaveTypes`, `Shifts`, `ShiftAssignments`, `RosterOverrides`, `AttendancePunches`, `HalfDayRequests`, `AttendanceCorrections`, `RecordNotes`, `LeaveEntitlementOverrides`; `IsActive`/audit columns and `RowVersion` on several tables |
>
> `db.md` also still describes production. Update it (v2.0) after the production rollout.

**Database:** SQL Server. `db.md` (v1.4, 2026-09-23) is the documented live schema (it names the database `HRBTK`; `appsettings.json` connects to `HRMS` – the name differs per environment). Access is through EF Core `ApplicationDbContext` (18 `DbSet`s on production's model, more now). The app no longer uses a fixed database name: it comes from the connection string of the environment.

## Tables (dbo)
| Table | Entity | Purpose | Key columns |
|-------|--------|---------|-------------|
| `Employee` | `Employee` | Employee master | `uid` (PK, identity), `EmployeeID` (business id), name, father name, CNIC, DOB (string), mobile, `Department` (string), `Designation`, `DateOfJoining`, `EmployeeStatus`, `Project`, `BasicSalary` decimal(18,2), `ApplyTax`, `GenStatus`, legacy leave columns (`CarryForwardLeaves`, `Year2022/2023/2024`, `AdjustedAjusted`…) |
| `Departments` | `Department` | Department lookup | `DepartmentID`, `DepartmentName` |
| `Attendance` | `Attendance` | One row per employee per day | `AttendanceID`, `EmployeeID` (string), name, `DepartmentName`, `AttendanceDate` (date), `TimeIn/TimeOut` (time), `Status`, `Comments`; **unique** (`EmployeeID`,`AttendanceDate`) |
| `AttendanceUploadLogs` | `AttendanceUploadLog` | Import audit | file, uploaded by, counts, `ErrorDetails` |
| `LeaveRequests` | `LeaveRequest` | Simple leave requests | `Id`, `EmployeeId`, `LeaveType`, `FromDate`, `ToDate`, `Status` |
| `EmployeeLeaves` | `EmployeeLeave` | Detailed leave records | `uid`, `EmployeeID` (string), type, start/end, `TotalDays`, add/exclude days, comments, `Year`, `Status`, approver |
| `LeaveQuota` | `LeaveQuota` | Quota per type/year | `UID`, `LeaveTypeName`, `TotalLeaves`, `Year` |
| `CarryforwardLeaves` | `CarryforwardLeave` | Carry-forward days | employee, type, from/to year, days |
| `GazettedHolidays` / `GazettedHoliday` | `GazettedHoliday` | Public holidays | date, name |
| `Allowances` | `Allowance` | Employee allowances | `EmployeeId` (→ `Employee.uid`), `AllowanceType`, `Name`, `Frequency`, `Amount`, `Quantity`, `IsPercentage`, `PercentageValue`, effective/end date, `IsActive`, `Remarks`, audit columns |
| `Deductions` | `Deduction` | Employee deductions | `EmployeeId`, type, name, frequency, `CalculationMethod` (Fixed/Percentage), `PercentageValue` (also holds fixed amount), `IsMandatory`, dates, `IsActive` |
| `TaxRules` | `TaxRule` | Tax slabs | `MinSalary`, `MaxSalary?`, `TaxPercentage`, `TaxYear` (string) |
| `Payslips` | `Payslip` | Payslip header | `EmployeeId` (→ `Employee.uid`, Restrict), `Month` (name), `Year`, basic, `TotalAllowances`, gross, tax %, tax amount, `TotalDeductions`, net, `CalculationDetails`, `IsLocked`, `LockedDate`, generated by/date |
| `PayslipDetails` | `PayslipDetail` | Payslip lines | `PayslipId` (FK, cascade delete), `ItemType` (Allowance/Deduction/Tax), name, category, amount, sort order |
| `Users` | `AppUser` | Application logins | `uid`, `EmployeeId`, `Username`, `PasswordHash` (255), `Role` (free text; replaced by table `UserRoles` in the redesigned schema), created by/on, `History` |
| `Configuration` | `HrConfiguration` | Key/value settings (e.g. `FuelPrice`) | `UID`, `ConfigKey` (50), `ConfigValue` (max) |
| `EmployeeDocuments` | `EmployeeDocument` | Uploaded file metadata | `EmployeeId` (→ `Employee.uid`), file/stored name, `FilePath`, extension, size, `UploadedOn`, `IsDeleted` |
| `AuditLogs` | `AuditLog` | Change audit | table, operation, record id, `OldData`/`NewData` JSON, `ChangedBy`, `ChangedAt`, module, IP |
| `__EFMigrationsHistory` | – | EF bookkeeping | – |

## Relationships (enforced in EF)
```
Employee (uid) 1 ──< Payslips (EmployeeId)  1 ──< PayslipDetails (PayslipId, cascade)
Employee (uid) 1 ──< EmployeeDocuments (EmployeeId) – FK declared in EF only; NOT present in the live database
Employee (uid) 1 ──< Allowances / Deductions (EmployeeId) – real FKs in the database (no action)
```
Everything else is linked **by convention/string, not by foreign key**:
- `Attendance.EmployeeID`, `EmployeeLeaves.EmployeeID`, `CarryforwardLeaves.EmployeeID` → `Employee.EmployeeID` (string business key)
- `Employee.Department` / `Attendance.DepartmentName` → free text, not `Departments.DepartmentID`
- `Users.EmployeeId` → `Employee.EmployeeID` (string, optional)
- `LeaveRequests.EmployeeId` – int (to `uid`) while `EmployeeLeaves` uses the string id (inconsistent).

## Schema management (multiple, overlapping mechanisms)
1. **EF Core migrations** (`Migrations/`, 19 files, Dec 2025 → Sep 2026) – cover Payroll, Employee, Attendance, LMS, Tax, then incremental column additions.
2. **Hand-written SQL**: `scripts/database/schema/database-schema.sql` (1,139-line generated script; contains *old* table names such as `Payrolls`, `Employees`, `Attendances` alongside the current ones), `schema/database-schema-patches.sql`, `scripts/database/fixes/*.sql` (`FixLeaveQuotaSchema`, `FixPayslipsTaxColumns`, `PostMigrationPatches`), `Migrations/*.sql`.
3. **`db.md`** – human-maintained schema snapshot (versioned header; produced by `scripts/database/tools/export-db-schema.ps1`). `scripts/database/tools/export-database-production.ps1` dumps full data (output file `database-production.sql` is git-ignored).

Several tables (`Users`, `Configuration`, `Departments`, `EmployeeLeaves`, `LeaveQuota`, `GazettedHoliday`, `CarryforwardLeaves`, `AuditLogs`) have **no EF migration** – they are expected to pre-exist from the legacy database. Model/`db.md` mismatches were seen (e.g. `CarryforwardLeave` entity has `FromYear/ToYear/CarryForwardDays/Description`; `db.md` lists `Year/TotalLeaves/LeavesUsed/LeavesRemaining`; `EmployeeLeave.Comments` vs `DepSupervisorComments`).

## Data-type notes
- Money: `decimal(18,2)`; percentages `decimal(5,2)` (allowances/deductions) or `decimal(18,2)` (tax).
- Dates stored as `date`, `datetime`, `datetime2`, and **strings** (`Employee.DOB`, `ModifiedOn`, `Users.CreatedOn`) – inconsistent.
- Payslip period = (`Month` English name string, `Year` int); ordering done with a hard-coded CASE expression.
- Employee `uid` (int) is the payroll FK; `EmployeeID` (string) is the attendance/leave key – both identify an employee.

## Files on disk
`wwwroot/uploads/employees/{uid}/{guid}.ext` (git-ignored). Metadata in `EmployeeDocuments`.

## Live database verification (2026-11-16)
Read-only catalog queries against the production database `HRBTK` (SQL Server 2022) matched `db.md`: 19 tables, 175 columns, identical types and nullability. Details are in `db.md` (defaults, keys, indexes, row counts). Highlights:
- Real FKs: Allowances, Deductions, Payslips -> `Employee.uid`; PayslipDetails -> Payslips (cascade). `EmployeeDocuments` has **no** FK in the DB. `Employee.EmployeeID` has a unique constraint.
- **No primary key** on `Configuration`, `EmployeeLeaves`, `LeaveQuota`. `CarryforwardLeaves` (EmployeeId+Year) and `GazettedHolidays` (HolidayDate) have a different shape from their EF entities.
- Row counts: Employee 848 (all Active), Payslips 99, PayslipDetails 132, Allowances 23, Deductions 11, LeaveRequests 24, TaxRules 14, Users 6, EmployeeDocuments 8. **Attendance, EmployeeLeaves, CarryforwardLeaves, GazettedHolidays are empty** - attendance and detailed leave are not yet in real use.
- Projects: 8 distinct values (Bahria Town Karachi 338, Bahria Dastarkhuwans 324, M&S BT Tower 83, Galaxy Construction 45, The Arena 27, and three smaller). Departments: 58 distinct strings in `Employee`, 4 rows in `Departments`.
- `__EFMigrationsHistory` records 13 migrations; 6 repo migrations were applied by hand and are not recorded, and one recorded migration (`20260110114250_LMSModule`) has no file in the repo.
