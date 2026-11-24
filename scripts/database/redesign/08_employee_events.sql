/*
  HRMS database redesign - phase 8: employee history (key events).
  Adds dbo.EmployeeEvents, an append-only timeline of Joined, Promotion, Transfer, Resignation, Termination, Rejoin and Other
  (db_changes.md: EmployeeEvents; decisions.md: B14, T1). The app writes a row and applies its effect to Employee in one transaction.
  Backfills one row per existing employee from Employee.DateOfJoining / DateOfLeaving / DateOfReturn / EmployeeStatus.
  Needs 02 (Employee.DepartmentId etc.) and 03. Additive: the old app keeps working on the schema; the new build uses the table.
  Re-runnable. Run with sqlcmd -b -I (see 01_lookups_and_keys.sql). Back up first (S19).
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

IF OBJECT_ID('dbo.EmployeeEvents', 'U') IS NULL
BEGIN
  CREATE TABLE dbo.EmployeeEvents (
    Id               int IDENTITY(1,1) NOT NULL,
    EmployeeId       int           NOT NULL,
    EventType        varchar(20)   NOT NULL,
    EventDate        date          NOT NULL,
    FromProjectId    int           NULL,
    ToProjectId      int           NULL,
    FromDepartmentId int           NULL,
    ToDepartmentId   int           NULL,
    FromDesignationId int          NULL,
    ToDesignationId  int           NULL,
    Remarks          nvarchar(1000) NULL,
    IsVoided         bit           NOT NULL CONSTRAINT DF_EmployeeEvents_IsVoided DEFAULT (0),
    VoidReason       nvarchar(500) NULL,
    VoidedAt         datetime2(0)  NULL,
    VoidedBy         nvarchar(100) NULL,
    CreatedAt        datetime2(0)  NOT NULL CONSTRAINT DF_EmployeeEvents_CreatedAt DEFAULT (SYSDATETIME()),
    CreatedBy        nvarchar(100) NULL,
    RequestId        uniqueidentifier NULL,
    CONSTRAINT PK_EmployeeEvents PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_EmployeeEvents_Employee        FOREIGN KEY (EmployeeId)        REFERENCES dbo.Employee (uid),
    CONSTRAINT FK_EmployeeEvents_FromProject     FOREIGN KEY (FromProjectId)     REFERENCES dbo.Projects (Id),
    CONSTRAINT FK_EmployeeEvents_ToProject       FOREIGN KEY (ToProjectId)       REFERENCES dbo.Projects (Id),
    CONSTRAINT FK_EmployeeEvents_FromDepartment  FOREIGN KEY (FromDepartmentId)  REFERENCES dbo.Departments (Id),
    CONSTRAINT FK_EmployeeEvents_ToDepartment    FOREIGN KEY (ToDepartmentId)    REFERENCES dbo.Departments (Id),
    CONSTRAINT FK_EmployeeEvents_FromDesignation FOREIGN KEY (FromDesignationId) REFERENCES dbo.Designations (Id),
    CONSTRAINT FK_EmployeeEvents_ToDesignation   FOREIGN KEY (ToDesignationId)   REFERENCES dbo.Designations (Id),
    CONSTRAINT CK_EmployeeEvents_Type CHECK (EventType IN ('Joined','Promotion','Transfer','Resignation','Termination','Rejoin','Other')),
    CONSTRAINT CK_EmployeeEvents_Void CHECK (IsVoided = 0 OR (VoidReason IS NOT NULL AND VoidedAt IS NOT NULL))
  );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeEvents_Employee_Date' AND object_id = OBJECT_ID('dbo.EmployeeEvents'))
  CREATE NONCLUSTERED INDEX IX_EmployeeEvents_Employee_Date ON dbo.EmployeeEvents (EmployeeId, EventDate, Id) INCLUDE (EventType, IsVoided);
GO
/* One live "Joined" per employee: a second stint is a Rejoin. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_EmployeeEvents_Employee_Joined' AND object_id = OBJECT_ID('dbo.EmployeeEvents'))
  CREATE UNIQUE NONCLUSTERED INDEX UX_EmployeeEvents_Employee_Joined ON dbo.EmployeeEvents (EmployeeId) WHERE EventType = 'Joined' AND IsVoided = 0;
GO
/* Idempotency key (S21-style): a double-submitted form inserts one row. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_EmployeeEvents_RequestId' AND object_id = OBJECT_ID('dbo.EmployeeEvents'))
  CREATE UNIQUE NONCLUSTERED INDEX UX_EmployeeEvents_RequestId ON dbo.EmployeeEvents (RequestId) WHERE RequestId IS NOT NULL;
GO

/* Backfill from the current employee record (only employees that have no event yet). The To* columns carry the CURRENT position, which
   is the best known value; the remark says so. */
INSERT INTO dbo.EmployeeEvents (EmployeeId, EventType, EventDate, ToProjectId, ToDepartmentId, ToDesignationId, Remarks, CreatedBy)
SELECT e.uid, 'Joined', e.DateOfJoining, e.ProjectId, e.DepartmentId, e.DesignationId, N'Backfilled from the employee record (position shown is the current one).', N'Migration'
FROM dbo.Employee e
WHERE e.DateOfJoining IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.EmployeeEvents v WHERE v.EmployeeId = e.uid AND v.EventType = 'Joined');

INSERT INTO dbo.EmployeeEvents (EmployeeId, EventType, EventDate, Remarks, CreatedBy)
SELECT e.uid, CASE WHEN e.EmployeeStatus = 'Terminated' THEN 'Termination' ELSE 'Resignation' END, e.DateOfLeaving, N'Backfilled from the employee record.', N'Migration'
FROM dbo.Employee e
WHERE e.DateOfLeaving IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.EmployeeEvents v WHERE v.EmployeeId = e.uid AND v.EventType IN ('Resignation','Termination'));

INSERT INTO dbo.EmployeeEvents (EmployeeId, EventType, EventDate, Remarks, CreatedBy)
SELECT e.uid, 'Rejoin', e.DateOfReturn, N'Backfilled from the employee record.', N'Migration'
FROM dbo.Employee e
WHERE e.DateOfReturn IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.EmployeeEvents v WHERE v.EmployeeId = e.uid AND v.EventType = 'Rejoin');
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261124000008_Redesign_Phase8_EmployeeEvents', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261124000008_Redesign_Phase8_EmployeeEvents');
GO
COMMIT TRANSACTION;
GO
