using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRMBT.Web.Models;

public class LeaveRequest
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Employee ID is required")]
    [Display(Name = "Employee ID")]
    public int EmployeeId { get; set; }

    /// <summary>FK to LeaveTypes.Id.</summary>
    public int LeaveTypeId { get; set; }

    [ValidateNever]
    public LeaveType? LeaveTypeInfo { get; set; }

    private string? _leaveTypeName;

    /// <summary>Leave type name. Reads from the LeaveTypes row; setting it is resolved to LeaveTypeId by the controller.</summary>
    [NotMapped]
    [Required(ErrorMessage = "Leave type is required")]
    [Display(Name = "Leave Type")]
    [StringLength(500)]
    public string LeaveType
    {
        get => _leaveTypeName ?? LeaveTypeInfo?.Name ?? string.Empty;
        set => _leaveTypeName = value;
    }

    [Required(ErrorMessage = "From date is required")]
    [Display(Name = "From Date")]
    [DataType(DataType.Date)]
    public DateTime FromDate { get; set; }

    [Required(ErrorMessage = "To date is required")]
    [Display(Name = "To Date")]
    [DataType(DataType.Date)]
    public DateTime ToDate { get; set; }

    /// <summary>Pending, Approved, Rejected or Cancelled.</summary>
    [Display(Name = "Status")]
    [StringLength(10)]
    public string? Status { get; set; }

    [Display(Name = "Half day")]
    public bool IsHalfDay { get; set; }

    /// <summary>Days charged to the balance.</summary>
    [Display(Name = "Total Days")]
    public decimal TotalDays { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    public int? DocumentId { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [StringLength(1000)]
    public string? SupervisorComments { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime AppliedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }

    /// <summary>Concurrency token (S15): two people editing the same row can no longer overwrite each other silently.</summary>
    public byte[]? RowVersion { get; set; }
}
