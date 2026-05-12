using System.Collections.Generic;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers;

public class DeductionsController : Controller
{
    private const string DeductionTypesConfigKey = "DeductionTypes";

    private static readonly string[] FallbackDeductionTypes =
    {
        "Tax", "Provident Fund", "Loan", "Insurance", "Advance", "Other"
    };

    private static readonly string[] CalculationMethods = { "Fixed", "Percentage" };

    private static readonly string[] Frequencies =
    {
        "Monthly", "Once", "Bi-Weekly", "Weekly", "Annual"
    };

    private readonly ApplicationDbContext _context;

    public DeductionsController(ApplicationDbContext context)
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

    /// <summary>Types from <c>dbo.Configuration</c> where <c>ConfigKey</c> is DeductionTypes (CSV in ConfigValue).</summary>
    private async Task<List<string>> GetDeductionTypesFromConfigurationAsync()
    {
        var rows = await _context.HrConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey != null && c.ConfigValue != null)
            .ToListAsync();

        return rows
            .Where(c => ConfigKeyMatches(c.ConfigKey, DeductionTypesConfigKey))
            .SelectMany(c => SplitConfigCsv(c.ConfigValue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();
    }

    private static void ValidateDeduction(Deduction model, ModelStateDictionary modelState)
    {
        if (string.IsNullOrWhiteSpace(model.DeductionName))
            modelState.AddModelError(nameof(model.DeductionName), "Deduction name is required.");

        if (string.IsNullOrWhiteSpace(model.Frequency))
            modelState.AddModelError(nameof(model.Frequency), "Frequency is required.");

        var isPct = string.Equals(model.CalculationMethod, "Percentage", StringComparison.OrdinalIgnoreCase);
        if (isPct)
        {
            if (!model.PercentageValue.HasValue)
                modelState.AddModelError(nameof(model.PercentageValue), "Enter a percentage for percentage-based deductions.");
            else if (model.PercentageValue.Value < 0 || model.PercentageValue.Value > 100)
                modelState.AddModelError(nameof(model.PercentageValue), "Percentage must be between 0 and 100.");
        }
        else if (string.Equals(model.CalculationMethod, "Fixed", StringComparison.OrdinalIgnoreCase))
        {
            if (!model.PercentageValue.HasValue || model.PercentageValue.Value < 0)
                modelState.AddModelError(nameof(model.PercentageValue), "Enter a fixed amount (PKR) for fixed deductions.");
        }
    }

    private async Task LoadEmployeeSelectAsync(int? selectedUid = null)
    {
        var employees = await _context.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeStatus == "Active")
            .OrderBy(e => e.EmployeeName)
            .Select(e => new { e.uid, Label = (e.EmployeeName ?? "") + " (" + e.EmployeeID + ")" })
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

    private async Task LoadDeductionTypeSelectAsync(string? selected = null)
    {
        var types = await GetDeductionTypesFromConfigurationAsync();
        if (types.Count == 0)
            types = FallbackDeductionTypes.ToList();

        ViewBag.DeductionType = new SelectList(
            types.Select(t => new SelectListItem
            {
                Value = t,
                Text = t,
                Selected = !string.IsNullOrEmpty(selected) && string.Equals(t, selected, StringComparison.OrdinalIgnoreCase)
            }).ToList());
    }

    private void LoadCalculationMethodSelect(string? selected = null) =>
        ViewBag.CalculationMethod = new SelectList(
            CalculationMethods.Select(t => new SelectListItem
            {
                Value = t,
                Text = t,
                Selected = !string.IsNullOrEmpty(selected) && string.Equals(t, selected, StringComparison.OrdinalIgnoreCase)
            }).ToList());

    private void LoadFrequencySelect(string? selected = null)
    {
        var sel = string.IsNullOrWhiteSpace(selected) ? "Monthly" : selected!;
        ViewBag.Frequency = new SelectList(
            Frequencies.Select(t => new SelectListItem
            {
                Value = t,
                Text = t,
                Selected = string.Equals(t, sel, StringComparison.OrdinalIgnoreCase)
            }).ToList());
    }

    // GET: Deductions
    public async Task<IActionResult> Index(int? deptId = null, string? empSearch = null, bool? isActive = null, int page = 1, int pageSize = 20)
    {
        ViewData["Module"] = "Deductions";
        ViewData["Title"] = "Deductions";

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Deductions
            .AsNoTracking()
            .Include(d => d.Employee)
            .AsQueryable();

        if (deptId.HasValue && deptId.Value > 0)
        {
            var deptName = await _context.Departments
                .AsNoTracking()
                .Where(d => d.DepartmentID == deptId.Value)
                .Select(d => d.DepartmentName)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(deptName))
                query = query.Where(d => d.Employee != null && d.Employee.Department == deptName);
        }

        if (!string.IsNullOrWhiteSpace(empSearch))
        {
            var t = empSearch.Trim();
            if (int.TryParse(t, out var uidMatch))
            {
                query = query.Where(d =>
                    (d.Employee != null && d.Employee.EmployeeID != null && d.Employee.EmployeeID.Contains(t)) ||
                    (d.Employee != null && d.Employee.EmployeeName != null && d.Employee.EmployeeName.Contains(t)) ||
                    d.EmployeeId == uidMatch);
            }
            else
            {
                query = query.Where(d =>
                    (d.Employee != null && d.Employee.EmployeeID != null && d.Employee.EmployeeID.Contains(t)) ||
                    (d.Employee != null && d.Employee.EmployeeName != null && d.Employee.EmployeeName.Contains(t)));
            }
        }

        if (isActive == true)
            query = query.Where(d => d.IsActive);
        else if (isActive == false)
            query = query.Where(d => !d.IsActive);

        query = query.OrderByDescending(d => d.EffectiveDate).ThenBy(d => d.DeductionName);

        var list = await PaginatedList<Deduction>.CreateAsync(query, page, pageSize);

        await LoadDepartmentFilterOptionsAsync(deptId);
        ViewBag.CurrentDeptId = deptId;
        ViewBag.CurrentEmpSearch = empSearch ?? "";
        ViewBag.CurrentIsActive = isActive;
        ViewBag.CurrentPageSize = pageSize;
        ViewBag.PageSizeOptions = new List<int> { 10, 20, 50, 100 };

        return View(list);
    }

