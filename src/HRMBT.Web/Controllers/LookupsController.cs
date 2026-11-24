using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers;

/// <summary>
/// Setup pages for the HR-managed lists: projects, departments, sub-departments, designations and leave types
/// (decisions B2, B3, B4; db_changes.md). Nothing here is hard-coded: HR adds, renames, reorders and deactivates rows.
/// A row that is in use is deactivated, never deleted (db_standards S6, S14).
/// </summary>
[Authorize(Roles = "Admin,HR")]
[Route("setup/lists")]
public class LookupsController : Controller
{
    private static readonly string[] Kinds = { "projects", "departments", "subdepartments", "designations", "leavetypes" };

    private readonly ApplicationDbContext _context;

    public LookupsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ---------- list ----------

    [HttpGet("{kind?}")]
    public async Task<IActionResult> Index(string? kind, int? departmentId)
    {
        kind = NormalizeKind(kind);
        ViewData["Module"] = "Setup";
        var vm = new LookupListVm { Kind = kind, DepartmentFilterId = departmentId };

        switch (kind)
        {
            case "projects":
                vm.Title = "Projects";
                vm.Description = "The list HR works with. Add, rename, reorder or deactivate projects here; the system has no fixed number of projects.";
                vm.DetailHeader = "Code / leave management";
                vm.Rows = await _context.Projects.AsNoTracking()
                    .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                    .Select(p => new LookupRowVm
                    {
                        Id = p.Id, Name = p.Name, SortOrder = p.SortOrder, IsActive = p.IsActive,
                        Detail = (p.Code ?? "—") + " · leave " + (p.HasLeaveManagement ? "managed" : "not managed"),
                        Usage = _context.Employees.Count(e => e.ProjectId == p.Id) + _context.GazettedHolidays.Count(h => h.ProjectId == p.Id)
                    }).ToListAsync();
                break;

            case "departments":
                vm.Title = "Departments";
                vm.Description = "The main departments, shared by all projects (Administration, Operations, Security...). A function inside a department is a sub-department.";
                vm.DetailHeader = "Sub-departments";
                vm.Rows = await _context.Departments.AsNoTracking()
                    .Where(d => d.DepartmentName != null)
                    .OrderBy(d => d.SortOrder).ThenBy(d => d.DepartmentName)
                    .Select(d => new LookupRowVm
                    {
                        Id = d.DepartmentID, Name = d.DepartmentName!, SortOrder = d.SortOrder, IsActive = d.IsActive,
                        Detail = _context.SubDepartments.Count(s => s.DepartmentId == d.DepartmentID) + " sub-department(s)",
                        Usage = _context.Employees.Count(e => e.DepartmentId == d.DepartmentID)
                    }).ToListAsync();
                break;

            case "subdepartments":
                vm.Title = "Sub-departments";
                vm.Description = "Optional detail under a department (for example Mess under Administration). For information only: no rule, payroll or approval reads it.";
                vm.DetailHeader = "Department";
                vm.Departments = await DepartmentChoicesAsync();
                var sq = _context.SubDepartments.AsNoTracking().AsQueryable();
                if (departmentId.HasValue) sq = sq.Where(s => s.DepartmentId == departmentId.Value);
                vm.Rows = await sq
                    .OrderBy(s => _context.Departments.Where(d => d.DepartmentID == s.DepartmentId).Select(d => d.DepartmentName).FirstOrDefault())
                    .ThenBy(s => s.SortOrder).ThenBy(s => s.Name)
                    .Select(s => new LookupRowVm
                    {
                        Id = s.Id, Name = s.Name, SortOrder = s.SortOrder, IsActive = s.IsActive,
                        Detail = _context.Departments.Where(d => d.DepartmentID == s.DepartmentId).Select(d => d.DepartmentName).FirstOrDefault() ?? "",
                        Usage = _context.Employees.Count(e => e.SubDepartmentId == s.Id)
                    }).ToListAsync();
                break;

            case "designations":
                vm.Title = "Designations";
                vm.Description = "Job titles. The rank decides the order in employee lists (lower = more senior; equal ranks sort by name).";
                vm.DetailHeader = "Rank";
                vm.SortHeader = "Rank";
                vm.Rows = await _context.Designations.AsNoTracking()
                    .OrderBy(d => d.Rank).ThenBy(d => d.Name)
                    .Select(d => new LookupRowVm
                    {
                        Id = d.Id, Name = d.Name, SortOrder = d.Rank, IsActive = d.IsActive,
                        Detail = "rank " + d.Rank,
                        Usage = _context.Employees.Count(e => e.DesignationId == d.Id)
                    }).ToListAsync();
                break;

            default:
                vm.Title = "Leave types";
                vm.Description = "Leave types used by leave requests and quotas. Whether a type is paid is recorded here; its effect on payroll is not applied yet.";
                vm.DetailHeader = "Code / options";
                vm.Rows = await _context.LeaveTypes.AsNoTracking()
                    .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
                    .Select(t => new LookupRowVm
                    {
                        Id = t.Id, Name = t.Name, SortOrder = t.SortOrder, IsActive = t.IsActive,
                        Detail = t.Code + " · " + (t.IsPaid ? "paid" : "unpaid") + (t.IsHalfDayAllowed ? " · half day allowed" : "") + (t.AllowsCarryForward ? (t.MaxCarryForwardDays != null ? " · carry forward up to " + t.MaxCarryForwardDays + " days" : " · carries forward") : " · no carry-forward"),
                        Usage = _context.LeaveRequests.Count(r => r.LeaveTypeId == t.Id)
                                + _context.LeaveQuotas.Count(q => q.LeaveTypeId == t.Id)
                                + _context.CarryforwardLeaves.Count(c => c.LeaveTypeId == t.Id)
                                + _context.LeaveEntitlementOverrides.Count(o => o.LeaveTypeId == t.Id)
                    }).ToListAsync();
                break;
        }

        return View(vm);
    }

