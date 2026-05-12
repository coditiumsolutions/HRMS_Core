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

    [Display(Name = "Father Name")]
    [StringLength(100)]
    public string? FatherName { get; set; }

    [Display(Name = "Date of Birth")]
    public string? DOB { get; set; }

    [Display(Name = "CNIC")]
    [StringLength(15)]
    public string? CNIC { get; set; }

    [Display(Name = "Mobile No")]
    [StringLength(20)]
    public string? MobileNo { get; set; }

    [Display(Name = "Department")]
    [StringLength(100)]
    public string? Department { get; set; }

    [Display(Name = "Designation")]
    [StringLength(100)]
    public string? Designation { get; set; }

    [Display(Name = "Date of Joining")]
    [DataType(DataType.Date)]
    public DateTime? DateOfJoining { get; set; }

    [Display(Name = "Employee Status")]
    [StringLength(50)]
    public string? EmployeeStatus { get; set; }

    [Display(Name = "Modified By")]
    [StringLength(100)]
    public string? ModifiedBy { get; set; }

    [Display(Name = "Modified On")]
    public string? ModifiedOn { get; set; }

    [Display(Name = "Details")]
    public string? Details { get; set; }

    [Display(Name = "Project")]
    [StringLength(100)]
    public string? Project { get; set; }

    [Display(Name = "Carry Forward Leaves")]
    public double? CarryForwardLeaves { get; set; }

    [Display(Name = "Year 2022")]
    public double? Year2022 { get; set; }

    [Display(Name = "Year 2023")]
    public double? Year2023 { get; set; }

    [Display(Name = "Adjusted Ajusted")]
    public int? AdjustedAjusted { get; set; }

    [Display(Name = "Year 2024")]
    public int? Year2024 { get; set; }

    [Display(Name = "Carry Forward Leaves 1")]
    public double? CarryForwardLeaves1 { get; set; }

    [Display(Name = "Year 2023 New")]
    public decimal? Year2023New { get; set; }

    [Display(Name = "Basic Salary")]
    [Range(0, double.MaxValue, ErrorMessage = "Basic salary must be greater than or equal to zero")]
    [DataType(DataType.Currency)]
    public decimal? BasicSalary { get; set; }

    [Display(Name = "Apply Tax")]
    [StringLength(10)]
    public string? ApplyTax { get; set; }

    /// <summary>Generation / payroll generation status (maps to dbo.Employee.GenStatus, nvarchar(50)).</summary>
    [Display(Name = "Gen Status")]
    [StringLength(50)]
    public string? GenStatus { get; set; }
}
