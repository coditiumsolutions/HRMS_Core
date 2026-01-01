using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;


public class Attendance
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Employee ID is required")]
    [Display(Name = "Employee ID")]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "Date is required")]
    [Display(Name = "Date")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; }

    [Required(ErrorMessage = "Status is required")]
    [Display(Name = "Status")]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    [Display(Name = "Department")]
    [StringLength(100)]
    public string? Department { get; set; }
}

