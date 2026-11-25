namespace HRMBT.Web.Models.ViewModels;

/// <summary>One card on the Tax rules page: a tax year (July to June) and a summary of its slabs.</summary>
public class TaxYearCardVM
{
    public int StartYear { get; set; }
    public string Label => $"{StartYear}-{StartYear + 1}";
    public int SlabCount { get; set; }
    public decimal LowestRate { get; set; }
    public decimal HighestRate { get; set; }
    /// <summary>Annual income where the last (highest) slab starts.</summary>
    public decimal TopSlabFrom { get; set; }
    public bool IsCurrent { get; set; }
    public bool HasWarnings { get; set; }
}

/// <summary>The slabs of one tax year, with checks that help HR spot a typo (gaps, overlaps, fixed amounts that do not follow from the lower slab).</summary>
public class TaxYearVM
{
    public int StartYear { get; set; }
    public string Label => $"{StartYear}-{StartYear + 1}";
    public bool IsCurrent { get; set; }
    public List<TaxRule> Rules { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>One row of the "New tax year" form. Blank rows are ignored.</summary>
public class TaxSlabRowVM
{
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public decimal? FixedAmount { get; set; }
    public decimal? TaxPercentage { get; set; }

    public bool IsBlank => !MinSalary.HasValue && !MaxSalary.HasValue && !FixedAmount.HasValue && !TaxPercentage.HasValue;
}

/// <summary>The "New tax year" form: a tax year and all of its slabs saved together.</summary>
public class TaxYearNewVM
{
    public string TaxYear { get; set; } = string.Empty;
    public List<TaxSlabRowVM> Slabs { get; set; } = new();
}
