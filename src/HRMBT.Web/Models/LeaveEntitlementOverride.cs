using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>Per-person change to the standard leave quota: an adjustment (+/- days) or a full override, with reason and approver.</summary>
public class LeaveEntitlementOverride
{
    [Key]
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal? AdjustmentDays { get; set; }
    public decimal? OverrideTotalDays { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public int ApprovedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public Guid? RequestId { get; set; }
}
