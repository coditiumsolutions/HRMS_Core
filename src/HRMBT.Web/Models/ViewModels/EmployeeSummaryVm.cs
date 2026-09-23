namespace HRMBT.Web.Models.ViewModels;

public class EmployeeSummaryRowVm
{
    public string Department { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
}

public class EmployeeSummaryVm
{
    public string? Department { get; set; }
    public List<EmployeeSummaryRowVm> Rows { get; set; } = new();
    public int TotalEmployees => Rows.Sum(r => r.EmployeeCount);
}
