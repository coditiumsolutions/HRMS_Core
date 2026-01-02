using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models
{
    public class Deduction
    {
        [Key]
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string DeductionType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string CalculationMethod { get; set; } = string.Empty;
        public decimal? PercentageValue { get; set; }
        public bool IsMandatory { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;
    }
}
