using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models;

/// <summary>HR-managed project list (db_changes.md: Projects). No project name or count is built into the app.</summary>
public class Project
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Project")]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Code { get; set; }

    public int SortOrder { get; set; }

    [Display(Name = "Leave management")]
    public bool HasLeaveManagement { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }
}
