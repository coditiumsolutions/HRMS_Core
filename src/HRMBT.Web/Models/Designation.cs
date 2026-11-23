using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models;

public class Designation
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Lower = more senior (decision B4: ranking from HR, default 100).</summary>
    public int Rank { get; set; } = 100;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }
}
