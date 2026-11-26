using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models.ViewModels;

public sealed class HolidayWorkerVm
{
    public int Uid { get; set; }
    public string? EmployeeCode { get; set; }
    public string? Name { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Project { get; set; }
    /// <summary>Days of this holiday the person is marked as working.</summary>
    public List<DateTime> Marked { get; set; } = new();
}

public sealed class HolidayWorkingVm
{
    public GazettedHoliday Holiday { get; set; } = null!;
    public List<DateTime> Days { get; set; } = new();
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public List<string> Departments { get; set; } = new();
    public List<string> Designations { get; set; } = new();
    public int MarkedPeople { get; set; }
    public List<HolidayWorkerVm> Rows { get; set; } = new();
    public bool Filtered => !string.IsNullOrWhiteSpace(Department) || !string.IsNullOrWhiteSpace(Designation);
}

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
