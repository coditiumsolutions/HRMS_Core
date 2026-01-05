using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// GazettedHoliday entity representing non-working holidays.
/// Holidays are excluded from leave day calculation.
/// </summary>
public class GazettedHoliday
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Holiday date
    /// </summary>
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

    /// <summary>
    /// Optional description
    /// </summary>
    [Display(Name = "Description")]
    [StringLength(255)]
    [Column("Description")]
    public string? Description { get; set; }
}

