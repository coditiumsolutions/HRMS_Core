using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models;

/// <summary>Key employment event (db_changes.md: EmployeeEvents). Append-only: a wrong entry is voided with a reason, never edited or deleted.</summary>
public class EmployeeEvent
{
    public const string Joined = "Joined";
    public const string Promotion = "Promotion";
    public const string Transfer = "Transfer";
    public const string Resignation = "Resignation";
    public const string Termination = "Termination";
    public const string Rejoin = "Rejoin";
    public const string Other = "Other";

    /// <summary>Types HR can record from Edit Employee ("Joined" is written when the employee is created).</summary>
    public static readonly string[] Recordable = { Promotion, Transfer, Resignation, Termination, Rejoin, Other };

    [Key] public int Id { get; set; }
    public int EmployeeId { get; set; }
    [Required, StringLength(20)] public string EventType { get; set; } = Other;
    public DateTime EventDate { get; set; }

    public int? FromProjectId { get; set; }
    public int? ToProjectId { get; set; }
    public int? FromDepartmentId { get; set; }
    public int? ToDepartmentId { get; set; }
    public int? FromDesignationId { get; set; }
    public int? ToDesignationId { get; set; }

    [StringLength(1000)] public string? Remarks { get; set; }

    public bool IsVoided { get; set; }
    [StringLength(500)] public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }
    [StringLength(100)] public string? VoidedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    [StringLength(100)] public string? CreatedBy { get; set; }
    public Guid? RequestId { get; set; }
}
