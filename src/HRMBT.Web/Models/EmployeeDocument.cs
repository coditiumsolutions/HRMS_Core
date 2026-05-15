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

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }
}
