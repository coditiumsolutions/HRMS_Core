using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// Attendance entity representing one employee's attendance on one calendar date.
/// Each record represents one employee on one calendar date (uniqueness enforced).
/// </summary>
public class Attendance
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Display(Name = "Attendance ID")]
    public int AttendanceID { get; set; }

    /// <summary>
    /// Employee ID - must exist in Employee master (Employee.EmployeeID)
    /// </summary>
    [Required(ErrorMessage = "Employee ID is required")]
    [Display(Name = "Employee ID")]
    [StringLength(50)]
    public string EmployeeID { get; set; } = string.Empty;

    /// <summary>
    /// Employee Name - stored for quick reference
    /// </summary>
    [Required(ErrorMessage = "Employee Name is required")]
    [Display(Name = "Employee Name")]
    [StringLength(100)]
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>
    /// Department Name
    /// </summary>
    [Required(ErrorMessage = "Department Name is required")]
    [Display(Name = "Department Name")]
    [StringLength(50)]
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>
    /// Attendance date - one record per employee per date
    /// </summary>
    [Required(ErrorMessage = "Attendance Date is required")]
    [Display(Name = "Attendance Date")]
    [DataType(DataType.Date)]
    public DateTime AttendanceDate { get; set; }

    /// <summary>
    /// Time In (optional)
    /// </summary>
    [Display(Name = "Time In")]
    [DataType(DataType.Time)]
    public TimeSpan? TimeIn { get; set; }

    /// <summary>
    /// Time Out (optional) - must be later than TimeIn if both are provided
    /// </summary>
    [Display(Name = "Time Out")]
    [DataType(DataType.Time)]
    public TimeSpan? TimeOut { get; set; }

    /// <summary>
    /// Status: P (Present), A (Absent), L (Late), H (Holiday)
    /// </summary>
    [Display(Name = "Status")]
    [StringLength(10)]
    public string? Status { get; set; }

    /// <summary>
    /// Comments (optional)
    /// </summary>
    [Display(Name = "Comments")]
    [StringLength(255)]
    public string? Comments { get; set; }
}
