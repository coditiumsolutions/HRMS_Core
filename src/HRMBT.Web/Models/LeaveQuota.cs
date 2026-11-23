using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRMBT.Web.Models;

/// <summary>Standard leave policy: days per leave type per year (db_changes.md: LeaveQuota).</summary>
[Table("LeaveQuota")]
public class LeaveQuota
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int UID { get; set; }                       // column Id

    public int LeaveTypeId { get; set; }

    [ValidateNever]
    public LeaveType? LeaveTypeInfo { get; set; }

    private string? _leaveTypeName;

    [NotMapped]
    [Required]
    [Display(Name = "Leave type")]
    [StringLength(50)]
    public string LeaveTypeName
    {
        get => _leaveTypeName ?? LeaveTypeInfo?.Name ?? string.Empty;
        set => _leaveTypeName = value;
    }

    /// <summary>Stored as TotalDays decimal(5,2); kept as whole days in the existing screens.</summary>
    [Display(Name = "Total leaves")]
    [Range(0, 366, ErrorMessage = "Total leaves must be between 0 and 366.")]
    public int TotalLeaves { get; set; }

    /// <summary>Calendar year as text; stored as an int.</summary>
    [Required]
    [Display(Name = "Year")]
    [StringLength(50)]
    public string Year { get; set; } = string.Empty;
}
