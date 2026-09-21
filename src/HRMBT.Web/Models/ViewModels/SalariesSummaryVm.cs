namespace HRMBT.Web.Models.ViewModels;

public class SalariesSummaryVm
{
    public string? Month { get; set; }
    public int? Year { get; set; }
    public string? Department { get; set; }
    public bool HasFilters { get; set; }
    public int PayslipCount { get; set; }
    public decimal TotalBasicSalary { get; set; }
    public decimal TotalAllowances { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalGrossSalary { get; set; }
}
