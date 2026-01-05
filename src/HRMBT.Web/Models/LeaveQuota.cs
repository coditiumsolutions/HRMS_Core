using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// LeaveQuota entity representing yearly leave quota definitions per leave type.
/// Leave quota is defined per LeaveTypeName per Year.
/// </summary>
public class LeaveQuota
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Leave type name (e.g., Casual, Sick, Annual, Unpaid)
    /// </summary>
    [Required(ErrorMessage = "Leave type name is required")]
    [Display(Name = "Leave Type Name")]
    [StringLength(50)]
    [Column("LeaveTypeName")]
    public string LeaveTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Year for which this quota applies
    /// </summary>
    [Required(ErrorMessage = "Year is required")]
    [Display(Name = "Year")]
    [Column("Year")]
    public int Year { get; set; }

    /// <summary>
    /// Number of leave days allocated for this leave type in this year
    /// </summary>
    [Display(Name = "Quota Days")]
    [Column("QuotaDays")]
    public decimal? QuotaDays { get; set; }

    /// <summary>
    /// Optional description or notes
    /// </summary>
    [Display(Name = "Description")]
    [StringLength(255)]
    [Column("Description")]
    public string? Description { get; set; }
}

