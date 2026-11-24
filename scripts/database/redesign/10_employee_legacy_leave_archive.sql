/*
  HRMS database redesign - phase 10: the seven per-year leave columns leave Employee (decision B18).
  Copies Employee.CarryForwardLeaves, CarryForwardLeaves1, Year2022, Year2023, Year2023New, Year2024 and AdjustedAjusted into the read-only
  archive dbo.EmployeeLegacyLeaveBalances (original names and values), checks the copy, then drops the columns from Employee.
  The live leave balance is derived from LeaveQuota / CarryforwardLeaves / LeaveEntitlementOverrides / LeaveRequests (vLeaveBalances, script 11).
  The new app build no longer reads these columns, so run this in the same window as the build. Re-runnable; running again after the drop changes nothing.
  Run with sqlcmd -b -I (see 01_lookups_and_keys.sql). Back up first (S19).
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

IF OBJECT_ID('dbo.EmployeeLegacyLeaveBalances', 'U') IS NULL
BEGIN
  CREATE TABLE dbo.EmployeeLegacyLeaveBalances (
    EmployeeId          int           NOT NULL,
    CarryForwardLeaves  float         NULL,
    CarryForwardLeaves1 float         NULL,
    Year2022            float         NULL,
    Year2023            float         NULL,
    Year2023New         decimal(18,2) NULL,
    Year2024            int           NULL,
    AdjustedAjusted     int           NULL,
    ArchivedAt          datetime2(0)  NOT NULL CONSTRAINT DF_EmployeeLegacyLeaveBalances_ArchivedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_EmployeeLegacyLeaveBalances PRIMARY KEY CLUSTERED (EmployeeId),
    CONSTRAINT FK_EmployeeLegacyLeaveBalances_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (uid)
  );
END
GO

/* Copy, verify, then drop. Dynamic SQL because the columns are gone on a second run. */
IF COL_LENGTH('dbo.Employee', 'Year2024') IS NOT NULL
BEGIN
  EXEC sp_executesql N'
    INSERT INTO dbo.EmployeeLegacyLeaveBalances (EmployeeId, CarryForwardLeaves, CarryForwardLeaves1, Year2022, Year2023, Year2023New, Year2024, AdjustedAjusted)
    SELECT e.uid, e.CarryForwardLeaves, e.CarryForwardLeaves1, e.Year2022, e.Year2023, e.Year2023New, e.Year2024, e.AdjustedAjusted
    FROM dbo.Employee e
    WHERE (e.CarryForwardLeaves IS NOT NULL OR e.CarryForwardLeaves1 IS NOT NULL OR e.Year2022 IS NOT NULL OR e.Year2023 IS NOT NULL
           OR e.Year2023New IS NOT NULL OR e.Year2024 IS NOT NULL OR e.AdjustedAjusted IS NOT NULL)
      AND NOT EXISTS (SELECT 1 FROM dbo.EmployeeLegacyLeaveBalances a WHERE a.EmployeeId = e.uid);';

  DECLARE @expected int, @actual int;
  EXEC sp_executesql N'
    SELECT @e = COUNT(*) FROM dbo.Employee e
    WHERE e.CarryForwardLeaves IS NOT NULL OR e.CarryForwardLeaves1 IS NOT NULL OR e.Year2022 IS NOT NULL OR e.Year2023 IS NOT NULL
       OR e.Year2023New IS NOT NULL OR e.Year2024 IS NOT NULL OR e.AdjustedAjusted IS NOT NULL;', N'@e int OUTPUT', @e = @expected OUTPUT;
  SELECT @actual = COUNT(*) FROM dbo.EmployeeLegacyLeaveBalances;
  IF @actual < @expected THROW 50100, 'Legacy leave figures were not all archived; Employee columns not dropped.', 1;

  /* default constraints on those columns (none expected) */
  DECLARE @sql nvarchar(max) = N'';
  SELECT @sql += N'ALTER TABLE dbo.Employee DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
  FROM sys.default_constraints dc
  JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
  WHERE dc.parent_object_id = OBJECT_ID('dbo.Employee')
    AND c.name IN ('CarryForwardLeaves','CarryForwardLeaves1','Year2022','Year2023','Year2023New','Year2024','AdjustedAjusted');
  IF LEN(@sql) > 0 EXEC sp_executesql @sql;

  ALTER TABLE dbo.Employee DROP COLUMN CarryForwardLeaves, CarryForwardLeaves1, Year2022, Year2023, Year2023New, Year2024, AdjustedAjusted;
END
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261124000010_Redesign_Phase10_LegacyLeaveArchive', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261124000010_Redesign_Phase10_LegacyLeaveArchive');
GO
COMMIT TRANSACTION;
GO