    // ---------- create ----------

    [HttpGet("{kind}/new")]
    public async Task<IActionResult> Create(string? kind, int? departmentId)
    {
        kind = NormalizeKind(kind);
        ViewData["Module"] = "Setup";
        var vm = new LookupFormVm { Kind = kind, DepartmentId = departmentId ?? 0 };
        await LoadFormLookupsAsync(vm);
        return View("Edit", vm);
    }

    [HttpPost("{kind}/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LookupFormVm model)
    {
        model.Kind = NormalizeKind(model.Kind);
        ViewData["Module"] = "Setup";
        Normalize(model);
        await ValidateAsync(model, isNew: true);
        if (!ModelState.IsValid)
        {
            await LoadFormLookupsAsync(model);
            return View("Edit", model);
        }

        var actor = User.Identity?.Name ?? "System";
        try
        {
            switch (model.Kind)
            {
                case "projects":
                    _context.Projects.Add(new Project { Name = model.Name, Code = model.Code, SortOrder = model.SortOrder, HasLeaveManagement = model.HasLeaveManagement, IsActive = model.IsActive, CreatedBy = actor });
                    break;
                case "departments":
                    _context.Departments.Add(new Department { DepartmentName = model.Name, SortOrder = model.SortOrder, IsActive = model.IsActive, CreatedBy = actor });
                    break;
                case "subdepartments":
                    _context.SubDepartments.Add(new SubDepartment { DepartmentId = model.DepartmentId, Name = model.Name, SortOrder = model.SortOrder, IsActive = model.IsActive, CreatedBy = actor });
                    break;
                case "designations":
                    _context.Designations.Add(new Designation { Name = model.Name, Rank = model.Rank, IsActive = model.IsActive, CreatedBy = actor });
                    break;
                default:
                    _context.LeaveTypes.Add(new LeaveType { Code = model.Code!, Name = model.Name, IsPaid = model.IsPaid, IsHalfDayAllowed = model.IsHalfDayAllowed, AllowsCarryForward = model.AllowsCarryForward, MaxCarryForwardDays = model.AllowsCarryForward ? model.MaxCarryForwardDays : null, SortOrder = model.SortOrder, IsActive = model.IsActive, CreatedBy = actor });
                    break;
            }
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // another save with the same name won the race (the unique index is the real guard)
            ModelState.AddModelError(nameof(model.Name), "An entry with this name already exists.");
            await LoadFormLookupsAsync(model);
            return View("Edit", model);
        }

        TempData["SuccessMessage"] = $"\"{model.Name}\" was added.";
        return RedirectToAction(nameof(Index), new { kind = model.Kind, departmentId = model.Kind == "subdepartments" ? model.DepartmentId : (int?)null });
    }

    // ---------- edit ----------

    [HttpGet("{kind}/{id}/edit")]
    public async Task<IActionResult> Edit(string? kind, int id)
    {
        kind = NormalizeKind(kind);
        ViewData["Module"] = "Setup";
        var vm = await LoadFormAsync(kind, id);
        if (vm == null) return NotFound();
        await LoadFormLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost("{kind}/{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(LookupFormVm model)
    {
        model.Kind = NormalizeKind(model.Kind);
        ViewData["Module"] = "Setup";
        Normalize(model);
        await ValidateAsync(model, isNew: false);
        if (!ModelState.IsValid)
        {
            await LoadFormLookupsAsync(model);
            return View(model);
        }

        var actor = User.Identity?.Name ?? "System";
        var now = DateTime.Now;
        try
        {
            switch (model.Kind)
            {
                case "projects":
                {
                    var p = await _context.Projects.FindAsync(model.Id);
                    if (p == null) return NotFound();
                    p.Name = model.Name; p.Code = model.Code; p.SortOrder = model.SortOrder;
                    p.HasLeaveManagement = model.HasLeaveManagement; p.IsActive = model.IsActive;
                    p.ModifiedAt = now; p.ModifiedBy = actor;
                    break;
                }
                case "departments":
                {
                    var d = await _context.Departments.FindAsync(model.Id);
                    if (d == null) return NotFound();
                    d.DepartmentName = model.Name; d.SortOrder = model.SortOrder; d.IsActive = model.IsActive;
                    d.ModifiedAt = now; d.ModifiedBy = actor;
                    break;
                }
                case "subdepartments":
                {
                    var s = await _context.SubDepartments.FindAsync(model.Id);
                    if (s == null) return NotFound();
                    // an employee row stores (department, sub-department) together, so a sub-department cannot move to another department
                    if (s.DepartmentId != model.DepartmentId && await _context.Employees.AnyAsync(e => e.SubDepartmentId == s.Id))
                    {
                        ModelState.AddModelError(nameof(model.DepartmentId), "Employees use this sub-department, so it cannot move to another department. Add a new sub-department and move the employees instead.");
                        await LoadFormLookupsAsync(model);
                        return View(model);
                    }
                    s.DepartmentId = model.DepartmentId; s.Name = model.Name; s.SortOrder = model.SortOrder; s.IsActive = model.IsActive;
                    s.ModifiedAt = now; s.ModifiedBy = actor;
                    break;
                }
                case "designations":
                {
                    var g = await _context.Designations.FindAsync(model.Id);
                    if (g == null) return NotFound();
                    g.Name = model.Name; g.Rank = model.Rank; g.IsActive = model.IsActive;
                    g.ModifiedAt = now; g.ModifiedBy = actor;
                    break;
                }
                default:
                {
                    var t = await _context.LeaveTypes.FindAsync(model.Id);
                    if (t == null) return NotFound();
                    t.Code = model.Code!; t.Name = model.Name; t.IsPaid = model.IsPaid; t.IsHalfDayAllowed = model.IsHalfDayAllowed;
                    t.AllowsCarryForward = model.AllowsCarryForward; t.MaxCarryForwardDays = model.AllowsCarryForward ? model.MaxCarryForwardDays : null;
                    t.SortOrder = model.SortOrder; t.IsActive = model.IsActive;
                    t.ModifiedAt = now; t.ModifiedBy = actor;
                    break;
                }
            }
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(model.Name), "An entry with this name already exists.");
            await LoadFormLookupsAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = $"\"{model.Name}\" was saved.";
        return RedirectToAction(nameof(Index), new { kind = model.Kind, departmentId = model.Kind == "subdepartments" ? model.DepartmentId : (int?)null });
    }

    // ---------- activate / deactivate / delete ----------

    [HttpPost("{kind}/{id}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string? kind, int id, int? departmentId)
    {
        kind = NormalizeKind(kind);
        var actor = User.Identity?.Name ?? "System";
        var now = DateTime.Now;
        string? name = null;
        bool? active = null;

        switch (kind)
        {
            case "projects":
                var p = await _context.Projects.FindAsync(id);
                if (p != null) { p.IsActive = !p.IsActive; p.ModifiedAt = now; p.ModifiedBy = actor; name = p.Name; active = p.IsActive; }
                break;
            case "departments":
                var d = await _context.Departments.FindAsync(id);
                if (d != null) { d.IsActive = !d.IsActive; d.ModifiedAt = now; d.ModifiedBy = actor; name = d.DepartmentName; active = d.IsActive; }
                break;
            case "subdepartments":
                var s = await _context.SubDepartments.FindAsync(id);
                if (s != null) { s.IsActive = !s.IsActive; s.ModifiedAt = now; s.ModifiedBy = actor; name = s.Name; active = s.IsActive; }
                break;
            case "designations":
                var g = await _context.Designations.FindAsync(id);
                if (g != null) { g.IsActive = !g.IsActive; g.ModifiedAt = now; g.ModifiedBy = actor; name = g.Name; active = g.IsActive; }
                break;
            default:
                var t = await _context.LeaveTypes.FindAsync(id);
                if (t != null) { t.IsActive = !t.IsActive; t.ModifiedAt = now; t.ModifiedBy = actor; name = t.Name; active = t.IsActive; }
                break;
        }

        if (name == null) return NotFound();
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = active == true
            ? $"\"{name}\" is active again."
            : $"\"{name}\" was deactivated. It is hidden from new entries; existing records keep it.";
        return RedirectToAction(nameof(Index), new { kind, departmentId });
    }

    [HttpPost("{kind}/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string? kind, int id, int? departmentId)
    {
        kind = NormalizeKind(kind);
        string? name = null;
        int usage = 0;

        switch (kind)
        {
            case "projects":
                var p = await _context.Projects.FindAsync(id);
                if (p == null) return NotFound();
                name = p.Name;
                usage = await _context.Employees.CountAsync(e => e.ProjectId == id) + await _context.GazettedHolidays.CountAsync(h => h.ProjectId == id);
                if (usage == 0) _context.Projects.Remove(p);
                break;
            case "departments":
                var d = await _context.Departments.FindAsync(id);
                if (d == null) return NotFound();
                name = d.DepartmentName;
                usage = await _context.Employees.CountAsync(e => e.DepartmentId == id) + await _context.SubDepartments.CountAsync(s => s.DepartmentId == id);
                if (usage == 0) _context.Departments.Remove(d);
                break;
            case "subdepartments":
                var s = await _context.SubDepartments.FindAsync(id);
                if (s == null) return NotFound();
                name = s.Name;
                usage = await _context.Employees.CountAsync(e => e.SubDepartmentId == id);
                if (usage == 0) _context.SubDepartments.Remove(s);
                break;
            case "designations":
                var g = await _context.Designations.FindAsync(id);
                if (g == null) return NotFound();
                name = g.Name;
                usage = await _context.Employees.CountAsync(e => e.DesignationId == id);
                if (usage == 0) _context.Designations.Remove(g);
                break;
            default:
                var t = await _context.LeaveTypes.FindAsync(id);
                if (t == null) return NotFound();
                name = t.Name;
                usage = await _context.LeaveRequests.CountAsync(r => r.LeaveTypeId == id)
                        + await _context.LeaveQuotas.CountAsync(q => q.LeaveTypeId == id)
                        + await _context.CarryforwardLeaves.CountAsync(c => c.LeaveTypeId == id)
                        + await _context.LeaveEntitlementOverrides.CountAsync(o => o.LeaveTypeId == id);
                if (usage == 0) _context.LeaveTypes.Remove(t);
                break;
        }

        if (usage > 0)
        {
            TempData["ErrorMessage"] = $"\"{name}\" is used by {usage} record(s) and cannot be deleted. Deactivate it instead.";
        }
        else
        {
            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"\"{name}\" was deleted.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = $"\"{name}\" is still referenced by other records and cannot be deleted. Deactivate it instead.";
            }
        }

        return RedirectToAction(nameof(Index), new { kind, departmentId });
    }

    // ---------- helpers ----------

    private static string NormalizeKind(string? kind)
    {
        var k = (kind ?? "").Trim().ToLowerInvariant();
        return Kinds.Contains(k) ? k : "projects";
    }

    private static void Normalize(LookupFormVm m)
    {
        m.Name = (m.Name ?? string.Empty).Trim();
        m.Code = string.IsNullOrWhiteSpace(m.Code) ? null : m.Code.Trim();
        if (m.Kind == "leavetypes" && m.Code != null) m.Code = m.Code.ToUpperInvariant();
    }

    private async Task<List<(int Id, string Name)>> DepartmentChoicesAsync()
    {
        var rows = await _context.Departments.AsNoTracking()
            .Where(d => d.DepartmentName != null)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.DepartmentName)
            .Select(d => new { d.DepartmentID, d.DepartmentName })
            .ToListAsync();
        return rows.Select(r => (r.DepartmentID, r.DepartmentName!)).ToList();
    }

    private async Task LoadFormLookupsAsync(LookupFormVm vm)
    {
        ViewBag.Departments = vm.Kind == "subdepartments" ? await DepartmentChoicesAsync() : new List<(int Id, string Name)>();
    }

    private async Task ValidateAsync(LookupFormVm m, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(m.Name))
        {
            ModelState.AddModelError(nameof(m.Name), "Name is required.");
            return;
        }

        switch (m.Kind)
        {
            case "projects":
                if (await _context.Projects.AnyAsync(p => p.Name == m.Name && (isNew || p.Id != m.Id)))
                    ModelState.AddModelError(nameof(m.Name), "A project with this name already exists.");
                break;
            case "departments":
                if (await _context.Departments.AnyAsync(d => d.DepartmentName == m.Name && (isNew || d.DepartmentID != m.Id)))
                    ModelState.AddModelError(nameof(m.Name), "A department with this name already exists.");
                break;
            case "subdepartments":
                if (m.DepartmentId <= 0 || !await _context.Departments.AnyAsync(d => d.DepartmentID == m.DepartmentId))
                    ModelState.AddModelError(nameof(m.DepartmentId), "Please select a department.");
                else if (await _context.SubDepartments.AnyAsync(s => s.DepartmentId == m.DepartmentId && s.Name == m.Name && (isNew || s.Id != m.Id)))
                    ModelState.AddModelError(nameof(m.Name), "This department already has a sub-department with this name.");
                break;
            case "designations":
                if (m.Rank < 0 || m.Rank > 10000)
                    ModelState.AddModelError(nameof(m.Rank), "Rank must be between 0 and 10000.");
                if (await _context.Designations.AnyAsync(g => g.Name == m.Name && (isNew || g.Id != m.Id)))
                    ModelState.AddModelError(nameof(m.Name), "A designation with this name already exists.");
                break;
            default:
                if (m.Name.Length > 50)
                    ModelState.AddModelError(nameof(m.Name), "Leave type names are at most 50 characters.");
                if (string.IsNullOrWhiteSpace(m.Code))
                    ModelState.AddModelError(nameof(m.Code), "A short code is required (for example AL, CL, SL).");
                else if (m.Code.Length > 10)
                    ModelState.AddModelError(nameof(m.Code), "The code is at most 10 characters.");
                else if (await _context.LeaveTypes.AnyAsync(t => t.Code == m.Code && (isNew || t.Id != m.Id)))
                    ModelState.AddModelError(nameof(m.Code), "This code is already used by another leave type.");
                if (await _context.LeaveTypes.AnyAsync(t => t.Name == m.Name && (isNew || t.Id != m.Id)))
                    ModelState.AddModelError(nameof(m.Name), "A leave type with this name already exists.");
                break;
        }
    }

    private async Task<LookupFormVm?> LoadFormAsync(string kind, int id)
    {
        switch (kind)
        {
            case "projects":
                var p = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                return p == null ? null : new LookupFormVm { Kind = kind, Id = p.Id, Name = p.Name, Code = p.Code, SortOrder = p.SortOrder, IsActive = p.IsActive, HasLeaveManagement = p.HasLeaveManagement };
            case "departments":
                var d = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.DepartmentID == id);
                return d == null ? null : new LookupFormVm { Kind = kind, Id = d.DepartmentID, Name = d.DepartmentName ?? "", SortOrder = d.SortOrder, IsActive = d.IsActive };
            case "subdepartments":
                var s = await _context.SubDepartments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                return s == null ? null : new LookupFormVm { Kind = kind, Id = s.Id, Name = s.Name, DepartmentId = s.DepartmentId, SortOrder = s.SortOrder, IsActive = s.IsActive };
            case "designations":
                var g = await _context.Designations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                return g == null ? null : new LookupFormVm { Kind = kind, Id = g.Id, Name = g.Name, Rank = g.Rank, IsActive = g.IsActive };
            default:
                var t = await _context.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                return t == null ? null : new LookupFormVm { Kind = kind, Id = t.Id, Name = t.Name, Code = t.Code, SortOrder = t.SortOrder, IsActive = t.IsActive, IsPaid = t.IsPaid, IsHalfDayAllowed = t.IsHalfDayAllowed, AllowsCarryForward = t.AllowsCarryForward, MaxCarryForwardDays = t.MaxCarryForwardDays };
        }
    }
}
