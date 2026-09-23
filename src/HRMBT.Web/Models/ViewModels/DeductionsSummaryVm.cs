namespace HRMBT.Web.Models.ViewModels;

public class DeductionsSummaryRowVm
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string DeductionType { get; set; } = string.Empty;
    public string DeductionName { get; set; } = string.Empty;
    public string? Frequency { get; set; }
    public string? CalculationMethod { get; set; }
    public decimal Amount { get; set; }
    public bool IsPercentage { get; set; }
    public decimal? PercentageValue { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
}

public class DeductionsSummaryVm
{
    public string? DeductionType { get; set; }
    public string? DeductionName { get; set; }
    public string? Department { get; set; }
    public List<DeductionsSummaryRowVm> Rows { get; set; } = new();
    public int TotalCount => Rows.Count;
}
