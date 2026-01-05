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
    [StringLength(50)]
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
    public decimal? TotalDays { get; set; }

    /// <summary>
    /// Short leave or manual adjustment
    /// </summary>
    [Display(Name = "Short Adjustment")]
    [Column("Short_Adj")]
    public decimal? Short_Adj { get; set; }

    /// <summary>
    /// Additional days to add (manual adjustment)
    /// </summary>
    [Display(Name = "Add Days")]
    [Column("AddDays")]
    public decimal? AddDays { get; set; }

    /// <summary>
    /// Days to exclude (manual adjustment)
    /// </summary>
    [Display(Name = "Exclude Days")]
    [Column("ExcludeDays")]
    public decimal? ExcludeDays { get; set; }

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
    [StringLength(100)]
    [Column("ApprovedBy")]
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Date and time when leave was approved
    /// </summary>
    [Display(Name = "Approved On")]
    [DataType(DataType.DateTime)]
    [Column("ApprovedOn")]
    public DateTime? ApprovedOn { get; set; }

    /// <summary>
    /// Optional comments or remarks
    /// </summary>
    [Display(Name = "Comments")]
    [StringLength(500)]
    [Column("Comments")]
    public string? Comments { get; set; }
}

