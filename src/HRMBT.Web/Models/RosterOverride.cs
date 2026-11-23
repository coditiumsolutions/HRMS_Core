using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>One-off schedule change on a single date: ShiftChange, Off, Swap or ExtraDuty.</summary>
public class RosterOverride
{
    [Key]
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime OverrideDate { get; set; }

    [Required, StringLength(15)]
    public string OverrideType { get; set; } = string.Empty;

    public int? ShiftId { get; set; }
    public int? SwapWithEmployeeId { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }
}
