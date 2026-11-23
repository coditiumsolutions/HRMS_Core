/*
  HRMS database redesign - phase 3: leave consolidation and Users.
  Source: docs/db/db_changes.md v3.4-draft ("Migration order" step 3). Needs 01 and 02 first.
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

/* ---- LeaveTypes seed: distinct names from LeaveRequests, LeaveQuota and the Configuration list ---- */
;WITH src AS (
  SELECT CAST(LTRIM(RTRIM(LeaveType)) AS nvarchar(50)) AS n FROM dbo.LeaveRequests
  UNION SELECT CAST(LTRIM(RTRIM(LeaveTypeName)) AS nvarchar(50)) FROM dbo.LeaveQuota
  UNION SELECT CAST(LTRIM(RTRIM(REPLACE(REPLACE(s.value, CHAR(13), ''), CHAR(10), ''))) AS nvarchar(50))
        FROM dbo.Configuration c CROSS APPLY STRING_SPLIT(c.ConfigValue, ',') s WHERE c.ConfigKey = 'LeaveTypes'
), fixed AS (
  -- 'Conpansation Leave' in the Configuration list is a typo for Compensation Leave
  SELECT DISTINCT CASE WHEN n = 'Conpansation Leave' THEN 'Compensation Leave' ELSE n END AS n FROM src WHERE NULLIF(n, '') IS NOT NULL
)
INSERT INTO dbo.LeaveTypes (Code, Name, IsPaid, CreatedBy)
SELECT CASE n WHEN 'Annual Leave' THEN 'AL' WHEN 'Casual Leave' THEN 'CL' WHEN 'Sick Leave' THEN 'SL'
              WHEN 'Unpaid Leave' THEN 'UL' WHEN 'Compensation Leave' THEN 'COMP'
              ELSE UPPER(LEFT(REPLACE(n, ' ', ''), 10)) END,
       n,
       CASE WHEN n LIKE 'Unpaid%' THEN 0 ELSE 1 END,   -- B11: effect on payroll still open
       'redesign-migration'
FROM fixed f
WHERE NOT EXISTS (SELECT 1 FROM dbo.LeaveTypes t WHERE t.Name = f.n);
GO

/* ---- LeaveQuota ---- */
IF COL_LENGTH('dbo.LeaveQuota','LeaveTypeId') IS NULL ALTER TABLE dbo.LeaveQuota ADD LeaveTypeId int NULL;
GO
UPDATE q SET LeaveTypeId = t.Id
FROM dbo.LeaveQuota q JOIN dbo.LeaveTypes t ON t.Name = LTRIM(RTRIM(q.LeaveTypeName))
WHERE q.LeaveTypeId IS NULL;
IF EXISTS (SELECT 1 FROM dbo.LeaveQuota WHERE LeaveTypeId IS NULL) THROW 50040, 'LeaveQuota rows without a leave type.', 1;
IF COLUMNPROPERTY(OBJECT_ID('dbo.LeaveQuota'),'LeaveTypeId','AllowsNull') = 1
  ALTER TABLE dbo.LeaveQuota ALTER COLUMN LeaveTypeId int NOT NULL;
IF COL_LENGTH('dbo.LeaveQuota','TotalLeaves') IS NOT NULL AND COL_LENGTH('dbo.LeaveQuota','TotalDays') IS NULL
  EXEC sp_rename N'dbo.LeaveQuota.TotalLeaves', N'TotalDays', N'COLUMN';
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.LeaveQuota') AND name='TotalDays' AND TYPE_NAME(user_type_id)='int')
  ALTER TABLE dbo.LeaveQuota ALTER COLUMN TotalDays decimal(5,2) NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.LeaveQuota') AND name='Year' AND TYPE_NAME(user_type_id)='varchar')
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.LeaveQuota WHERE TRY_CONVERT(int, Year) IS NULL) THROW 50041, 'LeaveQuota.Year has non-numeric values.', 1;
  ALTER TABLE dbo.LeaveQuota ALTER COLUMN [Year] int NOT NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_LeaveQuota_LeaveTypes')
  ALTER TABLE dbo.LeaveQuota ADD CONSTRAINT FK_LeaveQuota_LeaveTypes FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes (Id);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_LeaveQuota_TotalDays')
  ALTER TABLE dbo.LeaveQuota ADD CONSTRAINT CK_LeaveQuota_TotalDays CHECK (TotalDays >= 0);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_LeaveQuota_Type_Year' AND object_id=OBJECT_ID('dbo.LeaveQuota'))
  CREATE UNIQUE INDEX UX_LeaveQuota_Type_Year ON dbo.LeaveQuota (LeaveTypeId, [Year]);
