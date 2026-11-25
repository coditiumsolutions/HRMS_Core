using HRMBT.Web.Data;
using HRMBT.Web.Infrastructure;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services.Payroll;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public PayrollController(ApplicationDbContext context, PayrollCalculationService payrollService)
        {
            _context = context;
            _payrollService = payrollService;
        }

        /// <summary>The first reason that stops a payslip being created, or null when it may be created.</summary>
        private static string? BlockingReason(List<PayrollCalculationService.PayrollIssue> issues) =>
            issues.FirstOrDefault(i => i.Blocks)?.Text;

        /// <summary>Warnings that do not stop the payslip, as a sentence to append to a message.</summary>
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

        private async Task<bool> SlipExistsAsync(int uid, string month, int year) =>
            await _context.Payslips.AnyAsync(p => p.EmployeeId == uid && p.Month == month && p.Year == year);

        // GET: Payroll
        [HttpGet("")]
        public async Task<IActionResult> Index(string? month, int? year, string? search, string? department, string? employeeId, string? employeeName, int page = 1, int pageSize = 20)
        {
            ViewData["Module"] = "Payroll";
            
            // Ensure valid pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100; // Max page size limit

            var searchTerm = (search ?? employeeId ?? employeeName)?.Trim();
            var departmentFilter = department?.Trim();

            string? monthFilter = null;
            if (!string.IsNullOrWhiteSpace(month))
            {
                monthFilter = PayrollMonthHelper.Normalize(month);
            }

            var hasPeriodFilter = monthFilter != null && year.HasValue && year.Value > 0;
            ViewBag.RequiresPeriodFilter = !hasPeriodFilter;

            var departments = await _context.Employees
                .Where(e => e.DepartmentInfo!.DepartmentName != null && e.DepartmentInfo!.DepartmentName != "")
                .Select(e => e.DepartmentInfo!.DepartmentName)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            // Pass filter values to view
            ViewBag.Month = monthFilter ?? month;
            ViewBag.Year = year;
            ViewBag.CurrentSearch = searchTerm;
            ViewBag.SelectedDepartment = departmentFilter;
            ViewBag.Departments = departments;
            ViewBag.CurrentPageSize = pageSize;

            // Month and Year are required — show empty grid until both are selected
            if (!hasPeriodFilter)
            {
                return View(new PaginatedList<Payslip>(new List<Payslip>(), 0, page, pageSize));
            }

            var query = _context.Payslips.Include(p => p.Employee).AsQueryable();

            query = query.Where(p => p.Month == monthFilter && p.Year == year!.Value);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => p.Employee != null &&
                    ((p.Employee.EmployeeID != null && p.Employee.EmployeeID.Contains(searchTerm)) ||
                     (p.Employee.EmployeeName != null && p.Employee.EmployeeName.Contains(searchTerm))));
            }

            if (!string.IsNullOrWhiteSpace(departmentFilter))
            {
                query = query.Where(p => p.Employee != null && p.Employee.DepartmentInfo!.DepartmentName == departmentFilter);
            }

            query = query.OrderByPeriodDescending();

            var paginatedPayslips = await PaginatedList<Payslip>.CreateAsync(query, page, pageSize);

            return View(paginatedPayslips);
        }

        // GET: Payroll/GeneratePayslips
        [HttpGet("run")]
        public async Task<IActionResult> GeneratePayslips()
        {
            ViewData["Module"] = "Payroll";
            
            // Get distinct departments for dropdown
            var departments = await _context.Employees
                .Where(e => e.DepartmentInfo!.DepartmentName != null && e.DepartmentInfo!.DepartmentName != "")
                .Select(e => e.DepartmentInfo!.DepartmentName)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            ViewBag.Departments = departments;

            return View(new GeneratePayslipsVM
            {
                Month = PayrollMonthHelper.CurrentMonthName(),
                Year = DateTime.Now.Year
            });
        }

        // POST: Payroll/GeneratePayslips
        [HttpPost("run")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePayslips(GeneratePayslipsVM model)
        {
            ViewData["Module"] = "Payroll";
            model.Month = PayrollMonthHelper.Normalize(model.Month);

            if (!PayrollMonthHelper.IsValid(model.Month))
                ModelState.AddModelError("Month", "Invalid month.");

            if (model.Year < 2000 || model.Year > 2100)
                ModelState.AddModelError("Year", "Invalid year.");

            ViewBag.Departments = await DepartmentNamesAsync();

            if (!ModelState.IsValid)
                return View(model);

            // Same rules as every other route: employees who are not Active or already have a slip are skipped.
            // (A locked slip elsewhere in the month no longer blocks the run for everyone.)
            var rows = _payrollService.Preview(await RunEmployeesAsync(model.Department), model.Month, model.Year);

            int generated = 0, skipped = 0, failed = 0, withWarnings = 0, negative = 0;
            foreach (var row in rows.Where(r => r.Employee.EmployeeStatus == "Active"))
            {
                if (!row.WillCreate) { skipped++; continue; }
                try
                {
                    _payrollService.GeneratePayslip(row.Employee.uid, model.Month, model.Year, User.Identity?.Name ?? "System");
                    generated++;
                    if (row.Issues.Count > 0) withWarnings++;
                    if (row.Net < 0m) negative++;
                }
                catch (Exception)
                {
                    failed++;
                }
            }

            var msg = $"Payroll completed. Generated: {generated}, Skipped: {skipped} (already have a payslip)";
            if (failed > 0) msg += $", Failed: {failed}";
            if (withWarnings > 0) msg += $". {withWarnings} generated with warnings (use Preview to see them)";
            if (negative > 0) msg += $"; {negative} have a negative net pay";
            TempData["SuccessMessage"] = msg + ".";

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<Employee>> RunEmployeesAsync(string? department)
        {
            var query = _context.Employees.Where(e => e.EmployeeStatus == "Active");
            if (!string.IsNullOrWhiteSpace(department))
                query = query.Where(e => e.DepartmentInfo!.DepartmentName == department);
            return await query.ToListAsync();
        }

        // POST: Payroll/run/preview — shows what a run would do and the warnings; saves nothing
        [HttpPost("run/preview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewRun(GeneratePayslipsVM model)
        {
            ViewData["Module"] = "Payroll";
            model.Month = PayrollMonthHelper.Normalize(model.Month);

            if (!PayrollMonthHelper.IsValid(model.Month))
                ModelState.AddModelError("Month", "Invalid month.");
            if (model.Year < 2000 || model.Year > 2100)
                ModelState.AddModelError("Year", "Invalid year.");

            if (!ModelState.IsValid)
            {
                ViewBag.Departments = await DepartmentNamesAsync();
                return View("GeneratePayslips", model);
            }

            var rows = _payrollService.Preview(await RunEmployeesAsync(model.Department), model.Month, model.Year)
                .OrderByDescending(r => r.Issues.Count(i => i.Blocks) == 0 && r.Issues.Count > 0)
                .ThenBy(r => r.Employee.EmployeeID)
                .ToList();
            ViewBag.Rows = rows;
            return View("RunPreview", model);
        }

        // GET: Payroll/Details/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            ViewData["Module"] = "Payroll";
            if (id == null)
            {
                return NotFound();
            }

            var payslip = await _context.Payslips
                .Include(p => p.Employee)
                .Include(p => p.PayslipDetails)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (payslip == null)
            {
                return NotFound();
            }

            return View(payslip);
        }

        // GET: Payroll/Search
        [HttpGet("search")]
        public async Task<IActionResult> Search(string? month, int? year, string employeeId, string employeeName)
        {
            ViewData["Module"] = "Payroll";
            
            var query = _context.Payslips.Include(p => p.Employee).AsQueryable();

            string? monthFilter = null;
            if (!string.IsNullOrWhiteSpace(month))
            {
                monthFilter = PayrollMonthHelper.Normalize(month);
            }

            // Default to current month if no filters
            if (monthFilter == null && !year.HasValue && string.IsNullOrEmpty(employeeId) && string.IsNullOrEmpty(employeeName))
            {
                monthFilter = PayrollMonthHelper.CurrentMonthName();
                year = DateTime.Now.Year;
            }

            if (monthFilter != null)
                query = query.Where(p => p.Month == monthFilter);

            if (year.HasValue)
                query = query.Where(p => p.Year == year.Value);

            if (!string.IsNullOrWhiteSpace(employeeId))
                query = query.Where(p => p.Employee != null && p.Employee.EmployeeID.Contains(employeeId));

            if (!string.IsNullOrWhiteSpace(employeeName))
                query = query.Where(p => p.Employee != null && p.Employee.EmployeeName.Contains(employeeName));

            var payslips = await query
                .OrderByPeriodDescending()
                .ToListAsync();

            ViewBag.Month = monthFilter ?? PayrollMonthHelper.CurrentMonthName();
            ViewBag.Year = year ?? DateTime.Now.Year;
            ViewBag.EmployeeId = employeeId ?? "";
            ViewBag.EmployeeName = employeeName ?? "";

            return View(payslips);
        }

        // GET: Payroll/GenerateIndividual
        [HttpGet("run/single")]
        public IActionResult GenerateIndividual()
        {
            ViewData["Module"] = "Payroll";
            return View(new GenerateIndividualVM
            {
                Month = PayrollMonthHelper.CurrentMonthName(),
                Year = DateTime.Now.Year
            });
        }

        // POST: Payroll/GenerateIndividual
        [HttpPost("run/single")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateIndividual(GenerateIndividualVM model)
        {
            ViewData["Module"] = "Payroll";

            if (string.IsNullOrWhiteSpace(model.EmployeeID))
            {
                ModelState.AddModelError("EmployeeID", "Employee ID is required.");
            }

            model.Month = PayrollMonthHelper.Normalize(model.Month);

            if (!PayrollMonthHelper.IsValid(model.Month))
            {
                ModelState.AddModelError("Month", "Invalid month.");
            }

            if (model.Year < 2000 || model.Year > 2100)
            {
                ModelState.AddModelError("Year", "Invalid year.");
            }

            if (!ModelState.IsValid)
                return View(model);

            // Find employee by EmployeeID
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeID != null && 
                    e.EmployeeID.Trim().ToLower() == model.EmployeeID.Trim().ToLower());

            if (employee == null)
            {
                ModelState.AddModelError("EmployeeID", $"Employee with ID '{model.EmployeeID}' not found.");
                return View(model);
            }

            var issues = PayrollCalculationService.CheckEmployee(employee, model.Month, model.Year,
                await SlipExistsAsync(employee.uid, model.Month, model.Year));
            var block = BlockingReason(issues);
            if (block != null)
            {
                ModelState.AddModelError("", $"Employee {model.EmployeeID} ({employee.EmployeeName}), {PayrollMonthHelper.FormatPeriod(model.Month, model.Year)}: {block}");
                return View(model);
            }

            try
            {
                var slip = _payrollService.GeneratePayslip(
                    employee.uid,
                    model.Month,
                    model.Year,
                    User.Identity?.Name ?? "System");

                var negative = slip.NetSalary < 0m ? $" Net pay is negative ({slip.NetSalary:N2})." : "";
                TempData["SuccessMessage"] = $"Payslip generated successfully for Employee {model.EmployeeID} ({employee.EmployeeName}) for {PayrollMonthHelper.FormatPeriod(model.Month, model.Year)}." + WarningSuffix(issues) + negative;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error generating payslip: {ex.Message}");
                return View(model);
            }
        }

        // GET: Payroll/Create
        [HttpGet("new")]
        public IActionResult Create()
        {
            ViewData["Module"] = "Payroll";
            ViewData["EmployeeId"] = string.Empty;
            ViewData["Month"] = PayrollMonthHelper.CurrentMonthName();
            ViewData["Year"] = DateTime.Now.Year;
            return View();
        }

        // POST: Payroll/Create
        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string employeeId, string? month, int? year)
        {
            ViewData["Module"] = "Payroll";
            
            // Clear model state and set values for view
            ModelState.Clear();
            ViewData["EmployeeId"] = employeeId ?? string.Empty;
            var monthName = !string.IsNullOrWhiteSpace(month)
                ? PayrollMonthHelper.Normalize(month)
                : PayrollMonthHelper.CurrentMonthName();
            ViewData["Month"] = monthName;
            ViewData["Year"] = year ?? DateTime.Now.Year;

            // Validate Employee ID
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                ModelState.AddModelError("EmployeeId", "Employee ID is required.");
                return View();
            }

            if (!PayrollMonthHelper.IsValid(monthName))
            {
                ModelState.AddModelError("Month", "Please select a valid month.");
                return View();
            }

            if (!year.HasValue || year < 2000 || year > 2100)
            {
                ModelState.AddModelError("Year", "Please enter a valid year.");
                return View();
            }

            // Find employee by EmployeeID
            var trimmedEmployeeId = employeeId.Trim();
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeID != null &&
                    e.EmployeeID.Trim().ToLower() == trimmedEmployeeId.ToLower());

            if (employee == null)
            {
                ModelState.AddModelError("EmployeeId", $"Employee with ID '{employeeId}' not found.");
                return View();
            }

            var issues = PayrollCalculationService.CheckEmployee(employee, monthName, year.Value,
                await SlipExistsAsync(employee.uid, monthName, year.Value));
            var block = BlockingReason(issues);
            if (block != null)
            {
                ModelState.AddModelError("", $"Employee {employee.EmployeeID} ({employee.EmployeeName}), {PayrollMonthHelper.FormatPeriod(monthName, year.Value)}: {block}");
                return View();
            }

            try
            {
                var slip = _payrollService.GeneratePayslip(
                    employee.uid,
                    monthName,
                    year.Value,
                    User.Identity?.Name ?? "System");

                var negative = slip.NetSalary < 0m ? $" Net pay is negative ({slip.NetSalary:N2})." : "";
                TempData["SuccessMessage"] = $"Payslip generated successfully for Employee ID: {employeeId}!" + WarningSuffix(issues) + negative;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error generating payslip: {ex.Message}");
                if (ex.InnerException != null)
                {
                    ModelState.AddModelError("", $"Details: {ex.InnerException.Message}");
                }
                return View();
            }
        }

        // GET: Payroll/EmployeeDetail
        [HttpGet("employees")]
        public async Task<IActionResult> EmployeeDetail(string department, int page = 1, int pageSize = 20)
        {
            ViewData["Module"] = "Payroll";
            
            // Ensure valid pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100; // Max page size limit

            var query = _context.Employees.AsQueryable();

            // Filter by department if provided
            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(e => e.DepartmentInfo!.DepartmentName != null && e.DepartmentInfo!.DepartmentName == department);
            }

            // Apply ordering before pagination
            query = query.OrderBy(e => e.EmployeeName);

            // Get paginated results
            var paginatedEmployees = await PaginatedList<Employee>.CreateAsync(query, page, pageSize);

            ViewBag.GrossSalaryByUid = _payrollService.ComputeGrossPreviewForEmployees(paginatedEmployees);

            // Get distinct departments for dropdown
            var departments = await _context.Employees
                .Where(e => e.DepartmentInfo!.DepartmentName != null && e.DepartmentInfo!.DepartmentName != "")
                .Select(e => e.DepartmentInfo!.DepartmentName)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            ViewBag.Departments = departments;
            ViewBag.SelectedDepartment = department;
            ViewBag.CurrentPageSize = pageSize;

            return View("EmployeeDetail", paginatedEmployees);
        }

        // GET: Payroll/GeneratePayroll
        // Reuses EmployeeDetail data/view so functionality stays identical.
        [HttpGet("generate")]
        public Task<IActionResult> GeneratePayroll(string department, int page = 1, int pageSize = 20)
        {
            return EmployeeDetail(department, page, pageSize);
        }

        // POST: Payroll/GeneratePayrollForSelected
        [HttpPost("generate/selected")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePayrollForSelected(List<int> selectedEmployeeIds, string month, int year, string? sourceAction, string? department)
        {
            ViewData["Module"] = "Payroll";
            var returnAction = string.Equals(sourceAction, nameof(GeneratePayroll), StringComparison.OrdinalIgnoreCase)
                ? nameof(GeneratePayroll)
                : nameof(EmployeeDetail);

            var monthName = PayrollMonthHelper.Normalize(month);

            if (selectedEmployeeIds == null || !selectedEmployeeIds.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one employee.";
                return RedirectToAction(returnAction, new { department });
            }

            if (!PayrollMonthHelper.IsValid(monthName))
            {
                TempData["ErrorMessage"] = "Invalid month selected.";
                return RedirectToAction(returnAction, new { department });
            }

            if (year < 2000 || year > 2100)
            {
                TempData["ErrorMessage"] = "Invalid year selected.";
                return RedirectToAction(returnAction, new { department });
            }

            int successCount = 0;
            int skipCount = 0;
            int errorCount = 0;
            var errors = new List<string>();
            int warned = 0;

            foreach (var employeeId in selectedEmployeeIds)
            {
                try
                {
                    var employee = await _context.Employees.FindAsync(employeeId);
                    if (employee == null)
                    {
                        errorCount++;
                        errors.Add($"Employee ID {employeeId} not found.");
                        continue;
                    }

                    var slipExists = await SlipExistsAsync(employeeId, monthName, year);
                    var issues = PayrollCalculationService.CheckEmployee(employee, monthName, year, slipExists);
                    if (slipExists)
                    {
                        skipCount++;
                        continue;
                    }

                    var block = BlockingReason(issues);
                    if (block != null)
                    {
                        errorCount++;
                        errors.Add($"Employee ID {employee.EmployeeID}: {block}");
                        continue;
                    }

                    var slip = _payrollService.GeneratePayslip(
                        employeeId,
                        monthName,
                        year,
                        User.Identity?.Name ?? "System");

                    successCount++;
                    if (issues.Count > 0 || slip.NetSalary < 0m)
                    {
                        warned++;
                        errors.Add($"Employee ID {employee.EmployeeID} generated, check: "
                            + string.Join(" ", issues.Select(i => i.Text))
                            + (slip.NetSalary < 0m ? " Net pay is negative." : ""));
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errors.Add($"Error for Employee ID {employeeId}: {ex.Message}");
                }
            }

            // Build result message
            var message = $"Payroll generation completed: {successCount} successful";
            if (skipCount > 0) message += $", {skipCount} skipped (already exists)";
            if (errorCount > 0) message += $", {errorCount} errors";

            if (successCount > 0)
            {
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            if (errors.Any())
            {
                TempData["ErrorDetails"] = string.Join("<br/>", errors);
            }

            return RedirectToAction(returnAction, new { department });
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

