/*
  HRMS database redesign - phase 2: Employee org links, new Employee columns, type conversions.
  Source: docs/db/db_changes.md v3.4-draft ("Migration order" step 2). Needs 01_lookups_and_keys.sql first.
  Re-runnable. Run with sqlcmd -b -I (see 01_lookups_and_keys.sql). Back up first (S19).

  NOTE: the type conversions at the end (DOB, ModifiedOn, ApplyTax, lengths) change what the EF model must map,
  so the running app needs the matching model changes (step 5 in the draft).
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

/* ---- new Employee columns ---- */
IF COL_LENGTH('dbo.Employee','ProjectId')        IS NULL ALTER TABLE dbo.Employee ADD ProjectId int NULL;
IF COL_LENGTH('dbo.Employee','DepartmentId')     IS NULL ALTER TABLE dbo.Employee ADD DepartmentId int NULL;
IF COL_LENGTH('dbo.Employee','SubDepartmentId')  IS NULL ALTER TABLE dbo.Employee ADD SubDepartmentId int NULL;
IF COL_LENGTH('dbo.Employee','DesignationId')    IS NULL ALTER TABLE dbo.Employee ADD DesignationId int NULL;
IF COL_LENGTH('dbo.Employee','DateOfLeaving')    IS NULL ALTER TABLE dbo.Employee ADD DateOfLeaving date NULL;      -- T1
IF COL_LENGTH('dbo.Employee','DateOfReturn')     IS NULL ALTER TABLE dbo.Employee ADD DateOfReturn date NULL;       -- T1
IF COL_LENGTH('dbo.Employee','PayBasis')         IS NULL ALTER TABLE dbo.Employee ADD PayBasis varchar(10) NOT NULL CONSTRAINT DF_Employee_PayBasis DEFAULT 'Monthly';
IF COL_LENGTH('dbo.Employee','HourlyRate')       IS NULL ALTER TABLE dbo.Employee ADD HourlyRate decimal(18,2) NULL;
IF COL_LENGTH('dbo.Employee','IsLeaveManagedOverride') IS NULL ALTER TABLE dbo.Employee ADD IsLeaveManagedOverride bit NULL;
IF COL_LENGTH('dbo.Employee','Notes')            IS NULL ALTER TABLE dbo.Employee ADD Notes nvarchar(1000) NULL;
IF COL_LENGTH('dbo.Employee','CreatedAt')        IS NULL ALTER TABLE dbo.Employee ADD CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Employee_CreatedAt DEFAULT sysdatetime();
IF COL_LENGTH('dbo.Employee','CreatedBy')        IS NULL ALTER TABLE dbo.Employee ADD CreatedBy nvarchar(100) NULL;
IF COL_LENGTH('dbo.Employee','RowVersion')       IS NULL ALTER TABLE dbo.Employee ADD RowVersion rowversion NOT NULL;
GO

/* ---- review table for HR: raw free-text value -> department / sub-department / project ---- */
IF SCHEMA_ID('mig') IS NULL EXEC('CREATE SCHEMA mig');
GO
IF OBJECT_ID('mig.EmployeeOrgMap','U') IS NULL
CREATE TABLE mig.EmployeeOrgMap (
  Kind              varchar(12)   NOT NULL,     -- 'Department' or 'Project'
  RawValue          nvarchar(250) NOT NULL,     -- value exactly as in Employee.Department / Employee.Project (trimmed)
  DepartmentName    nvarchar(100) NULL,         -- Kind = Department: the main department
  SubDepartmentName nvarchar(100) NULL,         -- Kind = Department: the function after the first ' - ', if any
  ProjectName       nvarchar(100) NULL,         -- Kind = Project
  CONSTRAINT PK_EmployeeOrgMap PRIMARY KEY (Kind, RawValue)
);
GO

