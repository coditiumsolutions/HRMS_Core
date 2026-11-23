using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>Raw check-in/check-out event from a device, an upload or manual entry. Never edited.</summary>
public class AttendancePunch
{
    [Key]
    public long Id { get; set; }
    public int? EmployeeId { get; set; }

    [Required, StringLength(50)]
    public string RawEmployeeCode { get; set; } = string.Empty;

    public DateTime PunchAt { get; set; }

    [StringLength(7)]
    public string Direction { get; set; } = "Unknown";

    [Required, StringLength(10)]
    public string Source { get; set; } = string.Empty;

    [StringLength(50)]
    public string? DeviceId { get; set; }

    public int? UploadLogId { get; set; }
    public int? AttendanceId { get; set; }
    public bool IsIgnored { get; set; }

    [StringLength(200)]
    public string? IgnoreReason { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }
}
