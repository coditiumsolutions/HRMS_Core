using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

public class Employee
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int uid { get; set; }

    [Required(ErrorMessage = "Employee ID is required")]
    [Display(Name = "Employee ID")]
    [StringLength(50)]
    public string EmployeeID { get; set; } = string.Empty;

    [Required(ErrorMessage = "Employee Name is required")]
    [Display(Name = "Employee Name")]
    [StringLength(100)]
    public string EmployeeName { get; set; } = string.Empty;

    [Display(Name = "CNIC")]
    [StringLength(15)]
    public string? CNIC { get; set; }

    [Display(Name = "Department")]
    [StringLength(100)]
    public string? Department { get; set; }

    [Display(Name = "Designation")]
    [StringLength(100)]
    public string? Designation { get; set; }

    [Display(Name = "Date of Joining")]
    [DataType(DataType.Date)]
    public DateTime? DateOfJoining { get; set; }

    [Display(Name = "Basic Salary")]
    [Range(0, double.MaxValue, ErrorMessage = "Basic salary must be greater than or equal to zero")]
    [DataType(DataType.Currency)]
    public decimal BasicSalary { get; set; } = 0;

    [Display(Name = "Apply Tax")]
    public bool ApplyTax { get; set; }

    [Display(Name = "Status")]
    [StringLength(50)]
    public string? Status { get; set; }
}

