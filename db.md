# HRMS Database Schema
Version: 2.2 | Last Updated: 2026-11-25
Database: HRBTK
Server: Teezhost 16GB (production; hr.coditium.com, SQL Server 2022 16.0.1000.6 Express on port 1433, same machine as the web app). Its connection string is in the git-ignored `src/HRMBT.Web/appsettings.Production.json`. See `docs/deployment.md`.

## MAINTENANCE RULES:
    1. Bump Version and Last Updated above whenever this file changes
    2. Every table's dated inline notes explain WHY a column/table exists or changed - keep new notes dated (YYYY-MM-DD) and specific, don't just describe the column type
    3. A table marked [REMOVED] stays in this file as a historical/legacy record - do not delete those sections, they document what old code may still (incorrectly) reference
    4. Add an entry to Change History for every schema change, and keep the matching EF migration / SQL script named in it
---

> **2026-11-24: the redesigned schema is live on production.** Scripts 01-04 and 06-11 (`scripts/database/redesign/`) were run on `HRBTK` on 2026-11-24 after a full backup; the old schema (v1.10 of this file) no longer exists, apart from the archive tables below. The reasoning behind the redesign (why each column, decisions, standards) is in `docs/db/db_changes.md`, `db_standards.md` and `decisions.md`; how it was applied is in `docs/db/prod_rollout.md`. The pre-migration state is kept in the backups `HRBTK_pre_redesign_20261124.bak` and `HRBTK_after04_20261124.bak` in the SQL Server default backup folder on the server.

Source of truth for the structure: the live database, read with catalog queries on 2026-11-24 right after the migration (33 tables in `dbo`, 2 views, plus 2 in `mig`). Column notes were carried over from `db_changes.md` ("was ..." = the old definition). EF entities are in `src/HRMBT.Web/Models/`, mapped in `Data/ApplicationDbContext.cs`. Reference SQL: `scripts/database/redesign/` (run in order; scripts 02-04 cannot be re-run once 06 has dropped the old columns) and `scripts/database/tools/`.

## Standards in short (db_standards.md)
- Primary key `Id int IDENTITY` (`bigint` for `AttendancePunches` and `AuditLogs`); `Employee` and `Users` keep `uid`. Foreign key columns are `<Entity>Id`; employee references are `EmployeeId` -> `Employee.uid`. Constraint names: `PK_`, `FK_<Child>_<Parent>`, `IX_`, `UX_` (unique), `CK_`, `DF_`.
- Dates are `date`, moments `datetime2(0)` in server local time (PKT, no UTC conversion), booleans `bit NOT NULL`, money `decimal(18,2)`, leave days `decimal(5,1)`. Fixed value sets are `varchar` + `CHECK`; lists with attributes are tables (Projects, Departments, SubDepartments, Designations, LeaveTypes).
- Soft delete (`IsActive`, `IsDeleted`, voided/cancelled status); rows with money or history are never deleted. `rowversion` on `Employee`, `LeaveRequests`, `HalfDayRequests`, `Attendance`, `Payslips`.
- Documented exceptions: tables `Employee` and `Attendance` are singular; `Employee` and `Users` use `uid`; existing column names `DepartmentName`, `ApplyTax`, `DOB`, `CNIC`, `MobileNo`, `GenStatus`, `ModifiedOn`, `CreatedDate`, `CreatedOn`, `UploadDate` are kept; `Employee.IsLeaveManagedOverride` is a nullable bit (NULL = follow the project); `__EFMigrationsHistory` is EF's.

## Relationships
Foreign keys in the database (from the live catalog):
- `Allowances.EmployeeId` -> `Employee.uid` (`FK_Allowances_Employee`, no action)
- `Attendance.EmployeeId` -> `Employee.uid` (`FK_Attendance_Employee`, no action)
- `Attendance.HalfDayRequestId` -> `HalfDayRequests.Id` (`FK_Attendance_HalfDayRequests`, no action)
- `Attendance.LeaveRequestId` -> `LeaveRequests.Id` (`FK_Attendance_LeaveRequests`, no action)
- `Attendance.ShiftId` -> `Shifts.Id` (`FK_Attendance_Shifts`, no action)
- `AttendanceCorrections.AttendanceId` -> `Attendance.Id` (`FK_AttendanceCorrections_Attendance`, no action)
- `AttendanceCorrections.RecordedByUserId` -> `Users.uid` (`FK_AttendanceCorrections_Users`, no action)
- `AttendancePunches.AttendanceId` -> `Attendance.Id` (`FK_AttendancePunches_Attendance`, no action)
- `AttendancePunches.UploadLogId` -> `AttendanceUploadLogs.Id` (`FK_AttendancePunches_AttendanceUploadLogs`, no action)
- `AttendancePunches.EmployeeId` -> `Employee.uid` (`FK_AttendancePunches_Employee`, no action)
- `AuditLogs.EmployeeId` -> `Employee.uid` (`FK_AuditLogs_Employee`, no action)
- `CarryforwardLeaves.EmployeeId` -> `Employee.uid` (`FK_CarryforwardLeaves_Employee`, no action)
- `CarryforwardLeaves.LeaveTypeId` -> `LeaveTypes.Id` (`FK_CarryforwardLeaves_LeaveTypes`, no action)
- `Deductions.EmployeeId` -> `Employee.uid` (`FK_Deductions_Employee`, no action)
- `Employee.DepartmentId` -> `Departments.Id` (`FK_Employee_Departments`, no action)
- `Employee.DesignationId` -> `Designations.Id` (`FK_Employee_Designations`, no action)
- `Employee.ProjectId` -> `Projects.Id` (`FK_Employee_Projects`, no action)
- `Employee.DepartmentId, SubDepartmentId` -> `SubDepartments.DepartmentId, Id` (`FK_Employee_SubDepartments`, no action)
- `EmployeeDocuments.EmployeeId` -> `Employee.uid` (`FK_EmployeeDocuments_Employee`, no action)
- `EmployeeEvents.EmployeeId` -> `Employee.uid` (`FK_EmployeeEvents_Employee`, no action)
- `EmployeeEvents.FromDepartmentId` -> `Departments.Id` (`FK_EmployeeEvents_FromDepartment`, no action)
- `EmployeeEvents.FromDesignationId` -> `Designations.Id` (`FK_EmployeeEvents_FromDesignation`, no action)
- `EmployeeEvents.FromProjectId` -> `Projects.Id` (`FK_EmployeeEvents_FromProject`, no action)
- `EmployeeEvents.ToDepartmentId` -> `Departments.Id` (`FK_EmployeeEvents_ToDepartment`, no action)
- `EmployeeEvents.ToDesignationId` -> `Designations.Id` (`FK_EmployeeEvents_ToDesignation`, no action)
- `EmployeeEvents.ToProjectId` -> `Projects.Id` (`FK_EmployeeEvents_ToProject`, no action)
- `EmployeeLegacyLeaveBalances.EmployeeId` -> `Employee.uid` (`FK_EmployeeLegacyLeaveBalances_Employee`, no action)
- `GazettedHolidays.ProjectId` -> `Projects.Id` (`FK_GazettedHolidays_Projects`, no action)
- `HalfDayRequests.EmployeeId` -> `Employee.uid` (`FK_HalfDayRequests_Employee`, no action)
- `HalfDayRequests.DocumentId` -> `EmployeeDocuments.Id` (`FK_HalfDayRequests_EmployeeDocuments`, no action)
- `HalfDayRequests.LeaveRequestId` -> `LeaveRequests.Id` (`FK_HalfDayRequests_LeaveRequests`, no action)
- `HalfDayRequests.ApprovedByUserId` -> `Users.uid` (`FK_HalfDayRequests_Users`, no action)
- `LeaveEntitlementOverrides.EmployeeId` -> `Employee.uid` (`FK_LeaveEntitlementOverrides_Employee`, no action)
- `LeaveEntitlementOverrides.LeaveTypeId` -> `LeaveTypes.Id` (`FK_LeaveEntitlementOverrides_LeaveTypes`, no action)
- `LeaveEntitlementOverrides.ApprovedByUserId` -> `Users.uid` (`FK_LeaveEntitlementOverrides_Users`, no action)
- `LeaveQuota.LeaveTypeId` -> `LeaveTypes.Id` (`FK_LeaveQuota_LeaveTypes`, no action)
- `LeaveRequests.EmployeeId` -> `Employee.uid` (`FK_LeaveRequests_Employee`, no action)
- `LeaveRequests.DocumentId` -> `EmployeeDocuments.Id` (`FK_LeaveRequests_EmployeeDocuments`, no action)
- `LeaveRequests.LeaveTypeId` -> `LeaveTypes.Id` (`FK_LeaveRequests_LeaveTypes`, no action)
- `LeaveRequests.ApprovedByUserId` -> `Users.uid` (`FK_LeaveRequests_Users`, no action)
- `PayslipDetails.PayslipId` -> `Payslips.Id` (`FK_PayslipDetails_Payslips`, cascade)
- `Payslips.EmployeeId` -> `Employee.uid` (`FK_Payslips_Employee`, no action)
- `RecordNotes.AttendanceId` -> `Attendance.Id` (`FK_RecordNotes_Attendance`, no action)
- `RecordNotes.EmployeeId` -> `Employee.uid` (`FK_RecordNotes_Employee`, no action)
- `RecordNotes.LeaveRequestId` -> `LeaveRequests.Id` (`FK_RecordNotes_LeaveRequests`, no action)
- `RosterOverrides.EmployeeId` -> `Employee.uid` (`FK_RosterOverrides_Employee`, no action)
- `RosterOverrides.SwapWithEmployeeId` -> `Employee.uid` (`FK_RosterOverrides_Employee_Swap`, no action)
- `RosterOverrides.ShiftId` -> `Shifts.Id` (`FK_RosterOverrides_Shifts`, no action)
- `RosterOverrides.ApprovedByUserId` -> `Users.uid` (`FK_RosterOverrides_Users`, no action)
- `ShiftAssignments.EmployeeId` -> `Employee.uid` (`FK_ShiftAssignments_Employee`, no action)
- `ShiftAssignments.ShiftId` -> `Shifts.Id` (`FK_ShiftAssignments_Shifts`, no action)
- `SubDepartments.DepartmentId` -> `Departments.Id` (`FK_SubDepartments_Departments`, no action)
- `UserRoles.UserId` -> `Users.uid` (`FK_UserRoles_Users`, cascade)
- `Users.EmployeeId` -> `Employee.uid` (`FK_Users_Employee`, no action)

Convention-only links that no longer exist: `Attendance`, `Users`, `CarryforwardLeaves` and `LeaveRequests` now use `EmployeeId` (int, FK to `Employee.uid`); the string `Employee.EmployeeID` is only the business key shown to users.

## Table: dbo.__EFMigrationsHistory
EF Core bookkeeping: which migrations have been applied.
- 2026-11-16: No schema change. Before the first new migration, insert rows for the six hand-applied migrations (see Known issues in db.md) so `dotnet ef database update` does not try to re-apply them. Table name and PK name are EF's and stay as they are.
Rows on 2026-11-24 (right after the migration): 29.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| MigrationId | nvarchar(150) | NOT NULL |  | PK. |
| ProductVersion | nvarchar(32) | NOT NULL |  |  |

Keys and indexes
- `PK___EFMigrationsHistory` - primary key, clustered, (MigrationId)

