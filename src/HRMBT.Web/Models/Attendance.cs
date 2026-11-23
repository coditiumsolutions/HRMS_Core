using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRMBT.Web.Models;

/// <summary>
/// One row per employee per shift date (db_changes.md: Attendance).
/// Stored as CheckInAt/CheckOutAt (full date-times, so overnight shifts work), DayStatus and LateMinutes.
/// The legacy shape (EmployeeID code, TimeIn/TimeOut, one-letter Status) is kept as NotMapped facade properties
/// so forms and views keep working; queries must use the real columns (EmployeeId, AttendanceDate, DayStatus...).
/// </summary>
public class Attendance
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Display(Name = "Attendance ID")]
    public int AttendanceID { get; set; }

    /// <summary>FK to Employee.uid (column EmployeeId). Named EmployeeUid because form fields are matched case-insensitively and EmployeeID (the employee code) is the facade below.</summary>
    public int EmployeeUid { get; set; }

    [ValidateNever]
    public Employee? Employee { get; set; }

    [Required(ErrorMessage = "Attendance Date is required")]
    [Display(Name = "Attendance Date")]
    [DataType(DataType.Date)]
    public DateTime AttendanceDate { get; set; }          // column ShiftDate

    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }

    [StringLength(20)]
    public string DayStatus { get; set; } = "Absent";

    public int LateMinutes { get; set; }

    /// <summary>Computed column: LateMinutes &gt; 0. Read only.</summary>
    public bool IsLate { get; private set; }

    [Display(Name = "Comments")]
    [StringLength(1000)]
    public string? Comments { get; set; }                 // column Notes

    // ---- facade (not stored) ----

    private string? _employeeCode;
    [NotMapped]
    [Display(Name = "Employee ID")]
    [StringLength(50)]
    public string EmployeeID
    {
        get => _employeeCode ?? Employee?.EmployeeID ?? string.Empty;
        set => _employeeCode = value;
    }

    [NotMapped]
    [Display(Name = "Employee Name")]
    public string EmployeeName => Employee?.EmployeeName ?? string.Empty;

    [NotMapped]
    [Display(Name = "Department Name")]
    public string DepartmentName => Employee?.Department ?? string.Empty;

    private TimeSpan? _timeIn, _timeOut;
    private bool _timeInSet, _timeOutSet;

    [NotMapped]
    [Display(Name = "Time In")]
    [DataType(DataType.Time)]
    public TimeSpan? TimeIn
    {
        get => _timeInSet ? _timeIn : CheckInAt?.TimeOfDay;
        set { _timeIn = value; _timeInSet = true; }
    }

    [NotMapped]
    [Display(Name = "Time Out")]
    [DataType(DataType.Time)]
    public TimeSpan? TimeOut
    {
        get => _timeOutSet ? _timeOut : CheckOutAt?.TimeOfDay;
        set { _timeOut = value; _timeOutSet = true; }
    }

    private string? _status;
    private bool _statusSet;

    /// <summary>Legacy one-letter status: P present, L late, A absent, H holiday (other values show their DayStatus).</summary>
    [NotMapped]
    [Display(Name = "Status")]
    [StringLength(10)]
    public string? Status
    {
        get
        {
            if (_statusSet) return _status;
            return DayStatus switch
            {
                "Present" => LateMinutes > 0 ? "L" : "P",
                "Absent" => "A",
                "Holiday" => "H",
                _ => DayStatus
            };
        }
        set { _status = value; _statusSet = true; }
    }

    /// <summary>
    /// Writes the facade values (TimeIn/TimeOut/Status) into the stored columns. Called by the DbContext before saving.
    /// </summary>
    public void ApplyFacade()
    {
        var day = AttendanceDate.Date;
        if (_timeInSet) CheckInAt = _timeIn.HasValue ? day + _timeIn.Value : null;
        if (_timeOutSet)
        {
            if (!_timeOut.HasValue) CheckOutAt = null;
            else
            {
                var inTime = _timeInSet ? _timeIn : CheckInAt?.TimeOfDay;
                // check-out earlier than check-in means the shift ran past midnight
                CheckOutAt = day + _timeOut.Value + (inTime.HasValue && _timeOut.Value < inTime.Value ? TimeSpan.FromDays(1) : TimeSpan.Zero);
            }
        }
        if (_statusSet)
        {
            switch ((_status ?? "").Trim().ToUpperInvariant())
            {
                case "P": DayStatus = "Present"; LateMinutes = 0; break;
                case "L": DayStatus = "Present"; if (LateMinutes < 1) LateMinutes = 1; break;
                case "A": DayStatus = "Absent"; LateMinutes = 0; break;
                case "H": DayStatus = "Holiday"; LateMinutes = 0; break;
                case "": break;
                default: DayStatus = _status!.Trim(); break;
            }
        }
        _timeInSet = _timeOutSet = _statusSet = false;
    }

    /// <summary>Concurrency token (S15): processing and an HR correction can run at the same time.</summary>
    public byte[]? RowVersion { get; set; }
}
