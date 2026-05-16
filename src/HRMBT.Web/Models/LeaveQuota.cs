using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// Maps to <c>dbo.LeaveQuota</c>: yearly quota per leave type (see db.txt).
/// </summary>
public class LeaveQuota
{
    [Key]
    [Column("UID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int UID { get; set; }

    [Required]
    [Display(Name = "Leave type")]
    [StringLength(50)]
    [Column("LeaveTypeName")]
    public string LeaveTypeName { get; set; } = string.Empty;

    [Display(Name = "Total leaves")]
    [Range(0, 366, ErrorMessage = "Total leaves must be between 0 and 366.")]
    [Column("TotalLeaves")]
    public int TotalLeaves { get; set; }

    [Required]
    [Display(Name = "Year")]
    [StringLength(50)]
    [Column("Year")]
    public string Year { get; set; } = string.Empty;
}
