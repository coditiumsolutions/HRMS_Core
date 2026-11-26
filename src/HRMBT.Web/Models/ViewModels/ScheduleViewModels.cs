namespace HRMBT.Web.Models.ViewModels;

/// <summary>One date on the month view.</summary>
public class MonthDayVm
{
    public DateTime Date { get; set; }
    /// <summary>False before joining or after leaving.</summary>
    public bool Employed { get; set; }
    public bool WorkDay { get; set; }
    /// <summary>"Off" or "ExtraDuty" when this date was changed by hand.</summary>
    public string? Override { get; set; }
    public string? OverrideReason { get; set; }
    /// <summary>Name of the holiday on this date, if any.</summary>
    public string? HolidayName { get; set; }
    public bool OptionalHoliday { get; set; }
    /// <summary>Not worked because it is a holiday (not optional, and not changed by hand).</summary>
    public bool OffForHoliday => !WorkDay && Override == null && HolidayName != null && !OptionalHoliday;
    public string? LeaveType { get; set; }
    public string? LeaveStatus { get; set; }
    public bool HalfDay { get; set; }
    public int? LeaveRequestId { get; set; }
}

public class WorkWeekRowVm
{
    public DateTime From { get; set; }
    public DateTime? To { get; set; }
    public byte Mask { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>An employee's month: which days they work and which of those they took as leave.</summary>
public class EmployeeMonthVm
{
    public Employee Employee { get; set; } = null!;
    public DateTime Month { get; set; }
    public List<MonthDayVm> Days { get; set; } = new();
    public byte CurrentMask { get; set; }
    /// <summary>False when the company default applies (no row of their own).</summary>
    public bool OwnWorkWeek { get; set; }
    public List<WorkWeekRowVm> History { get; set; } = new();
    public bool LeaveManaged { get; set; }

    public int WorkDays => Days.Count(d => d.Employed && d.WorkDay);
    public int Holidays => Days.Count(d => d.Employed && d.OffForHoliday);
    public int DaysOff => Days.Count(d => d.Employed && !d.WorkDay && !d.OffForHoliday);
    /// <summary>Approved leave falling on work days (a half day is 0.5). Each date counts once.</summary>
    public decimal LeaveDays => Leave("Approved");
    public decimal PendingDays => Leave("Pending");
    private decimal Leave(string status) =>
        Days.Where(d => d.Employed && d.WorkDay && d.LeaveStatus == status).Sum(d => d.HalfDay ? 0.5m : 1m);
}

public class WorkWeekEmployeeVm
{
    public int Uid { get; set; }
    public string? EmployeeCode { get; set; }
    public string? Name { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Project { get; set; }
    public byte Mask { get; set; }
    public bool Own { get; set; }
    public DateTime? Since { get; set; }
}

/// <summary>The work weeks screen: who works which days, and setting it for several people at once.</summary>
public class WorkWeeksVm
{
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public List<string> Departments { get; set; } = new();
    public List<string> Designations { get; set; } = new();
    /// <summary>With no filter the list holds only the employees whose week differs from the default.</summary>
    public bool Filtered => !string.IsNullOrWhiteSpace(Department) || !string.IsNullOrWhiteSpace(Designation);
    public List<WorkWeekEmployeeVm> Rows { get; set; } = new();
}

public class LeaveAdjustmentRowVm
{
    public DateTime CreatedAt { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal? Days { get; set; }
    public decimal? Total { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
}

public class LeaveAdjustmentsVm
{
    public Employee Employee { get; set; } = null!;
    public int Year { get; set; }
    public bool Managed { get; set; }
    public List<LeaveType> Types { get; set; } = new();
    public List<LeaveBalanceRow> Balances { get; set; } = new();
    public List<LeaveAdjustmentRowVm> History { get; set; } = new();
    /// <summary>Sent back with the form so a double submit is saved once.</summary>
    public Guid RequestId { get; set; } = Guid.NewGuid();
}
