using HRMBT.Web.Data;
using HRMBT.Web.Infrastructure;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers;

/// <summary>
/// Holiday notices. Everyone signed in can see them; Admin and HR add, change and remove them.
/// Every change recounts the open leave on the dates it touches.
/// </summary>
[Route("holidays")]
public class HolidaysController : Controller
{
    private const string Managers = "Admin,HR";
    /// <summary>A longer span is almost certainly a mistyped year or month.</summary>
    private const int MaxDays = 31;

    private readonly ApplicationDbContext _context;
    private readonly WorkScheduleService _schedules;
    private readonly IAuditLogService _audit;

    public HolidaysController(ApplicationDbContext context, WorkScheduleService schedules, IAuditLogService audit)
    {
        _context = context;
        _schedules = schedules;
        _audit = audit;
    }

    private static string Recounted(int n) => n == 0 ? "" : $" {n} leave request(s) were recounted.";

    /// <summary>People marked as working on dates in the range. Marks belong to a date, not to a holiday notice.</summary>
    private IQueryable<RosterOverride> MarksBetween(DateTime from, DateTime to) =>
        _context.RosterOverrides.AsNoTracking().Where(o => o.OverrideType == "ExtraDuty" && o.OverrideDate >= from && o.OverrideDate <= to);

    private static object Snapshot(GazettedHoliday h) => new
    {
        h.HolidayName, h.HolidayDate, h.EndDate, h.NoticeNumber, h.NoticeDate, h.Description, h.IsOptional
    };

    private IActionResult Page(string view, object model)
    {
        ViewData["Module"] = "LMS";
        return View(view, model);
    }

