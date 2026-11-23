using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>Shift times and the attendance rules that go with them (7-hour full day, grace minutes, half-day cutoff).</summary>
public class Shift
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    /// <summary>Computed column: the shift ends on the next calendar day.</summary>
    public bool IsOvernight { get; private set; }

    public short BreakMinutes { get; set; }
    public short MinFullDayMinutes { get; set; } = 420;
    public byte GraceLateMinutes { get; set; } = 1;
    public byte GraceEarlyMinutes { get; set; } = 1;
    public short? HalfDayMinMinutes { get; set; }
    public TimeSpan? HalfDayCutoffTime { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }
}
