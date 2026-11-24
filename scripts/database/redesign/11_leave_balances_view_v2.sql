/*
  HRMS database redesign - phase 11: leave balances with history, and carry-forward rules per leave type.
  1. dbo.vLeaveBalances v2: the (employee, leave type, year) rows come from every year that has ANY leave data
     (quota, carry-forward, adjustment or a request), not only years that have a LeaveQuota row. A year with leave taken and no quota
     shows base 0 (negative remaining). Carry-forward, adjustment and base days are separate columns.
  2. dbo.LeaveTypes.AllowsCarryForward (bit, default 1) and MaxCarryForwardDays (decimal(5,1), NULL = no cap): the year-end rollover reads them
     (Unpaid and Compensation default to no carry-forward). HR edits them in Setup -> Lists.
  Needs 03 and 09. Re-runnable (CREATE OR ALTER VIEW). Run with sqlcmd -b -I (see 01_lookups_and_keys.sql). Back up first (S19).
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

IF COL_LENGTH('dbo.LeaveTypes', 'AllowsCarryForward') IS NULL
  ALTER TABLE dbo.LeaveTypes ADD AllowsCarryForward bit NOT NULL CONSTRAINT DF_LeaveTypes_AllowsCarryForward DEFAULT (1);
GO
IF COL_LENGTH('dbo.LeaveTypes', 'MaxCarryForwardDays') IS NULL
  ALTER TABLE dbo.LeaveTypes ADD MaxCarryForwardDays decimal(5,1) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LeaveTypes_MaxCarryForward' AND parent_object_id = OBJECT_ID('dbo.LeaveTypes'))
  ALTER TABLE dbo.LeaveTypes ADD CONSTRAINT CK_LeaveTypes_MaxCarryForward CHECK (MaxCarryForwardDays IS NULL OR MaxCarryForwardDays >= 0);
GO
/* defaults for the seeded types, only while nobody has changed the flag */
UPDATE dbo.LeaveTypes SET AllowsCarryForward = 0
WHERE Code IN ('UL', 'COMP') AND AllowsCarryForward = 1 AND ModifiedAt IS NULL;
GO

CREATE OR ALTER VIEW dbo.vLeaveBalances AS
WITH Keys AS (
  SELECT e.uid AS EmployeeId, q.LeaveTypeId, q.[Year] FROM dbo.Employee e CROSS JOIN dbo.LeaveQuota q
  UNION SELECT c.EmployeeId, c.LeaveTypeId, c.ToYear FROM dbo.CarryforwardLeaves c
  UNION SELECT o.EmployeeId, o.LeaveTypeId, o.[Year] FROM dbo.LeaveEntitlementOverrides o
  UNION SELECT r.EmployeeId, r.LeaveTypeId, YEAR(r.FromDate) FROM dbo.LeaveRequests r WHERE r.Status IN ('Approved', 'Pending')
)
SELECT k.EmployeeId, k.LeaveTypeId, k.[Year],
       BaseDays    = COALESCE(ov.OverrideTotalDays, q.TotalDays, 0),
       Adjustment  = COALESCE(adj.AdjustmentDays, 0),
       CarryForward = COALESCE(cf.CarryForwardDays, 0),
       Entitled    = COALESCE(ov.OverrideTotalDays, q.TotalDays, 0) + COALESCE(adj.AdjustmentDays, 0) + COALESCE(cf.CarryForwardDays, 0),
       Taken       = COALESCE(tk.Days, 0),
       Pending     = COALESCE(pd.Days, 0),
       Remaining   = COALESCE(ov.OverrideTotalDays, q.TotalDays, 0) + COALESCE(adj.AdjustmentDays, 0) + COALESCE(cf.CarryForwardDays, 0) - COALESCE(tk.Days, 0)
FROM Keys k
JOIN dbo.Employee e ON e.uid = k.EmployeeId
JOIN dbo.Projects p ON p.Id = e.ProjectId
JOIN dbo.LeaveTypes lt ON lt.Id = k.LeaveTypeId
LEFT JOIN dbo.LeaveQuota q ON q.LeaveTypeId = k.LeaveTypeId AND q.[Year] = k.[Year]
OUTER APPLY (SELECT TOP (1) o.OverrideTotalDays FROM dbo.LeaveEntitlementOverrides o
             WHERE o.EmployeeId = k.EmployeeId AND o.LeaveTypeId = k.LeaveTypeId AND o.[Year] = k.[Year] AND o.OverrideTotalDays IS NOT NULL) ov
OUTER APPLY (SELECT SUM(o.AdjustmentDays) AS AdjustmentDays FROM dbo.LeaveEntitlementOverrides o
             WHERE o.EmployeeId = k.EmployeeId AND o.LeaveTypeId = k.LeaveTypeId AND o.[Year] = k.[Year] AND o.AdjustmentDays IS NOT NULL) adj
OUTER APPLY (SELECT SUM(c.CarryForwardDays) AS CarryForwardDays FROM dbo.CarryforwardLeaves c
             WHERE c.EmployeeId = k.EmployeeId AND c.LeaveTypeId = k.LeaveTypeId AND c.ToYear = k.[Year]) cf
OUTER APPLY (SELECT SUM(r.TotalDays) AS Days FROM dbo.LeaveRequests r
             WHERE r.EmployeeId = k.EmployeeId AND r.LeaveTypeId = k.LeaveTypeId AND r.Status = 'Approved' AND YEAR(r.FromDate) = k.[Year]) tk
OUTER APPLY (SELECT SUM(r.TotalDays) AS Days FROM dbo.LeaveRequests r
             WHERE r.EmployeeId = k.EmployeeId AND r.LeaveTypeId = k.LeaveTypeId AND r.Status = 'Pending' AND YEAR(r.FromDate) = k.[Year]) pd
WHERE COALESCE(e.IsLeaveManagedOverride, p.HasLeaveManagement) = 1;
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261124000011_Redesign_Phase11_LeaveBalancesV2', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261124000011_Redesign_Phase11_LeaveBalancesV2');
GO
COMMIT TRANSACTION;
GO