-- Departments: split at the first ' - ' (B2). 'Admin' is the Administration department. Rows already present (HR edits) are kept.
INSERT INTO mig.EmployeeOrgMap (Kind, RawValue, DepartmentName, SubDepartmentName)
SELECT 'Department', d.v,
       CASE WHEN y.base0 = 'Admin' THEN 'Administration' ELSE y.base0 END,
       y.sub
FROM (SELECT DISTINCT LTRIM(RTRIM(Department)) AS v FROM dbo.Employee WHERE NULLIF(LTRIM(RTRIM(Department)), '') IS NOT NULL) d
CROSS APPLY (SELECT p = CHARINDEX(' - ', d.v)) x
CROSS APPLY (SELECT base0 = LTRIM(RTRIM(CASE WHEN x.p > 0 THEN LEFT(d.v, x.p - 1) ELSE d.v END)),
                    sub   = CASE WHEN x.p > 0 THEN LTRIM(RTRIM(SUBSTRING(d.v, x.p + 3, 200))) END) y
WHERE NOT EXISTS (SELECT 1 FROM mig.EmployeeOrgMap m WHERE m.Kind = 'Department' AND m.RawValue = d.v);

INSERT INTO mig.EmployeeOrgMap (Kind, RawValue, ProjectName)
SELECT 'Project', d.v, d.v
FROM (SELECT DISTINCT LTRIM(RTRIM(Project)) AS v FROM dbo.Employee WHERE NULLIF(LTRIM(RTRIM(Project)), '') IS NOT NULL) d
WHERE NOT EXISTS (SELECT 1 FROM mig.EmployeeOrgMap m WHERE m.Kind = 'Project' AND m.RawValue = d.v);
GO

/* ---- seed Departments, SubDepartments, Projects, Designations from the map / distinct values ---- */
INSERT INTO dbo.Departments (DepartmentName, CreatedBy)
SELECT DISTINCT m.DepartmentName, 'redesign-migration'
FROM mig.EmployeeOrgMap m
WHERE m.Kind = 'Department' AND m.DepartmentName IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Departments d WHERE d.DepartmentName = m.DepartmentName);

INSERT INTO dbo.SubDepartments (DepartmentId, Name, CreatedBy)
SELECT DISTINCT d.DepartmentID, m.SubDepartmentName, 'redesign-migration'
FROM mig.EmployeeOrgMap m
JOIN dbo.Departments d ON d.DepartmentName = m.DepartmentName
WHERE m.Kind = 'Department' AND m.SubDepartmentName IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.SubDepartments s WHERE s.DepartmentId = d.DepartmentID AND s.Name = m.SubDepartmentName);

-- HasLeaveManagement = 0 for the Dastarkhuwan project (HR rule, 2026-11-16); it is a value on the row, HR edits it in Setup.
INSERT INTO dbo.Projects (Name, HasLeaveManagement, CreatedBy)
SELECT DISTINCT m.ProjectName, CASE WHEN m.ProjectName LIKE '%Dastarkhuwan%' THEN 0 ELSE 1 END, 'redesign-migration'
FROM mig.EmployeeOrgMap m
WHERE m.Kind = 'Project' AND m.ProjectName IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Projects p WHERE p.Name = m.ProjectName);

IF EXISTS (SELECT 1 FROM dbo.Employee WHERE LEN(LTRIM(RTRIM(Designation))) > 100)
  THROW 50020, 'A designation is longer than 100 characters.', 1;
INSERT INTO dbo.Designations (Name, CreatedBy)
SELECT DISTINCT LTRIM(RTRIM(e.Designation)), 'redesign-migration'
FROM dbo.Employee e
WHERE NULLIF(LTRIM(RTRIM(e.Designation)), '') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Designations g WHERE g.Name = LTRIM(RTRIM(e.Designation)));
GO

