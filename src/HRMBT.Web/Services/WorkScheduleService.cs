using HRMBT.Web.Data;
using HRMBT.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Services;

/// <summary>
/// Weekday sets as a bit mask: Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64 (the ShiftAssignments columns).
/// </summary>
public static class WorkWeek
{
    /// <summary>The company work week, Monday to Saturday (decided 2026-11-26). Used when an employee has no row of their own.</summary>
    public const byte Default = 63;
    public const byte AllDays = 127;

    public static readonly DayOfWeek[] Days =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    public static byte Bit(DayOfWeek day) => (byte)(1 << (((int)day + 6) % 7));

    public static byte Mask(IEnumerable<DayOfWeek> days) => (byte)days.Aggregate(0, (m, d) => m | Bit(d));

    private static string Short(DayOfWeek d) => d.ToString()[..3];

    /// <summary>"Mon–Sat", "Mon–Fri", "Every day", or the days listed.</summary>
    public static string Describe(byte mask)
    {
        var days = Days.Where(d => (mask & Bit(d)) != 0).ToList();
        if (days.Count == 7) return "Every day";
        if (days.Count == 0) return "No days";
        var first = Array.IndexOf(Days, days[0]);
        var consecutive = days.Select((d, i) => Array.IndexOf(Days, d) == first + i).All(x => x);
        return consecutive && days.Count > 2 ? $"{Short(days[0])}–{Short(days[^1])}" : string.Join(", ", days.Select(Short));
    }
}

/// <summary>One employee's schedule over a period: their work week rows, single-day changes and the company holidays.</summary>
public sealed class EmployeeSchedule
{
    private readonly List<ShiftAssignment> _weeks;
    private readonly Dictionary<DateTime, RosterOverride> _days;

    private readonly IReadOnlyList<GazettedHoliday> _holidays;

    public EmployeeSchedule(IEnumerable<ShiftAssignment> weeks, IEnumerable<RosterOverride> days, IReadOnlyList<GazettedHoliday> holidays)
    {
        _holidays = holidays;
        // higher priority wins, then the latest start
        _weeks = weeks.OrderByDescending(w => w.Priority).ThenByDescending(w => w.EffectiveFromDate).ToList();
        _days = days.ToDictionary(d => d.OverrideDate.Date);
    }

    public byte MaskOn(DateTime date) =>
        _weeks.FirstOrDefault(w => w.EffectiveFromDate.Date <= date.Date && (w.EffectiveToDate == null || w.EffectiveToDate.Value.Date >= date.Date))
            ?.DaysOfWeekMask ?? WorkWeek.Default;

    public RosterOverride? OverrideOn(DateTime date) => _days.GetValueOrDefault(date.Date);

    public bool IsHoliday(DateTime date) =>
        _holidays.Any(h => h.HolidayDate.Date <= date.Date && h.EndDate.Date >= date.Date);

    /// <summary>A day changed by hand wins (a guard marked as working on Eid); then holidays; then the work week.</summary>
    public bool IsWorkDay(DateTime date)
    {
        if (OverrideOn(date) is { } o)
            return o.OverrideType != "Off";
        if (IsHoliday(date))
            return false;
        return (MaskOn(date) & WorkWeek.Bit(date.DayOfWeek)) != 0;
    }

    /// <summary>Work days from <paramref name="from"/> to <paramref name="to"/>, both included.</summary>
    public int WorkDays(DateTime from, DateTime to)
    {
        var n = 0;
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            if (IsWorkDay(d)) n++;
        return n;
    }
}

