using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// Maps to dbo.EmployeeDocuments. <see cref="EmployeeId"/> references <see cref="Employee.uid"/>.
/// </summary>
[Table("EmployeeDocuments", Schema = "dbo")]
public class EmployeeDocument
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    [Required]
    [StringLength(300)]
    public string FileName { get; set; } = string.Empty;

    [StringLength(300)]
    public string? OriginalFileName { get; set; }

    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [StringLength(50)]
    public string? FileExtension { get; set; }

    public long? FileSize { get; set; }

    public DateTime? UploadedOn { get; set; }

    public bool IsDeleted { get; set; }

    [StringLength(30)]
    public string? DocumentType { get; set; }

    /// <summary>SHA-256 of the file content (T3).</summary>
    [StringLength(64)]
    public string? FileHash { get; set; }

    [StringLength(100)]
    public string? UploadedBy { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }
}