/* ---- backfill Employee FKs (set only where NULL, so a second run changes nothing) ---- */
UPDATE e SET ProjectId = p.Id
FROM dbo.Employee e
JOIN mig.EmployeeOrgMap m ON m.Kind = 'Project' AND m.RawValue = LTRIM(RTRIM(e.Project))
JOIN dbo.Projects p ON p.Name = m.ProjectName
WHERE e.ProjectId IS NULL;

UPDATE e SET DepartmentId = d.DepartmentID
FROM dbo.Employee e
JOIN mig.EmployeeOrgMap m ON m.Kind = 'Department' AND m.RawValue = LTRIM(RTRIM(e.Department))
JOIN dbo.Departments d ON d.DepartmentName = m.DepartmentName
WHERE e.DepartmentId IS NULL;

UPDATE e SET SubDepartmentId = s.Id
FROM dbo.Employee e
JOIN mig.EmployeeOrgMap m ON m.Kind = 'Department' AND m.RawValue = LTRIM(RTRIM(e.Department)) AND m.SubDepartmentName IS NOT NULL
JOIN dbo.SubDepartments s ON s.DepartmentId = e.DepartmentId AND s.Name = m.SubDepartmentName
WHERE e.SubDepartmentId IS NULL;

UPDATE e SET DesignationId = g.Id
FROM dbo.Employee e
JOIN dbo.Designations g ON g.Name = LTRIM(RTRIM(e.Designation))
WHERE e.DesignationId IS NULL;

-- unmapped rows are reported, not guessed
IF EXISTS (SELECT 1 FROM dbo.Employee WHERE NULLIF(LTRIM(RTRIM(Project)),'') IS NOT NULL AND ProjectId IS NULL)
  THROW 50021, 'Some employees have a Project text that did not map to a project.', 1;
IF EXISTS (SELECT 1 FROM dbo.Employee WHERE NULLIF(LTRIM(RTRIM(Department)),'') IS NOT NULL AND DepartmentId IS NULL)
  THROW 50022, 'Some employees have a Department text that did not map to a department.', 1;
GO

