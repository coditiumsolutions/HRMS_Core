using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models;

/// <summary>Optional detail under a department (the function: Mess, Record...). Information only.</summary>
public class SubDepartment
{
    [Key]
    public int Id { get; set; }

    public int DepartmentId { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }
}
