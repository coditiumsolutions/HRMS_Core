using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>Half-day request with approval and documentation.</summary>
public class HalfDayRequest
{
    [Key]
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime HalfDayDate { get; set; }

    [Required, StringLength(10)]
    public string HalfType { get; set; } = "SecondHalf";

    public TimeSpan? ExpectedTime { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public int? DocumentId { get; set; }
    public bool RequiresCheckout { get; set; } = true;
    public int? LeaveRequestId { get; set; }

    [StringLength(10)]
    public string Status { get; set; } = "Pending";

    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [StringLength(500)]
    public string? ReviewerComments { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public byte[]? RowVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }
}
