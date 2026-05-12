namespace HRMBT.Web.Models.ViewModels;

/// <summary>One row for LMS list: <c>LeaveRequests</c> joined with <c>dbo.Employee</c> for display.</summary>
public class LmsLeaveRowVm
{
    public int Id { get; set; }
    public int EmployeeUid { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? Status { get; set; }

    public int CalendarDays =>
        ToDate >= FromDate ? (ToDate.Date - FromDate.Date).Days + 1 : 0;
}
