using HRMBT.Web.Data;
using HRMBT.Web.Infrastructure;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services.Payroll;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMBT.Web.Controllers
{
[Route("payroll")]
    public class PayrollController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PayrollCalculationService _payrollService;
        private readonly ILogger<PayrollController> _logger;

        public PayrollController(ApplicationDbContext context, PayrollCalculationService payrollService, ILogger<PayrollController> logger)
        {
            _context = context;
            _payrollService = payrollService;
            _logger = logger;
        }

        private static string WarningSuffix(List<PayrollCalculationService.PayrollIssue> issues)
        {
            var warnings = issues.Where(i => !i.Blocks).Select(i => i.Text).ToList();
            return warnings.Count == 0 ? "" : " Check: " + string.Join(" ", warnings);
        }

        private async Task<List<string>> DepartmentNamesAsync() =>
            await _context.Employees
                .Where(e => e.DepartmentInfo!.DepartmentName != null && e.DepartmentInfo!.DepartmentName != "")
                .Select(e => e.DepartmentInfo!.DepartmentName!)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

        // ---------- one list and one detail page: both live under /payslips (T10) ----------

        [HttpGet("")]
        public IActionResult Index(string? month, int? year, string? search, string? department) =>
            RedirectToAction("Index", "Payslips", new { month, year, employeeQuery = search, department });

        [HttpGet("{id:int}")]
        public IActionResult Details(int id) => RedirectToActionPermanent("Details", "Payslips", new { id });

        [HttpGet("search")]
        public IActionResult Search(string? month, int? year, string? employeeId, string? employeeName) =>
            RedirectToActionPermanent("Index", "Payslips",
                new { month, year, employeeQuery = string.IsNullOrWhiteSpace(employeeId) ? employeeName : employeeId });

        // ---------- one run page (T10): everyone, one department or one employee, with a preview ----------

        /// <summary>The separate single, new and pick-employees screens are all part of the run page now.</summary>
        [HttpGet("run/single")]
        [HttpGet("new")]
        [HttpGet("generate")]
        [HttpGet("employees")]
        public IActionResult OldRunScreens(string? department) =>
            RedirectToActionPermanent(nameof(GeneratePayslips), new { department });

        [HttpGet("run")]
        public async Task<IActionResult> GeneratePayslips(string? employeeId, string? department)
        {
            ViewData["Module"] = "Payroll";
            ViewBag.Departments = await DepartmentNamesAsync();

            return View(new GeneratePayslipsVM
            {
                Month = PayrollMonthHelper.CurrentMonthName(),
                Year = DateTime.Now.Year,
                EmployeeID = employeeId,
                Department = department
            });
        }

        [HttpPost("run")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePayslips(GeneratePayslipsVM model)
        {
            ViewData["Module"] = "Payroll";
            var employees = await RunScopeAsync(model);
            if (employees == null)
                return await RunFormAsync(model);

            // Same rules as the preview: employees who are not Active or already have a slip are skipped.
            var rows = _payrollService.Preview(employees, model.Month, model.Year);
            var period = PayrollMonthHelper.FormatPeriod(model.Month, model.Year);
            var user = User.Identity?.Name ?? "System";

            if (model.IsSingleEmployee)
            {
                var row = rows[0];
                var who = $"{row.Employee.EmployeeID} ({row.Employee.EmployeeName})";
                var block = row.Issues.FirstOrDefault(i => i.Blocks);
                if (block != null)
                {
                    ModelState.AddModelError("", $"Employee {who}, {period}: {block.Text}");
                    return await RunFormAsync(model);
                }
            }

            var leftOut = 0;
            if (model.FromPreview)
            {
                var ticked = (model.SelectedIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : 0).ToHashSet();
                leftOut = rows.Count(r => r.WillCreate && !ticked.Contains(r.Employee.uid));
                rows = rows.Where(r => !r.WillCreate || ticked.Contains(r.Employee.uid)).ToList();
                if (!rows.Any(r => r.WillCreate))
                {
                    ModelState.AddModelError("", "No employee was ticked, so no payslip was generated.");
                    return await RunFormAsync(model);
                }
            }

            int generated = 0, skipped = 0, withWarnings = 0, negative = 0;
            var failed = new List<string>();
            foreach (var row in rows.Where(r => r.Employee.EmployeeStatus == "Active" || model.IsSingleEmployee))
            {
                if (!row.WillCreate) { skipped++; continue; }
                try
                {
                    var slip = _payrollService.GeneratePayslip(row.Employee.uid, model.Month, model.Year, user);
                    generated++;
                    if (row.Issues.Count > 0) withWarnings++;
                    if (slip.NetSalary < 0m) negative++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Payslip generation failed for employee uid {Uid}", row.Employee.uid);
                    failed.Add(row.Employee.EmployeeID ?? row.Employee.uid.ToString());
                }
            }

            string message;
            if (model.IsSingleEmployee && generated == 1)
            {
                var row = rows[0];
                message = $"Payslip generated for {row.Employee.EmployeeID} ({row.Employee.EmployeeName}) for {period}."
                    + WarningSuffix(row.Issues) + (negative > 0 ? " Net pay is negative." : "");
            }
            else
            {
                message = $"Payroll for {period} completed. Generated: {generated}, Skipped: {skipped} (already have a payslip)";
                if (leftOut > 0) message += $", Left out: {leftOut}";
                if (withWarnings > 0) message += $". {withWarnings} generated with warnings (use Preview to see them)";
                if (negative > 0) message += $"; {negative} have a negative net pay";
                message += ".";
            }

            if (failed.Count > 0)
            {
                TempData["ErrorMessage"] = $"{failed.Count} payslip(s) could not be generated. Employee ID: "
                    + string.Join(", ", failed.Take(10)) + (failed.Count > 10 ? " and more (see the application log)." : ".");
            }
            if (generated > 0 || failed.Count == 0)
                TempData["SuccessMessage"] = message;

            return RedirectToAction("Index", "Payslips", new
            {
                month = model.Month,
                year = model.Year,
                employeeQuery = model.IsSingleEmployee ? rows[0].Employee.EmployeeID : null
            });
        }

        /// <summary>Shows what a run would do and the warnings; saves nothing.</summary>
        [HttpPost("run/preview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewRun(GeneratePayslipsVM model)
        {
            ViewData["Module"] = "Payroll";
            var employees = await RunScopeAsync(model);
            if (employees == null)
                return await RunFormAsync(model);

            ViewBag.Rows = _payrollService.Preview(employees, model.Month, model.Year)
                .OrderByDescending(r => r.WillCreate && r.Issues.Count > 0)
                .ThenBy(r => r.Employee.EmployeeID)
                .ToList();
            return View("RunPreview", model);
        }

        private async Task<IActionResult> RunFormAsync(GeneratePayslipsVM model)
        {
            ViewBag.Departments = await DepartmentNamesAsync();
            return View("GeneratePayslips", model);
        }

        /// <summary>
        /// Checks the period and returns the employees the run covers: one employee (whatever their status, so the reason for
        /// refusing can be shown), or the active employees of a department or of everyone. Null with ModelState errors when invalid.
        /// </summary>
        private async Task<List<Employee>?> RunScopeAsync(GeneratePayslipsVM model)
        {
            model.Month = PayrollMonthHelper.Normalize(model.Month);
            if (!PayrollMonthHelper.IsValid(model.Month))
                ModelState.AddModelError("Month", "Invalid month.");
            if (model.Year < 2000 || model.Year > 2100)
                ModelState.AddModelError("Year", "Invalid year.");
            if (!ModelState.IsValid)
                return null;

            if (model.IsSingleEmployee)
            {
                var code = model.EmployeeID!.Trim().ToLower();
                var one = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID != null && e.EmployeeID.Trim().ToLower() == code);
                if (one != null)
                    return new List<Employee> { one };
                ModelState.AddModelError("EmployeeID", $"Employee with ID '{model.EmployeeID.Trim()}' not found.");
                return null;
            }

            var query = _context.Employees.Where(e => e.EmployeeStatus == "Active");
            if (!string.IsNullOrWhiteSpace(model.Department))
                query = query.Where(e => e.DepartmentInfo!.DepartmentName == model.Department);
            return await query.ToListAsync();
        }

        // GET: Payroll/PayrollStatus
        [HttpGet("status")]
        public async Task<IActionResult> PayrollStatus(string year, string? month, string department, string payrollStatus, int page = 1, int pageSize = 20)
        {
            ViewData["Module"] = "Payroll";
            
            // Ensure valid pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            string? monthFilter = null;
            if (!string.IsNullOrWhiteSpace(month))
            {
                monthFilter = PayrollMonthHelper.Normalize(month);
            }

            // Only process if year and month are provided (user has searched)
            bool hasSearched = !string.IsNullOrWhiteSpace(year) && monthFilter != null;
            
            int? actualYear = null;
            if (hasSearched)
            {
                // Parse year as integer
                if (int.TryParse(year, out int yearValue))
                {
                    actualYear = yearValue;
                }
                else
                {
                    // Fallback to current year if parsing fails
                    actualYear = DateTime.Now.Year;
                }
            }

            // Set default payroll status if not provided
            if (string.IsNullOrWhiteSpace(payrollStatus))
                payrollStatus = "Payroll Not Generated";

            // Get all departments for dropdown
            var departments = await _context.Employees
                .Where(e => e.DepartmentInfo!.DepartmentName != null && e.DepartmentInfo!.DepartmentName != "")
                .Select(e => e.DepartmentInfo!.DepartmentName)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            PaginatedList<Employee> paginatedList;
            Dictionary<int, Payslip> payslipLookupFull = new Dictionary<int, Payslip>();

            if (hasSearched && actualYear.HasValue)
            {
                // Start with all active employees
                var employeeQuery = _context.Employees
                    .Where(e => e.EmployeeStatus == "Active")
                    .AsQueryable();

                // Filter by department if provided
                if (!string.IsNullOrWhiteSpace(department))
                {
                    employeeQuery = employeeQuery.Where(e => e.DepartmentInfo!.DepartmentName != null && e.DepartmentInfo!.DepartmentName == department);
                }

                // Get payslip IDs for the selected month/year grouped by employee
                var payslipData = await _context.Payslips
                    .Where(p => p.Month == monthFilter && p.Year == actualYear.Value)
                    .Select(p => new { p.EmployeeId, p.Id, p.IsLocked })
                    .ToListAsync();

                // Create dictionaries for quick lookup
                var payslipLookup = payslipData.ToDictionary(p => p.EmployeeId, p => new { p.Id, p.IsLocked });
                var employeeIdsWithPayslips = payslipData.Select(p => p.EmployeeId).ToHashSet();

                // Filter employees based on payroll status using database query
                IQueryable<Employee> filteredQuery = employeeQuery;

                if (payrollStatus == "Payroll Not Generated")
                {
                    // Employees without payslips
                    filteredQuery = filteredQuery.Where(e => !employeeIdsWithPayslips.Contains(e.uid));
                }
                else if (payrollStatus == "Payroll Generated")
                {
                    // Employees with locked payslips
                    var lockedEmployeeIds = payslipData
                        .Where(p => p.IsLocked)
                        .Select(p => p.EmployeeId)
                        .ToHashSet();
                    filteredQuery = filteredQuery.Where(e => lockedEmployeeIds.Contains(e.uid));
                }
                else if (payrollStatus == "Payroll Pending")
                {
                    // Employees with unlocked payslips
                    var pendingEmployeeIds = payslipData
                        .Where(p => !p.IsLocked)
                        .Select(p => p.EmployeeId)
                        .ToHashSet();
                    filteredQuery = filteredQuery.Where(e => pendingEmployeeIds.Contains(e.uid));
                }

                // Get total count before pagination
                int totalCount = await filteredQuery.CountAsync();

                // Apply ordering and pagination
                var paginatedEmployees = await filteredQuery
                    .OrderBy(e => e.EmployeeName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Create paginated list
                paginatedList = new PaginatedList<Employee>(paginatedEmployees, totalCount, page, pageSize);
                
                // Rebuild payslip lookup with full payslip objects for the paginated results
                var paginatedEmployeeIds = paginatedEmployees.Select(e => e.uid).ToList();
                var payslipsForPaginated = await _context.Payslips
                    .Where(p => p.Month == monthFilter && p.Year == actualYear.Value && paginatedEmployeeIds.Contains(p.EmployeeId))
                    .ToListAsync();
                payslipLookupFull = payslipsForPaginated.ToDictionary(p => p.EmployeeId);
            }
            else
            {
                // Return empty list if no search has been performed
                paginatedList = new PaginatedList<Employee>(new List<Employee>(), 0, 1, pageSize);
            }

            // Pass data to view
            ViewBag.Year = year;
            ViewBag.Month = monthFilter ?? month;
            ViewBag.Department = department;
            ViewBag.PayrollStatus = payrollStatus;
            ViewBag.ActualYear = actualYear;
            ViewBag.Departments = departments;
            ViewBag.CurrentPageSize = pageSize;
            ViewBag.PayslipLookup = payslipLookupFull; // For displaying status in view

            return View(paginatedList);
        }

        // GET: Payroll/Dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard(string? month, int? year)
        {
            ViewData["Module"] = "Payroll";
            
            var selectedMonth = !string.IsNullOrWhiteSpace(month)
                ? PayrollMonthHelper.Normalize(month)
                : PayrollMonthHelper.CurrentMonthName();
            int selectedYear = year ?? DateTime.Now.Year;

            // Get total active employees
            int totalEmployees = await _context.Employees
                .Where(e => e.EmployeeStatus == "Active")
                .CountAsync();

            // Get employees with payroll generated for the selected month/year
            var employeesWithPayroll = await _context.Payslips
                .Where(p => p.Month == selectedMonth && p.Year == selectedYear)
                .Select(p => p.EmployeeId)
                .Distinct()
                .CountAsync();

            // Calculate employees without payroll
            int employeesWithoutPayroll = totalEmployees - employeesWithPayroll;

            // Calculate total payroll amount for the selected month/year
            decimal totalPayrollAmount = await _context.Payslips
                .Where(p => p.Month == selectedMonth && p.Year == selectedYear)
                .SumAsync(p => (decimal?)p.NetSalary) ?? 0;

            var dashboardData = new PayrollDashboardVM
            {
                TotalEmployees = totalEmployees,
                EmployeesWithPayrollGenerated = employeesWithPayroll,
                EmployeesWithoutPayroll = employeesWithoutPayroll,
                TotalPayrollAmount = totalPayrollAmount,
                Month = selectedMonth,
                Year = selectedYear
            };

            ViewBag.Month = selectedMonth;
            ViewBag.Year = selectedYear;

            return View(dashboardData);
        }
    }
}

