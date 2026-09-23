using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>Maps to dbo.AuditLogs (see db.txt).</summary>
[Table("AuditLogs")]
public class AuditLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int LogId { get; set; }

    [Required]
    [StringLength(128)]
    public string TableName { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Operation { get; set; } = string.Empty;

    [StringLength(100)]
    public string? RecordId { get; set; }

    public int? EmployeeId { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? OldData { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? NewData { get; set; }

    [StringLength(128)]
    public string? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }

    [StringLength(100)]
    public string? ModuleName { get; set; }

    [StringLength(50)]
    public string? IPAddress { get; set; }
}