GO

/* ---- LeaveRequests: archive rows pointing at a missing employee (the FK needs a real employee) ---- */
IF SCHEMA_ID('mig') IS NULL EXEC('CREATE SCHEMA mig');
GO
IF OBJECT_ID('mig.LeaveRequests_Orphans','U') IS NULL
CREATE TABLE mig.LeaveRequests_Orphans (
  Id int NOT NULL PRIMARY KEY, EmployeeId int NOT NULL, LeaveType nvarchar(MAX) NOT NULL,
  FromDate datetime2(7) NOT NULL, ToDate datetime2(7) NOT NULL, Status nvarchar(MAX) NULL,
  ArchivedAt datetime2(0) NOT NULL DEFAULT sysdatetime());
GO
IF COL_LENGTH('dbo.LeaveRequests','LeaveTypeId') IS NULL   -- only on the first run (the old column shapes are still there)
BEGIN
  INSERT INTO mig.LeaveRequests_Orphans (Id, EmployeeId, LeaveType, FromDate, ToDate, Status)
  SELECT r.Id, r.EmployeeId, r.LeaveType, r.FromDate, r.ToDate, r.Status
  FROM dbo.LeaveRequests r
  WHERE NOT EXISTS (SELECT 1 FROM dbo.Employee e WHERE e.uid = r.EmployeeId)
    AND NOT EXISTS (SELECT 1 FROM mig.LeaveRequests_Orphans o WHERE o.Id = r.Id);
  DELETE r FROM dbo.LeaveRequests r WHERE NOT EXISTS (SELECT 1 FROM dbo.Employee e WHERE e.uid = r.EmployeeId);
END
GO

/* ---- LeaveRequests: new columns ---- */
IF COL_LENGTH('dbo.LeaveRequests','LeaveTypeId')         IS NULL ALTER TABLE dbo.LeaveRequests ADD LeaveTypeId int NULL;
IF COL_LENGTH('dbo.LeaveRequests','IsHalfDay')           IS NULL ALTER TABLE dbo.LeaveRequests ADD IsHalfDay bit NOT NULL CONSTRAINT DF_LeaveRequests_IsHalfDay DEFAULT 0;
IF COL_LENGTH('dbo.LeaveRequests','TotalDays')           IS NULL ALTER TABLE dbo.LeaveRequests ADD TotalDays decimal(5,2) NULL;
IF COL_LENGTH('dbo.LeaveRequests','Reason')              IS NULL ALTER TABLE dbo.LeaveRequests ADD Reason nvarchar(500) NULL;
IF COL_LENGTH('dbo.LeaveRequests','DocumentId')          IS NULL ALTER TABLE dbo.LeaveRequests ADD DocumentId int NULL;
IF COL_LENGTH('dbo.LeaveRequests','ApprovedByUserId')    IS NULL ALTER TABLE dbo.LeaveRequests ADD ApprovedByUserId int NULL;
IF COL_LENGTH('dbo.LeaveRequests','ApprovedAt')          IS NULL ALTER TABLE dbo.LeaveRequests ADD ApprovedAt datetime2(0) NULL;
IF COL_LENGTH('dbo.LeaveRequests','SupervisorComments')  IS NULL ALTER TABLE dbo.LeaveRequests ADD SupervisorComments nvarchar(1000) NULL;
IF COL_LENGTH('dbo.LeaveRequests','Notes')               IS NULL ALTER TABLE dbo.LeaveRequests ADD Notes nvarchar(1000) NULL;
IF COL_LENGTH('dbo.LeaveRequests','AppliedAt')           IS NULL ALTER TABLE dbo.LeaveRequests ADD AppliedAt datetime2(0) NOT NULL CONSTRAINT DF_LeaveRequests_AppliedAt DEFAULT sysdatetime();  -- existing rows get the migration time
IF COL_LENGTH('dbo.LeaveRequests','CreatedBy')           IS NULL ALTER TABLE dbo.LeaveRequests ADD CreatedBy nvarchar(100) NULL;
IF COL_LENGTH('dbo.LeaveRequests','ModifiedAt')          IS NULL ALTER TABLE dbo.LeaveRequests ADD ModifiedAt datetime2(0) NULL;
IF COL_LENGTH('dbo.LeaveRequests','ModifiedBy')          IS NULL ALTER TABLE dbo.LeaveRequests ADD ModifiedBy nvarchar(100) NULL;
IF COL_LENGTH('dbo.LeaveRequests','RowVersion')          IS NULL ALTER TABLE dbo.LeaveRequests ADD RowVersion rowversion NOT NULL;
GO

