using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models.ViewModels;

public class HolidayListVm
{
    public int Year { get; set; }
    public List<int> Years { get; set; } = new();
    public List<GazettedHoliday> Rows { get; set; } = new();
    public bool CanManage { get; set; }
}

public class HolidayFormVm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Give the holiday a name."), StringLength(100)]
    [Display(Name = "Holiday")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Enter the first day."), DataType(DataType.Date)]
    [Display(Name = "First day")]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Last day")]
    public DateTime? To { get; set; }

    [StringLength(50)]
    [Display(Name = "Notice number")]
    public string? NoticeNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Notice date")]
    public DateTime? NoticeDate { get; set; }

    [StringLength(1000)]
    [Display(Name = "Notice text")]
    public string? Description { get; set; }

    [Display(Name = "Optional holiday")]
    public bool IsOptional { get; set; }
}