/* ---- FKs and indexes on the new columns ---- */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Employee_Projects')
  ALTER TABLE dbo.Employee WITH CHECK ADD CONSTRAINT FK_Employee_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Employee_Departments')
  ALTER TABLE dbo.Employee WITH CHECK ADD CONSTRAINT FK_Employee_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentID);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Employee_SubDepartments')
  ALTER TABLE dbo.Employee WITH CHECK ADD CONSTRAINT FK_Employee_SubDepartments FOREIGN KEY (DepartmentId, SubDepartmentId) REFERENCES dbo.SubDepartments (DepartmentId, Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Employee_Designations')
  ALTER TABLE dbo.Employee WITH CHECK ADD CONSTRAINT FK_Employee_Designations FOREIGN KEY (DesignationId) REFERENCES dbo.Designations (Id);

-- IX_Employee_Org and IX_Employee_Department_Project include columns that are altered below, so they are created after the conversions.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Employee_Department_SubDepartment' AND object_id=OBJECT_ID('dbo.Employee'))
  CREATE INDEX IX_Employee_Department_SubDepartment ON dbo.Employee (DepartmentId, SubDepartmentId) WHERE SubDepartmentId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Employee_DesignationId' AND object_id=OBJECT_ID('dbo.Employee'))
  CREATE INDEX IX_Employee_DesignationId ON dbo.Employee (DesignationId) WHERE DesignationId IS NOT NULL;
GO

/* ---- type conversions (each guarded by the current type; data checked first) ---- */
-- EmployeeID: unique constraint replaced by the named unique index while the column is narrowed
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Employee') AND name='EmployeeID' AND max_length <> 50)
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Employee WHERE LEN(EmployeeID) > 50) THROW 50030, 'EmployeeID longer than 50.', 1;
  DECLARE @uq sysname = (SELECT name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('dbo.Employee') AND type = 'UQ');
  IF @uq IS NOT NULL EXEC('ALTER TABLE dbo.Employee DROP CONSTRAINT ' + @uq);
  ALTER TABLE dbo.Employee ALTER COLUMN EmployeeID varchar(50) NOT NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Employee_EmployeeID' AND object_id=OBJECT_ID('dbo.Employee'))
  CREATE UNIQUE INDEX UX_Employee_EmployeeID ON dbo.Employee (EmployeeID);
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Employee') AND name='EmployeeName' AND (max_length <> 200 OR system_type_id <> 231 OR is_nullable = 1))
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Employee WHERE EmployeeName IS NULL OR LEN(EmployeeName) > 100) THROW 50031, 'EmployeeName is NULL or longer than 100.', 1;
  ALTER TABLE dbo.Employee ALTER COLUMN EmployeeName nvarchar(100) NOT NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Employee') AND name='CNIC' AND max_length <> 15)
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Employee WHERE LEN(CNIC) > 15) THROW 50032, 'CNIC longer than 15.', 1;
  ALTER TABLE dbo.Employee ALTER COLUMN CNIC varchar(15) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Employee') AND name='FatherName' AND NOT (system_type_id = 231 AND max_length = 200))
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Employee WHERE LEN(FatherName) > 100) THROW 50033, 'FatherName longer than 100.', 1;
  ALTER TABLE dbo.Employee ALTER COLUMN FatherName nvarchar(100) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Employee') AND name='MobileNo' AND max_length <> 20)
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Employee WHERE LEN(MobileNo) > 20) THROW 50034, 'MobileNo longer than 20.', 1;
  ALTER TABLE dbo.Employee ALTER COLUMN MobileNo varchar(20) NULL;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Employee') AND name='DOB' AND TYPE_NAME(user_type_id)='varchar')
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Employee WHERE NULLIF(LTRIM(RTRIM(DOB)),'') IS NOT NULL AND TRY_CONVERT(date, DOB) IS NULL)
    THROW 50035, 'Some DOB values are not valid dates; list them for HR first.', 1;
  UPDATE dbo.Employee SET DOB = CONVERT(varchar(10), TRY_CONVERT(date, NULLIF(LTRIM(RTRIM(DOB)),'')), 23) WHERE DOB IS NOT NULL;
  ALTER TABLE dbo.Employee ALTER COLUMN DOB date NULL;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Employee') AND name='ModifiedOn' AND TYPE_NAME(user_type_id)='nvarchar')
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Employee WHERE NULLIF(LTRIM(RTRIM(ModifiedOn)),'') IS NOT NULL AND TRY_CONVERT(datetime2(0), ModifiedOn) IS NULL)
    THROW 50036, 'Some ModifiedOn values are not valid dates.', 1;
  UPDATE dbo.Employee SET ModifiedOn = CONVERT(nvarchar(19), TRY_CONVERT(datetime2(0), NULLIF(LTRIM(RTRIM(ModifiedOn)),'')), 126) WHERE ModifiedOn IS NOT NULL;
  ALTER TABLE dbo.Employee ALTER COLUMN ModifiedOn datetime2(0) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Employee') AND name='ModifiedBy' AND max_length = -1)
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Employee WHERE LEN(ModifiedBy) > 100) THROW 50037, 'ModifiedBy longer than 100.', 1;
  ALTER TABLE dbo.Employee ALTER COLUMN ModifiedBy nvarchar(100) NULL;
END
GO

-- ApplyTax: 1/Yes -> 1; 0/empty -> 0 (matches today's payroll rule)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Employee') AND name='ApplyTax' AND TYPE_NAME(user_type_id)='nvarchar')
BEGIN
  UPDATE dbo.Employee SET ApplyTax = CASE WHEN LOWER(LTRIM(RTRIM(ISNULL(ApplyTax,'')))) IN ('1','yes','y','true') THEN '1' ELSE '0' END;
  ALTER TABLE dbo.Employee ALTER COLUMN ApplyTax bit NOT NULL;
  ALTER TABLE dbo.Employee ADD CONSTRAINT DF_Employee_ApplyTax DEFAULT 1 FOR ApplyTax;
