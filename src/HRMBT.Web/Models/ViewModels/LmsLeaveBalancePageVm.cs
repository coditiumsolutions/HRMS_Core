namespace HRMBT.Web.Models.ViewModels;

/// <summary>Leave balance report: filters + grid rows.</summary>
public class LmsLeaveBalancePageVm
{
    public string SelectedYear { get; set; } = string.Empty;

    /// <summary>Department name from configuration; empty = all departments.</summary>
    public string SelectedDepartment { get; set; } = string.Empty;

    public IReadOnlyList<string> YearChoices { get; set; } = Array.Empty<string>();

    /// <summary>Value = department name (matches <c>Employee.Department</c>).</summary>
    public IReadOnlyList<(string Value, string Label)> DepartmentChoices { get; set; } = Array.Empty<(string, string)>();

    public List<LmsLeaveBalanceRowVm> Rows { get; set; } = new();
}
