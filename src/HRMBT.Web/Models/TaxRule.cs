using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

public class TaxRule
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Minimum salary is required")]
    [Display(Name = "Minimum Salary")]
    [Range(0, double.MaxValue, ErrorMessage = "Minimum salary must be greater than or equal to zero")]
    [DataType(DataType.Currency)]
    public decimal MinSalary { get; set; }

    [Display(Name = "Maximum Salary")]
    [Range(0, double.MaxValue, ErrorMessage = "Maximum salary must be greater than or equal to zero")]
    [DataType(DataType.Currency)]
    public decimal? MaxSalary { get; set; }

    [Required(ErrorMessage = "Tax percentage is required")]
    [Display(Name = "Tax Percentage")]
    [Range(0, 100, ErrorMessage = "Tax percentage must be between 0 and 100")]
    public decimal TaxPercentage { get; set; }

    /// <summary>Fiscal/tax calendar label for this slab (typically matches payroll <see cref="Payslip.Year"/> when stored as a 4-digit year).</summary>
    [Required(ErrorMessage = "Tax year is required")]
    [Display(Name = "Tax Year")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Tax year must be between 1 and 20 characters")]
    public string TaxYear { get; set; } = string.Empty;
}

