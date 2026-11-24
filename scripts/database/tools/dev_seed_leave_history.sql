/*
  DEVELOPMENT ONLY. Seeds earlier-year leave data so the Historical balances table and the year-end rollover have something to show:
  yearly quotas for 2024 and 2025 (Annual 10, Casual 10, Sick 15) and a few leave requests for the leave-managed employees in the first rows of Employee.
  Not part of the production rollout. Re-runnable (every insert is guarded). Remove the seed with the commented block at the bottom.
  Run with sqlcmd -b -I against the DEVELOPMENT database only.
*/
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @al int = (SELECT Id FROM dbo.LeaveTypes WHERE Code = 'AL');
DECLARE @cl int = (SELECT Id FROM dbo.LeaveTypes WHERE Code = 'CL');
DECLARE @sl int = (SELECT Id FROM dbo.LeaveTypes WHERE Code = 'SL');

INSERT INTO dbo.LeaveQuota (LeaveTypeId, [Year], TotalDays, CreatedBy)
SELECT v.t, v.y, v.d, 'dev-seed'
FROM (VALUES (@al, 2024, 10.0), (@cl, 2024, 10.0), (@sl, 2024, 15.0), (@al, 2025, 10.0), (@cl, 2025, 10.0), (@sl, 2025, 15.0)) v(t, y, d)
WHERE NOT EXISTS (SELECT 1 FROM dbo.LeaveQuota q WHERE q.LeaveTypeId = v.t AND q.[Year] = v.y);

/* a few requests for up to 5 leave-managed employees (employee 143 is included when managed) */
;WITH Emp AS (
  SELECT TOP (5) e.uid FROM dbo.Employee e JOIN dbo.Projects p ON p.Id = e.ProjectId
  WHERE COALESCE(e.IsLeaveManagedOverride, p.HasLeaveManagement) = 1 AND e.EmployeeStatus = 'Active'
  ORDER BY CASE WHEN e.uid = 143 THEN 0 ELSE 1 END, e.uid
)
INSERT INTO dbo.LeaveRequests (EmployeeId, FromDate, ToDate, Status, LeaveTypeId, IsHalfDay, TotalDays, Reason, CreatedBy)
SELECT e.uid, r.f, r.t, 'Approved', r.type, r.half, r.days, 'dev-seed', 'dev-seed'
FROM Emp e
CROSS JOIN (VALUES
  (CAST('2025-03-10' AS date), CAST('2025-03-12' AS date), @cl, 0, 3.0),
  (CAST('2025-07-01' AS date), CAST('2025-07-01' AS date), @al, 1, 0.5),
  (CAST('2024-09-02' AS date), CAST('2024-09-03' AS date), @sl, 0, 2.0)
) r(f, t, type, half, days)
WHERE NOT EXISTS (SELECT 1 FROM dbo.LeaveRequests x WHERE x.EmployeeId = e.uid AND x.FromDate = r.f AND x.ToDate = r.t AND x.LeaveTypeId = r.type);

COMMIT TRANSACTION;

/* To remove the seed:
   DELETE FROM dbo.LeaveRequests WHERE CreatedBy = 'dev-seed';
   DELETE FROM dbo.CarryforwardLeaves WHERE Description = 'Year-end rollover';
   DELETE FROM dbo.LeaveQuota WHERE CreatedBy = 'dev-seed';
*/
