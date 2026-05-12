namespace HRMBT.Web.Models.ViewModels;

/// <summary>One row for LMS leave balance: quota vs availed from <c>EmployeeLeaves</c> for a year.</summary>
public class LmsLeaveBalanceRowVm
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public int AvailableLeaves { get; set; }
    public double AvailedLeaves { get; set; }
    public double BalanceLeaves { get; set; }
}
