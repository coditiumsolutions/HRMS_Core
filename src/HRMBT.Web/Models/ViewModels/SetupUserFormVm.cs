using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models.ViewModels;

public class SetupUserFormVm
{
    public int? Uid { get; set; }

    [Required(ErrorMessage = "Username is required")]
    [Display(Name = "Username")]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Employee ID")]
    [StringLength(50)]
    public string? EmployeeId { get; set; }

    [Display(Name = "Role")]
    [StringLength(200)]
    public string? Role { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    [StringLength(100)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [StringLength(100)]
    public string? ConfirmPassword { get; set; }
}
