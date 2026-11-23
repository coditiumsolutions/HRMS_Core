using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRMBT.Web.Models;

[Table("Users")]
public class AppUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("uid")]
    public int Uid { get; set; }

    /// <summary>FK to Employee.uid (column EmployeeId). Optional: admin accounts need not be employees.</summary>
    [Column("EmployeeId")]
    public int? EmployeeUid { get; set; }

    [ValidateNever]
    public Employee? Employee { get; set; }

    private string? _employeeCode;

    /// <summary>The employee's business code (Employee.EmployeeID). Reads from the linked employee; a setter value is resolved to EmployeeUid by the controller.</summary>
    [NotMapped]
    [StringLength(50)]
    public string? EmployeeId
    {
        get => _employeeCode ?? Employee?.EmployeeID;
        set => _employeeCode = value;
    }

    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Role { get; set; }

    /// <summary>Logins are never deleted; IsActive = 0 disables one.</summary>
    public bool IsActive { get; set; } = true;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? CreatedOn { get; set; }

    public DateTime? ModifiedAt { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }

    public string? History { get; set; }
}
