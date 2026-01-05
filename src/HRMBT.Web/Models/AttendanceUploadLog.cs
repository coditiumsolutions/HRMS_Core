using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// AttendanceUploadLog entity for tracking CSV upload operations.
/// Records upload metadata, row counts, and error details for each upload session.
/// </summary>
public class AttendanceUploadLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Date and time when the upload was performed
    /// </summary>
    [Required]
    [Display(Name = "Upload Date")]
    public DateTime UploadDate { get; set; }

    /// <summary>
    /// User who performed the upload (HR Administrator)
    /// </summary>
    [Display(Name = "Uploaded By")]
    [StringLength(100)]
    public string? UploadedBy { get; set; }

    /// <summary>
    /// Original filename of the uploaded CSV file
    /// </summary>
    [Display(Name = "File Name")]
    [StringLength(255)]
    public string? FileName { get; set; }

    /// <summary>
    /// Total number of rows processed from the CSV
    /// </summary>
    [Display(Name = "Total Rows")]
    public int TotalRows { get; set; }

    /// <summary>
    /// Number of rows successfully processed and saved
    /// </summary>
    [Display(Name = "Success Count")]
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of rows that failed validation or processing
    /// </summary>
    [Display(Name = "Failure Count")]
    public int FailureCount { get; set; }

    /// <summary>
    /// JSON or formatted text containing detailed error information for failed rows
    /// </summary>
    [Display(Name = "Error Details")]
    [Column(TypeName = "nvarchar(max)")]
    public string? ErrorDetails { get; set; }
}

