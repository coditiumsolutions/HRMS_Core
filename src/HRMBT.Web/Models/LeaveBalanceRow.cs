namespace HRMBT.Web.Models;

/// <summary>One row of dbo.vLeaveBalances (db.md): balance of one employee, leave type and year. Derived, never stored.</summary>
public class LeaveBalanceRow
{
    public int EmployeeId { get; set; }
    public int LeaveTypeId { get; set; }
    public int Year { get; set; }
    /// <summary>The quota for the type and year (or the employee's override of it); 0 when the year has no quota.</summary>
    public decimal BaseDays { get; set; }
    public decimal Adjustment { get; set; }
    public decimal CarryForward { get; set; }
    public decimal Entitled { get; set; }
    public decimal Taken { get; set; }
    public decimal Pending { get; set; }
    public decimal Remaining { get; set; }
}
