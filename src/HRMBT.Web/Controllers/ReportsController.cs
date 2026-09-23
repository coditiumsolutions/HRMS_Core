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
    public async Task<IActionResult> EmployeeSummary(string? month, int? year, string? department)
    {
        SetModule();
        ViewData["Title"] = "Employee Summary";

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

        var vm = new EmployeeSummaryVm
        {
            Month = monthFilter,
            Year = year,
            Department = departmentFilter,
            HasFilters = !string.IsNullOrWhiteSpace(monthFilter) && year.HasValue
        };

        if (!vm.HasFilters)
            return View(vm);

        var employeeQuery = _context.Employees
            .AsNoTracking()
            .Where(e => e.Department != null && e.Department != "");

        if (!string.IsNullOrWhiteSpace(departmentFilter))
            employeeQuery = employeeQuery.Where(e => e.Department == departmentFilter);

        var employeeCounts = await employeeQuery
            .GroupBy(e => e.Department!)
            .Select(g => new { Department = g.Key, EmployeeCount = g.Count() })
            .ToListAsync();

        var salaryAmountsQuery =
            from p in _context.Payslips.AsNoTracking()
            join e in _context.Employees.AsNoTracking() on p.EmployeeId equals e.uid
            where p.Month == monthFilter
                  && p.Year == year!.Value
                  && e.Department != null
                  && e.Department != ""
            select new { p, e };

        if (!string.IsNullOrWhiteSpace(departmentFilter))
            salaryAmountsQuery = salaryAmountsQuery.Where(x => x.e.Department == departmentFilter);

        var salaryAmounts = await salaryAmountsQuery
            .GroupBy(x => x.e.Department!)
            .Select(g => new { Department = g.Key, SalariesGeneratedAmount = g.Sum(x => x.p.GrossSalary) })
            .ToListAsync();

        var salaryLookup = salaryAmounts.ToDictionary(
            x => x.Department,
            x => x.SalariesGeneratedAmount,
            StringComparer.OrdinalIgnoreCase);

        vm.Rows = employeeCounts
            .OrderBy(x => x.Department, StringComparer.OrdinalIgnoreCase)
            .Select(x => new EmployeeSummaryRowVm
            {
                Department = x.Department,
                EmployeeCount = x.EmployeeCount,
                SalariesGeneratedAmount = salaryLookup.TryGetValue(x.Department, out var amount) ? amount : 0m
            })
            .ToList();

        // Include departments that have payslips but no current employees matched (edge case)
        foreach (var salary in salaryAmounts)
        {
            if (vm.Rows.Any(r => string.Equals(r.Department, salary.Department, StringComparison.OrdinalIgnoreCase)))
                continue;
            vm.Rows.Add(new EmployeeSummaryRowVm
            {
                Department = salary.Department,
                EmployeeCount = 0,
                SalariesGeneratedAmount = salary.SalariesGeneratedAmount
            });
        }

        vm.Rows = vm.Rows
            .OrderBy(r => r.Department, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View(vm);
    }

    public IActionResult AllowancesSummary()
    {
        SetModule();
        ViewData["Title"] = "Allowances Summary";
        return View();
    }

    public IActionResult DeductionsSummary()
    {
        SetModule();
        ViewData["Title"] = "Deductions Summary";
        return View();
    }

    public IActionResult TaxesSummary()
    {
        SetModule();
        ViewData["Title"] = "Taxes Summary";
        return View();
    }
}
