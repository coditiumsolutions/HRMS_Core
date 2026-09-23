namespace HRMBT.Web.Models.ViewModels;

public class AllowancesSummaryRowVm
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Frequency { get; set; }
    public string? CalculationMethod { get; set; }
    public decimal Amount { get; set; }
    public int Quantity { get; set; } = 1;
    public bool IsPercentage { get; set; }
    public decimal? PercentageValue { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? Remarks { get; set; }
}

public class AllowancesSummaryVm
{
    public string? AllowanceType { get; set; }
    public string? AllowanceName { get; set; }
    public string? Department { get; set; }
    public List<AllowancesSummaryRowVm> Rows { get; set; } = new();
    public int TotalCount => Rows.Count;
    public decimal TotalAmount => Rows.Sum(r => r.Amount);
}
