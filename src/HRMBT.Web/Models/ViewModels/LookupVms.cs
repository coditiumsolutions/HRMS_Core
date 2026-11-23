using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models.ViewModels;

/// <summary>One row of a Setup lookup list (projects, departments, sub-departments, designations, leave types).</summary>
public class LookupRowVm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    /// <summary>How many records point at this row. A row that is used is deactivated, never deleted.</summary>
    public int Usage { get; set; }
}

public class LookupListVm
{
    public string Kind { get; set; } = "projects";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DetailHeader { get; set; } = "Detail";
    public string SortHeader { get; set; } = "Order";
    public string UsageHeader { get; set; } = "In use";
    public int? DepartmentFilterId { get; set; }
    public List<(int Id, string Name)> Departments { get; set; } = new();
    public List<LookupRowVm> Rows { get; set; } = new();
}

/// <summary>Create/edit form shared by all lookup kinds; only the fields that apply to the kind are shown.</summary>
public class LookupFormVm
{
    public string Kind { get; set; } = "projects";
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Projects (optional, 20) and leave types (required, 10).</summary>
    [StringLength(20)]
    public string? Code { get; set; }

    [Display(Name = "Display order")]
    public int SortOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    // projects
    [Display(Name = "Leave management")]
    public bool HasLeaveManagement { get; set; } = true;

    // sub-departments
    [Display(Name = "Department")]
    public int DepartmentId { get; set; }

    // designations
    [Display(Name = "Rank (lower = more senior)")]
    public int Rank { get; set; } = 100;

    // leave types
    [Display(Name = "Paid")]
    public bool IsPaid { get; set; } = true;

    [Display(Name = "Half day allowed")]
    public bool IsHalfDayAllowed { get; set; } = true;
}
