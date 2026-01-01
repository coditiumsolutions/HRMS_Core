using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models;

public class Payroll
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Employee ID is required")]
    [Display(Name = "Employee ID")]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "Basic salary is required")]
    [Display(Name = "Basic Salary")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Basic salary must be greater than zero")]
    [DataType(DataType.Currency)]
    public decimal BasicSalary { get; set; }

    [Required(ErrorMessage = "Month is required")]
    [Range(1, 12, ErrorMessage = "Month must be between 1 and 12")]
    public int Month { get; set; }

    [Required(ErrorMessage = "Year is required")]
    [Range(2000, 2100, ErrorMessage = "Year must be between 2000 and 2100")]
    public int Year { get; set; }
}

