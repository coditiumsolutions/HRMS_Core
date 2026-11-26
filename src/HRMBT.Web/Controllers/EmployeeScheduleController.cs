using System.Globalization;
using HRMBT.Web.Data;
using HRMBT.Web.Infrastructure;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers;

/// <summary>
/// Work weeks, the month view of one employee, and per-person leave adjustments.
/// Access matches the leave screens (any signed-in user) until HR answers who may do what (B13).
/// </summary>
public class EmployeeScheduleController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly WorkScheduleService _schedules;
    private readonly LeaveRulesService _rules;
    private readonly IAuditLogService _audit;

    public EmployeeScheduleController(ApplicationDbContext context, WorkScheduleService schedules, LeaveRulesService rules, IAuditLogService audit)
    {
        _context = context;
        _schedules = schedules;
        _rules = rules;
        _audit = audit;
    }

    private static string Recounted(int n) => n == 0 ? "" : $" {n} leave request(s) were recounted.";

    private static bool ValidDate(DateTime d) => d.Year is >= 2000 and <= 2100;

    private Task<Employee?> FindAsync(int id) => _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.uid == id);

    // ---------- month view ----------

    [HttpGet("/employees/{id:int}/schedule")]
    public async Task<IActionResult> Month(int id, string? month)
    {
        ViewData["Module"] = "Employees";
        var employee = await FindAsync(id);
        if (employee == null) return NotFound();

        var first = DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m) && ValidDate(m)
            ? m
            : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        var schedule = await _schedules.LoadAsync(id, first, last);
        var leave = await _context.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeId == id && (l.Status == "Approved" || l.Status == "Pending") && l.FromDate <= last && l.ToDate >= first)
            .ToListAsync();
        var weeks = await _context.ShiftAssignments.AsNoTracking()
            .Where(w => w.EmployeeId == id).OrderByDescending(w => w.EffectiveFromDate).ToListAsync();
        var holidays = await _context.GazettedHolidays.AsNoTracking()
            .Where(h => h.ProjectId == null && h.HolidayDate <= last && h.EndDate >= first)
            .ToListAsync();
        var today = DateTime.Today;
        var current = weeks.FirstOrDefault(w => w.EffectiveFromDate <= today && (w.EffectiveToDate == null || w.EffectiveToDate >= today));

        var vm = new EmployeeMonthVm
        {
            Employee = employee,
            Month = first,
            LeaveManaged = await _rules.IsManagedAsync(employee),
            CurrentMask = current?.DaysOfWeekMask ?? WorkWeek.Default,
            OwnWorkWeek = current != null,
            History = weeks.Select(w => new WorkWeekRowVm
            {
                From = w.EffectiveFromDate, To = w.EffectiveToDate, Mask = w.DaysOfWeekMask, Notes = w.Notes, CreatedBy = w.CreatedBy
            }).ToList()
        };
        for (var d = first; d <= last; d = d.AddDays(1))
        {
            // an approved request wins over a pending one on the same date
            var l = leave.Where(x => x.FromDate <= d && x.ToDate >= d).OrderBy(x => x.Status == "Approved" ? 0 : 1).FirstOrDefault();
            var o = schedule.OverrideOn(d);
            var h = holidays.Where(x => x.HolidayDate <= d && x.EndDate >= d).OrderBy(x => x.IsOptional).FirstOrDefault();
            vm.Days.Add(new MonthDayVm
            {
                Date = d,
                Employed = IsEmployed(employee, d),
                WorkDay = schedule.IsWorkDay(d),
                Override = o?.OverrideType,
                OverrideReason = o?.Reason,
                HolidayName = h?.HolidayName,
                OptionalHoliday = h?.IsOptional ?? false,
                LeaveType = l?.LeaveType,
                LeaveStatus = l?.Status,
                HalfDay = l?.IsHalfDay ?? false,
                LeaveRequestId = l?.Id
            });
        }
        return View(vm);
    }

    private static bool IsEmployed(Employee e, DateTime d)
    {
        if (e.DateOfJoining is { } joined && d < joined.Date) return false;
        if (e.DateOfLeaving is { } left && d > left.Date)
            return e.DateOfReturn is { } back && d >= back.Date;
        return true;
    }

    [HttpPost("/employees/{id:int}/schedule/work-week")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetWorkWeek(int id, DayOfWeek[] days, DateTime from, string? notes, string? month)
    {
        if (await FindAsync(id) == null) return NotFound();
        if (days.Length == 0)
            TempData["ErrorMessage"] = "Tick at least one working day.";
        else if (!ValidDate(from))
            TempData["ErrorMessage"] = "Enter the date the work week starts from.";
        else if (notes?.Length > 1000)
            TempData["ErrorMessage"] = "The note is too long.";
        else
        {
            var mask = WorkWeek.Mask(days);
            var n = await _schedules.SetWorkWeekAsync(new[] { id }, mask, from, notes, User.Identity?.Name);
            TempData["SuccessMessage"] = $"Work week set to {WorkWeek.Describe(mask)} from {Fmt.ShortDate(from)}." + Recounted(n);
        }
        return RedirectToAction(nameof(Month), new { id, month });
    }

    [HttpPost("/employees/{id:int}/schedule/day")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDay(int id, DateTime date, string? setting, string? reason, string? month)
    {
        if (await FindAsync(id) == null) return NotFound();
        bool? working = setting switch { "working" => true, "off" => false, _ => null };
        if (!ValidDate(date))
            TempData["ErrorMessage"] = "Pick the date to change.";
        else if (setting is not ("working" or "off" or "default"))
            TempData["ErrorMessage"] = "Choose what the day should be.";
        else if (reason?.Length > 500)
            TempData["ErrorMessage"] = "The reason is too long.";
        else
        {
            var n = await _schedules.SetDayAsync(id, date, working, reason, User.Identity?.Name);
            var what = working == null ? "follows the work week again" : working.Value ? "is a working day" : "is a day off";
            TempData["SuccessMessage"] = $"{Fmt.ShortDate(date)} {what}." + Recounted(n);
        }
        return RedirectToAction(nameof(Month), new { id, month });
    }

    // ---------- work weeks for several employees ----------

    [HttpGet("/work-weeks")]
    public async Task<IActionResult> WorkWeeks(string? department, string? designation)
    {
        ViewData["Module"] = "LMS";
        var vm = new WorkWeeksVm
        {
            Department = department,
            Designation = designation,
            Departments = await _context.Departments.AsNoTracking().Where(d => d.DepartmentName != null && d.IsActive)
                .OrderBy(d => d.DepartmentName).Select(d => d.DepartmentName!).ToListAsync(),
            Designations = await _context.Designations.AsNoTracking().OrderBy(d => d.Name).Select(d => d.Name).Distinct().ToListAsync()
        };

        var today = DateTime.Today;
        var current = (await _context.ShiftAssignments.AsNoTracking()
                .Where(w => w.EffectiveToDate == null || w.EffectiveToDate >= today).ToListAsync())
            .GroupBy(w => w.EmployeeId)
            // the row in force today, else the one coming next
            .ToDictionary(g => g.Key, g => g.Where(w => w.EffectiveFromDate <= today).OrderByDescending(w => w.Priority).ThenByDescending(w => w.EffectiveFromDate).FirstOrDefault()
                                          ?? g.OrderBy(w => w.EffectiveFromDate).First());

        var query = _context.Employees.AsNoTracking().Where(e => e.EmployeeStatus == "Active");
        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(e => e.DepartmentInfo!.DepartmentName == department);
        if (!string.IsNullOrWhiteSpace(designation))
            query = query.Where(e => e.DesignationInfo!.Name == designation);
        if (!vm.Filtered)
        {
            var own = current.Where(p => p.Value.DaysOfWeekMask != WorkWeek.Default).Select(p => p.Key).ToList();
            query = query.Where(e => own.Contains(e.uid));
        }

        var employees = await query.OrderBy(e => e.EmployeeID)
            .Select(e => new WorkWeekEmployeeVm
            {
                Uid = e.uid, EmployeeCode = e.EmployeeID, Name = e.EmployeeName,
                Designation = e.DesignationInfo!.Name, Department = e.DepartmentInfo!.DepartmentName, Project = e.ProjectInfo!.Name
            })
            .ToListAsync();
        foreach (var row in employees)
        {
            var w = current.GetValueOrDefault(row.Uid);
            row.Mask = w?.DaysOfWeekMask ?? WorkWeek.Default;
            row.Own = w != null;
            row.Since = w?.EffectiveFromDate;
        }
        vm.Rows = employees;
        return View(vm);
    }

    [HttpPost("/work-weeks")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetWorkWeeks(string? selectedIds, DayOfWeek[] days, DateTime from, string? notes, string? department, string? designation)
    {
        // one comma separated field, so a large selection stays within the form limit
        var ids = (selectedIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var i) ? i : 0).Where(i => i > 0).Distinct().ToList();
        var valid = await _context.Employees.AsNoTracking().Where(e => ids.Contains(e.uid)).Select(e => e.uid).ToListAsync();
        if (valid.Count == 0)
            TempData["ErrorMessage"] = "Tick at least one employee.";
        else if (days.Length == 0)
            TempData["ErrorMessage"] = "Tick at least one working day.";
        else if (!ValidDate(from))
            TempData["ErrorMessage"] = "Enter the date the work week starts from.";
        else if (notes?.Length > 1000)
            TempData["ErrorMessage"] = "The note is too long.";
        else
        {
            var mask = WorkWeek.Mask(days);
            var n = await _schedules.SetWorkWeekAsync(valid, mask, from, notes, User.Identity?.Name);
            TempData["SuccessMessage"] = $"{valid.Count} employee(s) now work {WorkWeek.Describe(mask)} from {Fmt.ShortDate(from)}." + Recounted(n);
        }
        return RedirectToAction(nameof(WorkWeeks), new { department, designation });
    }

    // ---------- per-person leave adjustments ----------

    [HttpGet("/employees/{id:int}/leave-adjustments")]
    public async Task<IActionResult> Adjustments(int id, int? year)
    {
        ViewData["Module"] = "Employees";
        var employee = await FindAsync(id);
        if (employee == null) return NotFound();
        var y = year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year;

        var types = await _context.LeaveTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync();
        var history = await (from o in _context.LeaveEntitlementOverrides.AsNoTracking()
                             join t in _context.LeaveTypes.AsNoTracking() on o.LeaveTypeId equals t.Id
                             where o.EmployeeId == id
                             orderby o.CreatedAt descending, o.Id descending
                             select new LeaveAdjustmentRowVm
                             {
                                 CreatedAt = o.CreatedAt, LeaveType = t.Name, Year = o.Year, Days = o.AdjustmentDays,
                                 Total = o.OverrideTotalDays, Reason = o.Reason, CreatedBy = o.CreatedBy
                             }).ToListAsync();
        return View(new LeaveAdjustmentsVm
        {
            Employee = employee,
            Year = y,
            Managed = await _rules.IsManagedAsync(employee),
            Types = types,
            Balances = await _context.LeaveBalances.AsNoTracking().Where(b => b.EmployeeId == id && b.Year == y).ToListAsync(),
            History = history
        });
    }

    /// <summary>Adds or removes days for one person, type and year. Entries are never edited: a mistake is corrected by the opposite entry.</summary>
    [HttpPost("/employees/{id:int}/leave-adjustments")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAdjustment(int id, int year, int leaveTypeId, decimal? days, string? reason, Guid requestId)
    {
        var employee = await FindAsync(id);
        if (employee == null) return NotFound();
        var userId = await _rules.UserIdAsync(User.Identity?.Name);

        string? error = null;
        if (!await _rules.IsManagedAsync(employee)) error = "Leave is not managed for this employee's project.";
        else if (year is < 2000 or > 2100) error = "Enter a year.";
        else if (!await _context.LeaveTypes.AnyAsync(t => t.Id == leaveTypeId && t.IsActive)) error = "Choose a leave type.";
        else if (days is not { } d || d == 0 || Math.Abs(d) > 366 || d * 2 != decimal.Truncate(d * 2)) error = "Enter whole or half days, for example 2, 0.5 or -1.";
        else if (string.IsNullOrWhiteSpace(reason)) error = "Give the reason.";
        else if (reason.Length > 500) error = "The reason is too long.";
        else if (requestId == Guid.Empty) error = "Reload the page and try again.";
        else if (userId == null) error = "Your account was not found, so the change cannot be recorded.";

        if (error != null)
            TempData["ErrorMessage"] = error;
        else if (await _context.LeaveEntitlementOverrides.AnyAsync(o => o.RequestId == requestId))
            TempData["SuccessMessage"] = "That adjustment was already saved.";
        else
        {
            var row = new LeaveEntitlementOverride
            {
                EmployeeId = id, LeaveTypeId = leaveTypeId, Year = year, AdjustmentDays = days, Reason = reason!.Trim(),
                ApprovedByUserId = userId!.Value, CreatedBy = User.Identity?.Name, CreatedAt = DateTime.Now, RequestId = requestId
            };
            _context.LeaveEntitlementOverrides.Add(row);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("LeaveEntitlementOverrides", "INSERT", row.Id.ToString(), id, null,
                new { row.LeaveTypeId, row.Year, row.AdjustmentDays, row.Reason }, "LMS");
            TempData["SuccessMessage"] = $"Entitlement adjusted by {Fmt.Days(days!.Value)} day(s) for {year}.";
        }
        return RedirectToAction(nameof(Adjustments), new { id, year });
    }
}
