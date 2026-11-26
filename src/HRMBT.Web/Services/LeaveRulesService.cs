using HRMBT.Web.Data;
using HRMBT.Web.Infrastructure;
using HRMBT.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Services;

/// <summary>What entering a leave request would do: the days it charges, reasons it is refused, and things to check.</summary>
public sealed class LeaveCheck
{
    /// <summary>Work days in the range: what the request charges.</summary>
    public int Days { get; set; }
    public List<string> Blocks { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Ok => Blocks.Count == 0;
}

/// <summary>
/// The rules every way of entering leave applies (the leave request screens and Attendance -> Off Days), so they cannot disagree.
/// Refused: end before start, crossing 31 December, leave not managed for the employee, dates outside employment,
/// overlap with an open request, no work day in the range. Warned: more days than the balance holds (blocking is HR's call, B5).
/// </summary>
public class LeaveRulesService
{
    private readonly ApplicationDbContext _db;
    private readonly WorkScheduleService _schedules;

    public LeaveRulesService(ApplicationDbContext db, WorkScheduleService schedules)
    {
        _db = db;
        _schedules = schedules;
    }

    public static bool IsOpen(string? status) =>
        string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> IsManagedAsync(Employee e) =>
        e.IsLeaveManagedOverride
        ?? await _db.Projects.AsNoTracking().Where(p => p.Id == e.ProjectId).Select(p => p.HasLeaveManagement).FirstOrDefaultAsync();

    /// <summary>A rejected or cancelled request charges nothing, so only its dates are checked.</summary>
    public async Task<LeaveCheck> CheckAsync(Employee employee, DateTime from, DateTime to, int? leaveTypeId, string? status, int? ignoreRequestId)
    {
        from = from.Date;
        to = to.Date;
        var result = new LeaveCheck();
        if (to < from)
        {
            result.Blocks.Add("To date must be on or after the from date.");
            return result;
        }
        if (to.Year != from.Year)
        {
            result.Blocks.Add("A leave request cannot cross 31 December. Apply for the two parts separately so each counts in its own year.");
            return result;
        }

        var schedule = await _schedules.LoadAsync(employee.uid, from, to);
        result.Days = schedule.WorkDays(from, to);
        if (!IsOpen(status))
            return result;

        if (!await IsManagedAsync(employee))
            result.Blocks.Add("Leave is not managed for this employee's project.");

        if (employee.DateOfJoining is { } joined && from < joined.Date)
            result.Blocks.Add($"The employee joined on {Fmt.ShortDate(joined)}.");
        if (employee.DateOfLeaving is { } left)
        {
            var back = employee.DateOfReturn?.Date;
            var away = back == null ? to > left.Date : to > left.Date && from < back;
            if (away)
                result.Blocks.Add($"The employee left on {Fmt.ShortDate(left)}{(back == null ? "" : $" and returned on {Fmt.ShortDate(back)}")}.");
        }

        var other = await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeId == employee.uid
                        && (ignoreRequestId == null || l.Id != ignoreRequestId)
                        && (l.Status == "Pending" || l.Status == "Approved")
                        && l.FromDate <= to && l.ToDate >= from)
            .OrderBy(l => l.FromDate)
            .Select(l => new { l.Id, l.FromDate, l.ToDate, l.Status })
            .FirstOrDefaultAsync();
        if (other != null)
            result.Blocks.Add($"These dates overlap leave request #{other.Id} ({other.Status}), {Fmt.ShortDate(other.FromDate)} to {Fmt.ShortDate(other.ToDate)}.");

        if (result.Days == 0)
            result.Blocks.Add("There is no work day in these dates for this employee.");

        if (result.Ok && leaveTypeId is { } typeId)
        {
            var balance = await _db.LeaveBalances.AsNoTracking()
                .FirstOrDefaultAsync(b => b.EmployeeId == employee.uid && b.LeaveTypeId == typeId && b.Year == from.Year);
            // other open requests of this type and year; the one being edited is left out
            var others = await _db.LeaveRequests.AsNoTracking()
                .Where(l => l.EmployeeId == employee.uid && l.LeaveTypeId == typeId && l.FromDate.Year == from.Year
                            && (l.Status == "Pending" || l.Status == "Approved")
                            && (ignoreRequestId == null || l.Id != ignoreRequestId))
                .SumAsync(l => (decimal?)l.TotalDays) ?? 0m;
            var available = (balance?.Entitled ?? 0m) - others;
            if (result.Days > available)
                result.Warnings.Add($"This is {result.Days} day(s) but only {Fmt.Days(Math.Max(available, 0m))} of this leave type are left for {from.Year}.");
        }
        return result;
    }

    /// <summary>The signed-in user's Users.uid (approver columns), or null if the name is not in Users.</summary>
    public async Task<int?> UserIdAsync(string? userName) =>
        string.IsNullOrWhiteSpace(userName)
            ? null
            : await _db.AppUsers.AsNoTracking().Where(u => u.Username == userName).Select(u => (int?)u.Uid).FirstOrDefaultAsync();

    /// <summary>The fields worth keeping in an audit row.</summary>
    public static object Snapshot(LeaveRequest r) => new
    {
        r.Id, r.EmployeeId, r.LeaveTypeId, r.FromDate, r.ToDate, r.TotalDays, r.Status, r.ApprovedByUserId, r.ApprovedAt
    };

    /// <summary>Records who approved and when on becoming Approved; clears it when the request stops being Approved.</summary>
    public async Task StampApprovalAsync(LeaveRequest r, string? previousStatus, string? userName)
    {
        var approved = string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase);
        var was = string.Equals(previousStatus, "Approved", StringComparison.OrdinalIgnoreCase);
        if (approved && !was)
        {
            r.ApprovedByUserId = await UserIdAsync(userName);
            r.ApprovedAt = DateTime.Now;
        }
        else if (!approved)
        {
            r.ApprovedByUserId = null;
            r.ApprovedAt = null;
        }
    }
}
