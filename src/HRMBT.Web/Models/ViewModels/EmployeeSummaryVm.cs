namespace HRMBT.Web.Models.ViewModels;

public class EmployeeSummaryRowVm
{
    public string Department { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public decimal SalariesGeneratedAmount { get; set; }
}

public class EmployeeSummaryVm
{
    public string? Month { get; set; }
    public int? Year { get; set; }
    public string? Department { get; set; }
    public bool HasFilters { get; set; }
    public List<EmployeeSummaryRowVm> Rows { get; set; } = new();
    public int TotalEmployees => Rows.Sum(r => r.EmployeeCount);
    public decimal TotalSalariesGenerated => Rows.Sum(r => r.SalariesGeneratedAmount);
}
