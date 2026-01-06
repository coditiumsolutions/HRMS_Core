using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// EmployeeLeave entity representing employee leave applications.
/// This is the main table for leave requests and approvals.
/// </summary>
public class EmployeeLeave
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("uid")]
    public int Id { get; set; }

    /// <summary>
    /// Employee ID (string format, matches Employee.EmployeeID)
    /// </summary>
    [Required(ErrorMessage = "Employee ID is required")]
    [Display(Name = "Employee ID")]
    [StringLength(50)]
    [Column("EmployeeID")]
    public string EmployeeID { get; set; } = string.Empty;

    /// <summary>
    /// Leave type name (must match LeaveQuota.LeaveTypeName)
    /// </summary>
    [Required(ErrorMessage = "Leave type name is required")]
    [Display(Name = "Leave Type Name")]
    [StringLength(250)]
    [Column("LeaveTypeName")]
    public string LeaveTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Leave start date
    /// </summary>
    [Required(ErrorMessage = "Start date is required")]
    [Display(Name = "Start Date")]
    [DataType(DataType.Date)]
    [Column("StartDate")]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Leave end date
    /// </summary>
    [Required(ErrorMessage = "End date is required")]
    [Display(Name = "End Date")]
    [DataType(DataType.Date)]
    [Column("EndDate")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Total leave days calculated (Date range - excluded days + AddDays - ExcludeDays)
    /// </summary>
    [Display(Name = "Total Days")]
    [Column("TotalDays")]
    public double? TotalDays { get; set; }

    /// <summary>
    /// Additional days to add (manual adjustment)
    /// </summary>
    [Display(Name = "Add Days")]
    [Column("AddDays")]
    public int? AddDays { get; set; }

    /// <summary>
    /// Days to exclude (manual adjustment)
    /// </summary>
    [Display(Name = "Exclude Days")]
    [Column("ExcludeDays")]
    public int? ExcludeDays { get; set; }

    /// <summary>
    /// Short leave or manual adjustment (stored as varchar)
    /// </summary>
    [Display(Name = "Short Adjustment")]
    [Column("Short_Adj")]
    public string? Short_Adj { get; set; }

    /// <summary>
    /// Department supervisor comments
    /// </summary>
    [Display(Name = "Comments")]
    [Column("DepSupervisorComments")]
    public string? Comments { get; set; }

    /// <summary>
    /// Year for the leave application
    /// </summary>
    [Display(Name = "Year")]
    [StringLength(50)]
    [Column("Year")]
    public string? Year { get; set; }

    /// <summary>
    /// Leave status: Applied, Approved, Rejected, Cancelled
    /// </summary>
    [Display(Name = "Status")]
    [StringLength(50)]
    [Column("Status")]
    public string? Status { get; set; }

    /// <summary>
    /// Person who approved the leave
    /// </summary>
    [Display(Name = "Approved By")]
    [StringLength(50)]
    [Column("ApprovedBy")]
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Date when leave was approved
    /// </summary>
    [Display(Name = "Approved On")]
    [DataType(DataType.Date)]
    [Column("ApprovedOn")]
    public DateTime? ApprovedOn { get; set; }

    /// <summary>
    /// Date when leave was applied
    /// </summary>
    [Display(Name = "Applied Date")]
    [DataType(DataType.Date)]
    [Column("AppliedDate")]
    public DateTime? AppliedDate { get; set; }
}