    private Task<GazettedHoliday?> FindAsync(int id) => _context.GazettedHolidays.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);

    [HttpGet("")]
    public async Task<IActionResult> Index(int? year)
    {
        var y = year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year;
        var first = new DateTime(y, 1, 1);
        var last = new DateTime(y, 12, 31);
        var years = await _context.GazettedHolidays.AsNoTracking().Select(h => h.HolidayDate.Year).Distinct().ToListAsync();
        return Page("Index", new HolidayListVm
        {
            Year = y,
            Years = years.Concat(new[] { y, DateTime.Today.Year, DateTime.Today.Year + 1 }).Distinct().OrderByDescending(x => x).ToList(),
            Rows = await _context.GazettedHolidays.AsNoTracking()
                .Where(h => h.HolidayDate <= last && h.EndDate >= first).OrderBy(h => h.HolidayDate).ToListAsync(),
            CanManage = User.IsInRole("Admin") || User.IsInRole("HR")
        });
    }

    [HttpGet("new")]
    [Authorize(Roles = Managers)]
    public IActionResult Create() => Page("Form", new HolidayFormVm());

    [HttpPost("new")]
    [Authorize(Roles = Managers)]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(HolidayFormVm form)
    {
        form.Id = 0;
        return SaveAsync(form);
    }

    [HttpGet("{id:int}/edit")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> Edit(int id)
    {
        var h = await FindAsync(id);
        if (h == null) return NotFound();
        return Page("Form", new HolidayFormVm
        {
            Id = h.Id, Name = h.HolidayName, From = h.HolidayDate, To = h.EndDate, NoticeNumber = h.NoticeNumber,
            NoticeDate = h.NoticeDate, Description = h.Description, IsOptional = h.IsOptional
        });
    }

    [HttpPost("{id:int}/edit")]
    [Authorize(Roles = Managers)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, HolidayFormVm form)
    {
        if (await FindAsync(id) == null) return NotFound();
        form.Id = id;
        return await SaveAsync(form);
    }

    private async Task<IActionResult> SaveAsync(HolidayFormVm form)
    {
        var from = form.From?.Date ?? default;
        var to = (form.To ?? form.From)?.Date ?? default;   // a blank last day means one day
        if (ModelState.IsValid)
        {
            if (from.Year is < 2000 or > 2100 || to.Year is < 2000 or > 2100)
                ModelState.AddModelError(nameof(form.From), "Enter dates between 2000 and 2100.");
            else if (to < from)
                ModelState.AddModelError(nameof(form.To), "The last day cannot be before the first day.");
            else if ((to - from).Days >= MaxDays)
                ModelState.AddModelError(nameof(form.To), $"A holiday can be at most {MaxDays} days. Check the dates.");
            else
            {
                var other = await _context.GazettedHolidays.AsNoTracking()
                    .Where(h => h.ProjectId == null && h.Id != form.Id && h.HolidayDate <= to && h.EndDate >= from)
                    .OrderBy(h => h.HolidayDate).FirstOrDefaultAsync();
                if (other != null)
                    ModelState.AddModelError(nameof(form.From),
                        $"These dates overlap '{other.HolidayName}' ({Fmt.DateRange(other.HolidayDate, other.EndDate)}). Change that notice instead.");
            }
        }
        if (!ModelState.IsValid)
            return Page("Form", form);

        var actor = User.Identity?.Name;
        var old = form.Id == 0 ? null : await FindAsync(form.Id);
        int recounted;
        try
        {
            recounted = await _schedules.ChangeHolidaysAsync(async () =>
            {
                GazettedHoliday? row;
                object? before = null;
                var (start, end) = (from, to);
                if (form.Id == 0)
                {
                    row = new GazettedHoliday { CreatedBy = actor };
                    _context.GazettedHolidays.Add(row);
                }
                else
                {
                    row = await _context.GazettedHolidays.FirstAsync(h => h.Id == form.Id);
                    before = Snapshot(row);
                    // leave on the old dates has to be recounted as well
                    if (row.HolidayDate < start) start = row.HolidayDate;
                    if (row.EndDate > end) end = row.EndDate;
                    row.ModifiedAt = DateTime.Now;
                    row.ModifiedBy = actor;
                }
                row.HolidayName = form.Name!.Trim();
                row.HolidayDate = from;
                row.EndDate = to;
                row.NoticeNumber = Clean(form.NoticeNumber);
                row.NoticeDate = form.NoticeDate?.Date;
                row.Description = Clean(form.Description);
                row.IsOptional = form.IsOptional;
                await _context.SaveChangesAsync();
                await _audit.WriteAsync("GazettedHolidays", form.Id == 0 ? "INSERT" : "UPDATE", row.Id.ToString(), null, before, Snapshot(row), "Holidays");
                return (start, end);
            }, actor);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "The holiday was not saved. One starting on that date may have just been added.");
            return Page("Form", form);
        }

        var left = old == null ? 0 : await MarksBetween(old.HolidayDate, old.EndDate).CountAsync(o => o.OverrideDate < from || o.OverrideDate > to);
        TempData["SuccessMessage"] = $"{form.Name!.Trim()} ({Fmt.DateRange(from, to)}) saved." + Recounted(recounted)
            + (left == 0 ? "" : $" {left} \"working\" mark(s) are still on dates that are no longer part of it; check those people's schedules.");
        return RedirectToAction(nameof(Index), new { year = from.Year });
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ---------- who works a holiday (guards and shift staff) ----------

    /// <summary>The holiday if people can be marked to work it: an optional or one-project holiday is a work day anyway.</summary>
    private async Task<GazettedHoliday?> FindCompanyAsync(int id) =>
        await FindAsync(id) is { IsOptional: false, ProjectId: null } h ? h : null;

    private static List<DateTime> DaysOf(GazettedHoliday h) =>
        Enumerable.Range(0, (h.EndDate - h.HolidayDate).Days + 1).Select(i => h.HolidayDate.AddDays(i).Date).ToList();

    [HttpGet("{id:int}/working")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> Working(int id, string? department, string? designation)
    {
        var h = await FindCompanyAsync(id);
        if (h == null) return NotFound();

        var marks = (await MarksBetween(h.HolidayDate, h.EndDate).Select(o => new { o.EmployeeId, o.OverrideDate }).ToListAsync())
            .ToLookup(o => o.EmployeeId, o => o.OverrideDate.Date);
        var vm = new HolidayWorkingVm
        {
            Holiday = h,
            Days = DaysOf(h),
            Department = department,
            Designation = designation,
            MarkedPeople = marks.Count,
            Departments = await _context.Departments.AsNoTracking().Where(d => d.DepartmentName != null && d.IsActive)
                .OrderBy(d => d.DepartmentName).Select(d => d.DepartmentName!).Distinct().ToListAsync(),
            Designations = await _context.Designations.AsNoTracking().OrderBy(d => d.Name).Select(d => d.Name).Distinct().ToListAsync()
        };

        var query = _context.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(e => e.DepartmentInfo!.DepartmentName == department);
        if (!string.IsNullOrWhiteSpace(designation))
            query = query.Where(e => e.DesignationInfo!.Name == designation);
        if (vm.Filtered)
            query = query.Where(e => e.EmployeeStatus == "Active");
        else
        {
            var marked = marks.Select(g => g.Key).ToList();
            query = query.Where(e => marked.Contains(e.uid));
        }

        vm.Rows = await query.OrderBy(e => e.EmployeeID)
            .Select(e => new HolidayWorkerVm
            {
                Uid = e.uid, EmployeeCode = e.EmployeeID, Name = e.EmployeeName,
                Designation = e.DesignationInfo!.Name, Department = e.DepartmentInfo!.DepartmentName, Project = e.ProjectInfo!.Name
            })
            .ToListAsync();
        foreach (var row in vm.Rows)
            row.Marked = marks[row.Uid].OrderBy(d => d).ToList();
        return Page("Working", vm);
    }

    [HttpPost("{id:int}/working")]
    [Authorize(Roles = Managers)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetWorking(int id, string? selectedIds, DateTime[] dates, string? what, string? department, string? designation)
    {
        var h = await FindCompanyAsync(id);
        if (h == null) return NotFound();

        // one comma separated field, so a large selection stays within the form limit
        var ids = (selectedIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var i) ? i : 0).Where(i => i > 0).Distinct().ToList();
        var valid = await _context.Employees.AsNoTracking().Where(e => ids.Contains(e.uid)).Select(e => e.uid).ToListAsync();
        var days = dates.Select(d => d.Date).Intersect(DaysOf(h)).ToList();

        if (valid.Count == 0)
            TempData["ErrorMessage"] = "Tick at least one employee.";
        else if (days.Count == 0)
            TempData["ErrorMessage"] = "Tick at least one day of the holiday.";
        else if (what is not ("mark" or "remove"))
            TempData["ErrorMessage"] = "Choose to mark or to remove.";
        else
        {
            var mark = what == "mark";
            var (marks, recounted) = await _schedules.SetDutyAsync(valid, days, mark, $"Working {h.HolidayName}", User.Identity?.Name);
            TempData["SuccessMessage"] = (marks == 0
                ? "Nothing to change: those days were already that way."
                : $"{marks} employee day(s) {(mark ? "marked as working" : "no longer marked")} for {h.HolidayName}.") + Recounted(recounted);
        }
        return RedirectToAction(nameof(Working), new { id, department, designation });
    }

    [HttpGet("{id:int}/delete")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> Delete(int id)
    {
        var h = await FindAsync(id);
        if (h == null) return NotFound();
        ViewBag.Marks = await MarksBetween(h.HolidayDate, h.EndDate).CountAsync();
        return Page("Delete", h);
    }

    [HttpPost("{id:int}/delete")]
    [Authorize(Roles = Managers)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var h = await FindAsync(id);
        if (h == null)
        {
            TempData["SuccessMessage"] = "That holiday was already removed.";
            return RedirectToAction(nameof(Index));
        }
        var recounted = await _schedules.ChangeHolidaysAsync(async () =>
        {
            var row = await _context.GazettedHolidays.FirstOrDefaultAsync(x => x.Id == id);
            if (row != null)
            {
                _context.GazettedHolidays.Remove(row);
                await _context.SaveChangesAsync();
                await _audit.WriteAsync("GazettedHolidays", "DELETE", id.ToString(), null, Snapshot(row), null, "Holidays");
            }
            return (h.HolidayDate, h.EndDate);
        }, User.Identity?.Name);
        TempData["SuccessMessage"] = $"{h.HolidayName} removed." + Recounted(recounted);
        return RedirectToAction(nameof(Index), new { year = h.HolidayDate.Year });
    }
}