/* ---- LeaveRequests: backfill and tighten ---- */
UPDATE r SET LeaveTypeId = t.Id
FROM dbo.LeaveRequests r
JOIN dbo.LeaveTypes t ON t.Name = CASE WHEN LTRIM(RTRIM(r.LeaveType)) = 'Conpansation Leave' THEN 'Compensation Leave' ELSE LTRIM(RTRIM(r.LeaveType)) END
WHERE r.LeaveTypeId IS NULL;
IF EXISTS (SELECT 1 FROM dbo.LeaveRequests WHERE LeaveTypeId IS NULL) THROW 50042, 'LeaveRequests rows without a leave type.', 1;
IF COLUMNPROPERTY(OBJECT_ID('dbo.LeaveRequests'),'LeaveTypeId','AllowsNull') = 1
  ALTER TABLE dbo.LeaveRequests ALTER COLUMN LeaveTypeId int NOT NULL;

-- whole days: first calculation is the calendar span; weekly offs and holidays are excluded once schedules exist (B5)
UPDATE dbo.LeaveRequests SET TotalDays = CAST(DATEDIFF(day, FromDate, ToDate) + 1 AS decimal(5,2)) WHERE TotalDays IS NULL;
IF COLUMNPROPERTY(OBJECT_ID('dbo.LeaveRequests'),'TotalDays','AllowsNull') = 1
  ALTER TABLE dbo.LeaveRequests ALTER COLUMN TotalDays decimal(5,2) NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.LeaveRequests') AND name='FromDate' AND TYPE_NAME(user_type_id)='datetime2')
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.LeaveRequests WHERE FromDate <> CAST(FromDate AS date) OR ToDate <> CAST(ToDate AS date)) THROW 50043, 'LeaveRequests has dates with a time part.', 1;
  ALTER TABLE dbo.LeaveRequests ALTER COLUMN FromDate date NOT NULL;
  ALTER TABLE dbo.LeaveRequests ALTER COLUMN ToDate date NOT NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.LeaveRequests') AND name='Status' AND (max_length = -1 OR is_nullable = 1))
BEGIN
  UPDATE dbo.LeaveRequests SET Status = CASE WHEN Status IS NULL OR Status = 'Applied' THEN 'Pending' ELSE Status END;
  IF EXISTS (SELECT 1 FROM dbo.LeaveRequests WHERE Status NOT IN ('Pending','Approved','Rejected','Cancelled')) THROW 50044, 'LeaveRequests.Status has unmapped values.', 1;
  ALTER TABLE dbo.LeaveRequests ALTER COLUMN Status varchar(10) NOT NULL;
  ALTER TABLE dbo.LeaveRequests ADD CONSTRAINT DF_LeaveRequests_Status DEFAULT 'Pending' FOR Status;
END
GO