## Table: dbo.Allowances
Per-employee allowances (fixed, quantity-based or percentage of basic). Only active rows are used by payroll.
- 2026-11-16: `EffectiveDate`, `EndDate` and `Frequency` are stored but payroll only checks `IsActive`. That is an app fix (filter by the payslip month), not a schema change.
- 2026-11-17: Live check: no `EffectiveDate`/`EndDate` has a time part, so the conversion to `date` is lossless. Employees 325 and 376 each have two active `Fuel Allowance` rows, so there is no unique key on active `(EmployeeId, Name)`; the idempotency key is `RequestId`. Open question for payroll: is a second row of the same allowance valid (e.g. two vehicles), or a duplicate to clean up?
- Audit columns: existing `CreatedDate/CreatedBy/ModifiedDate/ModifiedBy` kept (S13).
Rows on 2026-11-24 (right after the migration): 23.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid. |
| AllowanceType | nvarchar(50) | NOT NULL |  | e.g. `Fixed Amount`, `Quantity`, `Fuel Allowance` (legacy). Values come from Configuration `AllowanceTypes`. |
| Name | nvarchar(200) | NOT NULL |  | `Fuel Allowance` triggers FuelPrice x Quantity logic when type is `Quantity`. Values come from Configuration `AllowanceNames`. |
| Amount | decimal(18,2) | NOT NULL |  | Fixed amount; multiplied by Quantity for non-fuel rows. |
| IsPercentage | bit | NOT NULL |  | When true, amount = basic x PercentageValue / 100. |
| PercentageValue | decimal(5,2) | NULL |  | CHECK 0-100 when set. |
| EffectiveDate | date | NOT NULL |  | was datetime2(7). 2026-11-17: whole day (S9). Should be honoured by payroll (app fix). |
| EndDate | date | NULL |  | was datetime2(7) NULL. Same. |
| IsActive | bit | NOT NULL |  |  |
| CreatedDate | datetime2(0) | NOT NULL | (sysdatetime() | was datetime2(7) with no default (S9, S12). |
| CreatedBy | nvarchar(100) | NOT NULL |  |  |
| ModifiedDate | datetime2(0) | NULL |  | was datetime2(7). |
| ModifiedBy | nvarchar(100) | NULL |  | was nvarchar(MAX) NULL. Same length as `CreatedBy` (S7); check `MAX(LEN(ModifiedBy))` first. |
| Quantity | int | NOT NULL | 1 | Added 2026-09-18 (migration 20260918120000, not recorded in __EFMigrationsHistory). |
| Notes | nvarchar(1000) | NULL |  | was `Remarks` nvarchar(500) NULL, added 2026-09-18 (migration 20260918130000). 2026-11-17 (S24, T7): standard note column name and length; renamed at contract. |
| Frequency | nvarchar(30) | NOT NULL | 'Monthly' | Stored, not honoured by payroll (app issue). |
| RequestId | uniqueidentifier | NULL |  | 2026-11-17 (S21): idempotency key; the form generates the GUID when it renders, so a replayed POST is rejected. NULL for existing rows. |

Keys and indexes
- `PK_Allowances` - primary key, clustered, (Id)
- `IX_Allowances_EmployeeId` - nonclustered, (EmployeeId)
- `UX_Allowances_RequestId` - unique, nonclustered, (RequestId) WHERE ([RequestId] IS NOT NULL)
- `FK_Allowances_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `CK_Allowances_Percentage` - check ([PercentageValue] IS NULL OR [PercentageValue]>=(0) AND [PercentageValue]<=(100)).

## Table: dbo.Attendance
Processed attendance: one row per employee per shift date (the schedule day the shift starts on), so a 22:00-06:00 shift stays on one row. Built from `AttendancePunches` + the schedule (`ShiftAssignments` / `RosterOverrides`) + approved `HalfDayRequests`, `LeaveRequests`, `GazettedHolidays` and HR's `AttendanceCorrections`. Table name kept (S3 exception).
- 2026-11-16: The table is empty, so there is no data migration. The running app still writes the old columns (`EmployeeID`, `EmployeeName`, `DepartmentName`, `AttendanceDate`, `TimeIn`, `TimeOut`, `Status`), so the change is expand then contract (S19): the new columns are added beside them (the new employee key temporarily as `EmployeeUid`), and the old ones are dropped after the app switch.
- 2026-11-16: Processing rules (draft, B6, B7, B16): late if `CheckInAt > ScheduledStartAt + GraceLateMinutes`; early leave if `CheckOutAt < ScheduledEndAt - GraceEarlyMinutes`; `WorkedMinutes >= MinFullDayMinutes` (420) -> `Present`; approved half day -> `HalfDay`; otherwise worked time under 420 minutes -> `Hourly` with `PayableMinutes` = worked minutes; no check-out -> `MissingCheckout` until HR records a correction, except when an approved half day has `RequiresCheckout = 0`.
- 2026-11-17 (S18): `LateMinutes`, `EarlyLeaveMinutes`, `WorkedMinutes`, `PayableMinutes`, `DayStatus`, `ScheduledStartAt/EndAt` are snapshots of processing, stored so history survives later shift or rule edits; reprocessing is idempotent (updates the row keyed by `(EmployeeId, ShiftDate)`; locked rows are skipped).
- 2026-11-16: Once a payslip for the month is generated, rows get `IsLocked = 1`; later changes go through `AttendanceCorrections` and need the payslip unlocked (B15).
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. was `AttendanceID` (S1); renamed in the contract phase. |
| Notes | nvarchar(1000) | NULL |  | was `Comments` nvarchar(255) NULL. 2026-11-17 (S24, T7): HR's short note on that day (e.g. a forgotten check-out told to HR); renamed at contract. Context only, never read by processing or payroll; dated, signed notes go in `RecordNotes`. |
| EmployeeId | int | NOT NULL |  | was `EmployeeID` nvarchar(50) NOT NULL (string, no FK). FK -> Employee.uid. Added as `EmployeeUid` NULL, renamed `EmployeeId` NOT NULL at contract. |
| ShiftDate | date | NOT NULL |  | was `AttendanceDate` date NOT NULL. The day the shift starts, which differs from the check-out day for overnight shifts. |
| ShiftId | int | NULL |  | FK -> Shifts.Id. Shift that applied that day after overrides; NULL = no schedule found (flag for HR). |
| ScheduledStartAt | datetime2(0) | NULL |  | ShiftDate + Shifts.StartTime (snapshot). |
| ScheduledEndAt | datetime2(0) | NULL |  | ShiftDate (+1 day if IsOvernight) + Shifts.EndTime (snapshot). |
| CheckInAt | datetime2(0) | NULL |  | was `TimeIn` time(7) NULL. A time alone can't express a check-out on the next day. |
| CheckOutAt | datetime2(0) | NULL |  | was `TimeOut` time(7) NULL. |
| CheckOutSource | varchar(15) | NOT NULL | 'None' | `Punch`, `HRCorrection`, `Auto`, `None`. |
| LateMinutes | int | NOT NULL | 0 | Minutes after ScheduledStartAt beyond grace (snapshot). |
| EarlyLeaveMinutes | int | NOT NULL | 0 | Minutes before ScheduledEndAt beyond grace (snapshot). |
| WorkedMinutes | int | NOT NULL | 0 | CheckOutAt - CheckInAt - Shifts.BreakMinutes (snapshot). |
| PayableMinutes | int | NOT NULL | 0 | Minutes payroll pays; basis for hourly pay (snapshot). |
| DayStatus | varchar(20) | NOT NULL | 'Absent' | was `Status` nvarchar(10) NULL (P/A/L/H). `Present`, `HalfDay`, `Hourly`, `Absent`, `Leave`, `Holiday`, `WeeklyOff`, `MissingCheckout`. Old L (Late) becomes `Present` + `IsLate = 1`. |
| HalfDayRequestId | int | NULL |  | FK -> HalfDayRequests.Id. Set when an approved half day applied. |
| LeaveRequestId | int | NULL |  | FK -> LeaveRequests.Id. Set when `DayStatus = 'Leave'`. |
| IsLocked | bit | NOT NULL | 0 | Set after payroll for the month. |
| ProcessedAt | datetime2(0) | NULL |  | Last time the row was (re)calculated. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() | Set M. |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |
| RowVersion | rowversion | NOT NULL |  | S15: processing and an HR correction can run at the same time; the later writer must re-read. |
| IsLate | bit | NOT NULL |  | computed: (CONVERT([bit],case when [LateMinutes]>(0) then (1) else (0) end)). Computed, persisted: `CASE WHEN LateMinutes > 0 THEN 1 ELSE 0 END`. |

Keys and indexes
- `PK_Attendance` - primary key, clustered, (Id)
- `IX_Attendance_HalfDayRequestId` - nonclustered, (HalfDayRequestId) WHERE ([HalfDayRequestId] IS NOT NULL)
- `IX_Attendance_LeaveRequestId` - nonclustered, (LeaveRequestId) WHERE ([LeaveRequestId] IS NOT NULL)
- `IX_Attendance_ShiftDate` - nonclustered, (ShiftDate)
- `IX_Attendance_ShiftId` - nonclustered, (ShiftId) WHERE ([ShiftId] IS NOT NULL)
- `UX_Attendance_Employee_ShiftDate` - unique, nonclustered, (EmployeeId, ShiftDate)
- `FK_Attendance_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `FK_Attendance_HalfDayRequests` - foreign key (HalfDayRequestId) -> HalfDayRequests(Id), no action.
- `FK_Attendance_LeaveRequests` - foreign key (LeaveRequestId) -> LeaveRequests(Id), no action.
- `FK_Attendance_Shifts` - foreign key (ShiftId) -> Shifts(Id), no action.
- `CK_Attendance_CheckOutSource` - check ([CheckOutSource]='None' OR [CheckOutSource]='Auto' OR [CheckOutSource]='HRCorrection' OR [CheckOutSource]='Punch').
- `CK_Attendance_CheckTimes` - check ([CheckOutAt] IS NULL OR [CheckInAt] IS NULL OR [CheckOutAt]>[CheckInAt]).
- `CK_Attendance_DayStatus` - check ([DayStatus]='MissingCheckout' OR [DayStatus]='WeeklyOff' OR [DayStatus]='Holiday' OR [DayStatus]='Leave' OR [DayStatus]='Absent' OR [DayStatus]='Hourly' OR [DayStatus]='HalfDay' OR [DayStatus]='Present').

## Table: dbo.AttendanceCorrections
HR's record of attendance fixes, mainly forgotten check-outs reported by the employee. The correction is applied to `Attendance`; the raw `AttendancePunches` are never overwritten. Append-only (set C only).
Rows: new table.
- 2026-11-17: No `EmployeeId` column: it would duplicate `Attendance.EmployeeId` and could contradict it. Per-employee history joins through `AttendanceId`. No `CreatedBy` either: `RecordedByUserId` is the user.
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| AttendanceId | int | NOT NULL |  | FK -> Attendance.Id. Processing creates a row for every scheduled day (Absent included), so there is always a row to correct. |
| CorrectionType | varchar(20) | NOT NULL |  | `MissedCheckOut`, `MissedCheckIn`, `TimeChange`, `StatusChange`. |
| CorrectedCheckInAt | datetime2(0) | NULL |  | New check-in, when corrected. |
| CorrectedCheckOutAt | datetime2(0) | NULL |  | New check-out, e.g. the time the employee tells HR. |
| OldDayStatus | varchar(20) | NULL |  | For `StatusChange`. |
| NewDayStatus | varchar(20) | NULL |  | For `StatusChange`. |
| ReportedByEmployeeAt | datetime2(0) | NULL |  | When the employee told HR. |
| Reason | nvarchar(500) | NOT NULL |  |  |
| RecordedByUserId | int | NOT NULL |  | FK -> Users.uid (the HR user). |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() | When HR recorded it. |
| RequestId | uniqueidentifier | NULL |  | S21 idempotency key, generated by the form. |

Keys and indexes
- `PK_AttendanceCorrections` - primary key, clustered, (Id)
- `IX_AttendanceCorrections_AttendanceId` - nonclustered, (AttendanceId)
- `IX_AttendanceCorrections_RecordedByUserId` - nonclustered, (RecordedByUserId)
- `UX_AttendanceCorrections_RequestId` - unique, nonclustered, (RequestId) WHERE ([RequestId] IS NOT NULL)
- `FK_AttendanceCorrections_Attendance` - foreign key (AttendanceId) -> Attendance(Id), no action.
- `FK_AttendanceCorrections_Users` - foreign key (RecordedByUserId) -> Users(uid), no action.
- `CK_AttendanceCorrections_Type` - check ([CorrectionType]='StatusChange' OR [CorrectionType]='TimeChange' OR [CorrectionType]='MissedCheckIn' OR [CorrectionType]='MissedCheckOut').
- `CK_AttendanceCorrections_Values` - check (([CorrectionType]<>'MissedCheckOut' OR [CorrectedCheckOutAt] IS NOT NULL) AND ([CorrectionType]<>'MissedCheckIn' OR [CorrectedCheckInAt] IS NOT NULL) AND ([CorrectionType]<>'StatusChange' OR [NewDayStatus] IS NOT NULL)).

## Table: dbo.AttendancePunches
Raw check-in / check-out events exactly as received from a device, an Excel/CSV upload or manual entry, so processing (and re-processing after a schedule change) always starts from untouched source data. Append-only (set C only).
Rows: new table. Expected volume: ~850 employees x a few punches a day, so `bigint` (S2).
- 2026-11-17 (S21): re-uploading a file cannot duplicate punches: the unique key is on the raw code (always present), not on `EmployeeId` (NULL for unmatched codes, which would otherwise duplicate on every re-upload).
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | bigint | NOT NULL |  | PK, identity. |
| EmployeeId | int | NULL |  | FK -> Employee.uid. NULL when `RawEmployeeCode` could not be matched (shown to HR). |
| RawEmployeeCode | varchar(50) | NOT NULL |  | Code as it came from the device/file; matched against Employee.EmployeeID. Manual punches use the employee's `EmployeeID`. |
| PunchAt | datetime2(0) | NOT NULL |  | Full date and time (overnight shifts). PKT, as uploaded (S9). |
| Direction | varchar(7) | NOT NULL | 'Unknown' | `In`, `Out`, `Unknown` (devices that don't send direction). |
| Source | varchar(10) | NOT NULL |  | `Device`, `Upload`, `Manual`. |
| DeviceId | nvarchar(50) | NULL |  | Device serial/name for `Device` punches. |
| UploadLogId | int | NULL |  | FK -> AttendanceUploadLogs.Id for `Upload` punches. |
| AttendanceId | int | NULL |  | FK -> Attendance.Id: the shift day this punch was assigned to by processing. |
| IsIgnored | bit | NOT NULL | 0 | Duplicate/accidental punch excluded by HR or processing. |
| IgnoreReason | nvarchar(200) | NULL |  |  |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_AttendancePunches` - primary key, clustered, (Id)
- `IX_AttendancePunches_AttendanceId` - nonclustered, (AttendanceId) WHERE ([AttendanceId] IS NOT NULL)
- `IX_AttendancePunches_Employee_PunchAt` - nonclustered, (EmployeeId, PunchAt)
- `IX_AttendancePunches_UploadLogId` - nonclustered, (UploadLogId) WHERE ([UploadLogId] IS NOT NULL)
- `UX_AttendancePunches_Dedup` - unique, nonclustered, (RawEmployeeCode, PunchAt, Direction)
- `FK_AttendancePunches_Attendance` - foreign key (AttendanceId) -> Attendance(Id), no action.
- `FK_AttendancePunches_AttendanceUploadLogs` - foreign key (UploadLogId) -> AttendanceUploadLogs(Id), no action.
- `FK_AttendancePunches_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `CK_AttendancePunches_Direction` - check ([Direction]='Unknown' OR [Direction]='Out' OR [Direction]='In').
- `CK_AttendancePunches_Source` - check ([Source]='Manual' OR [Source]='Upload' OR [Source]='Device').

## Table: dbo.AttendanceUploadLogs
Audit trail of Excel/CSV attendance imports (counts and error details). Existing `UploadedBy/UploadDate` serve as the S13 audit columns.
- 2026-11-17: Live check: all 3 rows are `LeaveRecords II.xlsx`, uploaded 3 times within 7 minutes on 2026-01-05, with a NULL `Status`. That is the replay problem `FileHash` (T3) fixes. Their `Status` is backfilled from `Message`/counts before the CHECK is added (proposed `Failed` when `SuccessCount` is 0 or NULL, else `Completed`; review the 3 rows by hand).
- 2026-11-16: Uploads now write `AttendancePunches` rows that point back here (`AttendancePunches.UploadLogId`).
Rows on 2026-11-24 (right after the migration): 3.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| FileName | nvarchar(255) | NOT NULL |  | was NULL. S12. |
| UploadedBy | nvarchar(100) | NULL |  |  |
| UploadDate | datetime2(0) | NOT NULL | (sysdatetime() | was datetime NULL, default getdate() (S9). Existing name kept (S4). |
| Status | varchar(20) | NOT NULL | 'Processing' | was nvarchar(50) NULL, unconstrained. `Processing`, `Completed`, `CompletedWithErrors`, `Failed` (verify against what the upload code writes). |
| Message | nvarchar(MAX) | NULL |  |  |
| ErrorDetails | nvarchar(MAX) | NULL |  |  |
| SuccessCount | int | NOT NULL | 0 | was NULL. S12. NULLs set to 0 first. |
| FailureCount | int | NOT NULL | 0 | was NULL. |
| TotalRows | int | NOT NULL | 0 | was NULL. |
| FileHash | char(64) | NULL |  | T3: SHA-256 of the uploaded file. NULL for the 3 existing rows. |

Keys and indexes
- `PK_AttendanceUploadLogs` - primary key, clustered, (Id)
- `UX_AttendanceUploadLogs_FileHash` - unique, nonclustered, (FileHash) WHERE ([FileHash] IS NOT NULL AND [Status]<>'Failed')
- `CK_AttendanceUploadLogs_Status` - check ([Status]='Failed' OR [Status]='CompletedWithErrors' OR [Status]='Completed' OR [Status]='Processing').

## Table: dbo.AuditLogs
Generic change audit (who, what, old/new JSON, IP). Append-only, so it is intentionally not idempotent (S21).
- 2026-11-16: Currently written only by payslip locking; should also be written by leave/half-day approvals, attendance corrections, roster overrides, entitlement overrides, Employee org changes and payslip unlocking (B15). It should write a row only when state actually changes.
- 2026-11-17: `EmployeeId` gets a real FK (S5); employees are never deleted (S6), so audit rows cannot be orphaned. Verify the 4 existing rows first.
Rows on 2026-11-24 (right after the migration): 4.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | bigint | NOT NULL |  | PK, identity. was `LogId` int (S1, S2: a log grows without limit). Renamed at contract; the type change is cheap on 4 rows. |
| TableName | nvarchar(128) | NOT NULL |  |  |
| Operation | nvarchar(20) | NOT NULL |  |  |
| RecordId | nvarchar(100) | NULL |  |  |
| EmployeeId | int | NULL |  | FK -> Employee.uid. |
| OldData | nvarchar(MAX) | NULL |  | JSON. |
| NewData | nvarchar(MAX) | NULL |  | JSON. |
| ChangedBy | nvarchar(128) | NULL | (suser_sname() |  |
| ChangedAt | datetime2(0) | NOT NULL | (sysdatetime() | was datetime2(7) (S9). Ordering uses `Id`. |
| ModuleName | nvarchar(100) | NULL |  |  |
| IPAddress | nvarchar(50) | NULL |  |  |

Keys and indexes
- `PK_AuditLogs` - primary key, clustered, (Id)
- `IX_AuditLogs_EmployeeId` - nonclustered, (EmployeeId) WHERE ([EmployeeId] IS NOT NULL)
- `FK_AuditLogs_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.

## Table: dbo.CarryforwardLeaves
Leave days carried from one leave year to the next, per employee per leave type.
- 2026-11-16: Empty, so the table is dropped and re-created in the new shape. This resolves the db.md mismatch with the EF entity (`Id`, `FromYear`, `ToYear`, `CarryForwardDays`, `Description`), now with `EmployeeId` int and `LeaveTypeId`. Used/remaining days are derived in `vLeaveBalances`, not stored (S18).
- B5: carry-forward cap and leave year (calendar assumed) are open; a `CK` for a cap is added once answered.
- 2026-11-24 (B18): the legacy per-year figures were archived in `EmployeeLegacyLeaveBalances`, not converted into rows here. New rows come from the yearly rollover (see `LeaveTypes`).
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | was nvarchar(50) (Employee.EmployeeID string, part of PK). FK -> Employee.uid. |
| LeaveTypeId | int | NOT NULL |  | FK -> LeaveTypes.Id. Replaces the entity's `LeaveTypeName`. |
| FromYear | int | NOT NULL |  | was `Year` int (part of PK). Leave year the days come from. |
| ToYear | int | NOT NULL |  | Leave year the days are added to. |
| CarryForwardDays | decimal(5,1) | NOT NULL |  | was `TotalLeaves` int. Half days; name matches the EF entity. |
| Description | nvarchar(500) | NULL |  |  |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_CarryforwardLeaves` - primary key, clustered, (Id)
- `IX_CarryforwardLeaves_LeaveTypeId` - nonclustered, (LeaveTypeId)
- `UX_CarryforwardLeaves_Employee_Type_FromYear` - unique, nonclustered, (EmployeeId, LeaveTypeId, FromYear)
- `FK_CarryforwardLeaves_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `FK_CarryforwardLeaves_LeaveTypes` - foreign key (LeaveTypeId) -> LeaveTypes(Id), no action.
- `CK_CarryforwardLeaves_Days` - check ([CarryForwardDays]>=(0)).
- `CK_CarryforwardLeaves_Years` - check ([ToYear]=([FromYear]+(1))).

## Table: dbo.Configuration
Key/value application settings edited under Setup: single values (`FuelPrice`) and name-only dropdown lists (`AllowanceTypes`, `AllowanceNames`), stored as comma-separated text (S11).
- 2026-11-17 (S11, T5, T6): Rows for lists that become tables or derived values are deleted in the last phase, after the app reads the new tables: `Project`, `Department`, `Designation`, `LeaveTypes`, `Year`, `TaxSlabs`, `CurrentTaxYear`. Rows not named here stay. Live check: no duplicate or NULL `ConfigKey` among the 19 rows.
Rows on 2026-11-24 (right after the migration): 12.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | Identity. was `UID`, not a key. Becomes the PK (EF already declares it); renamed at contract (S1). |
| ConfigKey | nvarchar(50) | NOT NULL |  | was NULL, not unique. Unique. |
| ConfigValue | nvarchar(MAX) | NULL |  | Not filtered or joined, so MAX is allowed (S7). |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_Configuration` - primary key, clustered, (Id)
- `UX_Configuration_ConfigKey` - unique, nonclustered, (ConfigKey)

## Table: dbo.Deductions
Per-employee payroll deductions (fixed amount or percentage of gross).
- 2026-11-17: Live check: all 11 rows are `Fixed`, with values up to 800.00 stored in `PercentageValue decimal(5,2)` (cap 999.99). They move to `Amount` and `PercentageValue` becomes percentage-only.
- 2026-11-16: `EffectiveDate`, `EndDate` and `Frequency` are stored but not honoured by payroll (app issue).
- 2026-11-17: Live check: no duplicate active `(EmployeeId, DeductionName)`, so a unique key on it can be added later if wanted; the idempotency key is `RequestId`.
Rows on 2026-11-24 (right after the migration): 11.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid. |
| DeductionType | nvarchar(50) | NOT NULL |  |  |
| DeductionName | nvarchar(100) | NOT NULL |  |  |
| Frequency | nvarchar(50) | NOT NULL |  | Not honoured by payroll (app issue). |
| CalculationMethod | varchar(20) | NOT NULL |  | was nvarchar(50). `Percentage` or `Fixed` (live: only `Fixed`). |
| PercentageValue | decimal(5,2) | NULL |  | Percentage only, 0-100. Fixed amounts move to `Amount`. |
| IsMandatory | bit | NOT NULL | 0 |  |
| EffectiveDate | date | NOT NULL |  | Not honoured by payroll (app issue). |
| EndDate | date | NULL |  |  |
| IsActive | bit | NOT NULL | 1 |  |
| CreatedDate | datetime2(0) | NOT NULL | (sysdatetime() | was datetime2(7), default getdate(). |
| CreatedBy | nvarchar(100) | NOT NULL |  |  |
| ModifiedDate | datetime2(0) | NULL |  | was datetime2(7). |
| ModifiedBy | nvarchar(100) | NULL |  |  |
| Amount | decimal(18,2) | NULL |  | Fixed amount when CalculationMethod = `Fixed`. Backfilled from PercentageValue for those rows. |
| Notes | nvarchar(1000) | NULL |  | S24, T7: HR's short note. |
| RequestId | uniqueidentifier | NULL |  | S21 idempotency key, generated by the form. |

Keys and indexes
- `PK_Deductions` - primary key, clustered, (Id)
- `IX_Deductions_EmployeeId` - nonclustered, (EmployeeId)
- `UX_Deductions_RequestId` - unique, nonclustered, (RequestId) WHERE ([RequestId] IS NOT NULL)
- `FK_Deductions_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `CK_Deductions_Method` - check ([CalculationMethod]='Fixed' OR [CalculationMethod]='Percentage').
- `CK_Deductions_Value` - check ([CalculationMethod]='Percentage' AND ([PercentageValue]>=(0) AND [PercentageValue]<=(100)) OR [CalculationMethod]='Fixed' AND [Amount] IS NOT NULL AND [Amount]>=(0)).

## Table: dbo.Departments
One shared department list, the FK target of `Employee.DepartmentId`. 2026-11-18 (decision B2, from HR): projects and departments are linked but not tightly. A department is not owned by a project, so this table has no `ProjectId` and there is no link table.
- 2026-11-18: HR's examples: Galaxy Construction has a Maintenance department that other projects do not have; every project has Admin and Security; there is one HR department for all projects and its staff sit under Bahria Town Karachi. HR sorts department first (all Admin staff in every project) and then by project, or the reverse. This matches the live data: 14 department names are used in more than one project (Security in 4, Accounts & Finance in 4, Admin - Transportation, Operations, House Keeping and Information Technology in 3 each, and 8 more in 2).
- 2026-11-18: Employee holds `ProjectId` and `DepartmentId` independently; the database does not check that a department "belongs" to the project. A project's department dropdown can be built from the departments already used in that project, or show them all.
- 2026-11-16: The 4 existing rows (Software Development, Human Resource, Civil Staff, Azizabad Dastarkhuwan) are kept. The list is seeded from the 58 distinct `Employee.Department` strings; the mapping of those strings to departments still needs HR (B2).
- 2026-11-18 (project owner, B2): this table holds the main department. The test data has names that are a base plus a function (`Admin - Mess`, `Operations - Record`); the base goes here and the function goes to `SubDepartments`. `Admin` is the Administration department, so that row is named `Administration`. Split rule: at the first ` - ` (see Migration order). Only Administration and Operations have sub-departments in the test data. For HR at go-live: whether `Administrator` (BT Tower, Icon) is the Administration department, and whether `Mess` and `Transportation` (no `Admin - ` prefix) are its functions.
Rows on 2026-11-24 (right after the migration): 48.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. was `DepartmentID` (S1); renamed at contract. |
| DepartmentName | nvarchar(100) | NOT NULL |  | was nvarchar(100) NULL. Existing name kept (S4). Unique. |
| SortOrder | int | NOT NULL | 0 | Default order of departments in lists (a global order now, not per project). |
| IsActive | bit | NOT NULL | 1 | S14. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_Departments` - primary key, clustered, (Id)
- `UX_Departments_DepartmentName` - unique, nonclustered, (DepartmentName)

## Table: dbo.Designations
Designation lookup, replacing the free-text `Employee.Designation` and the Configuration dropdown list. Shared by all projects. Lets employee lists sort by seniority (`Rank`) instead of alphabetically.
Rows: new table (seeded from distinct `Employee.Designation` values; 141 distinct in Bahria Town Karachi alone, so expect a long list to review).
Rows on 2026-11-24 (right after the migration): 209.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| Name | nvarchar(100) | NOT NULL |  |  |
| Rank | int | NOT NULL | 100 | Lower = more senior. Ranking to come from HR (B4). |
| IsActive | bit | NOT NULL | 1 |  |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_Designations` - primary key, clustered, (Id)
- `UX_Designations_Name` - unique, nonclustered, (Name)

## Table: dbo.Employee
Employee master. `uid` is the FK used by every table after this change; `EmployeeID` (unique) stays the business key for display and for matching device/upload codes (decision D1). Table name kept (S3 exception).
- 2026-11-17: Live check: `EmployeeID` is numeric for all 848 rows and at most 6 characters; `EmployeeName` at most 33, `CNIC` 15, `MobileNo` 12; `FatherName` is empty on every row. The proposed lengths below are therefore safe. Names are text people type (S7), so they become `nvarchar`.
- 2026-11-16: Org structure becomes FKs (Project -> Department -> Designation). The free-text columns stay until the app has switched, then are dropped. Backfilling needs the HR mappings (the 58 department strings from B2, and B3, B4); `ProjectId` and `DepartmentId` become NOT NULL once every employee is mapped.
- 2026-11-17 (T1): `DateOfLeaving` and `DateOfReturn` record leaving and returning. 2026-11-24: `Employee` always describes the latest stint only. A resignation or termination sets `DateOfLeaving` and clears `DateOfReturn` (so `CK_Employee_Dates` still holds when someone leaves a second time); the earlier leaving and return stay in `EmployeeEvents`, which carries every cycle.
- 2026-11-24 (B18, decided by Rayder): the 7 legacy per-year leave columns (`CarryForwardLeaves`, `CarryForwardLeaves1`, `Year2022`, `Year2023`, `Year2023New`, `Year2024`, `AdjustedAjusted`) are copied into `EmployeeLegacyLeaveBalances` and dropped from `Employee` (script 10). In the test data they held values for 827 of 848 employees; Year2023 was always CarryForwardLeaves minus Year2022 and Year2024 always CarryForwardLeaves1 minus Year2023New, i.e. results typed in as numbers. The live balance is derived (below), never stored on `Employee` (S18). The Employees grid no longer filters or sorts by Year 2024.
- 2026-11-16: All length/type tightenings need a `MAX(LEN(...))` / `TRY_CONVERT` check on the 848 rows first.
Rows on 2026-11-24 (right after the migration): 848.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| uid | int | NOT NULL |  | PK, identity. |
| EmployeeID | varchar(50) | NOT NULL |  | was varchar(250). Business key, unique. |
| EmployeeName | nvarchar(100) | NOT NULL |  | was varchar(250) NULL. Check for NULLs first. |
| CNIC | varchar(15) | NULL |  | was varchar(250). 13 digits + 2 dashes. |
| FatherName | nvarchar(100) | NULL |  | was varchar(250). |
| DOB | date | NULL |  | was varchar(250) (text). Converted with TRY_CONVERT; unparseable values listed for HR first. |
| MobileNo | varchar(20) | NULL |  | was varchar(250). |
| DateOfJoining | date | NULL |  |  |
| EmployeeStatus | varchar(20) | NOT NULL | 'Active' | was varchar(250) NULL, unconstrained (all 848 rows `Active`). CHECK list: B9 (proposed `Active`, `Inactive`, `Resigned`, `Terminated`). |
| ModifiedBy | nvarchar(100) | NULL |  | was varchar(MAX) NULL. Existing name kept (S13). |
| ModifiedOn | datetime2(0) | NULL |  | was nvarchar(MAX) NULL (text). Converted with TRY_CONVERT. Existing name kept. |
| Details | nvarchar(MAX) | NULL |  |  |
| BasicSalary | decimal(18,2) | NULL |  | Payroll basis for gross and tax. |
| ApplyTax | bit | NOT NULL | 1 | was nvarchar(10) NULL. Live `1` (827), `Yes` (1), `0` (1), empty (19). `1`/`Yes` -> 1; `0`/empty -> 0 (matches today's payroll rule). Existing name kept (S4). |
| GenStatus | nvarchar(50) | NULL |  |  |
| ProjectId | int | NOT NULL |  | FK -> Projects.Id. NOT NULL once every employee is mapped (B3). |
| DepartmentId | int | NOT NULL |  | FK -> Departments.Id. Independent of `ProjectId` (B2, 2026-11-18): the same department can appear in many projects and a project can use any department. NOT NULL once every employee is mapped. |
| SubDepartmentId | int | NULL |  | 2026-11-18 (B2): the function inside the department (e.g. Mess under Administration). Optional and for information only: no rule, payroll or approval reads it. Composite FK (DepartmentId, SubDepartmentId) -> SubDepartments(DepartmentId, Id). |
| DesignationId | int | NULL |  | FK -> Designations.Id (B4). |
| DateOfLeaving | date | NULL |  | T1. Last working day; processing skips attendance/payroll after it. |
| DateOfReturn | date | NULL |  | T1. Set when someone returns after leaving. |
| PayBasis | varchar(10) | NOT NULL | 'Monthly' | `Monthly` or `Hourly` (B6). |
| HourlyRate | decimal(18,2) | NULL |  | Optional per-person rate. NULL = derived from BasicSalary (B6). |
| IsLeaveManagedOverride | bit | NULL |  | NULL = follow Projects.HasLeaveManagement; 1/0 = exception for this person (S10 exception). |
| Notes | nvarchar(1000) | NULL |  | S24, T7: HR's short note on the employee. `Details` (purpose undocumented) is left as it is. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() | S13. Existing 848 rows get the migration time (noted). |
| CreatedBy | nvarchar(100) | NULL |  |  |
| RowVersion | rowversion | NOT NULL |  | S15: two HR users editing the same employee no longer overwrite each other silently. |

Keys and indexes
- `PK_Employee` - primary key, clustered, (uid)
- `IX_Employee_Department_Project` - nonclustered, (DepartmentId, ProjectId, DesignationId)
- `IX_Employee_Department_SubDepartment` - nonclustered, (DepartmentId, SubDepartmentId) WHERE ([SubDepartmentId] IS NOT NULL)
- `IX_Employee_DesignationId` - nonclustered, (DesignationId) WHERE ([DesignationId] IS NOT NULL)
- `IX_Employee_Org` - nonclustered, (ProjectId, DepartmentId, DesignationId)
- `UX_Employee_EmployeeID` - unique, nonclustered, (EmployeeID)
- `FK_Employee_Departments` - foreign key (DepartmentId) -> Departments(Id), no action.
- `FK_Employee_Designations` - foreign key (DesignationId) -> Designations(Id), no action.
- `FK_Employee_Projects` - foreign key (ProjectId) -> Projects(Id), no action.
- `FK_Employee_SubDepartments` - foreign key (DepartmentId, SubDepartmentId) -> SubDepartments(DepartmentId, Id), no action.
- `CK_Employee_Dates` - check (([DateOfLeaving] IS NULL OR [DateOfJoining] IS NULL OR [DateOfLeaving]>=[DateOfJoining]) AND ([DateOfReturn] IS NULL OR [DateOfLeaving] IS NOT NULL AND [DateOfReturn]>[DateOfLeaving])).
- `CK_Employee_EmployeeStatus` - check ([EmployeeStatus]='Terminated' OR [EmployeeStatus]='Resigned' OR [EmployeeStatus]='Inactive' OR [EmployeeStatus]='Active').
- `CK_Employee_HourlyRate` - check ([HourlyRate] IS NULL OR [HourlyRate]>(0)).
- `CK_Employee_PayBasis` - check ([PayBasis]='Hourly' OR [PayBasis]='Monthly').

## Table: dbo.EmployeeDocuments
Metadata for files uploaded per employee (files live under `wwwroot/uploads/employees/{uid}/`). Soft-deleted via `IsDeleted`. Existing `UploadedOn/UploadedBy` serve as the S13 audit columns.
- 2026-11-16: Also stores the evidence for half days and leave (`HalfDayRequests.DocumentId`, `LeaveRequests.DocumentId`). Live check: no employee has the same file name and size twice.
Rows on 2026-11-24 (right after the migration): 8.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid (EF already declares it; no orphans today). |
| FileName | nvarchar(300) | NOT NULL |  | GUID-based stored name. |
| OriginalFileName | nvarchar(300) | NULL |  |  |
| FilePath | nvarchar(500) | NOT NULL |  |  |
| FileExtension | nvarchar(50) | NULL |  |  |
| FileSize | bigint | NULL |  |  |
| UploadedOn | datetime2(0) | NOT NULL | (sysdatetime() | was datetime NULL, default getdate(). |
| IsDeleted | bit | NOT NULL | 0 | was bit NULL. NULLs set to 0 first. |
| DocumentType | varchar(30) | NULL |  | e.g. `CNIC`, `HalfDayProof`, `LeaveProof`, `MedicalCertificate`, `Other`. Code list: B9. |
| FileHash | char(64) | NULL |  | T3: SHA-256 of the file content; NULL for the 8 existing rows (a backfill script can hash the files on disk). |
| UploadedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_EmployeeDocuments` - primary key, clustered, (Id)
- `IX_EmployeeDocuments_EmployeeId` - nonclustered, (EmployeeId) WHERE ([IsDeleted]=(0))
- `UX_EmployeeDocuments_Employee_FileHash` - unique, nonclustered, (EmployeeId, FileHash) WHERE ([IsDeleted]=(0) AND [FileHash] IS NOT NULL)
- `FK_EmployeeDocuments_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.

## Table: dbo.EmployeeEvents
Key employment events per employee, shown as the Employee History tab and recorded from Edit Employee. An entry is never edited; a wrong one is voided with a reason (kept, shown struck through) and can then be deleted by an Admin. Script `08_employee_events.sql`.
- 2026-11-24 (B14, T1): decided by Rayder. History is a timeline of events, each with the position before and after, not a table of effective-dated positions. `Employee` still holds the current position; the events explain how it got there. "Where was this person on a past date" is answered by walking the events back from the current position (B14 stays open for whether reports must store a snapshot).
- 2026-11-24: Recording an event also updates `Employee`, in the same transaction (Rayder): `Joined` sets `DateOfJoining`; `Promotion` sets `DesignationId`; `Transfer` sets any of `ProjectId`, `DepartmentId`, `DesignationId` (a department change clears `SubDepartmentId`); `Resignation` / `Termination` set `DateOfLeaving`, clear `DateOfReturn` and set `EmployeeStatus` to `Resigned` / `Terminated`; `Rejoin` sets `DateOfReturn` and `EmployeeStatus = Active`; `Other` changes nothing. The service refuses combinations that cannot happen: a promotion or transfer for someone who has left, a rejoin for someone who has not, a second resignation without a rejoin, a date before the joining date, and (except `Other`) a date earlier than the latest live entry, so the timeline and the employee record cannot disagree.
- 2026-11-24 (T8, decided): only the latest live entry can be voided, and voiding it reverts its effect on `Employee` (promotion/transfer restore the From position; resignation, termination and rejoin replay the remaining entries; fields changed by hand since are left alone). A voided entry stays visible, marked voided, until an Admin deletes it (hard delete, only for voided rows, the full row is written to `AuditLogs`); then it no longer appears in the history.
- 2026-11-24: `Joined` is written when an employee is created (Employee Create) and by the backfill; HR records the other six types from Edit Employee. The backfill gives each employee with a `DateOfJoining` one `Joined` row carrying the current project, department and designation (the original position was never recorded; the remark says so), a `Resignation` / `Termination` row where `DateOfLeaving` is set (type from `EmployeeStatus`) and a `Rejoin` row where `DateOfReturn` is set; `CreatedBy = Migration`.
- 2026-11-24: Not part of payroll or attendance processing yet. Processing keeps reading `Employee.DateOfJoining/DateOfLeaving/DateOfReturn` (B10); the events are the audit trail behind them.
Rows on 2026-11-24 (right after the migration): 844.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid. |
| EventType | varchar(20) | NOT NULL |  | `Joined`, `Promotion`, `Transfer`, `Resignation`, `Termination`, `Rejoin`, `Other` (CHECK; more types are a CHECK change, B19). |
| EventDate | date | NOT NULL |  | The day the event took effect (last working day for a resignation or termination). |
| FromProjectId | int | NULL |  |  |
| ToProjectId | int | NULL |  |  |
| FromDepartmentId | int | NULL |  |  |
| ToDepartmentId | int | NULL |  |  |
| FromDesignationId | int | NULL |  |  |
| ToDesignationId | int | NULL |  |  |
| Remarks | nvarchar(1000) | NULL |  | Reason, letter reference. Required for `Other`. |
| IsVoided | bit | NOT NULL | 0 | Voided entries stay in the table and on screen (S14). |
| VoidReason | nvarchar(500) | NULL |  | Required when voided (CK). |
| VoidedAt | datetime2(0) | NULL |  | Set with `IsVoided`. |
| VoidedBy | nvarchar(100) | NULL |  | Username. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() | When it was recorded. |
| CreatedBy | nvarchar(100) | NULL |  | Username of the recorder (`Migration` for backfilled rows). |
| RequestId | uniqueidentifier | NULL |  | S21 idempotency key from the form: a double click records one event. |

Keys and indexes
- `PK_EmployeeEvents` - primary key, clustered, (Id)
- `IX_EmployeeEvents_Employee_Date` - nonclustered, (EmployeeId, EventDate, Id)
- `UX_EmployeeEvents_Employee_Joined` - unique, nonclustered, (EmployeeId) WHERE ([EventType]='Joined' AND [IsVoided]=(0))
- `UX_EmployeeEvents_RequestId` - unique, nonclustered, (RequestId) WHERE ([RequestId] IS NOT NULL)
- `FK_EmployeeEvents_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `FK_EmployeeEvents_FromDepartment` - foreign key (FromDepartmentId) -> Departments(Id), no action.
- `FK_EmployeeEvents_FromDesignation` - foreign key (FromDesignationId) -> Designations(Id), no action.
- `FK_EmployeeEvents_FromProject` - foreign key (FromProjectId) -> Projects(Id), no action.
- `FK_EmployeeEvents_ToDepartment` - foreign key (ToDepartmentId) -> Departments(Id), no action.
- `FK_EmployeeEvents_ToDesignation` - foreign key (ToDesignationId) -> Designations(Id), no action.
- `FK_EmployeeEvents_ToProject` - foreign key (ToProjectId) -> Projects(Id), no action.
- `CK_EmployeeEvents_Type` - check ([EventType]='Other' OR [EventType]='Rejoin' OR [EventType]='Termination' OR [EventType]='Resignation' OR [EventType]='Transfer' OR [EventType]='Promotion' OR [EventType]='Joined').
- `CK_EmployeeEvents_Void` - check ([IsVoided]=(0) OR [VoidReason] IS NOT NULL AND [VoidedAt] IS NOT NULL).

## Table: dbo.EmployeeLegacyLeaveBalances
Read-only archive of the seven per-year leave figures that lived on `Employee` (decision B18, script 10, 2026-11-24). Nothing in the app reads or writes it; it exists so the old spreadsheet figures can still be looked up. One row per employee that had any value.
Rows on 2026-11-24 (right after the migration): 828.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| EmployeeId | int | NOT NULL |  | PK and FK -> Employee.uid. |
| CarryForwardLeaves | float | NULL |  | Original names and values, unchanged. |
| CarryForwardLeaves1 | float | NULL |  | Original names and values, unchanged. |
| Year2022 | float | NULL |  | Original names and values, unchanged. |
| Year2023 | float | NULL |  | Original names and values, unchanged. |
| Year2023New | decimal(18,2) | NULL |  | Original. |
| Year2024 | int | NULL |  | Original. |
| AdjustedAjusted | int | NULL |  | Original. |
| ArchivedAt | datetime2(0) | NOT NULL | (sysdatetime() | When the row was archived. |

Keys and indexes
- `PK_EmployeeLegacyLeaveBalances` - primary key, clustered, (EmployeeId)
- `FK_EmployeeLegacyLeaveBalances_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.

## Table: dbo.GazettedHolidays
Public (gazetted) holidays. Empty in production on 2026-11-16.
- 2026-11-16: Resolves the EF mismatch: the EF entity (`GazettedHoliday`, with `Id`, `HolidayName`) is mapped to this plural table with `ToTable("GazettedHolidays")`; `database-schema.sql` should create the plural name. Empty, so it is rebuilt.
- 2026-11-16: `ProjectId` NULL = holiday for everyone; a value = project-specific. SQL Server unique indexes treat NULLs as equal, so the key below allows one company-wide row plus one row per project for the same date.
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity (EF entity key). |
| HolidayDate | date | NOT NULL |  | Type unchanged; was the PK. Now part of a unique key with ProjectId. |
| HolidayName | nvarchar(100) | NOT NULL |  | In the EF entity. |
| Description | nvarchar(255) | NULL |  |  |
| ProjectId | int | NULL |  | FK -> Projects.Id. NULL = all projects. |
| IsOptional | bit | NOT NULL | 0 | Optional holidays (employees may work); not auto-applied by processing. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_GazettedHolidays` - primary key, clustered, (Id)
- `IX_GazettedHolidays_ProjectId` - nonclustered, (ProjectId) WHERE ([ProjectId] IS NOT NULL)
- `UX_GazettedHolidays_Date_Project` - unique, nonclustered, (HolidayDate, ProjectId)
- `FK_GazettedHolidays_Projects` - foreign key (ProjectId) -> Projects(Id), no action.

## Table: dbo.HalfDayRequests
Half-day requests with approval and documentation. HR rule: half days (until at least 12-1pm) need approval with documentation; whether the employee must still check out depends on the approval and the case, so it is a per-request flag.
Rows: new table.
- B1: whether an approved half day is charged to leave balance is open. Drafted with an optional link (`LeaveRequestId NULL`); if HR says "always charged" this table merges into `LeaveRequests`, if "never" the column is dropped.
- B7: the exact half-day rule (minimum hours, cutoff, whether a document is mandatory) is open; `DocumentId` is nullable and has no CHECK until answered.
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid. |
| HalfDayDate | date | NOT NULL |  | Shift date the half day applies to. |
| HalfType | varchar(10) | NOT NULL |  | `FirstHalf` (arrives late) or `SecondHalf` (leaves early). |
| ExpectedTime | time(0) | NULL |  | SecondHalf: expected leave time (not before Shifts.HalfDayCutoffTime). FirstHalf: expected arrival. |
| Reason | nvarchar(500) | NOT NULL |  |  |
| DocumentId | int | NULL |  | FK -> EmployeeDocuments.Id (supporting document). |
| RequiresCheckout | bit | NOT NULL | 1 | 0 = approver accepts no check-out for this case; processing then doesn't mark MissingCheckout. |
| LeaveRequestId | int | NULL |  | B1. FK -> LeaveRequests.Id when the half day is charged to leave. |
| Status | varchar(10) | NOT NULL | 'Pending' | `Pending`, `Approved`, `Rejected`, `Cancelled`. |
| ApprovedByUserId | int | NULL |  | FK -> Users.uid. Approver or rejecter. |
| ApprovedAt | datetime2(0) | NULL |  |  |
| ReviewerComments | nvarchar(500) | NULL |  |  |
| Notes | nvarchar(1000) | NULL |  | S24, T7: HR's short note. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |
| RowVersion | rowversion | NOT NULL |  | S15. |

Keys and indexes
- `PK_HalfDayRequests` - primary key, clustered, (Id)
- `IX_HalfDayRequests_ApprovedByUserId` - nonclustered, (ApprovedByUserId) WHERE ([ApprovedByUserId] IS NOT NULL)
- `IX_HalfDayRequests_DocumentId` - nonclustered, (DocumentId) WHERE ([DocumentId] IS NOT NULL)
- `IX_HalfDayRequests_LeaveRequestId` - nonclustered, (LeaveRequestId) WHERE ([LeaveRequestId] IS NOT NULL)
- `UX_HalfDayRequests_Employee_Date` - unique, nonclustered, (EmployeeId, HalfDayDate) WHERE ([Status] IN ('Pending', 'Approved'))
- `FK_HalfDayRequests_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `FK_HalfDayRequests_EmployeeDocuments` - foreign key (DocumentId) -> EmployeeDocuments(Id), no action.
- `FK_HalfDayRequests_LeaveRequests` - foreign key (LeaveRequestId) -> LeaveRequests(Id), no action.
- `FK_HalfDayRequests_Users` - foreign key (ApprovedByUserId) -> Users(uid), no action.
- `CK_HalfDayRequests_Approval` - check ([Status]='Cancelled' OR [Status]='Pending' OR [ApprovedByUserId] IS NOT NULL AND [ApprovedAt] IS NOT NULL).
- `CK_HalfDayRequests_HalfType` - check ([HalfType]='SecondHalf' OR [HalfType]='FirstHalf').
- `CK_HalfDayRequests_Status` - check ([Status]='Cancelled' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending').

## Table: dbo.LeaveEntitlementOverrides
Per-person, per-case changes to the standard leave quota. HR rule: everyone has the same quota, but HR may change it for one person in a specific case; each change keeps its reason and approver instead of editing a shared number. Append-only (set C only).
Rows: new table.
- B5: leave year (calendar assumed), pro-rating and carry-forward cap are open.
- 2026-11-24 (B18): the legacy per-year figures were archived, not converted into overrides.
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid. |
| LeaveTypeId | int | NOT NULL |  | FK -> LeaveTypes.Id. |
| Year | int | NOT NULL |  | Leave year (T5: int). |
| AdjustmentDays | decimal(5,1) | NULL |  | + adds days, - removes days. Several adjustments per year allowed (one per case). |
| OverrideTotalDays | decimal(5,1) | NULL |  | Replaces the standard quota entirely for this person/type/year. |
| Reason | nvarchar(500) | NOT NULL |  |  |
| ApprovedByUserId | int | NOT NULL |  | FK -> Users.uid. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| RequestId | uniqueidentifier | NULL |  | S21: no natural key (several adjustments per year are allowed), so this is the idempotency key. A "+5 days" posted twice is rejected instead of giving +10. |

Keys and indexes
- `PK_LeaveEntitlementOverrides` - primary key, clustered, (Id)
- `IX_LeaveEntitlementOverrides_ApprovedByUserId` - nonclustered, (ApprovedByUserId)
- `IX_LeaveEntitlementOverrides_Employee_Year` - nonclustered, (EmployeeId, Year, LeaveTypeId)
- `IX_LeaveEntitlementOverrides_LeaveTypeId` - nonclustered, (LeaveTypeId)
- `UX_LeaveEntitlementOverrides_RequestId` - unique, nonclustered, (RequestId) WHERE ([RequestId] IS NOT NULL)
- `UX_LeaveEntitlementOverrides_Total` - unique, nonclustered, (EmployeeId, LeaveTypeId, Year) WHERE ([OverrideTotalDays] IS NOT NULL)
- `FK_LeaveEntitlementOverrides_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `FK_LeaveEntitlementOverrides_LeaveTypes` - foreign key (LeaveTypeId) -> LeaveTypes(Id), no action.
- `FK_LeaveEntitlementOverrides_Users` - foreign key (ApprovedByUserId) -> Users(uid), no action.
- `CK_LeaveEntitlementOverrides_OneValue` - check ((case when [AdjustmentDays] IS NULL then (0) else (1) end+case when [OverrideTotalDays] IS NULL then (0) else (1) end)=(1)).

## Table: dbo.LeaveQuota
The standard leave policy: days per leave type per leave year, the same for every employee. Per-person changes go to `LeaveEntitlementOverrides`.
- 2026-11-16: Kept under its current name to limit app changes. The 3 live rows are mapped to `LeaveTypes` by `LeaveTypeName`.
- 2026-11-16: Does not apply to employees whose leave is not managed (`COALESCE(Employee.IsLeaveManagedOverride, Projects.HasLeaveManagement) = 0`, e.g. Dastarkhuwans).
- B5: standard days per type and the leave year are open; the 3 rows are the current figures.
Rows on 2026-11-24 (right after the migration): 3.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | Identity. was `UID`, not a key. Becomes the PK (EF already declares it); renamed at contract. |
| TotalDays | decimal(5,1) | NOT NULL |  | was `TotalLeaves` int NOT NULL. Half days; one decimal (script 09). |
| Year | int | NOT NULL |  | was varchar(50) (T5). Live values convert. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |
| LeaveTypeId | int | NOT NULL |  | FK -> LeaveTypes.Id. Added NULL, backfilled from LeaveTypeName, then NOT NULL. |

Keys and indexes
- `PK_LeaveQuota` - primary key, clustered, (Id)
- `UX_LeaveQuota_Type_Year` - unique, nonclustered, (LeaveTypeId, Year)
- `FK_LeaveQuota_LeaveTypes` - foreign key (LeaveTypeId) -> LeaveTypes(Id), no action.
- `CK_LeaveQuota_TotalDays` - check ([TotalDays]>=(0)).

## Table: dbo.LeaveRequests
The single leave model (replaces the `EmployeeLeaves` / `LeaveRequests` split). Request -> approval, linked to employee, leave type and an optional document. Existing `AppliedAt`/`CreatedBy` serve as the creation audit.
- 2026-11-16: The 24 live rows are migrated in place: `LeaveType` text -> `LeaveTypeId`, dates truncated to `date`, `Status` mapped (`Applied` -> `Pending`, `Approved` stays), `TotalDays` calculated. The 1 row pointing at a missing employee must be fixed or archived before the FK is added.
- 2026-11-17: The 17 `Approved` rows have no approver, so no CHECK ties `Approved` to `ApprovedByUserId` here (unlike `HalfDayRequests`); add it once the approver is known or on new rows only.
- 2026-11-17: Live check: no exact duplicate requests, but requests 21 and 22 (employee uid 143) overlap. Review with HR and fix or cancel before overlap enforcement.
- 2026-11-16: A request may not cross a leave-year boundary (split it), so balances per year stay simple (B5).
Rows on 2026-11-24 (right after the migration): 23.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | Type unchanged. Real FK -> Employee.uid (was convention only). |
| FromDate | date | NOT NULL |  | was datetime2(7). Leave is whole days; check no live row has a time part first. |
| ToDate | date | NOT NULL |  | was datetime2(7). |
| Status | varchar(10) | NOT NULL | 'Pending' | was nvarchar(MAX) NULL. `Pending`, `Approved`, `Rejected`, `Cancelled`. |
| LeaveTypeId | int | NOT NULL |  | FK -> LeaveTypes.Id. Added NULL, backfilled from LeaveType, then NOT NULL. |
| IsHalfDay | bit | NOT NULL | 0 | Half-day leave (only for LeaveTypes.IsHalfDayAllowed = 1). B1. |
| TotalDays | decimal(5,1) | NOT NULL |  | Days charged to the balance, after excluding weekly offs and holidays (B5). |
| Reason | nvarchar(500) | NULL |  |  |
| DocumentId | int | NULL |  | FK -> EmployeeDocuments.Id. |
| ApprovedByUserId | int | NULL |  | FK -> Users.uid. |
| ApprovedAt | datetime2(0) | NULL |  |  |
| SupervisorComments | nvarchar(1000) | NULL |  | From EmployeeLeaves.DepSupervisorComments. |
| Notes | nvarchar(1000) | NULL |  | S24, T7: HR's short note (`Reason` is the applicant's). |
| AppliedAt | datetime2(0) | NOT NULL | (sysdatetime() | The 24 existing rows have no original applied date; they get the migration time (noted in the migration). |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |
| RowVersion | rowversion | NOT NULL |  | S15: two approvers cannot silently overwrite each other. |

Keys and indexes
- `PK_LeaveRequests` - primary key, clustered, (Id)
- `IX_LeaveRequests_ApprovedByUserId` - nonclustered, (ApprovedByUserId) WHERE ([ApprovedByUserId] IS NOT NULL)
- `IX_LeaveRequests_DocumentId` - nonclustered, (DocumentId) WHERE ([DocumentId] IS NOT NULL)
- `IX_LeaveRequests_Employee_FromDate` - nonclustered, (EmployeeId, FromDate)
- `IX_LeaveRequests_LeaveTypeId` - nonclustered, (LeaveTypeId)
- `UX_LeaveRequests_Open_Duplicate` - unique, nonclustered, (EmployeeId, FromDate, ToDate, LeaveTypeId) WHERE ([Status] IN ('Pending', 'Approved'))
- `FK_LeaveRequests_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `FK_LeaveRequests_EmployeeDocuments` - foreign key (DocumentId) -> EmployeeDocuments(Id), no action.
- `FK_LeaveRequests_LeaveTypes` - foreign key (LeaveTypeId) -> LeaveTypes(Id), no action.
- `FK_LeaveRequests_Users` - foreign key (ApprovedByUserId) -> Users(uid), no action.
- `CK_LeaveRequests_Dates` - check ([ToDate]>=[FromDate]).
- `CK_LeaveRequests_HalfDay` - check ([IsHalfDay]=(0) OR [FromDate]=[ToDate] AND [TotalDays]=(0.5)).
- `CK_LeaveRequests_SingleYear` - check (datepart(year,[FromDate])=datepart(year,[ToDate])).
- `CK_LeaveRequests_Status` - check ([Status]='Cancelled' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending').

## Table: dbo.LeaveTypes
Leave type lookup, replacing the leave type names spread over `Configuration` (LeaveTypes list), `LeaveQuota.LeaveTypeName`, `LeaveRequests.LeaveType` and `EmployeeLeaves.LeaveTypeName`.
Rows: new table (seeded from those sources, de-duplicated and reviewed; the live quota has Annual, Casual and Sick Leave).
Rows on 2026-11-24 (right after the migration): 5.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| Code | varchar(10) | NOT NULL |  | Short code for reports, e.g. `CL`, `SL`, `AL`. |
| Name | nvarchar(50) | NOT NULL |  |  |
| IsPaid | bit | NOT NULL | 1 | B11 (effect on payroll). |
| IsHalfDayAllowed | bit | NOT NULL | 1 |  |
| IsActive | bit | NOT NULL | 1 |  |
| SortOrder | int | NOT NULL | 0 |  |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |
| AllowsCarryForward | bit | NOT NULL | 1 | 2026-11-24 (script 11): false = the year-end rollover skips this type. Unpaid and Compensation are seeded as 0. HR edits it in Setup -> Lists. Holds the carry-forward eligibility part of B5 in data. |
| MaxCarryForwardDays | decimal(5,1) | NULL |  | 2026-11-24 (script 11): most days the rollover carries into the next year; NULL = no cap (`CK_LeaveTypes_MaxCarryForward`: NULL or >= 0). |

Keys and indexes
- `PK_LeaveTypes` - primary key, clustered, (Id)
- `UX_LeaveTypes_Code` - unique, nonclustered, (Code)
- `UX_LeaveTypes_Name` - unique, nonclustered, (Name)
- `CK_LeaveTypes_MaxCarryForward` - check ([MaxCarryForwardDays] IS NULL OR [MaxCarryForwardDays]>=(0)).

## Table: dbo.PayslipDetails
Line items of a payslip (Allowance / Deduction / Tax). Pure child rows of `Payslips`, so no audit columns (S13).
- 2026-11-17 (T4 pending): regeneration of an unlocked payslip deletes and re-inserts its lines in one transaction; the unique key makes a double insert fail instead of doubling. Live check: 0 duplicate groups among the 132 rows.
Rows on 2026-11-24 (right after the migration): 132.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| PayslipId | int | NOT NULL |  | FK -> Payslips.Id (`FK_PayslipDetails_Payslips`, ON DELETE CASCADE). |
| ItemType | varchar(20) | NOT NULL |  | was nvarchar(20). Code (S11): `Allowance`, `Deduction` or `Tax`, with a CHECK. |
| ItemName | nvarchar(200) | NOT NULL |  |  |
| ItemCategory | nvarchar(50) | NOT NULL |  |  |
| Amount | decimal(18,2) | NOT NULL |  |  |
| SortOrder | int | NULL |  |  |

Keys and indexes
- `PK_PayslipDetails` - primary key, clustered, (Id)
- `UX_PayslipDetails_Payslip_Item` - unique, nonclustered, (PayslipId, ItemType, ItemName)
- `FK_PayslipDetails_Payslips` - foreign key (PayslipId) -> Payslips(Id), cascade.
- `CK_PayslipDetails_ItemType` - check ([ItemType]='Tax' OR [ItemType]='Deduction' OR [ItemType]='Allowance').

## Table: dbo.Payslips
Payslip header: one per employee per month/year, with totals and lock state. Existing `GeneratedDate/GeneratedBy` serve as the audit columns. Table name stays.
- 2026-11-17 (T2): the period is `Year` (int) + `Month` (tinyint 1-12); the text month name is shown by the app. Expand/contract: `MonthNo` is added, backfilled from the text name, the app switches, the text `Month` is dropped, then `MonthNo` is renamed `Month` (collation is case-insensitive, so the old and new `Month` can't coexist).
- 2026-11-17 (T6): payroll finds the tax year from the payslip: `Month >= 7 ? Year : Year - 1`, matched to `TaxRules.TaxYearStart`.
- 2026-11-17 (S18): `TotalAllowances`, `TotalDeductions`, `NetSalary`, `WorkingDays`, `LeaveDays`, `LeaveBalance` and the hourly columns are snapshots of the generated slip (they must not change when attendance or rules change later).
- 2026-11-16: `WorkingDays` and `LeaveDays` are now filled from `Attendance` / `LeaveRequests`; hourly pay is a separate figure so it shows on the slip.
Rows on 2026-11-24 (right after the migration): 99.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid (`FK_Payslips_Employee`, no action). |
| Year | int | NOT NULL |  | Calendar year of the payslip period. |
| BasicSalary | decimal(18,2) | NOT NULL |  |  |
| GrossSalary | decimal(18,2) | NOT NULL |  |  |
| TotalDeductions | decimal(18,2) | NULL |  | Sum of Deductions lines only; excludes income tax (see TaxAmount). Snapshot. |
| TotalAllowances | decimal(18,2) | NULL |  | Added by migration 20260512100000 (not recorded in __EFMigrationsHistory). Snapshot. |
| NetSalary | decimal(18,2) | NOT NULL |  | Gross - TotalDeductions - TaxAmount. Snapshot. |
| WorkingDays | int | NULL |  | Type unchanged; was not populated. Filled from Attendance (exact rule per HR). Snapshot. |
| LeaveDays | decimal(5,2) | NULL |  | Type unchanged; was not populated. Filled from approved LeaveRequests in the month. Snapshot. |
| LeaveBalance | nvarchar(MAX) | NULL |  | Not populated by the app. Could be filled from vLeaveBalances (snapshot). |
| CalculationDetails | nvarchar(MAX) | NOT NULL |  | Snapshot of how the slip was calculated. |
| GeneratedDate | datetime2(0) | NOT NULL | (sysdatetime() | was datetime2(7) with no default. |
| GeneratedBy | nvarchar(100) | NOT NULL |  |  |
| IsLocked | bit | NOT NULL | 0 | Same type; default added (S12). Set to 1 for all of an employee's payslips when their allowances/deductions change. |
| LockedDate | datetime2(0) | NULL |  | was datetime2(7). |
| Notes | nvarchar(1000) | NULL |  |  |
| TaxPercentage | decimal(5,2) | NOT NULL | 0 | was decimal(18,2). Live max is 10.00 (S8). |
| TaxAmount | decimal(18,2) | NOT NULL | 0 |  |
| Month | tinyint | NOT NULL |  | was nvarchar(20) English month name. 1-12. Temporary name `MonthNo` until contract. |
| HourlyMinutes | int | NULL |  | Sum of PayableMinutes on `Hourly` days (B6). |
| HourlyAmount | decimal(18,2) | NULL |  | HourlyMinutes / 60 x hourly rate (B6). |
| RowVersion | rowversion | NOT NULL |  | S15. |

Keys and indexes
- `PK_Payslips` - primary key, clustered, (Id)
- `UX_Payslips_Employee_Year_Month` - unique, nonclustered, (EmployeeId, Year, Month)
- `FK_Payslips_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `CK_Payslips_Month` - check ([Month]>=(1) AND [Month]<=(12)).
- `CK_Payslips_Year` - check ([Year]>=(2000) AND [Year]<=(2100)).

## Table: dbo.Projects
Project lookup: one of the two org dimensions of an employee, next to Department. A dynamic, HR-managed list (decision B3, 2026-11-18): no number of projects and no project name is built into the schema or the app. HR adds, renames, reorders and deactivates projects from Setup.
Rows: new table, seeded from the distinct `Employee.Project` values of the test data for development; HR supplies the real list at go-live.
- 2026-11-18 (B3): `Employee.Project` is free text and the Configuration project list is a comma-separated string, so the two had drifted apart (the earlier "6 or 8 projects" question is dropped: the count is whatever HR keeps in this table). The data in the database is test data and is not a source of truth for the list.
- 2026-11-18 (S6, S14): a project that has employees or history is never hard-deleted; `IsActive = 0` hides it from new-employee dropdowns and keeps old records valid. The FK from `Employee.ProjectId` is `NO ACTION`, so the database refuses to delete a project that is still used. A project nobody uses may be deleted.
- 2026-11-18: `HasLeaveManagement` and `SortOrder` are per project and edited by HR; nothing in code assumes a particular project (the Dastarkhuwans rule is the value `0` on that project's row, not a name check).
Rows on 2026-11-24 (right after the migration): 8.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| Name | nvarchar(100) | NOT NULL |  | e.g. Bahria Town Karachi, Bahria Dastarkhuwans. |
| Code | varchar(20) | NULL |  | Short code for reports. |
| SortOrder | int | NOT NULL | 0 | First sort key of employee lists. |
| HasLeaveManagement | bit | NOT NULL | 1 | 0 for Bahria Dastarkhuwans (no leave management). |
| IsActive | bit | NOT NULL | 1 |  |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_Projects` - primary key, clustered, (Id)
- `UX_Projects_Name` - unique, nonclustered, (Name)

## Table: dbo.RecordNotes
Dated, signed notes HR writes against a record, for the edge cases HR tracks: a forgotten check-out told to HR, a late arrival with its reason, a special arrangement. Append-only: a note is never edited; a wrong one is retracted with `IsDeleted` (S14), kept in the table and hidden from the screen. T7.
Rows: new table.
- 2026-11-17 (S5): there is no generic `TableName`/`RecordId` pair, because that cannot be a foreign key. Each target is its own nullable FK column and exactly one is set per note. The targets drafted are the attendance day, the employee and the leave request; another target later is one more nullable FK column, its index and a term in the CHECK.
- 2026-11-17: Context only. A note never feeds processing or payroll; a change to a day's status or pay is an `AttendanceCorrections` row (or a half-day or leave request) with the note as extra explanation.
- 2026-11-17 (S24): the `Notes` column on each HR table holds the current short note; this table keeps the history with who wrote it and when.
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| AttendanceId | int | NULL |  | FK -> Attendance.Id. Set when the note is about one shift day (the main use). |
| EmployeeId | int | NULL |  | FK -> Employee.uid. Set when the note is about the person, not a day. |
| LeaveRequestId | int | NULL |  | FK -> LeaveRequests.Id. |
| Note | nvarchar(1000) | NOT NULL |  | The text. |
| IsDeleted | bit | NOT NULL | 0 | Retracted note: kept, hidden. Who retracted it is in `AuditLogs`. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() | When the note was written. |
| CreatedBy | nvarchar(100) | NULL |  | Username of the author. |
| RequestId | uniqueidentifier | NULL |  | S21 idempotency key, generated by the form: a double click does not write the same note twice. |

Keys and indexes
- `PK_RecordNotes` - primary key, clustered, (Id)
- `IX_RecordNotes_AttendanceId` - nonclustered, (AttendanceId, CreatedAt) WHERE ([AttendanceId] IS NOT NULL)
- `IX_RecordNotes_EmployeeId` - nonclustered, (EmployeeId, CreatedAt) WHERE ([EmployeeId] IS NOT NULL)
- `IX_RecordNotes_LeaveRequestId` - nonclustered, (LeaveRequestId, CreatedAt) WHERE ([LeaveRequestId] IS NOT NULL)
- `UX_RecordNotes_RequestId` - unique, nonclustered, (RequestId) WHERE ([RequestId] IS NOT NULL)
- `FK_RecordNotes_Attendance` - foreign key (AttendanceId) -> Attendance(Id), no action.
- `FK_RecordNotes_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `FK_RecordNotes_LeaveRequests` - foreign key (LeaveRequestId) -> LeaveRequests(Id), no action.
- `CK_RecordNotes_Note` - check (len(ltrim(rtrim([Note])))>(0)).
- `CK_RecordNotes_OneTarget` - check (((case when [AttendanceId] IS NULL then (0) else (1) end+case when [EmployeeId] IS NULL then (0) else (1) end)+case when [LeaveRequestId] IS NULL then (0) else (1) end)=(1)).

## Table: dbo.RosterOverrides
One-off changes to an employee's schedule on a single date: shift change, extra off day, swap with a colleague, extra duty on a weekly off, without editing the regular assignment. Overrides win over `ShiftAssignments`. B8: the full edge-case list from HR is still open.
Rows: new table.
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid. |
| OverrideDate | date | NOT NULL |  | Shift date affected. |
| OverrideType | varchar(15) | NOT NULL |  | `ShiftChange`, `Off`, `Swap`, `ExtraDuty`. |
| ShiftId | int | NULL |  | FK -> Shifts.Id. Required for ShiftChange, ExtraDuty and Swap; NULL for Off. |
| SwapWithEmployeeId | int | NULL |  | FK -> Employee.uid. Colleague for a Swap (each side gets a row). |
| Reason | nvarchar(500) | NULL |  |  |
| Notes | nvarchar(1000) | NULL |  | S24, T7: HR's short note. |
| ApprovedByUserId | int | NULL |  | FK -> Users.uid. |
| ApprovedAt | datetime2(0) | NULL |  |  |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_RosterOverrides` - primary key, clustered, (Id)
- `IX_RosterOverrides_ApprovedByUserId` - nonclustered, (ApprovedByUserId) WHERE ([ApprovedByUserId] IS NOT NULL)
- `IX_RosterOverrides_ShiftId` - nonclustered, (ShiftId) WHERE ([ShiftId] IS NOT NULL)
- `IX_RosterOverrides_SwapWithEmployeeId` - nonclustered, (SwapWithEmployeeId) WHERE ([SwapWithEmployeeId] IS NOT NULL)
- `UX_RosterOverrides_Employee_Date` - unique, nonclustered, (EmployeeId, OverrideDate)
- `FK_RosterOverrides_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `FK_RosterOverrides_Employee_Swap` - foreign key (SwapWithEmployeeId) -> Employee(uid), no action.
- `FK_RosterOverrides_Shifts` - foreign key (ShiftId) -> Shifts(Id), no action.
- `FK_RosterOverrides_Users` - foreign key (ApprovedByUserId) -> Users(uid), no action.
- `CK_RosterOverrides_Shift` - check ([OverrideType]='Off' AND [ShiftId] IS NULL OR [OverrideType]<>'Off' AND [ShiftId] IS NOT NULL).
- `CK_RosterOverrides_Swap` - check ([OverrideType]<>'Swap' OR [SwapWithEmployeeId] IS NOT NULL).
- `CK_RosterOverrides_Type` - check ([OverrideType]='ExtraDuty' OR [OverrideType]='Swap' OR [OverrideType]='Off' OR [OverrideType]='ShiftChange').

## Table: dbo.ShiftAssignments
Which shift an employee works, on which weekdays, over which date range. Attendance follows the schedule; weekly offs differ per person. B8.
Rows: new table.
- 2026-11-16: Day bitmask Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64 (Mon-Fri = 31, Mon-Sat = 63). Day bit for a date `d` = `POWER(2, DATEDIFF(day, '19000101', d) % 7)` (1900-01-01 was a Monday; independent of `SET DATEFIRST`).
- 2026-11-16: When two assignments cover the same date, the higher `Priority` wins (e.g. a Ramadan timing over the regular shift). Same start date and same priority is blocked by the unique key below; other overlaps at the same priority are blocked in the save transaction under `sp_getapplock` (S22).
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| EmployeeId | int | NOT NULL |  | FK -> Employee.uid. |
| ShiftId | int | NOT NULL |  | FK -> Shifts.Id. |
| EffectiveFromDate | date | NOT NULL |  |  |
| EffectiveToDate | date | NULL |  | NULL = open-ended. |
| DaysOfWeekMask | tinyint | NOT NULL |  | Days this shift is worked. |
| WeeklyOffMask | tinyint | NOT NULL | 0 | Rest days (become `WeeklyOff` in Attendance). |
| Priority | tinyint | NOT NULL | 0 | Higher wins on overlap. |
| Notes | nvarchar(1000) | NULL |  | S24, T7: HR's short note (e.g. why a Ramadan timing applies). |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_ShiftAssignments` - primary key, clustered, (Id)
- `IX_ShiftAssignments_Employee_From` - nonclustered, (EmployeeId, EffectiveFromDate)
- `IX_ShiftAssignments_ShiftId` - nonclustered, (ShiftId)
- `UX_ShiftAssignments_Employee_From_Priority` - unique, nonclustered, (EmployeeId, EffectiveFromDate, Priority)
- `FK_ShiftAssignments_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.
- `FK_ShiftAssignments_Shifts` - foreign key (ShiftId) -> Shifts(Id), no action.
- `CK_ShiftAssignments_Masks` - check ([DaysOfWeekMask]>=(1) AND [DaysOfWeekMask]<=(127) AND ([WeeklyOffMask]>=(0) AND [WeeklyOffMask]<=(127)) AND ([DaysOfWeekMask]&[WeeklyOffMask])=(0)).
- `CK_ShiftAssignments_Range` - check ([EffectiveToDate] IS NULL OR [EffectiveToDate]>=[EffectiveFromDate]).

## Table: dbo.Shifts
Shift definitions and the attendance rules that go with them. HR rules live here as data, not code - 7 hours (420 minutes) for a full day, 1 minute late/early grace, half-day cutoff around 12-1pm - so a shift with different rules needs no code change (B6, B7, B8 for the exact values).
Rows: new table.
Rows on 2026-11-24 (right after the migration): 0.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| Name | nvarchar(100) | NOT NULL |  | e.g. Morning 09:00-17:00, Night 22:00-06:00. |
| StartTime | time(0) | NOT NULL |  |  |
| EndTime | time(0) | NOT NULL |  |  |
| IsOvernight | bit | NOT NULL |  | computed: (CONVERT([bit],case when [EndTime]<=[StartTime] then (1) else (0) end)). Computed, persisted: `CASE WHEN EndTime <= StartTime THEN 1 ELSE 0 END`. Overnight shifts check out on the next calendar day (S18, S10 naming). |
| BreakMinutes | smallint | NOT NULL | 0 | Unpaid break subtracted from worked time (confirm with HR whether breaks are unpaid). |
| MinFullDayMinutes | smallint | NOT NULL | 420 | 7 hours; below this the day is Hourly. |
| GraceLateMinutes | tinyint | NOT NULL | 1 | Official policy: 1 minute. |
| GraceEarlyMinutes | tinyint | NOT NULL | 1 | Official policy: 1 minute. |
| HalfDayMinMinutes | smallint | NULL |  | Minimum worked minutes for a half day (B7, e.g. 240). |
| HalfDayCutoffTime | time(0) | NULL |  | Earliest leave time for a second-half half day (HR: "till at least 12-1pm", B7). |
| IsActive | bit | NOT NULL | 1 |  |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_Shifts` - primary key, clustered, (Id)
- `UX_Shifts_Name` - unique, nonclustered, (Name)
- `CK_Shifts_Minutes` - check ([MinFullDayMinutes]>(0) AND ([HalfDayMinMinutes] IS NULL OR [HalfDayMinMinutes]<[MinFullDayMinutes])).
- `CK_Shifts_Times` - check ([StartTime]<>[EndTime]).

## Table: dbo.SubDepartments
Optional detail under a department: the function inside it (Mess, Transportation and Sweeper under Administration; Record, Transfer, Dispatch and others under Operations). 2026-11-18 (decision B2, project owner): information and detail only. No attendance, payroll, leave or approval rule reads it, and a department with no sub-departments simply has none. Each sub-department belongs to exactly one department.
Rows: new table, seeded by splitting the test-data department names at the first ` - `: 10 sub-departments under 2 departments (Administration: Mess, Sweeper, Transportation; Operations: Audit & Verification, Data Center Q.C, Dispatch, Lahore / Rawalpindi, Nawabshah, Record, Transfer).
- 2026-11-18 (S11): a table and not free text on `Employee`, because free text drifts (the test data already has `Mess` and `Admin - Mess` for the same function). The simpler alternative is a free-text `SubDepartment nvarchar(100)` column on `Employee`.
Rows on 2026-11-24 (right after the migration): 10.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| DepartmentId | int | NOT NULL |  | FK -> Departments.Id. |
| Name | nvarchar(100) | NOT NULL |  | The function, without the department prefix (`Mess`, not `Admin - Mess`). |
| SortOrder | int | NOT NULL | 0 | Order inside the department. |
| IsActive | bit | NOT NULL | 1 | S14. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_SubDepartments` - primary key, clustered, (Id)
- `UX_SubDepartments_Department_Id` - unique, nonclustered, (DepartmentId, Id)
- `UX_SubDepartments_Department_Name` - unique, nonclustered, (DepartmentId, Name)
- `FK_SubDepartments_Departments` - foreign key (DepartmentId) -> Departments(Id), no action.

## Table: dbo.TaxRules
Income-tax slabs per tax year, on **annual** income (2026-11-25): annual tax = `FixedAmount` + `TaxPercentage` % of (annual income - `MinSalary`) for the slab with the highest `MinSalary` not above the income; monthly tax = annual tax / 12, rounded to the rupee. Method, source and slab figures: `docs/reference/pk-income-tax.md`. The taxable base (basic or gross) is still B17.
- 2026-11-25: **`MinSalary` and `MaxSalary` now hold ANNUAL income** (they were monthly salary bands with one flat rate on the whole basic, which was wrong). New column `FixedAmount`. Script `12_tax_slabs_annual.sql` adds it and replaces the old test slabs of tax years 2025 and 2026 with the Pakistan salaried slabs (2026-27 as published, 2025-26 with fixed amounts derived by continuity). **Script 12 was run on production on 2026-11-25** (production now has `FixedAmount`), so the table below is the live state; the 2025 and 2026 slabs are the annual ones.
- 2026-11-17 (T6): the text label `TaxYear` (`2025-2026`) is replaced by `TaxYearStart smallint` (2025 = 1 Jul 2025 to 30 Jun 2026); the label is derived in the app. This fixes the tax-year bug at its cause: payroll derives the tax year from the payslip (`Month >= 7 ? Year : Year - 1`) instead of comparing the label with `2026`. Expand/contract: add `TaxYearStart`, backfill `LEFT(TaxYear, 4)`, switch the app, drop `TaxYear`. `Configuration` rows `CurrentTaxYear` and `TaxSlabs` are removed afterwards.
- 2026-11-17: Live check: no duplicate `(TaxYear, MinSalary)`; max percentage is 20.00, so `decimal(5,2)` is safe.
Rows on 2026-11-24 (right after the migration): 14.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| Id | int | NOT NULL |  | PK, identity. |
| MinSalary | decimal(18,2) | NOT NULL |  | 2026-11-25: annual income where the slab starts, and the "amount over" threshold for the rate. Column name kept. |
| MaxSalary | decimal(18,2) | NULL |  | Annual income where the slab ends; NULL or 0 = no upper limit. Informational (the slab is chosen by `MinSalary`). |
| FixedAmount | decimal(18,2) | NOT NULL | 0 | 2026-11-25 (script 12, pending on production): tax in rupees already due at the start of the slab. `CK_TaxRules_Fixed` >= 0. |
| TaxPercentage | decimal(5,2) | NOT NULL |  | was decimal(18,2). S8. |
| TaxYearStart | smallint | NOT NULL |  | T6. Calendar year in which the July-June tax year starts. |
| CreatedAt | datetime2(0) | NOT NULL | (sysdatetime() |  |
| CreatedBy | nvarchar(100) | NULL |  |  |
| ModifiedAt | datetime2(0) | NULL |  |  |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_TaxRules` - primary key, clustered, (Id)
- `UX_TaxRules_Year_MinSalary` - unique, nonclustered, (TaxYearStart, MinSalary)
- `CK_TaxRules_Percentage` - check ([TaxPercentage]>=(0) AND [TaxPercentage]<=(100)).
- `CK_TaxRules_Year` - check ([TaxYearStart]>=(2000) AND [TaxYearStart]<=(2100)).
- `CK_TaxRules_Fixed` - check ([FixedAmount]>=(0)) (script 12, pending on production).

## Table: dbo.UserRoles
The roles each login holds. A user can have any number of rows (Admin, HR, or both); no rows = no role. Added 2026-11-23 so certain HR users can also have Admin privileges.
- 2026-11-23: A composite key, not an `Id` column: the row has no identity of its own and is never referenced (S1 exception, like a pure link table).
- 2026-11-23: The role list lives in the CHECK for now (B9, B13). Adding a role such as Supervisor means changing `CK_UserRoles_Role` and `AppRoles` in the app.
Rows on 2026-11-24 (right after the migration): 5.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| UserId | int | NOT NULL |  | FK -> Users.uid, ON DELETE CASCADE (logins are normally disabled, never deleted, S6). |
| Role | nvarchar(30) | NOT NULL |  | `Admin` or `HR`. |

Keys and indexes
- `PK_UserRoles` - primary key, clustered, (UserId, Role)
- `FK_UserRoles_Users` - foreign key (UserId) -> Users(uid), cascade.
- `CK_UserRoles_Role` - check ([Role]='HR' OR [Role]='Admin').

## Table: dbo.Users
Application logins (cookie auth). Existing `CreatedBy/CreatedOn` serve as the creation audit.
- 2026-11-17 (S6, S14): logins are never deleted; `IsActive = 0` disables one, and login must reject inactive users.
- 2026-11-16: Linked to employees by `uid` like everything else. Roles must be constrained values so they can be enforced for authorization (approvals for leave, half days, corrections; B13, B9 for the list of roles).
- 2026-11-23: One login can hold several roles (for example an HR user with Admin privileges), so the single `Role` column is replaced by the child table `UserRoles` (script `07_user_roles.sql`). The app issues one role claim per row, so `[Authorize(Roles = "Admin")]` passes for anyone who has the Admin row. A login with no rows has no role.
- 2026-11-17: Expand/contract: the new int key is added as `EmployeeUid`, backfilled by `EmployeeID` match, the app switches, the string `EmployeeId` is dropped and `EmployeeUid` is renamed `EmployeeId`.
Rows on 2026-11-24 (right after the migration): 6.

| Column | Type | Null | Default | Notes |
|---|---|---|---|---|
| uid | int | NOT NULL |  | PK, identity. |
| Username | nvarchar(100) | NOT NULL |  | Now unique. |
| PasswordHash | nvarchar(255) | NOT NULL |  | PBKDF2 `iterations.salt.hash`. Login code should stop accepting legacy SHA-256/plain text (app change). |
| CreatedBy | nvarchar(100) | NULL |  | was varchar(MAX) NULL. |
| CreatedOn | datetime2(0) | NULL |  | was varchar(MAX) NULL (text). Converted with TRY_CONVERT. Existing name kept. |
| History | varchar(MAX) | NULL |  |  |
| EmployeeId | int | NULL |  | was varchar(50) NULL: Employee.EmployeeID (string), no FK; 1 row points at a missing employee. Now FK -> Employee.uid, optional (admin accounts may not be employees). The orphan stays NULL. Temporary name `EmployeeUid`. |
| IsActive | bit | NOT NULL | 1 | S6/S14: soft delete. |
| ModifiedAt | datetime2(0) | NULL |  | Set M. |
| ModifiedBy | nvarchar(100) | NULL |  |  |

Keys and indexes
- `PK_Users` - primary key, clustered, (uid)
- `UX_Users_EmployeeId` - unique, nonclustered, (EmployeeId) WHERE ([EmployeeId] IS NOT NULL)
- `UX_Users_Username` - unique, nonclustered, (Username)
- `FK_Users_Employee` - foreign key (EmployeeId) -> Employee(uid), no action.

## Views
- `dbo.vEmployeeDirectory` - Employee joined to Projects, Departments, Designations. Columns: uid, EmployeeID, EmployeeName, ProjectName, DepartmentName, SubDepartmentName, DesignationName, EmployeeStatus, plus the sort keys `Projects.SortOrder`, `Departments.SortOrder`, `Designations.Rank`. Lists use either `ORDER BY p.SortOrder, d.SortOrder, g.Rank, e.EmployeeName` or, for department-first (HR's "all Admin staff, then by project"), `ORDER BY d.SortOrder, p.SortOrder, g.Rank, e.EmployeeName` (a view can't carry ORDER BY). Backed by `IX_Employee_Org` and `IX_Employee_Department_Project`.
- `dbo.vLeaveBalances` (v2, script 11, 2026-11-24) - per employee, leave type and year. The rows are the union of every year that has any leave data for that employee and type: a `LeaveQuota` row, a `CarryforwardLeaves.ToYear`, a `LeaveEntitlementOverrides.Year`, or a Pending/Approved `LeaveRequests` year (so a year with leave taken but no quota still shows, with `BaseDays` 0 and a negative balance; history is never lost because a quota row is missing). Columns: `BaseDays = COALESCE(OverrideTotalDays, LeaveQuota.TotalDays, 0)`, `Adjustment = SUM(AdjustmentDays)`, `CarryForward = SUM(CarryForwardDays)` where `ToYear` = year, `Entitled = BaseDays + Adjustment + CarryForward`, `Taken = SUM(TotalDays)` of `Approved` requests whose `FromDate` is in the year, `Pending` likewise, `Remaining = Entitled - Taken`. Only employees with `COALESCE(e.IsLeaveManagedOverride, p.HasLeaveManagement) = 1`. Derived, never stored (S18). Read by one service (`LeaveBalanceService`) for both the employee Leave Balance tab and the LMS Balances page. The year-end rollover (LMS -> Leave Balance -> Year-end rollover) writes next year's `CarryforwardLeaves` rows from `Remaining` (never below zero, capped by `LeaveTypes.MaxCarryForwardDays`, skipping types with `AllowsCarryForward = 0`), marks them `Description = 'Year-end rollover'`, updates only such rows on a re-run, never touches hand-entered rows, and writes one `AuditLogs` row.

`dbo.vEmployeeDirectory` columns (live): uid, EmployeeID, EmployeeName, ProjectName, DepartmentName, SubDepartmentName, DesignationName, EmployeeStatus, ProjectSortOrder, DepartmentSortOrder, DesignationRank.
`dbo.vLeaveBalances` columns (live): EmployeeId, LeaveTypeId, Year, BaseDays, Adjustment, CarryForward, Entitled, Taken, Pending, Remaining.

## Known issues and open items
- 2026-11-24: `mig.EmployeeOrgMap` (the automatic split of the old `Employee.Department` text into department and sub-department) and `mig.LeaveRequests_Orphans` (1 leave request that pointed at a missing employee, removed from `LeaveRequests`) are kept in the `mig` schema until HR has signed off; then drop those tables and `DROP SCHEMA mig`. `dbo.EmployeeLegacyLeaveBalances` keeps the seven old spreadsheet leave figures read-only (828 rows).
- 2026-11-24: Employee History: the backfill wrote each employee's **current** project, department and designation into the `Joined` row because the original position was never recorded.
- 2026-11-24: Leave Balance needs `LeaveQuota` rows for the current year (3 exist for 2026: Annual 10, Casual 10, Sick 15). `Unpaid Leave` is marked unpaid; payroll does not apply the paid/unpaid flag yet.
- 2026-11-24: Overlapping leave requests are not blocked by the database (only exact duplicates are, plus the app's overlap check). Employees 325 and 376 still have two active `Fuel Allowance` rows each (no unique key on active name; payroll decides).
- 2026-11-24: `__EFMigrationsHistory` now also holds the ten redesign entries (`2026112...`). The six older hand-applied migrations and the recorded-but-missing `20260110114250_LMSModule` are still not reconciled, so do not run `dotnet ef database update` without reading the migration notes in `docs/db/db_changes.md`.
- 2026-11-24: Connection strings are not committed. The old `sa` password is still in git history (rotation is deferred to the owner).
- 2026-11-25: Income tax is now the annual progressive slab method (`TaxRules.FixedAmount`, annual `MinSalary`/`MaxSalary`; script 12). Script 12 has been run on production (2026-11-25), so the new app build can be deployed (it reads `FixedAmount`). Tax years other than 2025 and 2026 keep old monthly bands and must be re-entered by HR.

## Change History
- 2026-09-23: Live schema exported (former `db.txt`, v1.4) with `scripts/database/tools/export-db-schema.ps1`; database `HRBTK`.
- 2026-11-16: `db.txt` converted to this Markdown file (v1.5) and verified against the live database (v1.6-v1.9: server identified, connection strings moved out of `appsettings.json`). No schema change.
- 2026-11-23: Added the migration-pending note (v1.10). No schema change.
- 2026-11-24: **Redesign applied to production (v2.0).** Full backup first (`HRBTK_pre_redesign_20261124.bak`, verified with `RESTORE VERIFYONLY`, plus `HRBTK_after04_20261124.bak` between 04 and 06). Scripts run in order with `sqlcmd -b -I`: 01 to 04 (migrations `20261123000001` to `20261123000004`), 06 contract, 07 user roles, 08 employee events, 09 leave days one decimal, 10 legacy leave archive, 11 leave balances view v2 (10 new rows in `__EFMigrationsHistory`). Result: 19 tables became 33 (plus 2 migration tables in `mig`); 848 employees, 99 payslips (132 lines), 23 allowances, 11 deductions and 8 documents unchanged; 1 orphan leave request moved to `mig.LeaveRequests_Orphans` (23 remain); 8 projects, 48 departments, 10 sub-departments and 209 designations seeded from the data; 5 `UserRoles` rows; `EmployeeLeaves` dropped; 7 legacy leave columns archived (828 rows) then dropped from `Employee`. Verified: no untrusted foreign keys, leave day columns at one decimal, every dated employee has a `Joined` event; scripts 01 and 06-11 re-ran as no-ops. The app (Development build, production database) loaded all main pages.
- 2026-11-25: **Tax slabs on the annual method (v2.1).** `TaxRules.FixedAmount` added; `MinSalary`/`MaxSalary` are annual income; slabs of tax years 2025 and 2026 replaced by the salaried slabs (script `12_tax_slabs_annual.sql`, migration `20261125000012_Redesign_Phase12_TaxSlabsAnnual`). Run on the dev copy, then on production the same day. Source and method: `docs/reference/pk-income-tax.md`.
