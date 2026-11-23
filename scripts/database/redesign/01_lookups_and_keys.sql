/*
  HRMS database redesign - phase 0 + 1: bookkeeping, lookup tables, keys.
  Source: docs/db/db_changes.md v3.4-draft ("Migration order" steps 0 and 1).
  Re-runnable (db_standards S23): every change is guarded; running twice changes nothing.
  Run with:  sqlcmd -S <server> -d HRBTK -U <user> -P <pwd> -C -b -I -i 01_lookups_and_keys.sql
  (-I = QUOTED_IDENTIFIER ON, needed for filtered indexes; -b = stop on first error; one transaction.)
  ALWAYS back up HRBTK first. Never run against production without approval (S19).
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

/* ---- Step 0: record the six hand-applied migrations so `dotnet ef database update` does not re-apply them ---- */
INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT v.id, '8.0.0'
FROM (VALUES
  ('20260105120000_CreateAttendanceUploadLogsTable'),
  ('20260512100000_PayslipTotalAllowancesColumn'),
  ('20260515120000_AlignDeductionsSchemaDbTxt'),
  ('20260515140000_CreateEmployeeDocumentsTable'),
  ('20260520100000_AddTaxYearColumnTaxRules'),
  ('20260918120000_AddAllowancesQuantityColumn')) v(id)
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory h WHERE h.MigrationId = v.id);
GO

