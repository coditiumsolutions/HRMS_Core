namespace HRMBT.Web.Models.ViewModels;

/// <summary>One row for LMS leave balance: quota vs availed from <c>EmployeeLeaves</c> for a year.</summary>
public class LmsLeaveBalanceRowVm
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public decimal CarryForwardLeaves { get; set; }
    /// <summary>Quota + carry-forward + adjustments.</summary>
    public decimal AvailableLeaves { get; set; }
    /// <summary>Approved leave.</summary>
    public decimal AvailedLeaves { get; set; }
    public decimal PendingLeaves { get; set; }
    public decimal BalanceLeaves { get; set; }
}
