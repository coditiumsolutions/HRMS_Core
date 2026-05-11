using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// Maps to dbo.Users — credentials for application login.
/// </summary>
[Table("Users")]
public class AppUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("uid")]
    public int Uid { get; set; }

    [StringLength(50)]
    [Column("EmployeeId")]
    public string? EmployeeId { get; set; }

    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    public string? Role { get; set; }

    public string? CreatedBy { get; set; }
    public string? CreatedOn { get; set; }
    public string? History { get; set; }
}
