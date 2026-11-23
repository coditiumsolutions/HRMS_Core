/*
  HRMS database redesign - phase 6: contract (drop legacy columns, rename temporary ones, make keys NOT NULL).
  Source: docs/db/db_changes.md v3.4-draft ("Migration order" step 6). Needs 01-04 first, and the app must already
  read the new columns (step 5). Re-runnable. Run with sqlcmd -b -I (see 01_lookups_and_keys.sql). Back up first (S19).
  Scripts 01-04 are the expand phases and must run before this one; they are not meant to be re-run after it
  (they read the legacy columns this script drops).
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_WARNINGS ON;
SET ANSI_PADDING ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

CREATE PROCEDURE #DropDefault @tbl nvarchar(256), @col sysname AS
BEGIN
  DECLARE @dc sysname = (SELECT dc.name FROM sys.default_constraints dc
                         JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                         WHERE dc.parent_object_id = OBJECT_ID(@tbl) AND c.name = @col);
  DECLARE @sql nvarchar(600) = N'ALTER TABLE ' + @tbl + N' DROP CONSTRAINT ' + QUOTENAME(@dc);
  IF @dc IS NOT NULL EXEC (@sql);
END
GO

/* ---- 1. drop the legacy columns (each guarded by "old column still exists AND replacement exists") ---- */
IF COL_LENGTH('dbo.Employee','Department') IS NOT NULL AND COL_LENGTH('dbo.Employee','DepartmentId') IS NOT NULL
BEGIN
  EXEC sp_executesql N'IF EXISTS (SELECT 1 FROM dbo.Employee WHERE NULLIF(LTRIM(RTRIM(Department)),'''') IS NOT NULL AND DepartmentId IS NULL) THROW 50200, ''Employee.Department has unmapped values.'', 1;';
  ALTER TABLE dbo.Employee DROP COLUMN Department;
END
IF COL_LENGTH('dbo.Employee','Designation') IS NOT NULL AND COL_LENGTH('dbo.Employee','DesignationId') IS NOT NULL
BEGIN
  EXEC sp_executesql N'IF EXISTS (SELECT 1 FROM dbo.Employee WHERE NULLIF(LTRIM(RTRIM(Designation)),'''') IS NOT NULL AND DesignationId IS NULL) THROW 50201, ''Employee.Designation has unmapped values.'', 1;';
  ALTER TABLE dbo.Employee DROP COLUMN Designation;
