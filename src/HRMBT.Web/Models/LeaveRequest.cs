using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

public class LeaveRequest
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Employee ID is required")]
    [Display(Name = "Employee ID")]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "Leave type is required")]
    [Display(Name = "Leave Type")]
    [StringLength(50)]
    public string LeaveType { get; set; } = string.Empty;

    [Required(ErrorMessage = "From date is required")]
    [Display(Name = "From Date")]
    [DataType(DataType.Date)]
    public DateTime FromDate { get; set; }

    [Required(ErrorMessage = "To date is required")]
    [Display(Name = "To Date")]
    [DataType(DataType.Date)]
    public DateTime ToDate { get; set; }

    [Display(Name = "Status")]
    [StringLength(50)]
    public string? Status { get; set; }
}

