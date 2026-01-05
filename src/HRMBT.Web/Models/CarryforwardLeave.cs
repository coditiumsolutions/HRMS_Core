using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// CarryforwardLeave entity representing yearly carry-forward of unused leaves.
/// Unused eligible leaves are carried forward yearly.
/// </summary>
public class CarryforwardLeave
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
    /// Year from which leaves are being carried forward
    /// </summary>
    [Required(ErrorMessage = "From year is required")]
    [Display(Name = "From Year")]
    [Column("FromYear")]
    public int FromYear { get; set; }

    /// <summary>
    /// Year to which leaves are being carried forward
    /// </summary>
    [Required(ErrorMessage = "To year is required")]
    [Display(Name = "To Year")]
    [Column("ToYear")]
    public int ToYear { get; set; }

    /// <summary>
    /// Number of days carried forward
    /// </summary>
    [Required(ErrorMessage = "Carry forward days is required")]
    [Display(Name = "Carry Forward Days")]
    [Column("CarryForwardDays")]
    public decimal CarryForwardDays { get; set; }

    /// <summary>
    /// Optional description or notes
    /// </summary>
    [Display(Name = "Description")]
    [StringLength(255)]
    [Column("Description")]
    public string? Description { get; set; }
}