END
IF COL_LENGTH('dbo.Employee','Project') IS NOT NULL AND COL_LENGTH('dbo.Employee','ProjectId') IS NOT NULL
BEGIN
  EXEC sp_executesql N'IF EXISTS (SELECT 1 FROM dbo.Employee WHERE NULLIF(LTRIM(RTRIM(Project)),'''') IS NOT NULL AND ProjectId IS NULL) THROW 50202, ''Employee.Project has unmapped values.'', 1;';
  ALTER TABLE dbo.Employee DROP COLUMN Project;
END
GO

-- Attendance: old string key, copies of employee data, old date/time/status
IF COL_LENGTH('dbo.Attendance','EmployeeID') IS NOT NULL AND COL_LENGTH('dbo.Attendance','EmployeeUid') IS NOT NULL
BEGIN
  EXEC sp_executesql N'IF EXISTS (SELECT 1 FROM dbo.Attendance WHERE EmployeeUid IS NULL OR ShiftDate IS NULL) THROW 50203, ''Attendance has rows not converted to the new keys.'', 1;';
  DROP INDEX IF EXISTS IX_Attendance_EmployeeID_Date ON dbo.Attendance;
  ALTER TABLE dbo.Attendance DROP COLUMN EmployeeID, EmployeeName, DepartmentName, AttendanceDate, TimeIn, TimeOut, Status;
END
GO

IF COL_LENGTH('dbo.LeaveQuota','LeaveTypeName') IS NOT NULL AND COL_LENGTH('dbo.LeaveQuota','LeaveTypeId') IS NOT NULL
  ALTER TABLE dbo.LeaveQuota DROP COLUMN LeaveTypeName;
IF COL_LENGTH('dbo.LeaveRequests','LeaveType') IS NOT NULL AND COL_LENGTH('dbo.LeaveRequests','LeaveTypeId') IS NOT NULL
  ALTER TABLE dbo.LeaveRequests DROP COLUMN LeaveType;
IF COL_LENGTH('dbo.Users','EmployeeUid') IS NOT NULL AND COL_LENGTH('dbo.Users','EmployeeId') IS NOT NULL
  ALTER TABLE dbo.Users DROP COLUMN EmployeeId;
IF COL_LENGTH('dbo.Payslips','MonthNo') IS NOT NULL AND COL_LENGTH('dbo.Payslips','Month') IS NOT NULL
  ALTER TABLE dbo.Payslips DROP COLUMN [Month];
IF COL_LENGTH('dbo.TaxRules','TaxYearStart') IS NOT NULL AND COL_LENGTH('dbo.TaxRules','TaxYear') IS NOT NULL
BEGIN
  EXEC #DropDefault 'dbo.TaxRules', 'TaxYear';
  ALTER TABLE dbo.TaxRules DROP COLUMN TaxYear;
END
IF OBJECT_ID('dbo.EmployeeLeaves','U') IS NOT NULL
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.EmployeeLeaves) THROW 50204, 'EmployeeLeaves is not empty; drop refused.', 1;
  DROP TABLE dbo.EmployeeLeaves;
END
GO

/* ---- 2. rename the temporary and descriptive columns ----
   sp_rename refuses columns used by filtered indexes, so drop those indexes first (recreated in step 3). */
IF COL_LENGTH('dbo.Users','EmployeeUid') IS NOT NULL DROP INDEX IF EXISTS UX_Users_EmployeeId ON dbo.Users;
IF COL_LENGTH('dbo.Attendance','EmployeeUid') IS NOT NULL
BEGIN
  DROP INDEX IF EXISTS UX_Attendance_Employee_ShiftDate ON dbo.Attendance;
  DROP INDEX IF EXISTS IX_Attendance_ShiftDate ON dbo.Attendance;
END
GO
IF COL_LENGTH('dbo.Users','EmployeeUid')       IS NOT NULL AND COL_LENGTH('dbo.Users','EmployeeId') IS NULL       EXEC sp_rename N'dbo.Users.EmployeeUid', N'EmployeeId', N'COLUMN';
IF COL_LENGTH('dbo.Attendance','EmployeeUid')  IS NOT NULL AND COL_LENGTH('dbo.Attendance','EmployeeId') IS NULL  EXEC sp_rename N'dbo.Attendance.EmployeeUid', N'EmployeeId', N'COLUMN';
IF COL_LENGTH('dbo.Payslips','MonthNo')        IS NOT NULL AND COL_LENGTH('dbo.Payslips','Month') IS NULL
BEGIN
  ALTER TABLE dbo.Payslips DROP CONSTRAINT CK_Payslips_Month;      -- a column used by a CHECK can't be renamed; recreated below
  EXEC sp_rename N'dbo.Payslips.MonthNo', N'Month', N'COLUMN';
  EXEC (N'ALTER TABLE dbo.Payslips ADD CONSTRAINT CK_Payslips_Month CHECK ([Month] BETWEEN 1 AND 12)');   -- dynamic: the column is new in this batch
END
IF COL_LENGTH('dbo.Attendance','Comments')     IS NOT NULL AND COL_LENGTH('dbo.Attendance','Notes') IS NULL      EXEC sp_rename N'dbo.Attendance.Comments', N'Notes', N'COLUMN';
IF COL_LENGTH('dbo.Allowances','Remarks')      IS NOT NULL AND COL_LENGTH('dbo.Allowances','Notes') IS NULL      EXEC sp_rename N'dbo.Allowances.Remarks', N'Notes', N'COLUMN';
IF COL_LENGTH('dbo.Attendance','AttendanceID') IS NOT NULL AND COL_LENGTH('dbo.Attendance','Id') IS NULL         EXEC sp_rename N'dbo.Attendance.AttendanceID', N'Id', N'COLUMN';
IF COL_LENGTH('dbo.Departments','DepartmentID') IS NOT NULL AND COL_LENGTH('dbo.Departments','Id') IS NULL       EXEC sp_rename N'dbo.Departments.DepartmentID', N'Id', N'COLUMN';
IF COL_LENGTH('dbo.AuditLogs','LogId')         IS NOT NULL AND COL_LENGTH('dbo.AuditLogs','Id') IS NULL          EXEC sp_rename N'dbo.AuditLogs.LogId', N'Id', N'COLUMN';
IF COL_LENGTH('dbo.Configuration','UID')       IS NOT NULL AND COL_LENGTH('dbo.Configuration','Id') IS NULL      EXEC sp_rename N'dbo.Configuration.UID', N'Id', N'COLUMN';
IF COL_LENGTH('dbo.LeaveQuota','UID')          IS NOT NULL AND COL_LENGTH('dbo.LeaveQuota','Id') IS NULL         EXEC sp_rename N'dbo.LeaveQuota.UID', N'Id', N'COLUMN';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Payslips_Employee_Year_MonthNo' AND object_id=OBJECT_ID('dbo.Payslips'))
  EXEC sp_rename N'dbo.Payslips.UX_Payslips_Employee_Year_MonthNo', N'UX_Payslips_Employee_Year_Month', N'INDEX';
GO

/* ---- 3. NOT NULL (ALTER COLUMN rejects filtered indexes, so drop them around the change and recreate) ---- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Users_EmployeeId' AND object_id=OBJECT_ID('dbo.Users'))
  CREATE UNIQUE INDEX UX_Users_EmployeeId ON dbo.Users (EmployeeId) WHERE EmployeeId IS NOT NULL;
GO
-- Attendance
IF COLUMNPROPERTY(OBJECT_ID('dbo.Attendance'),'EmployeeId','AllowsNull') = 1 OR COLUMNPROPERTY(OBJECT_ID('dbo.Attendance'),'ShiftDate','AllowsNull') = 1
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Attendance WHERE EmployeeId IS NULL OR ShiftDate IS NULL) THROW 50210, 'Attendance has NULL EmployeeId/ShiftDate.', 1;
  DROP INDEX IF EXISTS UX_Attendance_Employee_ShiftDate ON dbo.Attendance;
  DROP INDEX IF EXISTS IX_Attendance_ShiftDate ON dbo.Attendance;
  DROP INDEX IF EXISTS IX_Attendance_ShiftId ON dbo.Attendance;
  DROP INDEX IF EXISTS IX_Attendance_HalfDayRequestId ON dbo.Attendance;
  DROP INDEX IF EXISTS IX_Attendance_LeaveRequestId ON dbo.Attendance;
  ALTER TABLE dbo.Attendance DROP CONSTRAINT FK_Attendance_Employee;
  ALTER TABLE dbo.Attendance DROP COLUMN IsLate;
END
GO
IF COLUMNPROPERTY(OBJECT_ID('dbo.Attendance'),'EmployeeId','AllowsNull') = 1
  ALTER TABLE dbo.Attendance ALTER COLUMN EmployeeId int NOT NULL;
IF COLUMNPROPERTY(OBJECT_ID('dbo.Attendance'),'ShiftDate','AllowsNull') = 1
  ALTER TABLE dbo.Attendance ALTER COLUMN ShiftDate date NOT NULL;
GO
IF COL_LENGTH('dbo.Attendance','IsLate') IS NULL
  ALTER TABLE dbo.Attendance ADD IsLate AS (CONVERT(bit, CASE WHEN LateMinutes > 0 THEN 1 ELSE 0 END)) PERSISTED NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Attendance_Employee')
  ALTER TABLE dbo.Attendance ADD CONSTRAINT FK_Attendance_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (uid);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Attendance_Employee_ShiftDate' AND object_id=OBJECT_ID('dbo.Attendance'))
  CREATE UNIQUE INDEX UX_Attendance_Employee_ShiftDate ON dbo.Attendance (EmployeeId, ShiftDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Attendance_ShiftDate' AND object_id=OBJECT_ID('dbo.Attendance'))
  CREATE INDEX IX_Attendance_ShiftDate ON dbo.Attendance (ShiftDate) INCLUDE (EmployeeId, DayStatus, PayableMinutes);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Attendance_ShiftId' AND object_id=OBJECT_ID('dbo.Attendance'))
  CREATE INDEX IX_Attendance_ShiftId ON dbo.Attendance (ShiftId) WHERE ShiftId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Attendance_HalfDayRequestId' AND object_id=OBJECT_ID('dbo.Attendance'))
  CREATE INDEX IX_Attendance_HalfDayRequestId ON dbo.Attendance (HalfDayRequestId) WHERE HalfDayRequestId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Attendance_LeaveRequestId' AND object_id=OBJECT_ID('dbo.Attendance'))
  CREATE INDEX IX_Attendance_LeaveRequestId ON dbo.Attendance (LeaveRequestId) WHERE LeaveRequestId IS NOT NULL;
GO

-- Employee.ProjectId and DepartmentId
IF COLUMNPROPERTY(OBJECT_ID('dbo.Employee'),'ProjectId','AllowsNull') = 1 OR COLUMNPROPERTY(OBJECT_ID('dbo.Employee'),'DepartmentId','AllowsNull') = 1
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Employee WHERE ProjectId IS NULL OR DepartmentId IS NULL) THROW 50211, 'Some employees have no project or department; map them first.', 1;
  DROP INDEX IF EXISTS IX_Employee_Department_SubDepartment ON dbo.Employee;
  DROP INDEX IF EXISTS IX_Employee_DesignationId ON dbo.Employee;
  DROP INDEX IF EXISTS IX_Employee_Org ON dbo.Employee;
  DROP INDEX IF EXISTS IX_Employee_Department_Project ON dbo.Employee;
  ALTER TABLE dbo.Employee DROP CONSTRAINT FK_Employee_SubDepartments;
  ALTER TABLE dbo.Employee DROP CONSTRAINT FK_Employee_Departments;
  ALTER TABLE dbo.Employee DROP CONSTRAINT FK_Employee_Projects;
END
GO
IF COLUMNPROPERTY(OBJECT_ID('dbo.Employee'),'ProjectId','AllowsNull') = 1
  ALTER TABLE dbo.Employee ALTER COLUMN ProjectId int NOT NULL;
IF COLUMNPROPERTY(OBJECT_ID('dbo.Employee'),'DepartmentId','AllowsNull') = 1
  ALTER TABLE dbo.Employee ALTER COLUMN DepartmentId int NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Employee_Projects')
  ALTER TABLE dbo.Employee ADD CONSTRAINT FK_Employee_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Employee_Departments')
  ALTER TABLE dbo.Employee ADD CONSTRAINT FK_Employee_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Employee_SubDepartments')
  ALTER TABLE dbo.Employee ADD CONSTRAINT FK_Employee_SubDepartments FOREIGN KEY (DepartmentId, SubDepartmentId) REFERENCES dbo.SubDepartments (DepartmentId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Employee_Org' AND object_id=OBJECT_ID('dbo.Employee'))
  CREATE INDEX IX_Employee_Org ON dbo.Employee (ProjectId, DepartmentId, DesignationId) INCLUDE (EmployeeID, EmployeeName, EmployeeStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Employee_Department_Project' AND object_id=OBJECT_ID('dbo.Employee'))
  CREATE INDEX IX_Employee_Department_Project ON dbo.Employee (DepartmentId, ProjectId, DesignationId) INCLUDE (EmployeeID, EmployeeName, EmployeeStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Employee_Department_SubDepartment' AND object_id=OBJECT_ID('dbo.Employee'))
  CREATE INDEX IX_Employee_Department_SubDepartment ON dbo.Employee (DepartmentId, SubDepartmentId) WHERE SubDepartmentId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Employee_DesignationId' AND object_id=OBJECT_ID('dbo.Employee'))
  CREATE INDEX IX_Employee_DesignationId ON dbo.Employee (DesignationId) WHERE DesignationId IS NOT NULL;
GO

/* ---- 4. Configuration rows that moved to tables or became derived values (S11, T5, T6) ---- */
DELETE FROM dbo.Configuration WHERE ConfigKey IN ('Project','Department','Designation','LeaveTypes','Year','TaxSlabs','CurrentTaxYear');
GO

/* ---- 5. views that name renamed columns ---- */
CREATE OR ALTER VIEW dbo.vEmployeeDirectory AS
SELECT e.uid, e.EmployeeID, e.EmployeeName,
       p.Name AS ProjectName, d.DepartmentName, sd.Name AS SubDepartmentName, g.Name AS DesignationName,
       e.EmployeeStatus,
       p.SortOrder AS ProjectSortOrder, d.SortOrder AS DepartmentSortOrder, g.[Rank] AS DesignationRank
FROM dbo.Employee e
LEFT JOIN dbo.Projects p        ON p.Id = e.ProjectId
LEFT JOIN dbo.Departments d     ON d.Id = e.DepartmentId
LEFT JOIN dbo.SubDepartments sd ON sd.Id = e.SubDepartmentId
LEFT JOIN dbo.Designations g    ON g.Id = e.DesignationId;
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261123000006_Redesign_Phase6_Contract', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261123000006_Redesign_Phase6_Contract');
GO
COMMIT TRANSACTION;
GO
