namespace HRMBT.Web.Models.ViewModels;

/// <summary>One dated note on a record. EditedAt/EditedBy come from the audit log (RecordNotes keeps only the author).</summary>
public sealed class RecordNoteVm
{
    public int Id { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? EditedAt { get; set; }
    public string? EditedBy { get; set; }
}

/// <summary>The notes of one record, for the shared _RecordNotes partial.</summary>
public sealed class RecordNotesVm
{
    /// <summary>The record's notes route, for example /leave/5/notes.</summary>
    public string BaseUrl { get; set; } = string.Empty;
    public string ButtonClass { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public List<RecordNoteVm> Notes { get; set; } = new();
}

public sealed class LeaveCalendarDayVm
{
    public DateTime Date { get; set; }
    public string? Holiday { get; set; }
    /// <summary>People on approved (and pending) leave who would otherwise be working.</summary>
    public int Approved { get; set; }
    public int Pending { get; set; }
}

public sealed class LeaveCalendarCellVm
{
    public int LeaveRequestId { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public bool Approved { get; set; }
    public bool HalfDay { get; set; }
    /// <summary>False on a day off or holiday inside the leave: nothing is charged.</summary>
    public bool WorkDay { get; set; }
}

public sealed class LeaveCalendarRowVm
{
    public int Uid { get; set; }
    public string? EmployeeCode { get; set; }
    public string? Name { get; set; }
    public string? Designation { get; set; }
    /// <summary>One entry per day of the month; null when not on leave.</summary>
    public LeaveCalendarCellVm?[] Cells { get; set; } = Array.Empty<LeaveCalendarCellVm?>();
}

public sealed class LeaveCalendarVm
{
    public DateTime Month { get; set; }
    public string? Project { get; set; }
    public string? Department { get; set; }
    public List<string> Projects { get; set; } = new();
    public List<string> Departments { get; set; } = new();
    public List<LeaveCalendarDayVm> Days { get; set; } = new();
    public List<LeaveCalendarRowVm> Rows { get; set; } = new();
}
