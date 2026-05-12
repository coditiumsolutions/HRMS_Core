using System.Collections.Generic;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers;

public class AllowancesController : Controller
{
    private readonly ApplicationDbContext _context;

    public AllowancesController(ApplicationDbContext context)
    {
        _context = context;
    }

    private static IEnumerable<string> SplitConfigCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) yield break;
        foreach (var item in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(item))
                yield return item;
        }
    }

    private static bool ConfigKeyMatches(string? key, string match)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return string.Equals(key.Trim(), match, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<string>> GetAllowanceNamesFromConfigurationAsync()
    {
        var rows = await _context.HrConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey != null && c.ConfigValue != null)
            .ToListAsync();

        return rows
            .Where(c => ConfigKeyMatches(c.ConfigKey, "Allowances"))
            .SelectMany(c => SplitConfigCsv(c.ConfigValue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();
    }

    private static void ValidateAllowance(Allowance model, ModelStateDictionary modelState)
    {
        if (model.IsPercentage)
        {
            if (!model.PercentageValue.HasValue)
                modelState.AddModelError(nameof(model.PercentageValue), "Enter a percentage when using percentage-based allowance.");
            else if (model.PercentageValue.Value < 0 || model.PercentageValue.Value > 100)
                modelState.AddModelError(nameof(model.PercentageValue), "Percentage must be between 0 and 100.");
        }
        else if (model.Amount < 0)
            modelState.AddModelError(nameof(model.Amount), "Amount cannot be negative.");
    }

    private async Task LoadEmployeeSelectAsync(int? selectedUid = null)
    {
        var employees = await _context.Employees
            .AsNoTracking()
            .OrderBy(e => e.EmployeeName)
            .Select(e => new { e.uid, Label = e.EmployeeName + " (" + e.EmployeeID + ")" })
            .ToListAsync();

        var items = new List<SelectListItem>
        {
            new() { Value = "0", Text = "— Select employee —", Selected = !selectedUid.HasValue || selectedUid == 0 }
        };
        items.AddRange(employees.Select(e => new SelectListItem
        {
            Value = e.uid.ToString(),
            Text = e.Label,
            Selected = selectedUid == e.uid
        }));
        ViewBag.EmployeeOptions = items;
    }

    private async Task LoadDepartmentFilterOptionsAsync(int? selectedDeptId)
    {
        var departments = await _context.Departments
            .AsNoTracking()
            .OrderBy(d => d.DepartmentName)
            .ToListAsync();

        var deptItems = new List<SelectListItem>
        {
            new() { Value = "", Text = "All departments", Selected = !selectedDeptId.HasValue || selectedDeptId == 0 }
        };
        deptItems.AddRange(departments.Select(d => new SelectListItem
        {
            Value = d.DepartmentID.ToString(),
            Text = d.DepartmentName ?? ("#" + d.DepartmentID),
            Selected = selectedDeptId == d.DepartmentID
        }));
        ViewBag.DepartmentFilterOptions = deptItems;
    }

    /// <summary>View allowance rows filtered by employee department and/or employee ID, name, or internal uid.</summary>
    // GET: Allowances
    public async Task<IActionResult> Index(int? deptId = null, string? empSearch = null, bool? isActive = null, int page = 1, int pageSize = 20)
    {
        ViewData["Module"] = "Allowances";
        ViewData["Title"] = "Allowances";

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Allowances
            .AsNoTracking()
            .Include(a => a.Employee)
            .AsQueryable();

        if (deptId.HasValue && deptId.Value > 0)
        {
            var deptName = await _context.Departments
                .AsNoTracking()
                .Where(d => d.DepartmentID == deptId.Value)
                .Select(d => d.DepartmentName)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(deptName))
                query = query.Where(a => a.Employee != null && a.Employee.Department == deptName);
        }

        if (!string.IsNullOrWhiteSpace(empSearch))
        {
            var t = empSearch.Trim();
            if (int.TryParse(t, out var uidMatch))
            {
                query = query.Where(a =>
                    (a.Employee != null && a.Employee.EmployeeID.Contains(t)) ||
                    (a.Employee != null && a.Employee.EmployeeName != null && a.Employee.EmployeeName.Contains(t)) ||
                    a.EmployeeId == uidMatch);
            }
            else
            {
                query = query.Where(a =>
                    (a.Employee != null && a.Employee.EmployeeID.Contains(t)) ||
                    (a.Employee != null && a.Employee.EmployeeName != null && a.Employee.EmployeeName.Contains(t)));
            }
        }

        if (isActive == true)
            query = query.Where(a => a.IsActive);
        else if (isActive == false)
            query = query.Where(a => !a.IsActive);

        query = query.OrderByDescending(a => a.EffectiveDate).ThenBy(a => a.Name);

        var list = await PaginatedList<Allowance>.CreateAsync(query, page, pageSize);

        await LoadDepartmentFilterOptionsAsync(deptId);
        ViewBag.CurrentDeptId = deptId;
        ViewBag.CurrentEmpSearch = empSearch ?? "";
        ViewBag.CurrentIsActive = isActive;
        ViewBag.CurrentPageSize = pageSize;
        ViewBag.PageSizeOptions = new List<int> { 10, 20, 50, 100 };

        return View(list);
    }

    // GET: Allowances/Add — bulk add: filter employees, select checkboxes, apply configured allowance.
    public async Task<IActionResult> Add(int? deptId = null, string? empSearch = null, int ePage = 1, int ePageSize = 20)
    {
        ViewData["Module"] = "Allowances";
        ViewData["Title"] = "Add Allowances";

        if (ePage < 1) ePage = 1;
        if (ePageSize < 1) ePageSize = 20;
        if (ePageSize > 100) ePageSize = 100;

        var empQuery = _context.Employees.AsNoTracking().AsQueryable();

        if (deptId.HasValue && deptId.Value > 0)
        {
            var deptName = await _context.Departments
                .AsNoTracking()
                .Where(d => d.DepartmentID == deptId.Value)
                .Select(d => d.DepartmentName)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(deptName))
                empQuery = empQuery.Where(e => e.Department != null && e.Department == deptName);
        }

        if (!string.IsNullOrWhiteSpace(empSearch))
        {
            var t = empSearch.Trim();
            if (int.TryParse(t, out var uidMatch))
            {
                empQuery = empQuery.Where(e =>
                    e.EmployeeID.Contains(t) ||
                    (e.EmployeeName != null && e.EmployeeName.Contains(t)) ||
                    e.uid == uidMatch);
            }
            else
            {
                empQuery = empQuery.Where(e =>
                    e.EmployeeID.Contains(t) ||
                    (e.EmployeeName != null && e.EmployeeName.Contains(t)));
            }
        }

        empQuery = empQuery.OrderBy(e => e.EmployeeName);
        var employeesPage = await PaginatedList<Employee>.CreateAsync(empQuery, ePage, ePageSize);

        await LoadDepartmentFilterOptionsAsync(deptId);
        ViewBag.CurrentDeptId = deptId;
        ViewBag.CurrentEmpSearch = empSearch ?? "";
        ViewBag.CurrentEPage = ePage;
        ViewBag.CurrentEPageSize = ePageSize;
        ViewBag.EPageSizeOptions = new List<int> { 10, 20, 50, 100 };

        var allowanceNames = await GetAllowanceNamesFromConfigurationAsync();
        var nameItems = new List<SelectListItem>
        {
            new() { Value = "", Text = "— Select allowance name —", Selected = true }
        };
        nameItems.AddRange(allowanceNames.Select(n => new SelectListItem { Value = n, Text = n }));
        ViewBag.AllowanceNameOptions = nameItems;

        return View(employeesPage);
    }

    /// <summary>Add the same configured allowance (name + fixed amount) for each selected employee.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCreateAllowances(
        List<int>? selectedEmployeeIds,
        string? allowanceName,
        decimal allowanceValue,
        int? deptId,
        string? empSearch,
        int ePage = 1,
        int ePageSize = 20)
    {
        ViewData["Module"] = "Allowances";

        var routeValues = new RouteValueDictionary
        {
            ["deptId"] = deptId,
            ["empSearch"] = empSearch,
            ["ePage"] = ePage,
            ["ePageSize"] = ePageSize
        };

        if (selectedEmployeeIds == null || selectedEmployeeIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Select at least one employee using the checkboxes.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        if (string.IsNullOrWhiteSpace(allowanceName))
        {
            TempData["ErrorMessage"] = "Select an allowance name from the configuration list.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        var allowedNames = await GetAllowanceNamesFromConfigurationAsync();
        var trimmedName = allowanceName.Trim();
        if (!allowedNames.Any(n => string.Equals(n, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["ErrorMessage"] = "The selected allowance name is not defined for ConfigKey Allowances.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        if (allowanceValue < 0)
        {
            TempData["ErrorMessage"] = "Allowance value cannot be negative.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        var distinctIds = selectedEmployeeIds.Distinct().ToList();
        var user = User.Identity?.Name ?? "System";
        var now = DateTime.Now;
        var type = trimmedName.Length <= 50 ? trimmedName : trimmedName[..50];

        int added = 0;
        foreach (var uid in distinctIds)
        {
            if (!await _context.Employees.AnyAsync(e => e.uid == uid))
                continue;

            _context.Allowances.Add(new Allowance
            {
                EmployeeId = uid,
                AllowanceType = type,
                Name = trimmedName,
                Amount = allowanceValue,
                IsPercentage = false,
                PercentageValue = null,
                EffectiveDate = DateTime.Today,
                EndDate = null,
                IsActive = true,
                CreatedDate = now,
                CreatedBy = user,
                ModifiedDate = null,
                ModifiedBy = null
            });
            added++;
        }

        if (added == 0)
        {
            TempData["ErrorMessage"] = "No valid employees were selected.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Allowance “{trimmedName}” ({allowanceValue:N2}) added for {added} employee(s).";
        return RedirectToAction(nameof(Add), routeValues);
    }

    // GET: Allowances/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        ViewData["Module"] = "Allowances";
        if (id == null) return NotFound();

        var allowance = await _context.Allowances
            .AsNoTracking()
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (allowance == null) return NotFound();

        return View(allowance);
    }

    // GET: Allowances/Create
    public async Task<IActionResult> Create()
    {
        ViewData["Module"] = "Allowances";
        await LoadEmployeeSelectAsync();
        return View(new Allowance
        {
            EffectiveDate = DateTime.Today,
            IsActive = true,
            CreatedBy = User.Identity?.Name ?? "System",
            Amount = 0,
            IsPercentage = false
        });
    }

    // POST: Allowances/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EmployeeId,AllowanceType,Name,Amount,IsPercentage,PercentageValue,EffectiveDate,EndDate,IsActive")] Allowance allowance)
    {
        ViewData["Module"] = "Allowances";
        ValidateAllowance(allowance, ModelState);

        if (allowance.EmployeeId <= 0)
            ModelState.AddModelError(nameof(allowance.EmployeeId), "Select an employee.");

        if (!await _context.Employees.AnyAsync(e => e.uid == allowance.EmployeeId))
            ModelState.AddModelError(nameof(allowance.EmployeeId), "Select a valid employee.");

        if (ModelState.IsValid)
        {
            allowance.CreatedDate = DateTime.Now;
            allowance.CreatedBy = User.Identity?.Name ?? "System";
            allowance.ModifiedDate = null;
            allowance.ModifiedBy = null;
            _context.Add(allowance);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Allowance created successfully.";
            return RedirectToAction(nameof(Index));
        }

        await LoadEmployeeSelectAsync(allowance.EmployeeId);
        return View(allowance);
    }

    // GET: Allowances/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        ViewData["Module"] = "Allowances";
        if (id == null) return NotFound();

        var allowance = await _context.Allowances.FindAsync(id);
        if (allowance == null) return NotFound();

        await LoadEmployeeSelectAsync(allowance.EmployeeId);
        return View(allowance);
    }

    // POST: Allowances/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeId,AllowanceType,Name,Amount,IsPercentage,PercentageValue,EffectiveDate,EndDate,IsActive,CreatedDate,CreatedBy")] Allowance posted)
    {
        ViewData["Module"] = "Allowances";
        if (id != posted.Id) return NotFound();

        ValidateAllowance(posted, ModelState);

        if (posted.EmployeeId <= 0)
            ModelState.AddModelError(nameof(posted.EmployeeId), "Select an employee.");

        if (!await _context.Employees.AnyAsync(e => e.uid == posted.EmployeeId))
            ModelState.AddModelError(nameof(posted.EmployeeId), "Select a valid employee.");

        if (ModelState.IsValid)
        {
            try
            {
                var existing = await _context.Allowances.FindAsync(id);
                if (existing == null) return NotFound();

                existing.EmployeeId = posted.EmployeeId;
                existing.AllowanceType = posted.AllowanceType;
                existing.Name = posted.Name;
                existing.Amount = posted.Amount;
                existing.IsPercentage = posted.IsPercentage;
                existing.PercentageValue = posted.PercentageValue;
                existing.EffectiveDate = posted.EffectiveDate;
                existing.EndDate = posted.EndDate;
                existing.IsActive = posted.IsActive;
                existing.ModifiedDate = DateTime.Now;
                existing.ModifiedBy = User.Identity?.Name ?? "System";

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Allowance updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await AllowanceExistsAsync(posted.Id))
                    return NotFound();
                throw;
            }
        }

        await LoadEmployeeSelectAsync(posted.EmployeeId);
        return View(posted);
    }

    // GET: Allowances/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        ViewData["Module"] = "Allowances";
        if (id == null) return NotFound();

        var allowance = await _context.Allowances
            .AsNoTracking()
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (allowance == null) return NotFound();

        return View(allowance);
    }

    // POST: Allowances/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var allowance = await _context.Allowances.FindAsync(id);
        if (allowance != null)
        {
            _context.Allowances.Remove(allowance);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Allowance deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    private Task<bool> AllowanceExistsAsync(int id) =>
        _context.Allowances.AnyAsync(e => e.Id == id);
}
