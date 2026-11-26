using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// One holiday notice (dbo.GazettedHolidays), covering <see cref="HolidayDate"/> to <see cref="EndDate"/>.
/// A holiday that is not optional is not a work day (<c>WorkScheduleService</c>), so it is not charged as leave.
/// </summary>
public class GazettedHoliday
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>First day of the holiday.</summary>
    [Required(ErrorMessage = "Holiday date is required")]
    [Display(Name = "Holiday Date")]
    [DataType(DataType.Date)]
    [Column("HolidayDate")]
    public DateTime HolidayDate { get; set; }

    /// <summary>
    /// Name or description of the holiday
    /// </summary>
    [Required(ErrorMessage = "Holiday name is required")]
    [Display(Name = "Holiday Name")]
    [StringLength(100)]
    [Column("HolidayName")]
    public string HolidayName { get; set; } = string.Empty;

    /// <summary>Last day, the same as the first for a one-day holiday.</summary>
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [StringLength(50)]
    public string? NoticeNumber { get; set; }

    [DataType(DataType.Date)]
    public DateTime? NoticeDate { get; set; }

    /// <summary>The text of the notice.</summary>
    [Display(Name = "Description")]
    [StringLength(1000)]
    [Column("Description")]
    public string? Description { get; set; }

    [Display(Name = "Project")]
    public int? ProjectId { get; set; }

    [Display(Name = "Optional holiday")]
    public bool IsOptional { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}