/* ---- Users (needed by the LeaveRequests approver FK) ---- */
IF COL_LENGTH('dbo.Users','EmployeeUid') IS NULL ALTER TABLE dbo.Users ADD EmployeeUid int NULL;   -- renamed EmployeeId in the contract phase
IF COL_LENGTH('dbo.Users','IsActive')    IS NULL ALTER TABLE dbo.Users ADD IsActive bit NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1;
IF COL_LENGTH('dbo.Users','ModifiedAt')  IS NULL ALTER TABLE dbo.Users ADD ModifiedAt datetime2(0) NULL;
IF COL_LENGTH('dbo.Users','ModifiedBy')  IS NULL ALTER TABLE dbo.Users ADD ModifiedBy nvarchar(100) NULL;
GO
UPDATE u SET EmployeeUid = e.uid
FROM dbo.Users u JOIN dbo.Employee e ON e.EmployeeID = u.EmployeeId
WHERE u.EmployeeUid IS NULL;   -- a login whose EmployeeId matches nobody stays NULL (the orphan)

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Users') AND name='Role' AND max_length = -1)
BEGIN
  UPDATE dbo.Users SET Role = NULL WHERE LTRIM(RTRIM(Role)) = '';
  IF EXISTS (SELECT 1 FROM dbo.Users WHERE Role IS NOT NULL AND Role NOT IN ('Admin','HR')) THROW 50050, 'Users.Role has values outside Admin/HR.', 1;
  ALTER TABLE dbo.Users ALTER COLUMN Role nvarchar(30) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Users') AND name='CreatedBy' AND max_length = -1)
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Users WHERE LEN(CreatedBy) > 100) THROW 50051, 'Users.CreatedBy longer than 100.', 1;
  ALTER TABLE dbo.Users ALTER COLUMN CreatedBy nvarchar(100) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Users') AND name='CreatedOn' AND TYPE_NAME(user_type_id)='varchar')
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Users WHERE NULLIF(LTRIM(RTRIM(CreatedOn)),'') IS NOT NULL AND TRY_CONVERT(datetime2(0), CreatedOn) IS NULL) THROW 50052, 'Users.CreatedOn has invalid dates.', 1;
  UPDATE dbo.Users SET CreatedOn = CONVERT(varchar(19), TRY_CONVERT(datetime2(0), NULLIF(LTRIM(RTRIM(CreatedOn)),'')), 126) WHERE CreatedOn IS NOT NULL;
  ALTER TABLE dbo.Users ALTER COLUMN CreatedOn datetime2(0) NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Users_Role')
  ALTER TABLE dbo.Users ADD CONSTRAINT CK_Users_Role CHECK (Role IS NULL OR Role IN ('Admin','HR'));
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Users_Employee')
  ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT FK_Users_Employee FOREIGN KEY (EmployeeUid) REFERENCES dbo.Employee (uid);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Users_EmployeeId' AND object_id=OBJECT_ID('dbo.Users'))
  CREATE UNIQUE INDEX UX_Users_EmployeeId ON dbo.Users (EmployeeUid) WHERE EmployeeUid IS NOT NULL;
GO

