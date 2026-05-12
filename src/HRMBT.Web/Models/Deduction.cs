using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models
{
    /// <summary>Maps to dbo.Deductions (see db.txt). Fixed PKR amounts use <see cref="PercentageValue"/> when <see cref="CalculationMethod"/> is Fixed (no separate Amount column).</summary>
    public class Deduction
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Deduction type")]
        public string DeductionType { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Deduction name")]
        public string DeductionName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Frequency")]
        public string Frequency { get; set; } = "Monthly";

        [Required]
        [StringLength(50)]
        [Display(Name = "Calculation method")]
        public string CalculationMethod { get; set; } = string.Empty;

        /// <summary>Percentage of gross (when method is Percentage) or fixed PKR amount (when method is Fixed).</summary>
        [Display(Name = "Percentage / fixed amount")]
        public decimal? PercentageValue { get; set; }

        [Display(Name = "Mandatory")]
        public bool IsMandatory { get; set; }

        [Display(Name = "Effective date")]
        [DataType(DataType.Date)]
        public DateTime EffectiveDate { get; set; }

        [Display(Name = "End date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;
    }
}
