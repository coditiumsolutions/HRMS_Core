using System.ComponentModel.DataAnnotations;
using HRMBT.Web.Infrastructure;

namespace HRMBT.Web.Models.ViewModels
{
    public class PayslipEmployeeOption
    {
        public int Uid { get; set; }
        public string? EmployeeID { get; set; }
        public string? EmployeeName { get; set; }
        public string? Department { get; set; }
        public string? Designation { get; set; }
        public decimal BasicSalary { get; set; }
    }

    public class PayslipCreateVM
    {
        [Display(Name = "Department")]
        public string? Department { get; set; }

        [Required(ErrorMessage = "Please select an employee.")]
        [Display(Name = "Employee")]
        public int? EmployeeId { get; set; }

        [Required(ErrorMessage = "Please select a month.")]
        [StringLength(20)]
        public string Month { get; set; } = PayrollMonthHelper.CurrentMonthName();

        [Required]
        [Range(2000, 2100, ErrorMessage = "Year must be between 2000 and 2100.")]
        public int Year { get; set; } = DateTime.Now.Year;

        [Display(Name = "Basic Salary")]
        [DataType(DataType.Currency)]
        public decimal BasicSalary { get; set; }

        [Display(Name = "Gross Salary")]
        [DataType(DataType.Currency)]
        [Range(0, double.MaxValue, ErrorMessage = "Gross salary must be greater than or equal to zero.")]
        public decimal GrossSalary { get; set; }

        [Display(Name = "Total Deductions")]
        [DataType(DataType.Currency)]
        [Range(0, double.MaxValue, ErrorMessage = "Deductions must be greater than or equal to zero.")]
        public decimal TotalDeductions { get; set; }

        [Display(Name = "Net Salary")]
        [DataType(DataType.Currency)]
        public decimal NetSalary { get; set; }

        [Display(Name = "Working Days")]
        [Range(0, 31, ErrorMessage = "Working days must be between 0 and 31.")]
        public int? WorkingDays { get; set; }

        [Display(Name = "Leave Days")]
        [Range(0, 31, ErrorMessage = "Leave days must be between 0 and 31.")]
        public decimal? LeaveDays { get; set; }

        [Display(Name = "Leave Balance")]
        public string? LeaveBalance { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public string? Designation { get; set; }
    }
}