/* ---- LeaveRequests: keys, indexes, checks ---- */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_LeaveRequests_Employee')
  ALTER TABLE dbo.LeaveRequests WITH CHECK ADD CONSTRAINT FK_LeaveRequests_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (uid);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_LeaveRequests_LeaveTypes')
  ALTER TABLE dbo.LeaveRequests WITH CHECK ADD CONSTRAINT FK_LeaveRequests_LeaveTypes FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_LeaveRequests_EmployeeDocuments')
  ALTER TABLE dbo.LeaveRequests WITH CHECK ADD CONSTRAINT FK_LeaveRequests_EmployeeDocuments FOREIGN KEY (DocumentId) REFERENCES dbo.EmployeeDocuments (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_LeaveRequests_Users')
  ALTER TABLE dbo.LeaveRequests WITH CHECK ADD CONSTRAINT FK_LeaveRequests_Users FOREIGN KEY (ApprovedByUserId) REFERENCES dbo.Users (uid);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_LeaveRequests_Dates')
  ALTER TABLE dbo.LeaveRequests ADD CONSTRAINT CK_LeaveRequests_Dates CHECK (ToDate >= FromDate);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_LeaveRequests_HalfDay')
  ALTER TABLE dbo.LeaveRequests ADD CONSTRAINT CK_LeaveRequests_HalfDay CHECK (IsHalfDay = 0 OR (FromDate = ToDate AND TotalDays = 0.5));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_LeaveRequests_Status')
  ALTER TABLE dbo.LeaveRequests ADD CONSTRAINT CK_LeaveRequests_Status CHECK (Status IN ('Pending','Approved','Rejected','Cancelled'));
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LeaveRequests_Employee_FromDate' AND object_id=OBJECT_ID('dbo.LeaveRequests'))
  CREATE INDEX IX_LeaveRequests_Employee_FromDate ON dbo.LeaveRequests (EmployeeId, FromDate) INCLUDE (ToDate, Status, LeaveTypeId, TotalDays);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LeaveRequests_LeaveTypeId' AND object_id=OBJECT_ID('dbo.LeaveRequests'))
  CREATE INDEX IX_LeaveRequests_LeaveTypeId ON dbo.LeaveRequests (LeaveTypeId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LeaveRequests_DocumentId' AND object_id=OBJECT_ID('dbo.LeaveRequests'))
  CREATE INDEX IX_LeaveRequests_DocumentId ON dbo.LeaveRequests (DocumentId) WHERE DocumentId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LeaveRequests_ApprovedByUserId' AND object_id=OBJECT_ID('dbo.LeaveRequests'))
  CREATE INDEX IX_LeaveRequests_ApprovedByUserId ON dbo.LeaveRequests (ApprovedByUserId) WHERE ApprovedByUserId IS NOT NULL;
-- exact double submit blocked; true overlaps (e.g. requests 21/22) are checked in the save transaction under sp_getapplock (S22)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_LeaveRequests_Open_Duplicate' AND object_id=OBJECT_ID('dbo.LeaveRequests'))
  CREATE UNIQUE INDEX UX_LeaveRequests_Open_Duplicate ON dbo.LeaveRequests (EmployeeId, FromDate, ToDate, LeaveTypeId) WHERE Status IN ('Pending','Approved');
GO

/* ---- LeaveEntitlementOverrides ---- */
IF OBJECT_ID('dbo.LeaveEntitlementOverrides','U') IS NULL
CREATE TABLE dbo.LeaveEntitlementOverrides (
  Id                int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveEntitlementOverrides PRIMARY KEY,
  EmployeeId        int NOT NULL,
  LeaveTypeId       int NOT NULL,
  [Year]            int NOT NULL,
  AdjustmentDays    decimal(5,2) NULL,
  OverrideTotalDays decimal(5,2) NULL,
  Reason            nvarchar(500) NOT NULL,
  ApprovedByUserId  int NOT NULL,
  CreatedAt         datetime2(0) NOT NULL CONSTRAINT DF_LeaveEntitlementOverrides_CreatedAt DEFAULT sysdatetime(),
  CreatedBy         nvarchar(100) NULL,
  RequestId         uniqueidentifier NULL,
  CONSTRAINT FK_LeaveEntitlementOverrides_Employee    FOREIGN KEY (EmployeeId)       REFERENCES dbo.Employee (uid),
  CONSTRAINT FK_LeaveEntitlementOverrides_LeaveTypes  FOREIGN KEY (LeaveTypeId)      REFERENCES dbo.LeaveTypes (Id),
  CONSTRAINT FK_LeaveEntitlementOverrides_Users       FOREIGN KEY (ApprovedByUserId) REFERENCES dbo.Users (uid),
  CONSTRAINT CK_LeaveEntitlementOverrides_OneValue CHECK (
    (CASE WHEN AdjustmentDays IS NULL THEN 0 ELSE 1 END + CASE WHEN OverrideTotalDays IS NULL THEN 0 ELSE 1 END) = 1)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LeaveEntitlementOverrides_Employee_Year' AND object_id=OBJECT_ID('dbo.LeaveEntitlementOverrides'))
  CREATE INDEX IX_LeaveEntitlementOverrides_Employee_Year ON dbo.LeaveEntitlementOverrides (EmployeeId, [Year], LeaveTypeId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LeaveEntitlementOverrides_LeaveTypeId' AND object_id=OBJECT_ID('dbo.LeaveEntitlementOverrides'))
  CREATE INDEX IX_LeaveEntitlementOverrides_LeaveTypeId ON dbo.LeaveEntitlementOverrides (LeaveTypeId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LeaveEntitlementOverrides_ApprovedByUserId' AND object_id=OBJECT_ID('dbo.LeaveEntitlementOverrides'))
  CREATE INDEX IX_LeaveEntitlementOverrides_ApprovedByUserId ON dbo.LeaveEntitlementOverrides (ApprovedByUserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_LeaveEntitlementOverrides_Total' AND object_id=OBJECT_ID('dbo.LeaveEntitlementOverrides'))
  CREATE UNIQUE INDEX UX_LeaveEntitlementOverrides_Total ON dbo.LeaveEntitlementOverrides (EmployeeId, LeaveTypeId, [Year]) WHERE OverrideTotalDays IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_LeaveEntitlementOverrides_RequestId' AND object_id=OBJECT_ID('dbo.LeaveEntitlementOverrides'))
  CREATE UNIQUE INDEX UX_LeaveEntitlementOverrides_RequestId ON dbo.LeaveEntitlementOverrides (RequestId) WHERE RequestId IS NOT NULL;
GO

/* ---- CarryforwardLeaves: rebuilt (empty) ---- */
IF COL_LENGTH('dbo.CarryforwardLeaves','LeaveTypeId') IS NULL
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.CarryforwardLeaves) THROW 50060, 'CarryforwardLeaves is not empty; rebuild refused.', 1;
  DROP TABLE dbo.CarryforwardLeaves;
  CREATE TABLE dbo.CarryforwardLeaves (
    Id               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CarryforwardLeaves PRIMARY KEY,
    EmployeeId       int NOT NULL,
    LeaveTypeId      int NOT NULL,
    FromYear         int NOT NULL,
    ToYear           int NOT NULL,
    CarryForwardDays decimal(5,2) NOT NULL,
    Description      nvarchar(500) NULL,
    CreatedAt        datetime2(0) NOT NULL CONSTRAINT DF_CarryforwardLeaves_CreatedAt DEFAULT sysdatetime(),
    CreatedBy        nvarchar(100) NULL,
    ModifiedAt       datetime2(0) NULL,
    ModifiedBy       nvarchar(100) NULL,
    CONSTRAINT FK_CarryforwardLeaves_Employee   FOREIGN KEY (EmployeeId)  REFERENCES dbo.Employee (uid),
    CONSTRAINT FK_CarryforwardLeaves_LeaveTypes FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes (Id),
    CONSTRAINT CK_CarryforwardLeaves_Years CHECK (ToYear = FromYear + 1),
    CONSTRAINT CK_CarryforwardLeaves_Days  CHECK (CarryForwardDays >= 0)
  );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_CarryforwardLeaves_Employee_Type_FromYear' AND object_id=OBJECT_ID('dbo.CarryforwardLeaves'))
  CREATE UNIQUE INDEX UX_CarryforwardLeaves_Employee_Type_FromYear ON dbo.CarryforwardLeaves (EmployeeId, LeaveTypeId, FromYear);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_CarryforwardLeaves_LeaveTypeId' AND object_id=OBJECT_ID('dbo.CarryforwardLeaves'))
  CREATE INDEX IX_CarryforwardLeaves_LeaveTypeId ON dbo.CarryforwardLeaves (LeaveTypeId);
GO

/* ---- view: leave balances (derived, never stored, S18) ---- */
CREATE OR ALTER VIEW dbo.vLeaveBalances AS
SELECT e.uid AS EmployeeId, q.LeaveTypeId, q.[Year],
       Entitled  = COALESCE(ov.OverrideTotalDays, q.TotalDays) + COALESCE(adj.AdjustmentDays, 0) + COALESCE(cf.CarryForwardDays, 0),
       Taken     = COALESCE(tk.Days, 0),
       Pending   = COALESCE(pd.Days, 0),
       Remaining = COALESCE(ov.OverrideTotalDays, q.TotalDays) + COALESCE(adj.AdjustmentDays, 0) + COALESCE(cf.CarryForwardDays, 0) - COALESCE(tk.Days, 0)
FROM dbo.Employee e
JOIN dbo.Projects p ON p.Id = e.ProjectId
CROSS JOIN dbo.LeaveQuota q
OUTER APPLY (SELECT TOP (1) o.OverrideTotalDays FROM dbo.LeaveEntitlementOverrides o
             WHERE o.EmployeeId = e.uid AND o.LeaveTypeId = q.LeaveTypeId AND o.[Year] = q.[Year] AND o.OverrideTotalDays IS NOT NULL) ov
OUTER APPLY (SELECT SUM(o.AdjustmentDays) AS AdjustmentDays FROM dbo.LeaveEntitlementOverrides o
             WHERE o.EmployeeId = e.uid AND o.LeaveTypeId = q.LeaveTypeId AND o.[Year] = q.[Year] AND o.AdjustmentDays IS NOT NULL) adj
OUTER APPLY (SELECT SUM(c.CarryForwardDays) AS CarryForwardDays FROM dbo.CarryforwardLeaves c
             WHERE c.EmployeeId = e.uid AND c.LeaveTypeId = q.LeaveTypeId AND c.ToYear = q.[Year]) cf
OUTER APPLY (SELECT SUM(r.TotalDays) AS Days FROM dbo.LeaveRequests r
             WHERE r.EmployeeId = e.uid AND r.LeaveTypeId = q.LeaveTypeId AND r.Status = 'Approved' AND YEAR(r.FromDate) = q.[Year]) tk
OUTER APPLY (SELECT SUM(r.TotalDays) AS Days FROM dbo.LeaveRequests r
             WHERE r.EmployeeId = e.uid AND r.LeaveTypeId = q.LeaveTypeId AND r.Status = 'Pending' AND YEAR(r.FromDate) = q.[Year]) pd
WHERE COALESCE(e.IsLeaveManagedOverride, p.HasLeaveManagement) = 1;
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261123000003_Redesign_Phase3_LeaveAndUsers', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261123000003_Redesign_Phase3_LeaveAndUsers');
GO
COMMIT TRANSACTION;
GO
