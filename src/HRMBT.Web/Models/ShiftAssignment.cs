using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>Which shift an employee works, on which weekdays (bitmask Mon=1 ... Sun=64), over which date range.</summary>
public class ShiftAssignment
{
    [Key]
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int ShiftId { get; set; }
    public DateTime EffectiveFromDate { get; set; }
    public DateTime? EffectiveToDate { get; set; }
    public byte DaysOfWeekMask { get; set; }
    public byte WeeklyOffMask { get; set; }
    public byte Priority { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }
}
