using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRMBT.Web.Models
{
    public class Payslip
    {
        [Key]
        public int Id { get; set; }
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(20)]
        public string Month { get; set; } = string.Empty;

        public int Year { get; set; }
        public decimal BasicSalary { get; set; }
        /// <summary>Sum of computed allowance amounts for this period (dbo.Payslips.TotalAllowances).</summary>
        public decimal? TotalAllowances { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal TaxPercentage { get; set; }
        public decimal TaxAmount { get; set; }
        /// <summary>Sum of payroll deduction lines from <c>Deductions</c> for this payslip (excludes income tax; see <see cref="TaxAmount"/>).</summary>
        public decimal TotalDeductions { get; set; }
        public decimal NetSalary { get; set; }
        public int? WorkingDays { get; set; }
        public decimal? LeaveDays { get; set; }
        // DB migration created this as NOT NULL, so keep it non-null in the model too
        public string LeaveBalance { get; set; } = string.Empty;
        public string CalculationDetails { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public DateTime? LockedDate { get; set; }
        // DB migration created this as NOT NULL, so keep it non-null in the model too
        public string Notes { get; set; } = string.Empty;

        [ForeignKey("EmployeeId")]
        // Navigation properties should NOT participate in MVC validation/binding for these forms
        [ValidateNever]
        public Employee? Employee { get; set; }

        [ValidateNever]
        public ICollection<PayslipDetail> PayslipDetails { get; set; } = new List<PayslipDetail>();
    }
}
