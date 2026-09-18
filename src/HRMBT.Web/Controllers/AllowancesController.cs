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

    private async Task<decimal> GetFuelUnitPriceFromConfigurationAsync()
    {
        var rows = await _context.HrConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey != null && c.ConfigValue != null)
            .ToListAsync();

        var raw = rows
            .Where(c => ConfigKeyMatches(c.ConfigKey, "FuelPrice"))
            .Select(c => c.ConfigValue)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(raw))
            return 0m;

        // Prefer first CSV token if multiple values are stored.
        var token = SplitConfigCsv(raw).FirstOrDefault() ?? raw.Trim();
        return decimal.TryParse(token, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var price)
            ? price
            : 0m;
    }

    private static bool IsFuelAllowance(string? allowanceType, string? allowanceName = null) =>
        string.Equals((allowanceType ?? "").Trim(), "Fuel Allowance", StringComparison.OrdinalIgnoreCase)
        || string.Equals((allowanceType ?? "").Trim(), "Fuel", StringComparison.OrdinalIgnoreCase)
        || string.Equals((allowanceName ?? "").Trim(), "Fuel Allowance", StringComparison.OrdinalIgnoreCase)
        || string.Equals((allowanceName ?? "").Trim(), "Fuel", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Fuel Allowance with Amount &lt;= 0 is stored as 0; payroll uses ConfigKey FuelPrice × Quantity.
    /// Otherwise keep the posted amount.
    /// </summary>
    private static decimal ResolveBulkAllowanceAmount(
        decimal amount,
        bool isPercentage) =>
        isPercentage ? 0m : amount;

    private static void ValidateAllowance(Allowance model, ModelStateDictionary modelState)
    {
        if (model.Quantity < 0)
            modelState.AddModelError(nameof(model.Quantity), "Quantity must be a whole number 0 or greater.");

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
    public async Task<IActionResult> Index(int? deptId = null, string? empSearch = null, bool? isActive = null, int page = 1)
    {
        ViewData["Module"] = "Allowances";
        ViewData["Title"] = "Allowances";

        if (page < 1) page = 1;
        const int pageSize = 20;

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

        return View(list);
    }

    // GET: Allowances/Add — bulk add: filter employees, select checkboxes, apply configured allowance.
    public async Task<IActionResult> Add(int? deptId = null, int ePage = 1)
    {
        ViewData["Module"] = "Allowances";
        ViewData["Title"] = "Add Allowances";

        if (ePage < 1) ePage = 1;
        const int ePageSize = 20;

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

        empQuery = empQuery.OrderBy(e => e.EmployeeName);
        var employeesPage = await PaginatedList<Employee>.CreateAsync(empQuery, ePage, ePageSize);

        await LoadDepartmentFilterOptionsAsync(deptId);
        ViewBag.CurrentDeptId = deptId;
        ViewBag.CurrentEPage = ePage;

        var allowanceNames = await GetAllowanceNamesFromConfigurationAsync();
        var nameItems = new List<SelectListItem>
        {
            new() { Value = "", Text = "— Select allowance type —", Selected = true }
        };
        nameItems.AddRange(allowanceNames.Select(n => new SelectListItem { Value = n, Text = n }));
        ViewBag.AllowanceNameOptions = nameItems;

        return View(employeesPage);
    }

    /// <summary>Add the same allowance (type, name, amount/percentage, dates) for each selected employee.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCreateAllowances(
        List<int>? selectedEmployeeIds,
        string? allowanceName,
        string? name,
        decimal amount,
        int quantity = 1,
        bool isPercentage = false,
        decimal? percentageValue = null,
        DateTime? effectiveDate = null,
        DateTime? endDate = null,
        string? remarks = null,
        int? deptId = null,
        int ePage = 1)
    {
        ViewData["Module"] = "Allowances";

        var routeValues = new RouteValueDictionary
        {
            ["deptId"] = deptId,
            ["ePage"] = ePage
        };

        if (selectedEmployeeIds == null || selectedEmployeeIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Select at least one employee using the checkboxes.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        if (string.IsNullOrWhiteSpace(allowanceName))
        {
            TempData["ErrorMessage"] = "Select an allowance type from the configuration list.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        var allowedNames = await GetAllowanceNamesFromConfigurationAsync();
        var trimmedType = allowanceName.Trim();
        if (!allowedNames.Any(n => string.Equals(n, trimmedType, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["ErrorMessage"] = "The selected allowance type is not defined for ConfigKey Allowances.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        var trimmedName = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            TempData["ErrorMessage"] = "Enter an allowance name.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        if (isPercentage)
        {
            if (!percentageValue.HasValue)
            {
                TempData["ErrorMessage"] = "Enter a percentage when using percentage-based allowance.";
                return RedirectToAction(nameof(Add), routeValues);
            }
            if (percentageValue.Value < 0 || percentageValue.Value > 100)
            {
                TempData["ErrorMessage"] = "Percentage must be between 0 and 100.";
                return RedirectToAction(nameof(Add), routeValues);
            }
        }
        else if (amount < 0)
        {
            TempData["ErrorMessage"] = "Amount cannot be negative.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        if (quantity < 0)
        {
            TempData["ErrorMessage"] = "Quantity must be a whole number 0 or greater.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        var effective = effectiveDate?.Date ?? DateTime.Today;
        if (endDate.HasValue && endDate.Value.Date < effective)
        {
            TempData["ErrorMessage"] = "End date cannot be before effective date.";
            return RedirectToAction(nameof(Add), routeValues);
        }

        var distinctIds = selectedEmployeeIds.Distinct().ToList();
        var user = User.Identity?.Name ?? "System";
        var now = DateTime.Now;
        var type = trimmedType.Length <= 50 ? trimmedType : trimmedType[..50];
        var displayName = trimmedName.Length <= 200 ? trimmedName : trimmedName[..200];
        var qty = quantity < 0 ? 0 : quantity;
        var resolvedAmount = ResolveBulkAllowanceAmount(amount, isPercentage);
        var trimmedRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        if (trimmedRemarks != null && trimmedRemarks.Length > 500)
            trimmedRemarks = trimmedRemarks[..500];

        int added = 0;
        foreach (var uid in distinctIds)
        {
            if (!await _context.Employees.AnyAsync(e => e.uid == uid))
                continue;

            _context.Allowances.Add(new Allowance
            {
                EmployeeId = uid,
                AllowanceType = type,
                Name = displayName,
                Amount = resolvedAmount,
                Quantity = qty,
                IsPercentage = isPercentage,
                PercentageValue = isPercentage ? percentageValue : null,
                EffectiveDate = effective,
                EndDate = endDate?.Date,
                IsActive = true,
                Remarks = trimmedRemarks,
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
        string valueText;
        if (isPercentage)
            valueText = $"{percentageValue:N2}%";
        else if (IsFuelAllowance(type, displayName) && resolvedAmount <= 0m)
            valueText = $"Amount empty → payroll uses FuelPrice × Qty ({qty})";
        else
            valueText = resolvedAmount.ToString("N2");
        TempData["SuccessMessage"] = $"Allowance “{displayName}” ({valueText}) added for {added} employee(s).";
        return RedirectToAction(nameof(Add), routeValues);
    }

    /// <summary>Unit fuel price from dbo.Configuration where ConfigKey = FuelPrice (0 if missing).</summary>
    [HttpGet]
    public async Task<IActionResult> FuelPrice()
    {
        var unit = await GetFuelUnitPriceFromConfigurationAsync();
        return Json(new { unitPrice = unit, found = unit > 0 });
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
    public IActionResult Create()
    {
        ViewData["Module"] = "Allowances";
        ViewData["Title"] = "Add allowance";
        ViewBag.EmployeeCode = "";
        ViewBag.EmployeeDisplayName = "";
        return View(new Allowance
        {
            EffectiveDate = DateTime.Today,
            IsActive = true,
            CreatedBy = User.Identity?.Name ?? "System",
            Amount = 0,
            Quantity = 1,
            IsPercentage = false
        });
    }

    /// <summary>Lookup active employee by Employee ID (exact, then contains) for Create form search.</summary>
    [HttpGet]
    public async Task<IActionResult> FindEmployee(string? q)
    {
        var term = (q ?? "").Trim();
        if (string.IsNullOrWhiteSpace(term))
            return Json(new { found = false, message = "Enter an Employee ID." });

        var exact = await _context.Employees.AsNoTracking()
            .Where(e => e.EmployeeID != null && e.EmployeeID == term)
            .Select(e => new { e.uid, e.EmployeeID, e.EmployeeName, e.EmployeeStatus, e.Department })
            .FirstOrDefaultAsync();

        var match = exact;
        if (match == null)
        {
            match = await _context.Employees.AsNoTracking()
                .Where(e => e.EmployeeID != null && e.EmployeeID.Contains(term))
                .OrderBy(e => e.EmployeeID)
                .Select(e => new { e.uid, e.EmployeeID, e.EmployeeName, e.EmployeeStatus, e.Department })
                .FirstOrDefaultAsync();
        }

        if (match == null)
            return Json(new { found = false, message = $"No employee found for ID “{term}”." });

        var isActive = string.Equals(match.EmployeeStatus, "Active", StringComparison.OrdinalIgnoreCase);
        if (!isActive)
            return Json(new { found = false, message = $"Employee {match.EmployeeID} ({match.EmployeeName}) is not Active." });

        return Json(new
        {
            found = true,
            uid = match.uid,
            employeeId = match.EmployeeID,
            name = match.EmployeeName,
            department = match.Department,
            display = $"{match.EmployeeName} ({match.EmployeeID})"
        });
    }

    // POST: Allowances/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("EmployeeId,AllowanceType,Name,Amount,Quantity,IsPercentage,PercentageValue,EffectiveDate,EndDate,IsActive,Remarks")] Allowance allowance,
        string? employeeCode)
    {
        ViewData["Module"] = "Allowances";
        ViewData["Title"] = "Add allowance";
        ModelState.Remove(nameof(Allowance.Employee));
        ValidateAllowance(allowance, ModelState);

        Employee? employee = null;
        if (allowance.EmployeeId > 0)
        {
            employee = await _context.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.uid == allowance.EmployeeId);
        }

        if (employee == null && !string.IsNullOrWhiteSpace(employeeCode))
        {
            var code = employeeCode.Trim();
            employee = await _context.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeID != null && e.EmployeeID == code);
            if (employee != null)
                allowance.EmployeeId = employee.uid;
        }

        if (employee == null || allowance.EmployeeId <= 0)
        {
            ModelState.AddModelError(nameof(allowance.EmployeeId), "Find a valid employee by Employee ID before saving.");
        }
        else if (!string.Equals(employee.EmployeeStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(allowance.EmployeeId), "Selected employee is not Active.");
        }

        ViewBag.EmployeeCode = employee?.EmployeeID ?? employeeCode ?? "";
        ViewBag.EmployeeDisplayName = employee == null
            ? ""
            : $"{employee.EmployeeName} ({employee.EmployeeID})";

        if (ModelState.IsValid)
        {
            allowance.Quantity = allowance.Quantity < 0 ? 0 : allowance.Quantity;
            allowance.Remarks = string.IsNullOrWhiteSpace(allowance.Remarks) ? null : allowance.Remarks.Trim();
            allowance.CreatedDate = DateTime.Now;
            allowance.CreatedBy = User.Identity?.Name ?? "System";
            allowance.ModifiedDate = null;
            allowance.ModifiedBy = null;
            _context.Add(allowance);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Allowance created successfully.";
            return RedirectToAction(nameof(Index));
        }

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
    public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeId,AllowanceType,Name,Amount,Quantity,IsPercentage,PercentageValue,EffectiveDate,EndDate,IsActive,Remarks,CreatedDate,CreatedBy")] Allowance posted)
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
                existing.Quantity = posted.Quantity < 0 ? 0 : posted.Quantity;
                existing.IsPercentage = posted.IsPercentage;
                existing.PercentageValue = posted.PercentageValue;
                existing.EffectiveDate = posted.EffectiveDate;
                existing.EndDate = posted.EndDate;
                existing.IsActive = posted.IsActive;
                existing.Remarks = string.IsNullOrWhiteSpace(posted.Remarks) ? null : posted.Remarks.Trim();
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
