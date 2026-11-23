using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

public class CarryforwardLeave
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Employee is required")]
    [Display(Name = "Employee")]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "Leave type is required")]
    [Display(Name = "Leave Type")]
    public int LeaveTypeId { get; set; }

    [Required(ErrorMessage = "From year is required")]
    [Display(Name = "From Year")]
    public int FromYear { get; set; }

    [Required(ErrorMessage = "To year is required")]
    [Display(Name = "To Year")]
    public int ToYear { get; set; }

    [Required(ErrorMessage = "Carry forward days is required")]
    [Display(Name = "Carry Forward Days")]
    public decimal CarryForwardDays { get; set; }

    [Display(Name = "Description")]
    [StringLength(500)]
    public string? Description { get; set; }
}
