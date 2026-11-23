/*
  HRMS database redesign - phase 4: attendance, schedule, notes, payroll and document changes.
  Source: docs/db/db_changes.md v3.4-draft ("Migration order" step 4). Needs 01-03 first.
  Re-runnable. Run with sqlcmd -b -I (see 01_lookups_and_keys.sql). Back up first (S19).

  Expand only: Attendance keeps its old columns beside the new ones (the new employee key is EmployeeUid,
  the payslip month number is MonthNo, TaxRules.TaxYearStart sits beside TaxYear). The contract phase
  (drop old columns, rename temporary ones) is a separate step after the app switch (draft step 6).
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
  SET ANSI_WARNINGS ON; SET ANSI_PADDING ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
  DECLARE @dc sysname = (SELECT dc.name FROM sys.default_constraints dc
                         JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                         WHERE dc.parent_object_id = OBJECT_ID(@tbl) AND c.name = @col);
  DECLARE @sql nvarchar(600) = N'ALTER TABLE ' + @tbl + N' DROP CONSTRAINT ' + QUOTENAME(@dc);
  IF @dc IS NOT NULL EXEC (@sql);
END
GO

/* =====================================================================================
   Shifts, HalfDayRequests
   ===================================================================================== */
IF OBJECT_ID('dbo.Shifts','U') IS NULL
CREATE TABLE dbo.Shifts (
  Id                int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Shifts PRIMARY KEY,
  Name              nvarchar(100) NOT NULL,
  StartTime         time(0) NOT NULL,
  EndTime           time(0) NOT NULL,
  IsOvernight       AS (CONVERT(bit, CASE WHEN EndTime <= StartTime THEN 1 ELSE 0 END)) PERSISTED NOT NULL,
  BreakMinutes      smallint NOT NULL CONSTRAINT DF_Shifts_BreakMinutes DEFAULT 0,
  MinFullDayMinutes smallint NOT NULL CONSTRAINT DF_Shifts_MinFullDayMinutes DEFAULT 420,   -- 7 hours (HR)
  GraceLateMinutes  tinyint  NOT NULL CONSTRAINT DF_Shifts_GraceLateMinutes DEFAULT 1,
  GraceEarlyMinutes tinyint  NOT NULL CONSTRAINT DF_Shifts_GraceEarlyMinutes DEFAULT 1,
  HalfDayMinMinutes smallint NULL,
  HalfDayCutoffTime time(0) NULL,
  IsActive          bit NOT NULL CONSTRAINT DF_Shifts_IsActive DEFAULT 1,
  CreatedAt         datetime2(0) NOT NULL CONSTRAINT DF_Shifts_CreatedAt DEFAULT sysdatetime(),
  CreatedBy         nvarchar(100) NULL,
  ModifiedAt        datetime2(0) NULL,
  ModifiedBy        nvarchar(100) NULL,
  CONSTRAINT CK_Shifts_Times   CHECK (StartTime <> EndTime),
  CONSTRAINT CK_Shifts_Minutes CHECK (MinFullDayMinutes > 0 AND (HalfDayMinMinutes IS NULL OR HalfDayMinMinutes < MinFullDayMinutes))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Shifts_Name' AND object_id=OBJECT_ID('dbo.Shifts'))
  CREATE UNIQUE INDEX UX_Shifts_Name ON dbo.Shifts (Name);
GO

IF OBJECT_ID('dbo.HalfDayRequests','U') IS NULL
CREATE TABLE dbo.HalfDayRequests (
  Id               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_HalfDayRequests PRIMARY KEY,
  EmployeeId       int NOT NULL,
  HalfDayDate      date NOT NULL,
  HalfType         varchar(10) NOT NULL,
  ExpectedTime     time(0) NULL,
  Reason           nvarchar(500) NOT NULL,
  DocumentId       int NULL,
  RequiresCheckout bit NOT NULL CONSTRAINT DF_HalfDayRequests_RequiresCheckout DEFAULT 1,
  LeaveRequestId   int NULL,                                 -- B1 pending
  Status           varchar(10) NOT NULL CONSTRAINT DF_HalfDayRequests_Status DEFAULT 'Pending',
  ApprovedByUserId int NULL,
  ApprovedAt       datetime2(0) NULL,
  ReviewerComments nvarchar(500) NULL,
  Notes            nvarchar(1000) NULL,
  CreatedAt        datetime2(0) NOT NULL CONSTRAINT DF_HalfDayRequests_CreatedAt DEFAULT sysdatetime(),
  CreatedBy        nvarchar(100) NULL,
  ModifiedAt       datetime2(0) NULL,
  ModifiedBy       nvarchar(100) NULL,
  RowVersion       rowversion NOT NULL,
  CONSTRAINT FK_HalfDayRequests_Employee          FOREIGN KEY (EmployeeId)       REFERENCES dbo.Employee (uid),
  CONSTRAINT FK_HalfDayRequests_EmployeeDocuments FOREIGN KEY (DocumentId)       REFERENCES dbo.EmployeeDocuments (Id),
  CONSTRAINT FK_HalfDayRequests_LeaveRequests     FOREIGN KEY (LeaveRequestId)   REFERENCES dbo.LeaveRequests (Id),
  CONSTRAINT FK_HalfDayRequests_Users             FOREIGN KEY (ApprovedByUserId) REFERENCES dbo.Users (uid),
  CONSTRAINT CK_HalfDayRequests_HalfType CHECK (HalfType IN ('FirstHalf','SecondHalf')),
  CONSTRAINT CK_HalfDayRequests_Status   CHECK (Status IN ('Pending','Approved','Rejected','Cancelled')),
  CONSTRAINT CK_HalfDayRequests_Approval CHECK (Status IN ('Pending','Cancelled') OR (ApprovedByUserId IS NOT NULL AND ApprovedAt IS NOT NULL))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_HalfDayRequests_Employee_Date' AND object_id=OBJECT_ID('dbo.HalfDayRequests'))
  CREATE UNIQUE INDEX UX_HalfDayRequests_Employee_Date ON dbo.HalfDayRequests (EmployeeId, HalfDayDate) WHERE Status IN ('Pending','Approved');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_HalfDayRequests_DocumentId' AND object_id=OBJECT_ID('dbo.HalfDayRequests'))
  CREATE INDEX IX_HalfDayRequests_DocumentId ON dbo.HalfDayRequests (DocumentId) WHERE DocumentId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_HalfDayRequests_ApprovedByUserId' AND object_id=OBJECT_ID('dbo.HalfDayRequests'))
  CREATE INDEX IX_HalfDayRequests_ApprovedByUserId ON dbo.HalfDayRequests (ApprovedByUserId) WHERE ApprovedByUserId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_HalfDayRequests_LeaveRequestId' AND object_id=OBJECT_ID('dbo.HalfDayRequests'))
  CREATE INDEX IX_HalfDayRequests_LeaveRequestId ON dbo.HalfDayRequests (LeaveRequestId) WHERE LeaveRequestId IS NOT NULL;
GO

/* =====================================================================================
   Attendance: expand (old columns stay until the contract phase)
   ===================================================================================== */
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Attendance') AND name='Comments' AND max_length <> 2000)
  ALTER TABLE dbo.Attendance ALTER COLUMN Comments nvarchar(1000) NULL;   -- becomes Notes at contract
GO
IF COL_LENGTH('dbo.Attendance','EmployeeUid')       IS NULL ALTER TABLE dbo.Attendance ADD EmployeeUid int NULL;      -- renamed EmployeeId NOT NULL at contract
IF COL_LENGTH('dbo.Attendance','ShiftDate')         IS NULL ALTER TABLE dbo.Attendance ADD ShiftDate date NULL;       -- NOT NULL at contract (replaces AttendanceDate)
IF COL_LENGTH('dbo.Attendance','ShiftId')           IS NULL ALTER TABLE dbo.Attendance ADD ShiftId int NULL;
IF COL_LENGTH('dbo.Attendance','ScheduledStartAt')  IS NULL ALTER TABLE dbo.Attendance ADD ScheduledStartAt datetime2(0) NULL;
IF COL_LENGTH('dbo.Attendance','ScheduledEndAt')    IS NULL ALTER TABLE dbo.Attendance ADD ScheduledEndAt datetime2(0) NULL;
IF COL_LENGTH('dbo.Attendance','CheckInAt')         IS NULL ALTER TABLE dbo.Attendance ADD CheckInAt datetime2(0) NULL;
IF COL_LENGTH('dbo.Attendance','CheckOutAt')        IS NULL ALTER TABLE dbo.Attendance ADD CheckOutAt datetime2(0) NULL;
IF COL_LENGTH('dbo.Attendance','CheckOutSource')    IS NULL ALTER TABLE dbo.Attendance ADD CheckOutSource varchar(15) NOT NULL CONSTRAINT DF_Attendance_CheckOutSource DEFAULT 'None';
IF COL_LENGTH('dbo.Attendance','LateMinutes')       IS NULL ALTER TABLE dbo.Attendance ADD LateMinutes int NOT NULL CONSTRAINT DF_Attendance_LateMinutes DEFAULT 0;
IF COL_LENGTH('dbo.Attendance','EarlyLeaveMinutes') IS NULL ALTER TABLE dbo.Attendance ADD EarlyLeaveMinutes int NOT NULL CONSTRAINT DF_Attendance_EarlyLeaveMinutes DEFAULT 0;
IF COL_LENGTH('dbo.Attendance','WorkedMinutes')     IS NULL ALTER TABLE dbo.Attendance ADD WorkedMinutes int NOT NULL CONSTRAINT DF_Attendance_WorkedMinutes DEFAULT 0;
IF COL_LENGTH('dbo.Attendance','PayableMinutes')    IS NULL ALTER TABLE dbo.Attendance ADD PayableMinutes int NOT NULL CONSTRAINT DF_Attendance_PayableMinutes DEFAULT 0;
IF COL_LENGTH('dbo.Attendance','DayStatus')         IS NULL ALTER TABLE dbo.Attendance ADD DayStatus varchar(20) NOT NULL CONSTRAINT DF_Attendance_DayStatus DEFAULT 'Absent';
GO
IF COL_LENGTH('dbo.Attendance','IsLate')            IS NULL ALTER TABLE dbo.Attendance ADD IsLate AS (CONVERT(bit, CASE WHEN LateMinutes > 0 THEN 1 ELSE 0 END)) PERSISTED NOT NULL;
IF COL_LENGTH('dbo.Attendance','HalfDayRequestId')  IS NULL ALTER TABLE dbo.Attendance ADD HalfDayRequestId int NULL;
IF COL_LENGTH('dbo.Attendance','LeaveRequestId')    IS NULL ALTER TABLE dbo.Attendance ADD LeaveRequestId int NULL;
IF COL_LENGTH('dbo.Attendance','IsLocked')          IS NULL ALTER TABLE dbo.Attendance ADD IsLocked bit NOT NULL CONSTRAINT DF_Attendance_IsLocked DEFAULT 0;
IF COL_LENGTH('dbo.Attendance','ProcessedAt')       IS NULL ALTER TABLE dbo.Attendance ADD ProcessedAt datetime2(0) NULL;
IF COL_LENGTH('dbo.Attendance','CreatedAt')         IS NULL ALTER TABLE dbo.Attendance ADD CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Attendance_CreatedAt DEFAULT sysdatetime();
IF COL_LENGTH('dbo.Attendance','CreatedBy')         IS NULL ALTER TABLE dbo.Attendance ADD CreatedBy nvarchar(100) NULL;
IF COL_LENGTH('dbo.Attendance','ModifiedAt')        IS NULL ALTER TABLE dbo.Attendance ADD ModifiedAt datetime2(0) NULL;
IF COL_LENGTH('dbo.Attendance','ModifiedBy')        IS NULL ALTER TABLE dbo.Attendance ADD ModifiedBy nvarchar(100) NULL;
IF COL_LENGTH('dbo.Attendance','RowVersion')        IS NULL ALTER TABLE dbo.Attendance ADD RowVersion rowversion NOT NULL;
GO

-- backfill the new employee key from the old string key (the table is empty on the real server; this keeps a copy with rows correct)
UPDATE a SET EmployeeUid = e.uid FROM dbo.Attendance a JOIN dbo.Employee e ON e.EmployeeID = a.EmployeeID WHERE a.EmployeeUid IS NULL;
UPDATE dbo.Attendance SET ShiftDate = AttendanceDate WHERE ShiftDate IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Attendance_Employee')
  ALTER TABLE dbo.Attendance ADD CONSTRAINT FK_Attendance_Employee FOREIGN KEY (EmployeeUid) REFERENCES dbo.Employee (uid);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Attendance_Shifts')
  ALTER TABLE dbo.Attendance ADD CONSTRAINT FK_Attendance_Shifts FOREIGN KEY (ShiftId) REFERENCES dbo.Shifts (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Attendance_HalfDayRequests')
  ALTER TABLE dbo.Attendance ADD CONSTRAINT FK_Attendance_HalfDayRequests FOREIGN KEY (HalfDayRequestId) REFERENCES dbo.HalfDayRequests (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Attendance_LeaveRequests')
  ALTER TABLE dbo.Attendance ADD CONSTRAINT FK_Attendance_LeaveRequests FOREIGN KEY (LeaveRequestId) REFERENCES dbo.LeaveRequests (Id);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Attendance_DayStatus')
  ALTER TABLE dbo.Attendance ADD CONSTRAINT CK_Attendance_DayStatus CHECK (DayStatus IN ('Present','HalfDay','Hourly','Absent','Leave','Holiday','WeeklyOff','MissingCheckout'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Attendance_CheckOutSource')
  ALTER TABLE dbo.Attendance ADD CONSTRAINT CK_Attendance_CheckOutSource CHECK (CheckOutSource IN ('Punch','HRCorrection','Auto','None'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Attendance_CheckTimes')
  ALTER TABLE dbo.Attendance ADD CONSTRAINT CK_Attendance_CheckTimes CHECK (CheckOutAt IS NULL OR CheckInAt IS NULL OR CheckOutAt > CheckInAt);
GO
-- unique key is filtered while the old columns still exist: rows written by the old app have no EmployeeUid/ShiftDate yet
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Attendance_Employee_ShiftDate' AND object_id=OBJECT_ID('dbo.Attendance'))
  CREATE UNIQUE INDEX UX_Attendance_Employee_ShiftDate ON dbo.Attendance (EmployeeUid, ShiftDate) WHERE EmployeeUid IS NOT NULL AND ShiftDate IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Attendance_ShiftDate' AND object_id=OBJECT_ID('dbo.Attendance'))
  CREATE INDEX IX_Attendance_ShiftDate ON dbo.Attendance (ShiftDate) INCLUDE (EmployeeUid, DayStatus, PayableMinutes);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Attendance_ShiftId' AND object_id=OBJECT_ID('dbo.Attendance'))
  CREATE INDEX IX_Attendance_ShiftId ON dbo.Attendance (ShiftId) WHERE ShiftId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Attendance_HalfDayRequestId' AND object_id=OBJECT_ID('dbo.Attendance'))
  CREATE INDEX IX_Attendance_HalfDayRequestId ON dbo.Attendance (HalfDayRequestId) WHERE HalfDayRequestId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Attendance_LeaveRequestId' AND object_id=OBJECT_ID('dbo.Attendance'))
  CREATE INDEX IX_Attendance_LeaveRequestId ON dbo.Attendance (LeaveRequestId) WHERE LeaveRequestId IS NOT NULL;
GO

/* =====================================================================================
   AttendanceUploadLogs
   ===================================================================================== */
UPDATE dbo.AttendanceUploadLogs
SET Status = CASE WHEN ISNULL(SuccessCount, 0) = 0 THEN 'Failed' ELSE 'Completed' END     -- proposed rule; review by hand
WHERE Status IS NULL;
UPDATE dbo.AttendanceUploadLogs SET SuccessCount = 0 WHERE SuccessCount IS NULL;
UPDATE dbo.AttendanceUploadLogs SET FailureCount = 0 WHERE FailureCount IS NULL;
UPDATE dbo.AttendanceUploadLogs SET TotalRows    = 0 WHERE TotalRows    IS NULL;
IF EXISTS (SELECT 1 FROM dbo.AttendanceUploadLogs WHERE Status NOT IN ('Processing','Completed','CompletedWithErrors','Failed'))
  THROW 50070, 'AttendanceUploadLogs.Status has values outside the allowed list.', 1;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AttendanceUploadLogs') AND name='UploadDate' AND TYPE_NAME(user_type_id)='datetime')
BEGIN
  EXEC #DropDefault 'dbo.AttendanceUploadLogs', 'UploadDate';
  UPDATE dbo.AttendanceUploadLogs SET UploadDate = sysdatetime() WHERE UploadDate IS NULL;
  ALTER TABLE dbo.AttendanceUploadLogs ALTER COLUMN UploadDate datetime2(0) NOT NULL;
  ALTER TABLE dbo.AttendanceUploadLogs ADD CONSTRAINT DF_AttendanceUploadLogs_UploadDate DEFAULT sysdatetime() FOR UploadDate;
END
IF COLUMNPROPERTY(OBJECT_ID('dbo.AttendanceUploadLogs'),'FileName','AllowsNull') = 1
  ALTER TABLE dbo.AttendanceUploadLogs ALTER COLUMN FileName nvarchar(255) NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AttendanceUploadLogs') AND name='Status' AND TYPE_NAME(user_type_id)='nvarchar')
BEGIN
  ALTER TABLE dbo.AttendanceUploadLogs ALTER COLUMN Status varchar(20) NOT NULL;
  ALTER TABLE dbo.AttendanceUploadLogs ADD CONSTRAINT DF_AttendanceUploadLogs_Status DEFAULT 'Processing' FOR Status;
END
IF COLUMNPROPERTY(OBJECT_ID('dbo.AttendanceUploadLogs'),'SuccessCount','AllowsNull') = 1
BEGIN
  ALTER TABLE dbo.AttendanceUploadLogs ALTER COLUMN SuccessCount int NOT NULL;
  ALTER TABLE dbo.AttendanceUploadLogs ADD CONSTRAINT DF_AttendanceUploadLogs_SuccessCount DEFAULT 0 FOR SuccessCount;
END
IF COLUMNPROPERTY(OBJECT_ID('dbo.AttendanceUploadLogs'),'FailureCount','AllowsNull') = 1
BEGIN
  ALTER TABLE dbo.AttendanceUploadLogs ALTER COLUMN FailureCount int NOT NULL;
  ALTER TABLE dbo.AttendanceUploadLogs ADD CONSTRAINT DF_AttendanceUploadLogs_FailureCount DEFAULT 0 FOR FailureCount;
END
IF COLUMNPROPERTY(OBJECT_ID('dbo.AttendanceUploadLogs'),'TotalRows','AllowsNull') = 1
BEGIN
  ALTER TABLE dbo.AttendanceUploadLogs ALTER COLUMN TotalRows int NOT NULL;
  ALTER TABLE dbo.AttendanceUploadLogs ADD CONSTRAINT DF_AttendanceUploadLogs_TotalRows DEFAULT 0 FOR TotalRows;
END
IF COL_LENGTH('dbo.AttendanceUploadLogs','FileHash') IS NULL ALTER TABLE dbo.AttendanceUploadLogs ADD FileHash char(64) NULL;   -- T3
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_AttendanceUploadLogs_Status')
  ALTER TABLE dbo.AttendanceUploadLogs ADD CONSTRAINT CK_AttendanceUploadLogs_Status CHECK (Status IN ('Processing','Completed','CompletedWithErrors','Failed'));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_AttendanceUploadLogs_FileHash' AND object_id=OBJECT_ID('dbo.AttendanceUploadLogs'))
  CREATE UNIQUE INDEX UX_AttendanceUploadLogs_FileHash ON dbo.AttendanceUploadLogs (FileHash) WHERE FileHash IS NOT NULL AND Status <> 'Failed';
GO

/* =====================================================================================
   AttendancePunches, AttendanceCorrections, RecordNotes
   ===================================================================================== */
IF OBJECT_ID('dbo.AttendancePunches','U') IS NULL
CREATE TABLE dbo.AttendancePunches (
  Id              bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AttendancePunches PRIMARY KEY,
  EmployeeId      int NULL,
  RawEmployeeCode varchar(50) NOT NULL,
  PunchAt         datetime2(0) NOT NULL,
  Direction       varchar(7) NOT NULL CONSTRAINT DF_AttendancePunches_Direction DEFAULT 'Unknown',
  Source          varchar(10) NOT NULL,
  DeviceId        nvarchar(50) NULL,
  UploadLogId     int NULL,
  AttendanceId    int NULL,
  IsIgnored       bit NOT NULL CONSTRAINT DF_AttendancePunches_IsIgnored DEFAULT 0,
  IgnoreReason    nvarchar(200) NULL,
  CreatedAt       datetime2(0) NOT NULL CONSTRAINT DF_AttendancePunches_CreatedAt DEFAULT sysdatetime(),
  CreatedBy       nvarchar(100) NULL,
  CONSTRAINT FK_AttendancePunches_Employee           FOREIGN KEY (EmployeeId)   REFERENCES dbo.Employee (uid),
  CONSTRAINT FK_AttendancePunches_AttendanceUploadLogs FOREIGN KEY (UploadLogId) REFERENCES dbo.AttendanceUploadLogs (Id),
  CONSTRAINT FK_AttendancePunches_Attendance         FOREIGN KEY (AttendanceId) REFERENCES dbo.Attendance (AttendanceID),
  CONSTRAINT CK_AttendancePunches_Direction CHECK (Direction IN ('In','Out','Unknown')),
  CONSTRAINT CK_AttendancePunches_Source    CHECK (Source IN ('Device','Upload','Manual'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AttendancePunches_Employee_PunchAt' AND object_id=OBJECT_ID('dbo.AttendancePunches'))
  CREATE INDEX IX_AttendancePunches_Employee_PunchAt ON dbo.AttendancePunches (EmployeeId, PunchAt) INCLUDE (Direction, IsIgnored);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_AttendancePunches_Dedup' AND object_id=OBJECT_ID('dbo.AttendancePunches'))
  CREATE UNIQUE INDEX UX_AttendancePunches_Dedup ON dbo.AttendancePunches (RawEmployeeCode, PunchAt, Direction);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AttendancePunches_UploadLogId' AND object_id=OBJECT_ID('dbo.AttendancePunches'))
  CREATE INDEX IX_AttendancePunches_UploadLogId ON dbo.AttendancePunches (UploadLogId) WHERE UploadLogId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AttendancePunches_AttendanceId' AND object_id=OBJECT_ID('dbo.AttendancePunches'))
  CREATE INDEX IX_AttendancePunches_AttendanceId ON dbo.AttendancePunches (AttendanceId) WHERE AttendanceId IS NOT NULL;
GO

IF OBJECT_ID('dbo.AttendanceCorrections','U') IS NULL
CREATE TABLE dbo.AttendanceCorrections (
  Id                   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AttendanceCorrections PRIMARY KEY,
  AttendanceId         int NOT NULL,
  CorrectionType       varchar(20) NOT NULL,
  CorrectedCheckInAt   datetime2(0) NULL,
  CorrectedCheckOutAt  datetime2(0) NULL,
  OldDayStatus         varchar(20) NULL,
  NewDayStatus         varchar(20) NULL,
  ReportedByEmployeeAt datetime2(0) NULL,
  Reason               nvarchar(500) NOT NULL,
  RecordedByUserId     int NOT NULL,
  CreatedAt            datetime2(0) NOT NULL CONSTRAINT DF_AttendanceCorrections_CreatedAt DEFAULT sysdatetime(),
  RequestId            uniqueidentifier NULL,
  CONSTRAINT FK_AttendanceCorrections_Attendance FOREIGN KEY (AttendanceId)     REFERENCES dbo.Attendance (AttendanceID),
  CONSTRAINT FK_AttendanceCorrections_Users      FOREIGN KEY (RecordedByUserId) REFERENCES dbo.Users (uid),
  CONSTRAINT CK_AttendanceCorrections_Type CHECK (CorrectionType IN ('MissedCheckOut','MissedCheckIn','TimeChange','StatusChange')),
  CONSTRAINT CK_AttendanceCorrections_Values CHECK (
        (CorrectionType <> 'MissedCheckOut' OR CorrectedCheckOutAt IS NOT NULL)
    AND (CorrectionType <> 'MissedCheckIn'  OR CorrectedCheckInAt  IS NOT NULL)
    AND (CorrectionType <> 'StatusChange'   OR NewDayStatus        IS NOT NULL))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AttendanceCorrections_AttendanceId' AND object_id=OBJECT_ID('dbo.AttendanceCorrections'))
  CREATE INDEX IX_AttendanceCorrections_AttendanceId ON dbo.AttendanceCorrections (AttendanceId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AttendanceCorrections_RecordedByUserId' AND object_id=OBJECT_ID('dbo.AttendanceCorrections'))
  CREATE INDEX IX_AttendanceCorrections_RecordedByUserId ON dbo.AttendanceCorrections (RecordedByUserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_AttendanceCorrections_RequestId' AND object_id=OBJECT_ID('dbo.AttendanceCorrections'))
  CREATE UNIQUE INDEX UX_AttendanceCorrections_RequestId ON dbo.AttendanceCorrections (RequestId) WHERE RequestId IS NOT NULL;
GO

IF OBJECT_ID('dbo.RecordNotes','U') IS NULL
CREATE TABLE dbo.RecordNotes (
  Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecordNotes PRIMARY KEY,
  AttendanceId   int NULL,
  EmployeeId     int NULL,
  LeaveRequestId int NULL,
  Note           nvarchar(1000) NOT NULL,
  IsDeleted      bit NOT NULL CONSTRAINT DF_RecordNotes_IsDeleted DEFAULT 0,
  CreatedAt      datetime2(0) NOT NULL CONSTRAINT DF_RecordNotes_CreatedAt DEFAULT sysdatetime(),
  CreatedBy      nvarchar(100) NULL,
  RequestId      uniqueidentifier NULL,
  CONSTRAINT FK_RecordNotes_Attendance    FOREIGN KEY (AttendanceId)   REFERENCES dbo.Attendance (AttendanceID),
  CONSTRAINT FK_RecordNotes_Employee      FOREIGN KEY (EmployeeId)     REFERENCES dbo.Employee (uid),
  CONSTRAINT FK_RecordNotes_LeaveRequests FOREIGN KEY (LeaveRequestId) REFERENCES dbo.LeaveRequests (Id),
  CONSTRAINT CK_RecordNotes_OneTarget CHECK (
    (CASE WHEN AttendanceId   IS NULL THEN 0 ELSE 1 END +
     CASE WHEN EmployeeId     IS NULL THEN 0 ELSE 1 END +
     CASE WHEN LeaveRequestId IS NULL THEN 0 ELSE 1 END) = 1),
  CONSTRAINT CK_RecordNotes_Note CHECK (LEN(LTRIM(RTRIM(Note))) > 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RecordNotes_AttendanceId' AND object_id=OBJECT_ID('dbo.RecordNotes'))
  CREATE INDEX IX_RecordNotes_AttendanceId ON dbo.RecordNotes (AttendanceId, CreatedAt) WHERE AttendanceId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RecordNotes_EmployeeId' AND object_id=OBJECT_ID('dbo.RecordNotes'))
  CREATE INDEX IX_RecordNotes_EmployeeId ON dbo.RecordNotes (EmployeeId, CreatedAt) WHERE EmployeeId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RecordNotes_LeaveRequestId' AND object_id=OBJECT_ID('dbo.RecordNotes'))
  CREATE INDEX IX_RecordNotes_LeaveRequestId ON dbo.RecordNotes (LeaveRequestId, CreatedAt) WHERE LeaveRequestId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_RecordNotes_RequestId' AND object_id=OBJECT_ID('dbo.RecordNotes'))
  CREATE UNIQUE INDEX UX_RecordNotes_RequestId ON dbo.RecordNotes (RequestId) WHERE RequestId IS NOT NULL;
GO

/* =====================================================================================
   ShiftAssignments, RosterOverrides
   ===================================================================================== */
IF OBJECT_ID('dbo.ShiftAssignments','U') IS NULL
CREATE TABLE dbo.ShiftAssignments (
  Id                int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShiftAssignments PRIMARY KEY,
  EmployeeId        int NOT NULL,
  ShiftId           int NOT NULL,
  EffectiveFromDate date NOT NULL,
  EffectiveToDate   date NULL,
  DaysOfWeekMask    tinyint NOT NULL,
  WeeklyOffMask     tinyint NOT NULL CONSTRAINT DF_ShiftAssignments_WeeklyOffMask DEFAULT 0,
  Priority          tinyint NOT NULL CONSTRAINT DF_ShiftAssignments_Priority DEFAULT 0,
  Notes             nvarchar(1000) NULL,
  CreatedAt         datetime2(0) NOT NULL CONSTRAINT DF_ShiftAssignments_CreatedAt DEFAULT sysdatetime(),
  CreatedBy         nvarchar(100) NULL,
  ModifiedAt        datetime2(0) NULL,
  ModifiedBy        nvarchar(100) NULL,
  CONSTRAINT FK_ShiftAssignments_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (uid),
  CONSTRAINT FK_ShiftAssignments_Shifts   FOREIGN KEY (ShiftId)    REFERENCES dbo.Shifts (Id),
  CONSTRAINT CK_ShiftAssignments_Range CHECK (EffectiveToDate IS NULL OR EffectiveToDate >= EffectiveFromDate),
  CONSTRAINT CK_ShiftAssignments_Masks CHECK (DaysOfWeekMask BETWEEN 1 AND 127 AND WeeklyOffMask BETWEEN 0 AND 127 AND (DaysOfWeekMask & WeeklyOffMask) = 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ShiftAssignments_Employee_From' AND object_id=OBJECT_ID('dbo.ShiftAssignments'))
  CREATE INDEX IX_ShiftAssignments_Employee_From ON dbo.ShiftAssignments (EmployeeId, EffectiveFromDate) INCLUDE (EffectiveToDate, ShiftId, DaysOfWeekMask, WeeklyOffMask, Priority);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ShiftAssignments_Employee_From_Priority' AND object_id=OBJECT_ID('dbo.ShiftAssignments'))
  CREATE UNIQUE INDEX UX_ShiftAssignments_Employee_From_Priority ON dbo.ShiftAssignments (EmployeeId, EffectiveFromDate, Priority);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ShiftAssignments_ShiftId' AND object_id=OBJECT_ID('dbo.ShiftAssignments'))
  CREATE INDEX IX_ShiftAssignments_ShiftId ON dbo.ShiftAssignments (ShiftId);
GO

IF OBJECT_ID('dbo.RosterOverrides','U') IS NULL
CREATE TABLE dbo.RosterOverrides (
  Id                 int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RosterOverrides PRIMARY KEY,
  EmployeeId         int NOT NULL,
  OverrideDate       date NOT NULL,
  OverrideType       varchar(15) NOT NULL,
  ShiftId            int NULL,
  SwapWithEmployeeId int NULL,
  Reason             nvarchar(500) NULL,
  Notes              nvarchar(1000) NULL,
  ApprovedByUserId   int NULL,
  ApprovedAt         datetime2(0) NULL,
  CreatedAt          datetime2(0) NOT NULL CONSTRAINT DF_RosterOverrides_CreatedAt DEFAULT sysdatetime(),
  CreatedBy          nvarchar(100) NULL,
  ModifiedAt         datetime2(0) NULL,
  ModifiedBy         nvarchar(100) NULL,
  CONSTRAINT FK_RosterOverrides_Employee      FOREIGN KEY (EmployeeId)         REFERENCES dbo.Employee (uid),
  CONSTRAINT FK_RosterOverrides_Shifts        FOREIGN KEY (ShiftId)            REFERENCES dbo.Shifts (Id),
  CONSTRAINT FK_RosterOverrides_Employee_Swap FOREIGN KEY (SwapWithEmployeeId) REFERENCES dbo.Employee (uid),
  CONSTRAINT FK_RosterOverrides_Users         FOREIGN KEY (ApprovedByUserId)   REFERENCES dbo.Users (uid),
  CONSTRAINT CK_RosterOverrides_Type  CHECK (OverrideType IN ('ShiftChange','Off','Swap','ExtraDuty')),
  CONSTRAINT CK_RosterOverrides_Shift CHECK ((OverrideType = 'Off' AND ShiftId IS NULL) OR (OverrideType <> 'Off' AND ShiftId IS NOT NULL)),
  CONSTRAINT CK_RosterOverrides_Swap  CHECK (OverrideType <> 'Swap' OR SwapWithEmployeeId IS NOT NULL)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_RosterOverrides_Employee_Date' AND object_id=OBJECT_ID('dbo.RosterOverrides'))
  CREATE UNIQUE INDEX UX_RosterOverrides_Employee_Date ON dbo.RosterOverrides (EmployeeId, OverrideDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RosterOverrides_ShiftId' AND object_id=OBJECT_ID('dbo.RosterOverrides'))
  CREATE INDEX IX_RosterOverrides_ShiftId ON dbo.RosterOverrides (ShiftId) WHERE ShiftId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RosterOverrides_SwapWithEmployeeId' AND object_id=OBJECT_ID('dbo.RosterOverrides'))
  CREATE INDEX IX_RosterOverrides_SwapWithEmployeeId ON dbo.RosterOverrides (SwapWithEmployeeId) WHERE SwapWithEmployeeId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RosterOverrides_ApprovedByUserId' AND object_id=OBJECT_ID('dbo.RosterOverrides'))
  CREATE INDEX IX_RosterOverrides_ApprovedByUserId ON dbo.RosterOverrides (ApprovedByUserId) WHERE ApprovedByUserId IS NOT NULL;
GO

/* =====================================================================================
   GazettedHolidays: rebuilt (empty)
   ===================================================================================== */
IF COL_LENGTH('dbo.GazettedHolidays','HolidayName') IS NULL
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.GazettedHolidays) THROW 50080, 'GazettedHolidays is not empty; rebuild refused.', 1;
  DROP TABLE dbo.GazettedHolidays;
  CREATE TABLE dbo.GazettedHolidays (
    Id          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_GazettedHolidays PRIMARY KEY,
    HolidayDate date NOT NULL,
    HolidayName nvarchar(100) NOT NULL,
    Description nvarchar(255) NULL,
    ProjectId   int NULL,
    IsOptional  bit NOT NULL CONSTRAINT DF_GazettedHolidays_IsOptional DEFAULT 0,
    CreatedAt   datetime2(0) NOT NULL CONSTRAINT DF_GazettedHolidays_CreatedAt DEFAULT sysdatetime(),
    CreatedBy   nvarchar(100) NULL,
    ModifiedAt  datetime2(0) NULL,
    ModifiedBy  nvarchar(100) NULL,
    CONSTRAINT FK_GazettedHolidays_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id)
  );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_GazettedHolidays_Date_Project' AND object_id=OBJECT_ID('dbo.GazettedHolidays'))
  CREATE UNIQUE INDEX UX_GazettedHolidays_Date_Project ON dbo.GazettedHolidays (HolidayDate, ProjectId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_GazettedHolidays_ProjectId' AND object_id=OBJECT_ID('dbo.GazettedHolidays'))
  CREATE INDEX IX_GazettedHolidays_ProjectId ON dbo.GazettedHolidays (ProjectId) WHERE ProjectId IS NOT NULL;
GO

/* =====================================================================================
   EmployeeDocuments
   ===================================================================================== */
IF COL_LENGTH('dbo.EmployeeDocuments','DocumentType') IS NULL ALTER TABLE dbo.EmployeeDocuments ADD DocumentType varchar(30) NULL;
IF COL_LENGTH('dbo.EmployeeDocuments','FileHash')     IS NULL ALTER TABLE dbo.EmployeeDocuments ADD FileHash char(64) NULL;      -- T3
IF COL_LENGTH('dbo.EmployeeDocuments','UploadedBy')   IS NULL ALTER TABLE dbo.EmployeeDocuments ADD UploadedBy nvarchar(100) NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EmployeeDocuments') AND name='UploadedOn' AND TYPE_NAME(user_type_id)='datetime')
BEGIN
  EXEC #DropDefault 'dbo.EmployeeDocuments', 'UploadedOn';
  UPDATE dbo.EmployeeDocuments SET UploadedOn = sysdatetime() WHERE UploadedOn IS NULL;
  ALTER TABLE dbo.EmployeeDocuments ALTER COLUMN UploadedOn datetime2(0) NOT NULL;
  ALTER TABLE dbo.EmployeeDocuments ADD CONSTRAINT DF_EmployeeDocuments_UploadedOn DEFAULT sysdatetime() FOR UploadedOn;
END
IF COLUMNPROPERTY(OBJECT_ID('dbo.EmployeeDocuments'),'IsDeleted','AllowsNull') = 1
BEGIN
  UPDATE dbo.EmployeeDocuments SET IsDeleted = 0 WHERE IsDeleted IS NULL;
  ALTER TABLE dbo.EmployeeDocuments ALTER COLUMN IsDeleted bit NOT NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_EmployeeDocuments_EmployeeId' AND object_id=OBJECT_ID('dbo.EmployeeDocuments'))
  CREATE INDEX IX_EmployeeDocuments_EmployeeId ON dbo.EmployeeDocuments (EmployeeId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_EmployeeDocuments_Employee_FileHash' AND object_id=OBJECT_ID('dbo.EmployeeDocuments'))
  CREATE UNIQUE INDEX UX_EmployeeDocuments_Employee_FileHash ON dbo.EmployeeDocuments (EmployeeId, FileHash) WHERE IsDeleted = 0 AND FileHash IS NOT NULL;
GO

/* =====================================================================================
   Payslips, PayslipDetails
   ===================================================================================== */
IF COL_LENGTH('dbo.Payslips','MonthNo') IS NULL ALTER TABLE dbo.Payslips ADD MonthNo tinyint NULL;   -- renamed Month at contract (T2)
GO
UPDATE dbo.Payslips
SET MonthNo = CASE WHEN CHARINDEX(LEFT(LTRIM(RTRIM([Month])), 3), 'JanFebMarAprMayJunJulAugSepOctNovDec') = 0 THEN NULL
                   ELSE (CHARINDEX(LEFT(LTRIM(RTRIM([Month])), 3), 'JanFebMarAprMayJunJulAugSepOctNovDec') + 2) / 3 END
WHERE MonthNo IS NULL;
IF EXISTS (SELECT 1 FROM dbo.Payslips WHERE MonthNo IS NULL) THROW 50090, 'Some payslips have a month name that could not be converted.', 1;
IF COLUMNPROPERTY(OBJECT_ID('dbo.Payslips'),'MonthNo','AllowsNull') = 1
  ALTER TABLE dbo.Payslips ALTER COLUMN MonthNo tinyint NOT NULL;
IF COL_LENGTH('dbo.Payslips','HourlyMinutes') IS NULL ALTER TABLE dbo.Payslips ADD HourlyMinutes int NULL;
IF COL_LENGTH('dbo.Payslips','HourlyAmount')  IS NULL ALTER TABLE dbo.Payslips ADD HourlyAmount decimal(18,2) NULL;
IF COL_LENGTH('dbo.Payslips','RowVersion')    IS NULL ALTER TABLE dbo.Payslips ADD RowVersion rowversion NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Payslips') AND name='TaxPercentage' AND precision = 18)
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Payslips WHERE TaxPercentage NOT BETWEEN 0 AND 999.99) THROW 50091, 'Payslips.TaxPercentage out of range.', 1;
  EXEC #DropDefault 'dbo.Payslips', 'TaxPercentage';
  ALTER TABLE dbo.Payslips ALTER COLUMN TaxPercentage decimal(5,2) NOT NULL;
  ALTER TABLE dbo.Payslips ADD CONSTRAINT DF_Payslips_TaxPercentage DEFAULT 0 FOR TaxPercentage;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Payslips') AND name='GeneratedDate' AND scale = 7)
BEGIN
  ALTER TABLE dbo.Payslips ALTER COLUMN GeneratedDate datetime2(0) NOT NULL;
  ALTER TABLE dbo.Payslips ADD CONSTRAINT DF_Payslips_GeneratedDate DEFAULT sysdatetime() FOR GeneratedDate;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Payslips') AND name='LockedDate' AND scale = 7)
  ALTER TABLE dbo.Payslips ALTER COLUMN LockedDate datetime2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('dbo.Payslips') AND parent_column_id=COLUMNPROPERTY(OBJECT_ID('dbo.Payslips'),'IsLocked','ColumnId'))
  ALTER TABLE dbo.Payslips ADD CONSTRAINT DF_Payslips_IsLocked DEFAULT 0 FOR IsLocked;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Payslips_Month')
  ALTER TABLE dbo.Payslips ADD CONSTRAINT CK_Payslips_Month CHECK (MonthNo BETWEEN 1 AND 12);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Payslips_Year')
  ALTER TABLE dbo.Payslips ADD CONSTRAINT CK_Payslips_Year CHECK ([Year] BETWEEN 2000 AND 2100);
GO
-- named ..._MonthNo until the contract rename (T2)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Payslips_Employee_Year_MonthNo' AND object_id=OBJECT_ID('dbo.Payslips'))
  CREATE UNIQUE INDEX UX_Payslips_Employee_Year_MonthNo ON dbo.Payslips (EmployeeId, [Year], MonthNo);
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.PayslipDetails') AND name='ItemType' AND TYPE_NAME(user_type_id)='nvarchar')
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.PayslipDetails WHERE ItemType NOT IN ('Allowance','Deduction','Tax')) THROW 50092, 'PayslipDetails.ItemType has unexpected values.', 1;
  ALTER TABLE dbo.PayslipDetails ALTER COLUMN ItemType varchar(20) NOT NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_PayslipDetails_ItemType')
  ALTER TABLE dbo.PayslipDetails ADD CONSTRAINT CK_PayslipDetails_ItemType CHECK (ItemType IN ('Allowance','Deduction','Tax'));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_PayslipDetails_Payslip_Item' AND object_id=OBJECT_ID('dbo.PayslipDetails'))
  CREATE UNIQUE INDEX UX_PayslipDetails_Payslip_Item ON dbo.PayslipDetails (PayslipId, ItemType, ItemName);
GO

/* =====================================================================================
   TaxRules (T6)
   ===================================================================================== */
IF COL_LENGTH('dbo.TaxRules','TaxYearStart') IS NULL ALTER TABLE dbo.TaxRules ADD TaxYearStart smallint NULL;
GO
UPDATE dbo.TaxRules SET TaxYearStart = TRY_CONVERT(smallint, LEFT(TaxYear, 4)) WHERE TaxYearStart IS NULL;
IF EXISTS (SELECT 1 FROM dbo.TaxRules WHERE TaxYearStart IS NULL) THROW 50100, 'TaxRules.TaxYear could not be converted to a start year.', 1;
IF COLUMNPROPERTY(OBJECT_ID('dbo.TaxRules'),'TaxYearStart','AllowsNull') = 1
  ALTER TABLE dbo.TaxRules ALTER COLUMN TaxYearStart smallint NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.TaxRules') AND name='TaxPercentage' AND precision = 18)
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.TaxRules WHERE TaxPercentage NOT BETWEEN 0 AND 100) THROW 50101, 'TaxRules.TaxPercentage out of range.', 1;
  ALTER TABLE dbo.TaxRules ALTER COLUMN TaxPercentage decimal(5,2) NOT NULL;
END
IF COL_LENGTH('dbo.TaxRules','CreatedAt')  IS NULL ALTER TABLE dbo.TaxRules ADD CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_TaxRules_CreatedAt DEFAULT sysdatetime();
IF COL_LENGTH('dbo.TaxRules','CreatedBy')  IS NULL ALTER TABLE dbo.TaxRules ADD CreatedBy nvarchar(100) NULL;
IF COL_LENGTH('dbo.TaxRules','ModifiedAt') IS NULL ALTER TABLE dbo.TaxRules ADD ModifiedAt datetime2(0) NULL;
IF COL_LENGTH('dbo.TaxRules','ModifiedBy') IS NULL ALTER TABLE dbo.TaxRules ADD ModifiedBy nvarchar(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_TaxRules_Year_MinSalary' AND object_id=OBJECT_ID('dbo.TaxRules'))
  CREATE UNIQUE INDEX UX_TaxRules_Year_MinSalary ON dbo.TaxRules (TaxYearStart, MinSalary);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_TaxRules_Percentage')
  ALTER TABLE dbo.TaxRules ADD CONSTRAINT CK_TaxRules_Percentage CHECK (TaxPercentage BETWEEN 0 AND 100);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_TaxRules_Year')
  ALTER TABLE dbo.TaxRules ADD CONSTRAINT CK_TaxRules_Year CHECK (TaxYearStart BETWEEN 2000 AND 2100);
GO

/* =====================================================================================
   Deductions, Allowances
   ===================================================================================== */
IF COL_LENGTH('dbo.Deductions','Amount')    IS NULL ALTER TABLE dbo.Deductions ADD Amount decimal(18,2) NULL;
IF COL_LENGTH('dbo.Deductions','Notes')     IS NULL ALTER TABLE dbo.Deductions ADD Notes nvarchar(1000) NULL;
IF COL_LENGTH('dbo.Deductions','RequestId') IS NULL ALTER TABLE dbo.Deductions ADD RequestId uniqueidentifier NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Deductions') AND name='CalculationMethod' AND TYPE_NAME(user_type_id)='nvarchar')
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Deductions WHERE CalculationMethod NOT IN ('Percentage','Fixed')) THROW 50110, 'Deductions.CalculationMethod has unexpected values.', 1;
  ALTER TABLE dbo.Deductions ALTER COLUMN CalculationMethod varchar(20) NOT NULL;
END
-- fixed amounts move from PercentageValue to Amount; PercentageValue becomes percentage-only
UPDATE dbo.Deductions SET Amount = PercentageValue, PercentageValue = NULL
WHERE CalculationMethod = 'Fixed' AND Amount IS NULL AND PercentageValue IS NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Deductions') AND name='CreatedDate' AND scale = 7)
BEGIN
  EXEC #DropDefault 'dbo.Deductions', 'CreatedDate';
  ALTER TABLE dbo.Deductions ALTER COLUMN CreatedDate datetime2(0) NOT NULL;
  ALTER TABLE dbo.Deductions ADD CONSTRAINT DF_Deductions_CreatedDate DEFAULT sysdatetime() FOR CreatedDate;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Deductions') AND name='ModifiedDate' AND scale = 7)
  ALTER TABLE dbo.Deductions ALTER COLUMN ModifiedDate datetime2(0) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Deductions_Method')
  ALTER TABLE dbo.Deductions ADD CONSTRAINT CK_Deductions_Method CHECK (CalculationMethod IN ('Percentage','Fixed'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Deductions_Value')
  ALTER TABLE dbo.Deductions ADD CONSTRAINT CK_Deductions_Value CHECK (
    (CalculationMethod = 'Percentage' AND PercentageValue BETWEEN 0 AND 100)
    OR (CalculationMethod = 'Fixed' AND Amount IS NOT NULL AND Amount >= 0));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Deductions_RequestId' AND object_id=OBJECT_ID('dbo.Deductions'))
  CREATE UNIQUE INDEX UX_Deductions_RequestId ON dbo.Deductions (RequestId) WHERE RequestId IS NOT NULL;
GO

IF COL_LENGTH('dbo.Allowances','RequestId') IS NULL ALTER TABLE dbo.Allowances ADD RequestId uniqueidentifier NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Allowances') AND name='Remarks' AND max_length <> 2000)
  ALTER TABLE dbo.Allowances ALTER COLUMN Remarks nvarchar(1000) NULL;      -- becomes Notes at contract
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Allowances') AND name='EffectiveDate' AND TYPE_NAME(user_type_id)='datetime2')
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Allowances WHERE EffectiveDate <> CAST(EffectiveDate AS date) OR EndDate <> CAST(EndDate AS date)) THROW 50120, 'Allowances has dates with a time part.', 1;
  ALTER TABLE dbo.Allowances ALTER COLUMN EffectiveDate date NOT NULL;
  ALTER TABLE dbo.Allowances ALTER COLUMN EndDate date NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Allowances') AND name='CreatedDate' AND scale = 7)
BEGIN
  ALTER TABLE dbo.Allowances ALTER COLUMN CreatedDate datetime2(0) NOT NULL;
  ALTER TABLE dbo.Allowances ADD CONSTRAINT DF_Allowances_CreatedDate DEFAULT sysdatetime() FOR CreatedDate;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Allowances') AND name='ModifiedDate' AND scale = 7)
  ALTER TABLE dbo.Allowances ALTER COLUMN ModifiedDate datetime2(0) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Allowances') AND name='ModifiedBy' AND max_length = -1)
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.Allowances WHERE LEN(ModifiedBy) > 100) THROW 50121, 'Allowances.ModifiedBy longer than 100.', 1;
  ALTER TABLE dbo.Allowances ALTER COLUMN ModifiedBy nvarchar(100) NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Allowances_Percentage')
  ALTER TABLE dbo.Allowances ADD CONSTRAINT CK_Allowances_Percentage CHECK (PercentageValue IS NULL OR PercentageValue BETWEEN 0 AND 100);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Allowances_RequestId' AND object_id=OBJECT_ID('dbo.Allowances'))
  CREATE UNIQUE INDEX UX_Allowances_RequestId ON dbo.Allowances (RequestId) WHERE RequestId IS NOT NULL;
GO

/* =====================================================================================
   AuditLogs: bigint key, datetime2(0)
   ===================================================================================== */
-- ALTER COLUMN runs with ANSI_WARNINGS off internally, which a filtered index on the table rejects (error 1934):
-- drop the filtered index, change the types, recreate it.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AuditLogs') AND name='LogId' AND TYPE_NAME(user_type_id)='int')
   OR EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AuditLogs') AND name='ChangedAt' AND scale = 7)
  DROP INDEX IF EXISTS IX_AuditLogs_EmployeeId ON dbo.AuditLogs;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AuditLogs') AND name='LogId' AND TYPE_NAME(user_type_id)='int')
BEGIN
  ALTER TABLE dbo.AuditLogs DROP CONSTRAINT PK_AuditLogs;
  ALTER TABLE dbo.AuditLogs ALTER COLUMN LogId bigint NOT NULL;
  ALTER TABLE dbo.AuditLogs ADD CONSTRAINT PK_AuditLogs PRIMARY KEY (LogId);   -- renamed Id at contract
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AuditLogs') AND name='ChangedAt' AND scale = 7)
BEGIN
  EXEC #DropDefault 'dbo.AuditLogs', 'ChangedAt';
  ALTER TABLE dbo.AuditLogs ALTER COLUMN ChangedAt datetime2(0) NOT NULL;
  ALTER TABLE dbo.AuditLogs ADD CONSTRAINT DF_AuditLogs_ChangedAt DEFAULT sysdatetime() FOR ChangedAt;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AuditLogs_EmployeeId' AND object_id=OBJECT_ID('dbo.AuditLogs'))
  CREATE INDEX IX_AuditLogs_EmployeeId ON dbo.AuditLogs (EmployeeId) WHERE EmployeeId IS NOT NULL;
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261123000004_Redesign_Phase4_AttendancePayroll', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261123000004_Redesign_Phase4_AttendancePayroll');
GO
COMMIT TRANSACTION;
GO
