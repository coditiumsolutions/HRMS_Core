using HRMBT.Web.Data;
using HRMBT.Web.Infrastructure;
using HRMBT.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReportsController(ApplicationDbContext context)
    {
        _context = context;
    }

    private void SetModule() => ViewData["Module"] = "Reports";

    private static IEnumerable<string> SplitConfigCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) yield break;
        foreach (var item in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(item))
                yield return item;
        }
    }

    private async Task<List<string>> GetConfigValuesAsync(params string[] configKeys)
    {
        var rows = await _context.HrConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey != null && c.ConfigValue != null)
            .ToListAsync();

        return rows
            .Where(c => configKeys.Any(k => string.Equals(c.ConfigKey!.Trim(), k, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(c => SplitConfigCsv(c.ConfigValue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();
    }

    private Task<List<string>> GetAllowanceTypesFromConfigurationAsync() =>
        GetConfigValuesAsync("AllowanceTypes");

    private Task<List<string>> GetAllowanceNamesFromConfigurationAsync() =>
        GetConfigValuesAsync("AllowanceName", "AllowanceNames");

    private Task<List<string>> GetDepartmentsFromConfigurationAsync() =>
        GetConfigValuesAsync("Department");

    private Task<List<string>> GetDeductionTypesFromConfigurationAsync() =>
        GetConfigValuesAsync("DeductionTypes");

    private async Task<List<string>> GetDeductionNamesForFilterAsync()
    {
        var fromConfig = await GetConfigValuesAsync("DeductionNames");
        if (fromConfig.Count > 0)
            return fromConfig;

        // Fall back to distinct values in use when DeductionNames is not configured yet.
        return await _context.Deductions
            .AsNoTracking()
            .Where(d => d.DeductionName != null && d.DeductionName != "")
            .Select(d => d.DeductionName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
    }

    public IActionResult Index()
    {
        SetModule();
        return RedirectToAction(nameof(SalariesSummary));
    }

    [HttpGet]
    public async Task<IActionResult> SalariesSummary(string? month, int? year, string? department)
    {
        SetModule();
        ViewData["Title"] = "Payroll Summary";

        var monthFilter = string.IsNullOrWhiteSpace(month) ? null : PayrollMonthHelper.Normalize(month);
        var departmentFilter = string.IsNullOrWhiteSpace(department) ? null : department.Trim();

        var departments = await _context.Employees
            .AsNoTracking()
            .Where(e => e.Department != null && e.Department != "")
            .Select(e => e.Department!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        ViewBag.Departments = departments;
        ViewBag.SelectedDepartment = departmentFilter;
        ViewBag.Month = monthFilter;
        ViewBag.Year = year;

        var vm = new SalariesSummaryVm
        {
            Month = monthFilter,
            Year = year,
            Department = departmentFilter,
            HasFilters = !string.IsNullOrWhiteSpace(monthFilter) && year.HasValue
        };

        if (!vm.HasFilters)
            return View(vm);

        var query = _context.Payslips
            .AsNoTracking()
            .Include(p => p.Employee)
            .Where(p => p.Month == monthFilter && p.Year == year!.Value);

        if (!string.IsNullOrWhiteSpace(departmentFilter))
            query = query.Where(p => p.Employee != null && p.Employee.Department == departmentFilter);

        var rows = await query
            .Select(p => new
            {
                p.BasicSalary,
                p.GrossSalary,
                Allowances = p.TotalAllowances ?? 0m,
                Deductions = p.TotalDeductions
            })
            .ToListAsync();

        vm.PayslipCount = rows.Count;
        vm.TotalBasicSalary = rows.Sum(r => r.BasicSalary);
        vm.TotalAllowances = rows.Sum(r => r.Allowances);
        vm.TotalDeductions = rows.Sum(r => r.Deductions);
        vm.TotalGrossSalary = rows.Sum(r => r.GrossSalary);

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> EmployeeSummary(string? department)
    {
        SetModule();
        ViewData["Title"] = "Employee Summary";

        var departmentFilter = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
        var departments = await GetDepartmentsFromConfigurationAsync();

        ViewBag.Departments = departments;
        ViewBag.SelectedDepartment = departmentFilter;

        var employeeQuery = _context.Employees
            .AsNoTracking()
            .Where(e => e.Department != null && e.Department != "");

        if (!string.IsNullOrWhiteSpace(departmentFilter))
            employeeQuery = employeeQuery.Where(e => e.Department == departmentFilter);

        var employeeCounts = await employeeQuery
            .GroupBy(e => e.Department!)
            .Select(g => new { Department = g.Key, EmployeeCount = g.Count() })
            .ToListAsync();

        var vm = new EmployeeSummaryVm
        {
            Department = departmentFilter,
            Rows = employeeCounts
                .OrderBy(x => x.Department, StringComparer.OrdinalIgnoreCase)
                .Select(x => new EmployeeSummaryRowVm
                {
                    Department = x.Department,
                    EmployeeCount = x.EmployeeCount
                })
                .ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> AllowancesSummary(
        string? allowanceType,
        string? allowanceName,
        string? department)
    {
        SetModule();
        ViewData["Title"] = "Allowances Summary";

        var allowanceTypeFilter = string.IsNullOrWhiteSpace(allowanceType) ? null : allowanceType.Trim();
        var allowanceNameFilter = string.IsNullOrWhiteSpace(allowanceName) ? null : allowanceName.Trim();
        var departmentFilter = string.IsNullOrWhiteSpace(department) ? null : department.Trim();

        ViewBag.Departments = await GetDepartmentsFromConfigurationAsync();
        ViewBag.AllowanceTypes = await GetAllowanceTypesFromConfigurationAsync();
        ViewBag.AllowanceNames = await GetAllowanceNamesFromConfigurationAsync();
        ViewBag.SelectedDepartment = departmentFilter;
        ViewBag.SelectedAllowanceType = allowanceTypeFilter;
        ViewBag.SelectedAllowanceName = allowanceNameFilter;

        var query = _context.Allowances
            .AsNoTracking()
            .Include(a => a.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(departmentFilter))
            query = query.Where(a => a.Employee != null && a.Employee.Department == departmentFilter);

        if (!string.IsNullOrWhiteSpace(allowanceTypeFilter))
            query = query.Where(a => a.AllowanceType == allowanceTypeFilter);

        if (!string.IsNullOrWhiteSpace(allowanceNameFilter))
            query = query.Where(a => a.Name == allowanceNameFilter);

        var allowances = await query.ToListAsync();
        var vm = new AllowancesSummaryVm
        {
            AllowanceType = allowanceTypeFilter,
            AllowanceName = allowanceNameFilter,
            Department = departmentFilter,
            Rows = allowances
                .OrderBy(a => a.Employee?.Department ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Employee?.EmployeeName ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.AllowanceType, StringComparer.OrdinalIgnoreCase)
                .Select(a => new AllowancesSummaryRowVm
                {
                    EmployeeCode = a.Employee?.EmployeeID ?? "",
                    EmployeeName = a.Employee?.EmployeeName ?? "",
                    Department = a.Employee?.Department ?? "",
                    ItemType = a.AllowanceType,
                    Name = a.Name,
                    Frequency = a.Frequency,
                    Amount = a.Amount,
                    Quantity = a.Quantity,
                    IsPercentage = a.IsPercentage,
                    PercentageValue = a.PercentageValue,
                    EffectiveDate = a.EffectiveDate,
                    EndDate = a.EndDate,
                    IsActive = a.IsActive,
                    Remarks = a.Remarks
                })
                .ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> DeductionsSummary(
        string? deductionType,
        string? deductionName,
        string? department)
    {
        SetModule();
        ViewData["Title"] = "Deductions Summary";

        var deductionTypeFilter = string.IsNullOrWhiteSpace(deductionType) ? null : deductionType.Trim();
        var deductionNameFilter = string.IsNullOrWhiteSpace(deductionName) ? null : deductionName.Trim();
        var departmentFilter = string.IsNullOrWhiteSpace(department) ? null : department.Trim();

        ViewBag.Departments = await GetDepartmentsFromConfigurationAsync();
        ViewBag.DeductionTypes = await GetDeductionTypesFromConfigurationAsync();
        ViewBag.DeductionNames = await GetDeductionNamesForFilterAsync();
        ViewBag.SelectedDepartment = departmentFilter;
        ViewBag.SelectedDeductionType = deductionTypeFilter;
        ViewBag.SelectedDeductionName = deductionNameFilter;

        var query = _context.Deductions
            .AsNoTracking()
            .Include(d => d.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(departmentFilter))
            query = query.Where(d => d.Employee != null && d.Employee.Department == departmentFilter);

        if (!string.IsNullOrWhiteSpace(deductionTypeFilter))
            query = query.Where(d => d.DeductionType == deductionTypeFilter);

        if (!string.IsNullOrWhiteSpace(deductionNameFilter))
            query = query.Where(d => d.DeductionName == deductionNameFilter);

        var deductions = await query.ToListAsync();
        var vm = new DeductionsSummaryVm
        {
            DeductionType = deductionTypeFilter,
            DeductionName = deductionNameFilter,
            Department = departmentFilter,
            Rows = deductions
                .OrderBy(d => d.Employee?.Department ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.Employee?.EmployeeName ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.DeductionName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.DeductionType, StringComparer.OrdinalIgnoreCase)
                .Select(d => new DeductionsSummaryRowVm
                {
                    EmployeeCode = d.Employee?.EmployeeID ?? "",
                    EmployeeName = d.Employee?.EmployeeName ?? "",
                    Department = d.Employee?.Department ?? "",
                    DeductionType = d.DeductionType,
                    DeductionName = d.DeductionName,
                    Frequency = d.Frequency,
                    CalculationMethod = d.CalculationMethod,
                    Amount = d.PercentageValue ?? 0m,
                    IsPercentage = string.Equals(d.CalculationMethod, "Percentage", StringComparison.OrdinalIgnoreCase),
                    PercentageValue = d.PercentageValue,
                    EffectiveDate = d.EffectiveDate,
                    EndDate = d.EndDate,
                    IsActive = d.IsActive
                })
                .ToList()
        };

        return View(vm);
    }

    public IActionResult TaxesSummary()
    {
        SetModule();
        ViewData["Title"] = "Taxes Summary";
        return View();
    }
}
