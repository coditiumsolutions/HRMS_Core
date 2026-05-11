using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

/// <summary>
/// Maps to dbo.Configuration — key/value settings.
/// </summary>
[Table("Configuration")]
public class HrConfiguration
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("UID")]
    public int UID { get; set; }

    [StringLength(50)]
    public string? ConfigKey { get; set; }

    /// <summary>Comma-separated or long text; mapped to nvarchar(max).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ConfigValue { get; set; }
}