    // GET: Deductions/Add — bulk: filter employees, checkboxes, department + frequency + type from Configuration.
    public async Task<IActionResult> Add(int? deptId = null, string? empSearch = null, int ePage = 1, int ePageSize = 20)
    {
        ViewData["Module"] = "Deductions";
        ViewData["Title"] = "Bulk add deductions";

        if (ePage < 1) ePage = 1;
        if (ePageSize < 1) ePageSize = 20;
        if (ePageSize > 100) ePageSize = 100;

        var empQuery = _context.Employees.AsNoTracking()
            .Where(e => e.EmployeeStatus == "Active")
            .AsQueryable();

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
                    (e.EmployeeID != null && e.EmployeeID.Contains(t)) ||
                    (e.EmployeeName != null && e.EmployeeName.Contains(t)) ||
                    e.uid == uidMatch);
            }
            else
            {
                empQuery = empQuery.Where(e =>
                    (e.EmployeeID != null && e.EmployeeID.Contains(t)) ||
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

        var types = await GetDeductionTypesFromConfigurationAsync();
        var typeItems = new List<SelectListItem>
        {
            new() { Value = "", Text = "— Select deduction type —", Selected = true }
        };
        typeItems.AddRange(types.Select(t => new SelectListItem { Value = t, Text = t }));
        ViewBag.DeductionTypeOptions = typeItems;

        ViewBag.BulkFrequencyOptions = new List<SelectListItem>
        {
            new() { Value = "Monthly", Text = "Monthly", Selected = true },
            new() { Value = "Once", Text = "Once", Selected = false }
        };

        return View(employeesPage);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCreateDeductions(
        List<int>? selectedEmployeeIds,
        string? deductionType,
        string? frequency,
        decimal deductionAmount,
        string? deductionName,
        int? deptId,
        string? empSearch,
        int ePage = 1,
        int ePageSize = 20)
    {
        ViewData["Module"] = "Deductions";

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

        var allowedTypes = await GetDeductionTypesFromConfigurationAsync();
        if (allowedTypes.Count == 0)
        {
            TempData["ErrorMessage"] =
                $"No deduction types are configured. Add rows in dbo.Configuration with ConfigKey = '{DeductionTypesConfigKey}' and a comma-separated ConfigValue.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        if (string.IsNullOrWhiteSpace(deductionType))
        {
            TempData["ErrorMessage"] = "Select a deduction type.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        var trimmedType = deductionType.Trim();
        if (trimmedType.Length > 50)
            trimmedType = trimmedType[..50];

        if (!allowedTypes.Any(t => string.Equals(t, trimmedType, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["ErrorMessage"] = $"The selected deduction type is not listed under Configuration.{DeductionTypesConfigKey}.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        if (string.IsNullOrWhiteSpace(frequency))
        {
            TempData["ErrorMessage"] = "Select frequency (Once or Monthly).";
            return RedirectToAction(nameof(Add), routeValues);
        }

        var freqNorm = frequency.Trim();
        if (!string.Equals(freqNorm, "Once", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(freqNorm, "Monthly", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Frequency must be Once or Monthly.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        if (freqNorm.Equals("Once", StringComparison.OrdinalIgnoreCase))
            freqNorm = "Once";
        else
            freqNorm = "Monthly";

        if (deductionAmount < 0)
        {
            TempData["ErrorMessage"] = "Deduction amount cannot be negative.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        var name = string.IsNullOrWhiteSpace(deductionName) ? trimmedType : deductionName.Trim();
        if (name.Length > 100)
            name = name[..100];

        var distinctIds = selectedEmployeeIds.Distinct().ToList();
        var user = User.Identity?.Name ?? "System";
        var now = DateTime.Now;
        var typeForRow = trimmedType.Length <= 50 ? trimmedType : trimmedType[..50];

        int added = 0;
        foreach (var uid in distinctIds)
        {
            if (!await _context.Employees.AnyAsync(e => e.uid == uid && e.EmployeeStatus == "Active"))
                continue;

            _context.Deductions.Add(new Deduction
            {
                EmployeeId = uid,
                DeductionType = typeForRow,
                DeductionName = name,
                Frequency = freqNorm,
                CalculationMethod = "Fixed",
                PercentageValue = deductionAmount,
                IsMandatory = false,
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
            TempData["ErrorMessage"] = "No valid active employees were selected.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] =
            $"Deduction “{name}” ({trimmedType}, {freqNorm}, PKR {deductionAmount:N2} fixed) added for {added} employee(s).";
        return RedirectToAction(nameof(Add), routeValues);
    }

    // GET: Deductions/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        ViewData["Module"] = "Deductions";
        ViewData["Title"] = "Deduction details";
        if (id == null) return NotFound();

        var deduction = await _context.Deductions
            .AsNoTracking()
            .Include(d => d.Employee)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (deduction == null) return NotFound();

        return View(deduction);
    }

    // GET: Deductions/Create
    public async Task<IActionResult> Create()
    {
        ViewData["Module"] = "Deductions";
        ViewData["Title"] = "Add deduction";
        await LoadEmployeeSelectAsync();
        await LoadDeductionTypeSelectAsync();
        LoadCalculationMethodSelect();
        LoadFrequencySelect("Monthly");
        return View(new Deduction
        {
            EffectiveDate = DateTime.Today,
            IsActive = true,
            Frequency = "Monthly",
            CalculationMethod = "Fixed"
        });
    }

    // POST: Deductions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("EmployeeId,DeductionType,DeductionName,Frequency,CalculationMethod,PercentageValue,IsMandatory,EffectiveDate,EndDate,IsActive")]
        Deduction deduction)
    {
        ViewData["Module"] = "Deductions";
        ViewData["Title"] = "Add deduction";

        if (deduction.EmployeeId <= 0)
            ModelState.AddModelError(nameof(deduction.EmployeeId), "Select an employee.");

        ValidateDeduction(deduction, ModelState);

        if (ModelState.IsValid)
        {
            deduction.CreatedDate = DateTime.Now;
            deduction.CreatedBy = User.Identity?.Name ?? "System";
            _context.Add(deduction);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Deduction created.";
            return RedirectToAction(nameof(Index));
        }

        await LoadEmployeeSelectAsync(deduction.EmployeeId);
        await LoadDeductionTypeSelectAsync(deduction.DeductionType);
        LoadCalculationMethodSelect(deduction.CalculationMethod);
        LoadFrequencySelect(string.IsNullOrWhiteSpace(deduction.Frequency) ? "Monthly" : deduction.Frequency);
        return View(deduction);
    }

    // GET: Deductions/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        ViewData["Module"] = "Deductions";
        ViewData["Title"] = "Edit deduction";
        if (id == null) return NotFound();

        var deduction = await _context.Deductions.FindAsync(id);
        if (deduction == null) return NotFound();

        await LoadEmployeeSelectAsync(deduction.EmployeeId);
        await LoadDeductionTypeSelectAsync(deduction.DeductionType);
        LoadCalculationMethodSelect(deduction.CalculationMethod);
        LoadFrequencySelect(deduction.Frequency);
        return View(deduction);
    }

    // POST: Deductions/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,
        [Bind("Id,EmployeeId,DeductionType,DeductionName,Frequency,CalculationMethod,PercentageValue,IsMandatory,EffectiveDate,EndDate,IsActive,CreatedDate,CreatedBy")]
        Deduction deduction)
    {
        ViewData["Module"] = "Deductions";
        ViewData["Title"] = "Edit deduction";
        if (id != deduction.Id) return NotFound();

        if (deduction.EmployeeId <= 0)
            ModelState.AddModelError(nameof(deduction.EmployeeId), "Select an employee.");

        ValidateDeduction(deduction, ModelState);

        if (ModelState.IsValid)
        {
            try
            {
                deduction.ModifiedDate = DateTime.Now;
                deduction.ModifiedBy = User.Identity?.Name ?? "System";
                _context.Update(deduction);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Deduction updated.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await DeductionExistsAsync(deduction.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        await LoadEmployeeSelectAsync(deduction.EmployeeId);
        await LoadDeductionTypeSelectAsync(deduction.DeductionType);
        LoadCalculationMethodSelect(deduction.CalculationMethod);
        LoadFrequencySelect(deduction.Frequency);
        return View(deduction);
    }

    // GET: Deductions/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        ViewData["Module"] = "Deductions";
        ViewData["Title"] = "Delete deduction";
        if (id == null) return NotFound();

        var deduction = await _context.Deductions
            .AsNoTracking()
            .Include(d => d.Employee)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (deduction == null) return NotFound();

        return View(deduction);
    }

    // POST: Deductions/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var deduction = await _context.Deductions.FindAsync(id);
        if (deduction != null)
        {
            _context.Deductions.Remove(deduction);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Deduction deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    private Task<bool> DeductionExistsAsync(int id) =>
        _context.Deductions.AnyAsync(e => e.Id == id);
}