/* ---- Step 1a: new lookup tables ---- */
IF OBJECT_ID('dbo.Projects','U') IS NULL
CREATE TABLE dbo.Projects (
  Id                 int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Projects PRIMARY KEY,
  Name               nvarchar(100) NOT NULL,
  Code               varchar(20)   NULL,
  SortOrder          int           NOT NULL CONSTRAINT DF_Projects_SortOrder DEFAULT 0,
  HasLeaveManagement bit           NOT NULL CONSTRAINT DF_Projects_HasLeaveManagement DEFAULT 1,
  IsActive           bit           NOT NULL CONSTRAINT DF_Projects_IsActive DEFAULT 1,
  CreatedAt          datetime2(0)  NOT NULL CONSTRAINT DF_Projects_CreatedAt DEFAULT sysdatetime(),
  CreatedBy          nvarchar(100) NULL,
  ModifiedAt         datetime2(0)  NULL,
  ModifiedBy         nvarchar(100) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Projects_Name' AND object_id=OBJECT_ID('dbo.Projects'))
  CREATE UNIQUE INDEX UX_Projects_Name ON dbo.Projects (Name);
GO

IF OBJECT_ID('dbo.Designations','U') IS NULL
CREATE TABLE dbo.Designations (
  Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Designations PRIMARY KEY,
  Name       nvarchar(100) NOT NULL,
  [Rank]     int           NOT NULL CONSTRAINT DF_Designations_Rank DEFAULT 100,   -- B4: HR ranking pending
  IsActive   bit           NOT NULL CONSTRAINT DF_Designations_IsActive DEFAULT 1,
  CreatedAt  datetime2(0)  NOT NULL CONSTRAINT DF_Designations_CreatedAt DEFAULT sysdatetime(),
  CreatedBy  nvarchar(100) NULL,
  ModifiedAt datetime2(0)  NULL,
  ModifiedBy nvarchar(100) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Designations_Name' AND object_id=OBJECT_ID('dbo.Designations'))
  CREATE UNIQUE INDEX UX_Designations_Name ON dbo.Designations (Name);
GO

IF OBJECT_ID('dbo.LeaveTypes','U') IS NULL
CREATE TABLE dbo.LeaveTypes (
  Id               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveTypes PRIMARY KEY,
  Code             varchar(10)   NOT NULL,
  Name             nvarchar(50)  NOT NULL,
  IsPaid           bit           NOT NULL CONSTRAINT DF_LeaveTypes_IsPaid DEFAULT 1,            -- B11 pending
  IsHalfDayAllowed bit           NOT NULL CONSTRAINT DF_LeaveTypes_IsHalfDayAllowed DEFAULT 1,
  IsActive         bit           NOT NULL CONSTRAINT DF_LeaveTypes_IsActive DEFAULT 1,
  SortOrder        int           NOT NULL CONSTRAINT DF_LeaveTypes_SortOrder DEFAULT 0,
  CreatedAt        datetime2(0)  NOT NULL CONSTRAINT DF_LeaveTypes_CreatedAt DEFAULT sysdatetime(),
  CreatedBy        nvarchar(100) NULL,
  ModifiedAt       datetime2(0)  NULL,
  ModifiedBy       nvarchar(100) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_LeaveTypes_Code' AND object_id=OBJECT_ID('dbo.LeaveTypes'))
  CREATE UNIQUE INDEX UX_LeaveTypes_Code ON dbo.LeaveTypes (Code);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_LeaveTypes_Name' AND object_id=OBJECT_ID('dbo.LeaveTypes'))
  CREATE UNIQUE INDEX UX_LeaveTypes_Name ON dbo.LeaveTypes (Name);
GO

/* ---- Step 1b: Departments becomes one shared list (B2) ---- */
IF EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentName IS NULL OR LTRIM(RTRIM(DepartmentName)) = '')
  THROW 50001, 'Departments has blank names; fix them before making DepartmentName NOT NULL.', 1;
IF EXISTS (SELECT 1 FROM dbo.Departments GROUP BY DepartmentName HAVING COUNT(*) > 1)
  THROW 50002, 'Departments has duplicate names; fix them before creating UX_Departments_DepartmentName.', 1;
IF COLUMNPROPERTY(OBJECT_ID('dbo.Departments'),'DepartmentName','AllowsNull') = 1
  ALTER TABLE dbo.Departments ALTER COLUMN DepartmentName nvarchar(100) NOT NULL;
IF COL_LENGTH('dbo.Departments','SortOrder') IS NULL
  ALTER TABLE dbo.Departments ADD SortOrder int NOT NULL CONSTRAINT DF_Departments_SortOrder DEFAULT 0;
IF COL_LENGTH('dbo.Departments','IsActive') IS NULL
  ALTER TABLE dbo.Departments ADD IsActive bit NOT NULL CONSTRAINT DF_Departments_IsActive DEFAULT 1;
IF COL_LENGTH('dbo.Departments','CreatedAt') IS NULL
  ALTER TABLE dbo.Departments ADD CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Departments_CreatedAt DEFAULT sysdatetime();
IF COL_LENGTH('dbo.Departments','CreatedBy') IS NULL  ALTER TABLE dbo.Departments ADD CreatedBy nvarchar(100) NULL;
IF COL_LENGTH('dbo.Departments','ModifiedAt') IS NULL ALTER TABLE dbo.Departments ADD ModifiedAt datetime2(0) NULL;
IF COL_LENGTH('dbo.Departments','ModifiedBy') IS NULL ALTER TABLE dbo.Departments ADD ModifiedBy nvarchar(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Departments_DepartmentName' AND object_id=OBJECT_ID('dbo.Departments'))
  CREATE UNIQUE INDEX UX_Departments_DepartmentName ON dbo.Departments (DepartmentName);
GO

/* ---- Step 1c: SubDepartments (optional detail under a department, B2) ---- */
IF OBJECT_ID('dbo.SubDepartments','U') IS NULL
CREATE TABLE dbo.SubDepartments (
  Id           int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubDepartments PRIMARY KEY,
  DepartmentId int           NOT NULL,   -- Departments PK is still DepartmentID until the contract phase
  Name         nvarchar(100) NOT NULL,
  SortOrder    int           NOT NULL CONSTRAINT DF_SubDepartments_SortOrder DEFAULT 0,
  IsActive     bit           NOT NULL CONSTRAINT DF_SubDepartments_IsActive DEFAULT 1,
  CreatedAt    datetime2(0)  NOT NULL CONSTRAINT DF_SubDepartments_CreatedAt DEFAULT sysdatetime(),
  CreatedBy    nvarchar(100) NULL,
  ModifiedAt   datetime2(0)  NULL,
  ModifiedBy   nvarchar(100) NULL,
  CONSTRAINT FK_SubDepartments_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentID)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_SubDepartments_Department_Name' AND object_id=OBJECT_ID('dbo.SubDepartments'))
  CREATE UNIQUE INDEX UX_SubDepartments_Department_Name ON dbo.SubDepartments (DepartmentId, Name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_SubDepartments_Department_Id' AND object_id=OBJECT_ID('dbo.SubDepartments'))
  CREATE UNIQUE INDEX UX_SubDepartments_Department_Id ON dbo.SubDepartments (DepartmentId, Id);   -- target of the composite FK from Employee
GO

/* ---- Step 1d: Configuration gets a real key (S1, S21) ---- */
IF EXISTS (SELECT 1 FROM dbo.Configuration WHERE ConfigKey IS NULL)
  THROW 50003, 'Configuration has NULL ConfigKey rows.', 1;
IF EXISTS (SELECT 1 FROM dbo.Configuration GROUP BY ConfigKey HAVING COUNT(*) > 1)
  THROW 50004, 'Configuration has duplicate ConfigKey rows.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('dbo.Configuration') AND type = 'PK')
  ALTER TABLE dbo.Configuration ADD CONSTRAINT PK_Configuration PRIMARY KEY (UID);   -- UID is renamed Id in the contract phase
IF COLUMNPROPERTY(OBJECT_ID('dbo.Configuration'),'ConfigKey','AllowsNull') = 1
  ALTER TABLE dbo.Configuration ALTER COLUMN ConfigKey nvarchar(50) NOT NULL;
IF COL_LENGTH('dbo.Configuration','CreatedAt') IS NULL
  ALTER TABLE dbo.Configuration ADD CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Configuration_CreatedAt DEFAULT sysdatetime();
IF COL_LENGTH('dbo.Configuration','CreatedBy') IS NULL  ALTER TABLE dbo.Configuration ADD CreatedBy nvarchar(100) NULL;
IF COL_LENGTH('dbo.Configuration','ModifiedAt') IS NULL ALTER TABLE dbo.Configuration ADD ModifiedAt datetime2(0) NULL;
IF COL_LENGTH('dbo.Configuration','ModifiedBy') IS NULL ALTER TABLE dbo.Configuration ADD ModifiedBy nvarchar(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Configuration_ConfigKey' AND object_id=OBJECT_ID('dbo.Configuration'))
  CREATE UNIQUE INDEX UX_Configuration_ConfigKey ON dbo.Configuration (ConfigKey);
GO

/* ---- Step 1e: LeaveQuota gets a real key (rest of its change is in phase 3) ---- */
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('dbo.LeaveQuota') AND type = 'PK')
  ALTER TABLE dbo.LeaveQuota ADD CONSTRAINT PK_LeaveQuota PRIMARY KEY (UID);          -- UID is renamed Id in the contract phase
IF COL_LENGTH('dbo.LeaveQuota','CreatedAt') IS NULL
  ALTER TABLE dbo.LeaveQuota ADD CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_LeaveQuota_CreatedAt DEFAULT sysdatetime();
IF COL_LENGTH('dbo.LeaveQuota','CreatedBy') IS NULL  ALTER TABLE dbo.LeaveQuota ADD CreatedBy nvarchar(100) NULL;
IF COL_LENGTH('dbo.LeaveQuota','ModifiedAt') IS NULL ALTER TABLE dbo.LeaveQuota ADD ModifiedAt datetime2(0) NULL;
IF COL_LENGTH('dbo.LeaveQuota','ModifiedBy') IS NULL ALTER TABLE dbo.LeaveQuota ADD ModifiedBy nvarchar(100) NULL;
GO

/* ---- Step 1f: missing FKs and indexes ---- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Deductions_EmployeeId' AND object_id=OBJECT_ID('dbo.Deductions'))
  CREATE INDEX IX_Deductions_EmployeeId ON dbo.Deductions (EmployeeId);

IF EXISTS (SELECT 1 FROM dbo.EmployeeDocuments d WHERE NOT EXISTS (SELECT 1 FROM dbo.Employee e WHERE e.uid = d.EmployeeId))
  THROW 50005, 'EmployeeDocuments has orphan EmployeeId rows.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_EmployeeDocuments_Employee')
  ALTER TABLE dbo.EmployeeDocuments WITH CHECK ADD CONSTRAINT FK_EmployeeDocuments_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (uid);

IF EXISTS (SELECT 1 FROM dbo.AuditLogs a WHERE a.EmployeeId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Employee e WHERE e.uid = a.EmployeeId))
  THROW 50006, 'AuditLogs has orphan EmployeeId rows.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_AuditLogs_Employee')
  ALTER TABLE dbo.AuditLogs WITH CHECK ADD CONSTRAINT FK_AuditLogs_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (uid);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AuditLogs_EmployeeId' AND object_id=OBJECT_ID('dbo.AuditLogs'))
  CREATE INDEX IX_AuditLogs_EmployeeId ON dbo.AuditLogs (EmployeeId) WHERE EmployeeId IS NOT NULL;

IF EXISTS (SELECT 1 FROM dbo.Users GROUP BY Username HAVING COUNT(*) > 1)
  THROW 50007, 'Users has duplicate usernames.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Users_Username' AND object_id=OBJECT_ID('dbo.Users'))
  CREATE UNIQUE INDEX UX_Users_Username ON dbo.Users (Username);
GO

/* ---- Step 1g: rename auto-named constraints (Appendix A; names found dynamically, no data change) ---- */
DECLARE @r TABLE (tbl sysname, newname sysname);
INSERT @r VALUES ('Attendance','PK_Attendance'), ('AttendanceUploadLogs','PK_AttendanceUploadLogs'),
  ('Deductions','PK_Deductions'), ('Departments','PK_Departments'), ('Employee','PK_Employee'),
  ('EmployeeDocuments','PK_EmployeeDocuments'), ('Users','PK_Users');
DECLARE @t sysname, @n sysname, @old sysname;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT tbl, newname FROM @r;
OPEN c; FETCH NEXT FROM c INTO @t, @n;
WHILE @@FETCH_STATUS = 0
BEGIN
  SET @old = NULL;
  SELECT @old = name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('dbo.' + @t) AND type = 'PK';
  IF @old IS NOT NULL AND @old <> @n
  BEGIN
    SET @old = 'dbo.' + @old;
    EXEC sp_rename @objname = @old, @newname = @n, @objtype = 'OBJECT';
  END
  FETCH NEXT FROM c INTO @t, @n;
END
CLOSE c; DEALLOCATE c;
GO
-- sp_rename on a constraint needs the schema-qualified name when called from a different context; do the FK renames explicitly.
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Allowances_Employee_EmployeeId')
  EXEC sp_rename N'dbo.FK_Allowances_Employee_EmployeeId', N'FK_Allowances_Employee', N'OBJECT';
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Payslips_Employee_EmployeeId')
  EXEC sp_rename N'dbo.FK_Payslips_Employee_EmployeeId', N'FK_Payslips_Employee', N'OBJECT';
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PayslipDetails_Payslips_PayslipId')
  EXEC sp_rename N'dbo.FK_PayslipDetails_Payslips_PayslipId', N'FK_PayslipDetails_Payslips', N'OBJECT';
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261123000001_Redesign_Phase1_LookupsAndKeys', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261123000001_Redesign_Phase1_LookupsAndKeys');
GO
COMMIT TRANSACTION;
GO
