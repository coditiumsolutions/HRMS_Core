using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models;

public class LeaveType
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public bool IsPaid { get; set; } = true;

    public bool IsHalfDayAllowed { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }
}
