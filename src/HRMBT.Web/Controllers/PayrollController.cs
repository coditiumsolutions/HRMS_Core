using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services.Payroll;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace HRMBT.Web.Controllers
{
    public class PayrollController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PayrollCalculationService _payrollService;

        public PayrollController(ApplicationDbContext context, PayrollCalculationService payrollService)
        {
            _context = context;
            _payrollService = payrollService;
        }

        private static string FormatMonthYearForGenStatus(int month, int year) =>
            $"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)} {year}";

        /// <summary>GenStatus is nvarchar(50) on dbo.Employee — keep messages within limit.</summary>
        private static string ClampGenStatus(string? value, int maxLen = 50)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length <= maxLen) return value;
            return value.Substring(0, maxLen - 3) + "...";
        }

        // GET: Payroll
        public async Task<IActionResult> Index(int? month, int? year, string employeeId, string employeeName, int page = 1, int pageSize = 20)
        {
            ViewData["Module"] = "Payroll";
            
            // Ensure valid pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100; // Max page size limit

            var query = _context.Payslips.Include(p => p.Employee).AsQueryable();

            // Filter by month if provided
            if (month.HasValue && month.Value > 0)
            {
                query = query.Where(p => p.Month == month.Value);
            }

            // Filter by year if provided
            if (year.HasValue && year.Value > 0)
            {
                query = query.Where(p => p.Year == year.Value);
            }

            // Filter by EmployeeID if provided (partial match on Employee.EmployeeID)
            if (!string.IsNullOrWhiteSpace(employeeId))
            {
                query = query.Where(p => p.Employee != null && 
                    p.Employee.EmployeeID != null && 
                    p.Employee.EmployeeID.Contains(employeeId));
            }

            // Filter by Employee Name if provided (partial match, case-insensitive)
            if (!string.IsNullOrWhiteSpace(employeeName))
            {
                query = query.Where(p => p.Employee != null && 
                    p.Employee.EmployeeName != null && 
                    p.Employee.EmployeeName.Contains(employeeName));
            }

            // Apply ordering before pagination
            query = query
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .ThenBy(p => p.Employee != null ? p.Employee.EmployeeName : "");

            // Get paginated results
            var paginatedPayslips = await PaginatedList<Payslip>.CreateAsync(query, page, pageSize);

            // Pass filter values to view
            ViewBag.Month = month;
            ViewBag.Year = year;
            ViewBag.EmployeeId = employeeId;
            ViewBag.EmployeeName = employeeName;
            ViewBag.CurrentPageSize = pageSize;

            return View(paginatedPayslips);
        }

        // GET: Payroll/GeneratePayslips
        public async Task<IActionResult> GeneratePayslips()
        {
            ViewData["Module"] = "Payroll";
            
            // Get distinct departments for dropdown
            var departments = await _context.Employees
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            ViewBag.Departments = departments;

            return View(new GeneratePayslipsVM
            {
                Month = DateTime.Now.Month,
                Year = DateTime.Now.Year
            });
        }

        // POST: Payroll/GeneratePayslips
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePayslips(GeneratePayslipsVM model)
        {
            ViewData["Module"] = "Payroll";
            if (model.Month < 1 || model.Month > 12)
                ModelState.AddModelError("Month", "Invalid month.");

            if (model.Year < 2000 || model.Year > 2100)
                ModelState.AddModelError("Year", "Invalid year.");

            if (!ModelState.IsValid)
                return View(model);

            bool locked = await _context.Payslips
                .AnyAsync(p => p.Month == model.Month && p.Year == model.Year && p.IsLocked);

            if (locked)
            {
                ModelState.AddModelError("", "Payroll for this month is locked.");
                return View(model);
            }

            // Get distinct departments for dropdown (in case of validation error)
            var departments = await _context.Employees
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();
            ViewBag.Departments = departments;

            // Filter employees by department if specified
            var query = _context.Employees.Where(e => e.EmployeeStatus == "Active");
            
            if (!string.IsNullOrWhiteSpace(model.Department))
            {
                query = query.Where(e => e.Department != null && e.Department == model.Department);
            }

            var employees = await query.ToListAsync();

            int generated = 0;
            int skipped = 0;

            foreach (var emp in employees)
            {
                if (await _context.Payslips.AnyAsync(p =>
                    p.EmployeeId == emp.uid &&
                    p.Month == model.Month &&
                    p.Year == model.Year))
                {
                    skipped++;
                    continue;
                }

                _payrollService.GeneratePayslip(
                    emp.uid,
                    model.Month,
                    model.Year,
                    User.Identity?.Name ?? "System");

                generated++;
            }

            TempData["SuccessMessage"] =
                $"Payroll completed. Generated: {generated}, Skipped: {skipped}";

            return RedirectToAction(nameof(Index));
        }

        // GET: Payroll/Details/5
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
        public async Task<IActionResult> Search(int? month, int? year, string employeeId, string employeeName)
        {
            ViewData["Module"] = "Payroll";
            
            var query = _context.Payslips.Include(p => p.Employee).AsQueryable();

            // Default to current month if no filters
            if (!month.HasValue && !year.HasValue && string.IsNullOrEmpty(employeeId) && string.IsNullOrEmpty(employeeName))
            {
                month = DateTime.Now.Month;
                year = DateTime.Now.Year;
            }

            if (month.HasValue)
                query = query.Where(p => p.Month == month.Value);

            if (year.HasValue)
                query = query.Where(p => p.Year == year.Value);

            if (!string.IsNullOrWhiteSpace(employeeId))
                query = query.Where(p => p.Employee != null && p.Employee.EmployeeID.Contains(employeeId));

            if (!string.IsNullOrWhiteSpace(employeeName))
                query = query.Where(p => p.Employee != null && p.Employee.EmployeeName.Contains(employeeName));

            var payslips = await query
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .ThenBy(p => p.Employee != null ? p.Employee.EmployeeName : "")
                .ToListAsync();

            ViewBag.Month = month ?? DateTime.Now.Month;
            ViewBag.Year = year ?? DateTime.Now.Year;
            ViewBag.EmployeeId = employeeId ?? "";
            ViewBag.EmployeeName = employeeName ?? "";

            return View(payslips);
        }

        // GET: Payroll/GenerateIndividual
        public IActionResult GenerateIndividual()
        {
            ViewData["Module"] = "Payroll";
            return View(new GenerateIndividualVM
            {
                Month = DateTime.Now.Month,
                Year = DateTime.Now.Year
            });
        }

        // POST: Payroll/GenerateIndividual
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateIndividual(GenerateIndividualVM model)
        {
            ViewData["Module"] = "Payroll";

            if (string.IsNullOrWhiteSpace(model.EmployeeID))
            {
                ModelState.AddModelError("EmployeeID", "Employee ID is required.");
            }

            if (model.Month < 1 || model.Month > 12)
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

            if (employee.EmployeeStatus != "Active")
            {
                ModelState.AddModelError("EmployeeID", $"Employee '{model.EmployeeID}' is not active.");
                return View(model);
            }

            // Check if payslip already exists
            bool exists = await _context.Payslips
                .AnyAsync(p => p.EmployeeId == employee.uid && 
                              p.Month == model.Month && 
                              p.Year == model.Year);

            if (exists)
            {
                ModelState.AddModelError("", $"Payslip already exists for Employee {model.EmployeeID} for {model.Month}/{model.Year}.");
                return View(model);
            }

            try
            {
                _payrollService.GeneratePayslip(
                    employee.uid,
                    model.Month,
                    model.Year,
                    User.Identity?.Name ?? "System");

                TempData["SuccessMessage"] = $"Payslip generated successfully for Employee {model.EmployeeID} ({employee.EmployeeName}) for {model.Month}/{model.Year}.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error generating payslip: {ex.Message}");
                return View(model);
            }
        }

        // GET: Payroll/Create
        public IActionResult Create()
        {
            ViewData["Module"] = "Payroll";
            ViewData["EmployeeId"] = string.Empty;
            ViewData["Month"] = DateTime.Now.Month;
            ViewData["Year"] = DateTime.Now.Year;
            return View();
        }

        // POST: Payroll/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string employeeId, int? month, int? year)
        {
            ViewData["Module"] = "Payroll";
            
            // Clear model state and set values for view
            ModelState.Clear();
            ViewData["EmployeeId"] = employeeId ?? string.Empty;
            ViewData["Month"] = month ?? DateTime.Now.Month;
            ViewData["Year"] = year ?? DateTime.Now.Year;

            // Validate Employee ID
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                ModelState.AddModelError("EmployeeId", "Employee ID is required.");
                return View();
            }

            // Validate month and year
            if (!month.HasValue || month < 1 || month > 12)
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
                    e.EmployeeID.Trim().ToLower() == trimmedEmployeeId.ToLower() && 
                    e.EmployeeStatus == "Active");

            if (employee == null)
            {
                var inactiveEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID != null && 
                        e.EmployeeID.Trim().ToLower() == trimmedEmployeeId.ToLower());
                
                if (inactiveEmployee != null)
                {
                    ModelState.AddModelError("EmployeeId", $"Employee with ID '{employeeId}' exists but is not active.");
                }
                else
                {
                    ModelState.AddModelError("EmployeeId", $"Employee with ID '{employeeId}' not found.");
                }
                return View();
            }

            // Check if payslip already exists
            var existing = await _context.Payslips
                .FirstOrDefaultAsync(p => p.EmployeeId == employee.uid && 
                                         p.Month == month.Value && 
                                         p.Year == year.Value);

            if (existing != null)
            {
                ModelState.AddModelError("", "A payslip already exists for this employee for the selected month and year.");
                return View();
            }

            try
            {
                _payrollService.GeneratePayslip(
                    employee.uid,
                    month.Value,
                    year.Value,
                    User.Identity?.Name ?? "System");

                TempData["SuccessMessage"] = $"Payslip generated successfully for Employee ID: {employeeId}!";
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
                query = query.Where(e => e.Department != null && e.Department == department);
            }

            // Apply ordering before pagination
            query = query.OrderBy(e => e.EmployeeName);

            // Get paginated results
            var paginatedEmployees = await PaginatedList<Employee>.CreateAsync(query, page, pageSize);

            ViewBag.GrossSalaryByUid = _payrollService.ComputeGrossPreviewForEmployees(paginatedEmployees);

            // Get distinct departments for dropdown
            var departments = await _context.Employees
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department)
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
        public Task<IActionResult> GeneratePayroll(string department, int page = 1, int pageSize = 20)
        {
            return EmployeeDetail(department, page, pageSize);
        }

        // POST: Payroll/GeneratePayrollForSelected
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePayrollForSelected(List<int> selectedEmployeeIds, int month, int year, string? sourceAction, string? department)
        {
            ViewData["Module"] = "Payroll";
            var returnAction = string.Equals(sourceAction, nameof(GeneratePayroll), StringComparison.OrdinalIgnoreCase)
                ? nameof(GeneratePayroll)
                : nameof(EmployeeDetail);

            if (selectedEmployeeIds == null || !selectedEmployeeIds.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one employee.";
                return RedirectToAction(returnAction, new { department });
            }

            if (month < 1 || month > 12)
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
            var monthYearLabel = FormatMonthYearForGenStatus(month, year);

            foreach (var employeeId in selectedEmployeeIds)
            {
                try
                {
                    // Check if payslip already exists
                    bool exists = await _context.Payslips
                        .AnyAsync(p => p.EmployeeId == employeeId &&
                                      p.Month == month &&
                                      p.Year == year);

                    if (exists)
                    {
                        var empSkip = await _context.Employees.FindAsync(employeeId);
                        if (empSkip != null)
                            empSkip.GenStatus = ClampGenStatus($"Already Created for {monthYearLabel}");
                        skipCount++;
                        continue;
                    }

                    var employee = await _context.Employees.FindAsync(employeeId);
                    if (employee == null)
                    {
                        errorCount++;
                        errors.Add($"Employee ID {employeeId} not found.");
                        continue;
                    }

                    if (!string.Equals(employee.EmployeeStatus, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        employee.GenStatus = ClampGenStatus($"Not created: not active for {monthYearLabel}");
                        errorCount++;
                        errors.Add($"Employee ID {employee.EmployeeID} is not active.");
                        continue;
                    }

                    _payrollService.GeneratePayslip(
                        employeeId,
                        month,
                        year,
                        User.Identity?.Name ?? "System");

                    employee.GenStatus = ClampGenStatus($"Payroll Created for {monthYearLabel}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    var empErr = await _context.Employees.FindAsync(employeeId);
                    if (empErr != null)
                    {
                        var coreReason = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                        empErr.GenStatus = ClampGenStatus($"Not created: {coreReason}");
                    }

                    errors.Add($"Error for Employee ID {empErr?.EmployeeID ?? employeeId.ToString()}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();

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
        public async Task<IActionResult> PayrollStatus(string year, int? month, string department, string payrollStatus, int page = 1, int pageSize = 20)
        {
            ViewData["Module"] = "Payroll";
            
            // Ensure valid pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            // Only process if year and month are provided (user has searched)
            bool hasSearched = !string.IsNullOrWhiteSpace(year) && month.HasValue;
            
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
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department)
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
                    employeeQuery = employeeQuery.Where(e => e.Department != null && e.Department == department);
                }

                // Get payslip IDs for the selected month/year grouped by employee
                var payslipData = await _context.Payslips
                    .Where(p => p.Month == month.Value && p.Year == actualYear.Value)
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
                    .Where(p => p.Month == month.Value && p.Year == actualYear.Value && paginatedEmployeeIds.Contains(p.EmployeeId))
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
            ViewBag.Month = month;
            ViewBag.Department = department;
            ViewBag.PayrollStatus = payrollStatus;
            ViewBag.ActualYear = actualYear;
            ViewBag.Departments = departments;
            ViewBag.CurrentPageSize = pageSize;
            ViewBag.PayslipLookup = payslipLookupFull; // For displaying status in view

            return View(paginatedList);
        }

        // GET: Payroll/Dashboard
        public async Task<IActionResult> Dashboard(int? month, int? year)
        {
            ViewData["Module"] = "Payroll";
            
            // Set defaults to current month/year if not provided
            int selectedMonth = month ?? DateTime.Now.Month;
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
