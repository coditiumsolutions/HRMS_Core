using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

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

        public DateTime CreatedDate { get; set; }

        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        public string? ModifiedBy { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;
    }
}