END
GO

-- EmployeeStatus: constrained code (list proposed, B9)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Employee') AND name='EmployeeStatus' AND (max_length <> 20 OR is_nullable = 1))
BEGIN
  UPDATE dbo.Employee SET EmployeeStatus = 'Active' WHERE EmployeeStatus IS NULL OR LTRIM(RTRIM(EmployeeStatus)) = '';
  IF EXISTS (SELECT 1 FROM dbo.Employee WHERE EmployeeStatus NOT IN ('Active','Inactive','Resigned','Terminated'))
    THROW 50038, 'EmployeeStatus has values outside the proposed list.', 1;
  ALTER TABLE dbo.Employee ALTER COLUMN EmployeeStatus varchar(20) NOT NULL;
  ALTER TABLE dbo.Employee ADD CONSTRAINT DF_Employee_EmployeeStatus DEFAULT 'Active' FOR EmployeeStatus;
END
GO

/* ---- covering org indexes (after the conversions above) ---- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Employee_Org' AND object_id=OBJECT_ID('dbo.Employee'))
  CREATE INDEX IX_Employee_Org ON dbo.Employee (ProjectId, DepartmentId, DesignationId) INCLUDE (EmployeeID, EmployeeName, EmployeeStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Employee_Department_Project' AND object_id=OBJECT_ID('dbo.Employee'))
  CREATE INDEX IX_Employee_Department_Project ON dbo.Employee (DepartmentId, ProjectId, DesignationId) INCLUDE (EmployeeID, EmployeeName, EmployeeStatus);
GO

/* ---- CHECK constraints ---- */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Employee_PayBasis')
  ALTER TABLE dbo.Employee ADD CONSTRAINT CK_Employee_PayBasis CHECK (PayBasis IN ('Monthly','Hourly'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Employee_EmployeeStatus')
  ALTER TABLE dbo.Employee ADD CONSTRAINT CK_Employee_EmployeeStatus CHECK (EmployeeStatus IN ('Active','Inactive','Resigned','Terminated'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Employee_HourlyRate')
  ALTER TABLE dbo.Employee ADD CONSTRAINT CK_Employee_HourlyRate CHECK (HourlyRate IS NULL OR HourlyRate > 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Employee_Dates')
  ALTER TABLE dbo.Employee ADD CONSTRAINT CK_Employee_Dates CHECK (
    (DateOfLeaving IS NULL OR DateOfJoining IS NULL OR DateOfLeaving >= DateOfJoining)
    AND (DateOfReturn IS NULL OR (DateOfLeaving IS NOT NULL AND DateOfReturn > DateOfLeaving)));
GO

/* ---- view: Employee joined to the org lookups ---- */
CREATE OR ALTER VIEW dbo.vEmployeeDirectory AS
SELECT e.uid, e.EmployeeID, e.EmployeeName,
       p.Name AS ProjectName, d.DepartmentName, sd.Name AS SubDepartmentName, g.Name AS DesignationName,
       e.EmployeeStatus,
       p.SortOrder AS ProjectSortOrder, d.SortOrder AS DepartmentSortOrder, g.[Rank] AS DesignationRank
FROM dbo.Employee e
LEFT JOIN dbo.Projects p        ON p.Id = e.ProjectId
LEFT JOIN dbo.Departments d     ON d.DepartmentID = e.DepartmentId
LEFT JOIN dbo.SubDepartments sd ON sd.Id = e.SubDepartmentId
LEFT JOIN dbo.Designations g    ON g.Id = e.DesignationId;
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261123000002_Redesign_Phase2_EmployeeOrg', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261123000002_Redesign_Phase2_EmployeeOrg');
GO
COMMIT TRANSACTION;
GO
