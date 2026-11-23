using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>Dated, signed, append-only note against an attendance day, an employee or a leave request (exactly one is set).</summary>
public class RecordNote
{
    [Key]
    public int Id { get; set; }
    public int? AttendanceId { get; set; }
    public int? EmployeeId { get; set; }
    public int? LeaveRequestId { get; set; }

    [Required, StringLength(1000)]
    public string Note { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public Guid? RequestId { get; set; }
}
