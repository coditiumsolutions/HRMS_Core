using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

public class TaxRule
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>Annual income where the slab starts; also the "amount over" threshold the rate applies above (column name kept from the old monthly design).</summary>
    [Required(ErrorMessage = "Annual income from is required")]
    [Display(Name = "Annual income from")]
    [Range(0, double.MaxValue, ErrorMessage = "Annual income from must be greater than or equal to zero")]
    [DataType(DataType.Currency)]
    public decimal MinSalary { get; set; }

    /// <summary>Annual income where the slab ends (NULL = no upper limit). Informational: the slab is chosen by its start.</summary>
    [Display(Name = "Annual income up to")]
    [Range(0, double.MaxValue, ErrorMessage = "Annual income up to must be greater than or equal to zero")]
    [DataType(DataType.Currency)]
    public decimal? MaxSalary { get; set; }

    /// <summary>Tax in rupees already due at the start of the slab (0 for the first taxed slab).</summary>
    [Display(Name = "Fixed amount")]
    [Range(0, double.MaxValue, ErrorMessage = "Fixed amount must be greater than or equal to zero")]
    [DataType(DataType.Currency)]
    public decimal FixedAmount { get; set; }

    /// <summary>Rate in percent charged on the part of the income above <see cref="MinSalary"/>.</summary>
    [Required(ErrorMessage = "Rate is required")]
    [Display(Name = "Rate on the excess (%)")]
    [Range(0, 100, ErrorMessage = "Tax percentage must be between 0 and 100")]
    public decimal TaxPercentage { get; set; }

    /// <summary>Fiscal/tax calendar label for this slab (typically matches payroll <see cref="Payslip.Year"/> when stored as a 4-digit year).</summary>
    [Required(ErrorMessage = "Tax year is required")]
    [Display(Name = "Tax Year")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Tax year must be between 1 and 20 characters")]
    public string TaxYear { get; set; } = string.Empty;

    /// <summary>Calendar year in which the July-June tax year starts (2026 for "2026-2027"); 0 when the label is empty.</summary>
    [NotMapped]
    public int StartYear => TaxYear.Length >= 4 && int.TryParse(TaxYear.AsSpan(0, 4), out var y) ? y : 0;
}

