using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRMBT.Web.Models
{
    /// <summary>Maps to dbo.Allowances (see db.txt).</summary>
    public class Allowance
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Allowance type")]
        public string AllowanceType { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Display(Name = "Allowance Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        [Display(Name = "Frequency")]
        public string Frequency { get; set; } = "Monthly";

        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Display(Name = "Quantity")]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity must be a whole number 0 or greater.")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "Percentage-based")]
        public bool IsPercentage { get; set; }

        [Display(Name = "Percentage (%)")]
        [Range(0, 100)]
        public decimal? PercentageValue { get; set; }

        [Display(Name = "Effective date")]
        [DataType(DataType.Date)]
        public DateTime EffectiveDate { get; set; }

        [Display(Name = "End date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        [StringLength(500)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        public DateTime CreatedDate { get; set; }

        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        public string? ModifiedBy { get; set; }

        [ForeignKey("EmployeeId")]
        [ValidateNever]
        public Employee? Employee { get; set; }
    }
}
