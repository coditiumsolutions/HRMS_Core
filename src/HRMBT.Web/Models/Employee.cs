using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

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
    // ---- Organisation (db_changes.md: Employee -> Project / Department / SubDepartment / Designation) ----
    public int ProjectId { get; set; }
    public int DepartmentId { get; set; }
    public int? SubDepartmentId { get; set; }
    public int? DesignationId { get; set; }

    [ValidateNever] public global::HRMBT.Web.Models.Project? ProjectInfo { get; set; }
    [ValidateNever] public global::HRMBT.Web.Models.Department? DepartmentInfo { get; set; }
    [ValidateNever] public global::HRMBT.Web.Models.SubDepartment? SubDepartmentInfo { get; set; }
    [ValidateNever] public global::HRMBT.Web.Models.Designation? DesignationInfo { get; set; }

    // Read-only display names, so views keep using Model.Department / .Designation / .Project.
    // Inside LINQ queries use the navigation (e.DepartmentInfo!.DepartmentName); these cannot be translated to SQL.
    [NotMapped] [Display(Name = "Project")] public string? Project => ProjectInfo?.Name;
    [NotMapped] [Display(Name = "Department")] public string? Department => DepartmentInfo?.DepartmentName;
    [NotMapped] [Display(Name = "Sub-department")] public string? SubDepartment => SubDepartmentInfo?.Name;
    [NotMapped] [Display(Name = "Designation")] public string? Designation => DesignationInfo?.Name;

    [Display(Name = "Date of Leaving")]
    [DataType(DataType.Date)]
    public DateTime? DateOfLeaving { get; set; }

    [Display(Name = "Date of Return")]
    [DataType(DataType.Date)]
    public DateTime? DateOfReturn { get; set; }

    [StringLength(10)]
    [Display(Name = "Pay basis")]
    public string PayBasis { get; set; } = "Monthly";

    [Display(Name = "Hourly rate")]
    public decimal? HourlyRate { get; set; }

    /// <summary>NULL = follow Projects.HasLeaveManagement.</summary>
    [Display(Name = "Leave managed (override)")]
    public bool? IsLeaveManagedOverride { get; set; }

    [StringLength(1000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    /// <summary>Concurrency token (S15): two people editing the same row can no longer overwrite each other silently.</summary>
    public byte[]? RowVersion { get; set; }
}
