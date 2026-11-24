/*
  HRMS database redesign - phase 9: leave days are tracked in one decimal (half days), decimal(5,1).
  Changes LeaveQuota.TotalDays, CarryforwardLeaves.CarryForwardDays, LeaveEntitlementOverrides.AdjustmentDays / OverrideTotalDays
  and LeaveRequests.TotalDays from decimal(5,2) to decimal(5,1). Values are rounded to one decimal first (half days are 0.5, so nothing real is lost).
  Constraints and indexes that depend on these columns are dropped and recreated unchanged. Also adds CK_LeaveRequests_SingleYear: one request lies in one calendar year
  (a request over 31 December is entered as two), so every request belongs to exactly one balance year. Views are not schema-bound and need no change.
  Needs 03 (the leave tables). Re-runnable. Run with sqlcmd -b -I (see 01_lookups_and_keys.sql). Back up first (S19).
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

/* 1. drop what depends on the columns (only if present) */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LeaveRequests_HalfDay' AND parent_object_id = OBJECT_ID('dbo.LeaveRequests'))
  ALTER TABLE dbo.LeaveRequests DROP CONSTRAINT CK_LeaveRequests_HalfDay;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LeaveQuota_TotalDays' AND parent_object_id = OBJECT_ID('dbo.LeaveQuota'))
  ALTER TABLE dbo.LeaveQuota DROP CONSTRAINT CK_LeaveQuota_TotalDays;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CarryforwardLeaves_Days' AND parent_object_id = OBJECT_ID('dbo.CarryforwardLeaves'))
  ALTER TABLE dbo.CarryforwardLeaves DROP CONSTRAINT CK_CarryforwardLeaves_Days;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LeaveEntitlementOverrides_OneValue' AND parent_object_id = OBJECT_ID('dbo.LeaveEntitlementOverrides'))
  ALTER TABLE dbo.LeaveEntitlementOverrides DROP CONSTRAINT CK_LeaveEntitlementOverrides_OneValue;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LeaveRequests_Employee_FromDate' AND object_id = OBJECT_ID('dbo.LeaveRequests'))
  DROP INDEX IX_LeaveRequests_Employee_FromDate ON dbo.LeaveRequests;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_LeaveEntitlementOverrides_Total' AND object_id = OBJECT_ID('dbo.LeaveEntitlementOverrides'))
  DROP INDEX UX_LeaveEntitlementOverrides_Total ON dbo.LeaveEntitlementOverrides;
GO

/* 2. round, then change the type (skipped when already decimal(5,1)) */
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.LeaveQuota') AND name = 'TotalDays' AND scale <> 1)
BEGIN
  UPDATE dbo.LeaveQuota SET TotalDays = ROUND(TotalDays, 1) WHERE TotalDays <> ROUND(TotalDays, 1);
  ALTER TABLE dbo.LeaveQuota ALTER COLUMN TotalDays decimal(5,1) NOT NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CarryforwardLeaves') AND name = 'CarryForwardDays' AND scale <> 1)
BEGIN
  UPDATE dbo.CarryforwardLeaves SET CarryForwardDays = ROUND(CarryForwardDays, 1) WHERE CarryForwardDays <> ROUND(CarryForwardDays, 1);
  ALTER TABLE dbo.CarryforwardLeaves ALTER COLUMN CarryForwardDays decimal(5,1) NOT NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.LeaveEntitlementOverrides') AND name = 'AdjustmentDays' AND scale <> 1)
BEGIN
  UPDATE dbo.LeaveEntitlementOverrides SET AdjustmentDays = ROUND(AdjustmentDays, 1) WHERE AdjustmentDays <> ROUND(AdjustmentDays, 1);
  ALTER TABLE dbo.LeaveEntitlementOverrides ALTER COLUMN AdjustmentDays decimal(5,1) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.LeaveEntitlementOverrides') AND name = 'OverrideTotalDays' AND scale <> 1)
BEGIN
  UPDATE dbo.LeaveEntitlementOverrides SET OverrideTotalDays = ROUND(OverrideTotalDays, 1) WHERE OverrideTotalDays <> ROUND(OverrideTotalDays, 1);
  ALTER TABLE dbo.LeaveEntitlementOverrides ALTER COLUMN OverrideTotalDays decimal(5,1) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.LeaveRequests') AND name = 'TotalDays' AND scale <> 1)
BEGIN
  UPDATE dbo.LeaveRequests SET TotalDays = ROUND(TotalDays, 1) WHERE TotalDays <> ROUND(TotalDays, 1);
  ALTER TABLE dbo.LeaveRequests ALTER COLUMN TotalDays decimal(5,1) NOT NULL;
END
GO

/* 3. recreate what was dropped, unchanged */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LeaveRequests_HalfDay' AND parent_object_id = OBJECT_ID('dbo.LeaveRequests'))
  ALTER TABLE dbo.LeaveRequests WITH CHECK ADD CONSTRAINT CK_LeaveRequests_HalfDay CHECK (IsHalfDay = 0 OR (FromDate = ToDate AND TotalDays = 0.5));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LeaveQuota_TotalDays' AND parent_object_id = OBJECT_ID('dbo.LeaveQuota'))
  ALTER TABLE dbo.LeaveQuota WITH CHECK ADD CONSTRAINT CK_LeaveQuota_TotalDays CHECK (TotalDays >= 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CarryforwardLeaves_Days' AND parent_object_id = OBJECT_ID('dbo.CarryforwardLeaves'))
  ALTER TABLE dbo.CarryforwardLeaves WITH CHECK ADD CONSTRAINT CK_CarryforwardLeaves_Days CHECK (CarryForwardDays >= 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LeaveEntitlementOverrides_OneValue' AND parent_object_id = OBJECT_ID('dbo.LeaveEntitlementOverrides'))
  ALTER TABLE dbo.LeaveEntitlementOverrides WITH CHECK ADD CONSTRAINT CK_LeaveEntitlementOverrides_OneValue
    CHECK ((CASE WHEN AdjustmentDays IS NULL THEN 0 ELSE 1 END + CASE WHEN OverrideTotalDays IS NULL THEN 0 ELSE 1 END) = 1);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LeaveRequests_Employee_FromDate' AND object_id = OBJECT_ID('dbo.LeaveRequests'))
  CREATE NONCLUSTERED INDEX IX_LeaveRequests_Employee_FromDate ON dbo.LeaveRequests (EmployeeId, FromDate) INCLUDE (ToDate, Status, LeaveTypeId, TotalDays);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_LeaveEntitlementOverrides_Total' AND object_id = OBJECT_ID('dbo.LeaveEntitlementOverrides'))
  CREATE UNIQUE NONCLUSTERED INDEX UX_LeaveEntitlementOverrides_Total ON dbo.LeaveEntitlementOverrides (EmployeeId, LeaveTypeId, [Year]) WHERE OverrideTotalDays IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LeaveRequests_SingleYear' AND parent_object_id = OBJECT_ID('dbo.LeaveRequests'))
BEGIN
  IF EXISTS (SELECT 1 FROM dbo.LeaveRequests WHERE YEAR(FromDate) <> YEAR(ToDate))
    THROW 50090, 'Leave requests that cross 31 December exist; split them into two requests before running 09.', 1;
  ALTER TABLE dbo.LeaveRequests WITH CHECK ADD CONSTRAINT CK_LeaveRequests_SingleYear CHECK (YEAR(FromDate) = YEAR(ToDate));
END
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261124000009_Redesign_Phase9_LeaveDaysOneDecimal', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261124000009_Redesign_Phase9_LeaveDaysOneDecimal');
GO
COMMIT TRANSACTION;
GO