/// <summary>
/// The one place that says which days an employee works. Leave charging, the month view and (later) attendance read it.
/// Company holidays are included.
/// Changing a schedule recalculates the days charged by the open leave requests it affects, so the figures stay in step.
/// </summary>
public class WorkScheduleService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public WorkScheduleService(ApplicationDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Dictionary<int, EmployeeSchedule>> LoadAsync(IReadOnlyCollection<int> employeeIds, DateTime from, DateTime to)
    {
        var weeks = (await _db.ShiftAssignments.AsNoTracking()
                .Where(w => employeeIds.Contains(w.EmployeeId) && w.EffectiveFromDate <= to && (w.EffectiveToDate == null || w.EffectiveToDate >= from))
                .ToListAsync())
            .ToLookup(w => w.EmployeeId);
        var days = (await _db.RosterOverrides.AsNoTracking()
                .Where(o => employeeIds.Contains(o.EmployeeId) && o.OverrideDate >= from && o.OverrideDate <= to)
                .ToListAsync())
            .ToLookup(o => o.EmployeeId);
        // company-wide and not optional (ProjectId is not used yet)
        var holidays = await _db.GazettedHolidays.AsNoTracking()
            .Where(h => h.ProjectId == null && !h.IsOptional && h.HolidayDate <= to && h.EndDate >= from)
            .ToListAsync();
        return employeeIds.Distinct().ToDictionary(id => id, id => new EmployeeSchedule(weeks[id], days[id], holidays));
    }

    public async Task<EmployeeSchedule> LoadAsync(int employeeId, DateTime from, DateTime to) =>
        (await LoadAsync(new[] { employeeId }, from, to))[employeeId];

    /// <summary>
    /// Gives each employee the work week <paramref name="mask"/> from a date on (open-ended): earlier rows end the day before,
    /// rows starting on or after the date are replaced. Returns how many open leave requests changed their day count.
    /// </summary>
    public async Task<int> SetWorkWeekAsync(IReadOnlyCollection<int> employeeIds, byte mask, DateTime from, string? notes, string? actor)
    {
        if (mask == 0 || mask > WorkWeek.AllDays) throw new ArgumentOutOfRangeException(nameof(mask));
        from = from.Date;
        var changed = 0;
        await InTransactionAsync(async () =>
        {
            var rows = await _db.ShiftAssignments.Where(w => employeeIds.Contains(w.EmployeeId) && w.Priority == 0).ToListAsync();
            var before = rows.Where(w => w.EffectiveToDate == null || w.EffectiveToDate >= from)
                .Select(w => new { w.EmployeeId, w.DaysOfWeekMask, w.EffectiveFromDate, w.EffectiveToDate }).ToList();
            _db.ShiftAssignments.RemoveRange(rows.Where(w => w.EffectiveFromDate >= from));
            foreach (var w in rows.Where(w => w.EffectiveFromDate < from && (w.EffectiveToDate == null || w.EffectiveToDate >= from)))
            {
                w.EffectiveToDate = from.AddDays(-1);
                w.ModifiedAt = DateTime.Now;
                w.ModifiedBy = actor;
            }
            await _db.SaveChangesAsync();   // first, so a row replaced from the same date does not clash with the new one

            foreach (var id in employeeIds)
                _db.ShiftAssignments.Add(new ShiftAssignment
                {
                    EmployeeId = id,
                    EffectiveFromDate = from,
                    DaysOfWeekMask = mask,
                    WeeklyOffMask = (byte)(WorkWeek.AllDays & ~mask),
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                    CreatedBy = actor
                });
            await _db.SaveChangesAsync();

            await _audit.WriteAsync("ShiftAssignments", "UPDATE", employeeIds.Count == 1 ? employeeIds.First().ToString() : "bulk",
                employeeIds.Count == 1 ? employeeIds.First() : null, before,
                new { Employees = employeeIds, WorkWeek = WorkWeek.Describe(mask), From = from, Notes = notes }, "Schedule");
            changed = await RecalculateLeaveAsync(employeeIds, from, null, actor);
        });
        return changed;
    }

    /// <summary>Marks one date as off or working, or puts it back to the work week (<paramref name="working"/> = null).</summary>
    public async Task<int> SetDayAsync(int employeeId, DateTime date, bool? working, string? reason, string? actor)
    {
        date = date.Date;
        var changed = 0;
        await InTransactionAsync(async () =>
        {
            var row = await _db.RosterOverrides.FirstOrDefaultAsync(o => o.EmployeeId == employeeId && o.OverrideDate == date);
            var before = row == null ? null : new { row.OverrideDate, row.OverrideType, row.Reason };
            if (working == null)
            {
                if (row != null) _db.RosterOverrides.Remove(row);
            }
            else
            {
                if (row == null)
                {
                    row = new RosterOverride { EmployeeId = employeeId, OverrideDate = date, CreatedBy = actor };
                    _db.RosterOverrides.Add(row);
                }
                else
                {
                    row.ModifiedAt = DateTime.Now;
                    row.ModifiedBy = actor;
                }
                row.OverrideType = working.Value ? "ExtraDuty" : "Off";
                row.ShiftId = null;
                row.SwapWithEmployeeId = null;
                row.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            }
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("RosterOverrides", working == null ? "DELETE" : "UPDATE", date.ToString("yyyy-MM-dd"), employeeId,
                before, working == null ? null : new { Date = date, Working = working, Reason = reason }, "Schedule");
            changed = await RecalculateLeaveAsync(new[] { employeeId }, date, date, actor);
        });
        return changed;
    }

    /// <summary>
    /// Saves a change to the holidays and recounts everyone's open leave on the dates it touches, in one transaction.
    /// <paramref name="change"/> does the saving and returns those dates; it may run again if the connection is retried.
    /// </summary>
    public async Task<int> ChangeHolidaysAsync(Func<Task<(DateTime From, DateTime To)>> change, string? actor)
    {
        var changed = 0;
        await InTransactionAsync(async () =>
        {
            var (from, to) = await change();
            var ids = await _db.LeaveRequests.AsNoTracking()
                .Where(l => (l.Status == "Pending" || l.Status == "Approved") && !l.IsHalfDay && l.FromDate <= to && l.ToDate >= from)
                .Select(l => l.EmployeeId).Distinct().ToListAsync();
            changed = await RecalculateLeaveAsync(ids, from, to, actor, "holidays changed");
        });
        return changed;
    }

    /// <summary>Re-counts the days of Pending and Approved leave that touches the changed dates. Half days keep 0.5.</summary>
    private async Task<int> RecalculateLeaveAsync(IReadOnlyCollection<int> employeeIds, DateTime from, DateTime? to, string? actor,
        string because = "work schedule changed")
    {
        var requests = await _db.LeaveRequests
            .Where(l => employeeIds.Contains(l.EmployeeId) && (l.Status == "Pending" || l.Status == "Approved") && !l.IsHalfDay
                        && l.ToDate >= from && (to == null || l.FromDate <= to))
            .ToListAsync();
        if (requests.Count == 0) return 0;

        var schedules = await LoadAsync(requests.Select(r => r.EmployeeId).Distinct().ToList(),
            requests.Min(r => r.FromDate), requests.Max(r => r.ToDate));
        var changes = new List<(LeaveRequest Request, decimal Old)>();
        foreach (var r in requests)
        {
            decimal days = schedules[r.EmployeeId].WorkDays(r.FromDate, r.ToDate);
            if (days == r.TotalDays) continue;
            changes.Add((r, r.TotalDays));
            r.TotalDays = days;
            r.ModifiedAt = DateTime.Now;
            r.ModifiedBy = actor;
        }
        await _db.SaveChangesAsync();
        foreach (var (r, old) in changes)
            await _audit.WriteAsync("LeaveRequests", "UPDATE", r.Id.ToString(), r.EmployeeId,
                new { TotalDays = old }, new { r.TotalDays, Because = because }, "Schedule");
        return changes.Count;
    }

    private async Task InTransactionAsync(Func<Task> work)
    {
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync();
            await work();
            await tx.CommitAsync();
        });
    }
}
