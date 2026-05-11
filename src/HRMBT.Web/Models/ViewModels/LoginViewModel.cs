using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Username")]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
}
