using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers
{
    public class PayslipsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PayslipsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Payslips
        public async Task<IActionResult> Index(
            string? employeeQuery,
            string? department,
            int? employeeId,
            int? month,
            int? year,
            string? status,
            int page = 1,
            int pageSize = 20)
        {
            ViewData["Module"] = "Payroll";

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Payslips.Include(p => p.Employee).AsQueryable();

            if (!string.IsNullOrWhiteSpace(employeeQuery))
            {
                var q = employeeQuery.Trim();
                query = query.Where(p => p.Employee != null && (
                    (p.Employee.EmployeeName != null && p.Employee.EmployeeName.Contains(q)) ||
                    (p.Employee.EmployeeID != null && p.Employee.EmployeeID.Contains(q))));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(p => p.Employee != null && p.Employee.Department == department);
            }

            if (employeeId.HasValue && employeeId.Value > 0)
            {
                query = query.Where(p => p.EmployeeId == employeeId.Value);
            }

            if (month.HasValue && month.Value > 0)
            {
                query = query.Where(p => p.Month == month.Value);
            }

            if (year.HasValue && year.Value > 0)
            {
                query = query.Where(p => p.Year == year.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (string.Equals(status, "Locked", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.IsLocked);
                else if (string.Equals(status, "Unlocked", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => !p.IsLocked);
            }

            query = query
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .ThenBy(p => p.Employee != null ? p.Employee.EmployeeName : string.Empty);

            var paginated = await PaginatedList<Payslip>.CreateAsync(query, page, pageSize);

            ViewBag.Departments = await _context.Employees
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department!)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            ViewBag.Employees = await _context.Employees
                .OrderBy(e => e.EmployeeName)
                .Select(e => new PayslipEmployeeOption
                {
                    Uid = e.uid,
                    EmployeeID = e.EmployeeID,
                    EmployeeName = e.EmployeeName,
                    Department = e.Department,
                    Designation = e.Designation,
                    BasicSalary = e.BasicSalary ?? 0m
                })
                .ToListAsync();

            ViewBag.EmployeeQuery = employeeQuery;
            ViewBag.Department = department;
            ViewBag.EmployeeId = employeeId;
            ViewBag.Month = month;
            ViewBag.Year = year;
            ViewBag.Status = status;
            ViewBag.CurrentPageSize = pageSize;
            ViewBag.PageSizeOptions = new List<int> { 10, 20, 50, 100 };

            ViewBag.TotalAmount = await _context.Payslips
                .Where(p => (!month.HasValue || p.Month == month.Value)
                         && (!year.HasValue || p.Year == year.Value))
                .SumAsync(p => (decimal?)p.NetSalary) ?? 0m;

            return View(paginated);
        }

        // GET: /Payslips/Create
        public async Task<IActionResult> Create()
        {
            ViewData["Module"] = "Payroll";

            await PopulateLookups();

            return View(new PayslipCreateVM
            {
                Month = DateTime.Now.Month,
                Year = DateTime.Now.Year
            });
        }

        // GET: /Payslips/Generate (alias for Create)
        public IActionResult Generate() => RedirectToAction(nameof(Create));

        // POST: /Payslips/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PayslipCreateVM model)
        {
            ViewData["Module"] = "Payroll";

            if (!model.EmployeeId.HasValue || model.EmployeeId.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "Please select an employee.");
            }

            if (model.Month < 1 || model.Month > 12)
            {
                ModelState.AddModelError(nameof(model.Month), "Please select a valid month.");
            }

            if (model.Year < 2000 || model.Year > 2100)
            {
                ModelState.AddModelError(nameof(model.Year), "Please enter a valid year.");
            }

            if (model.GrossSalary < 0)
            {
                ModelState.AddModelError(nameof(model.GrossSalary), "Gross salary cannot be negative.");
            }

            if (model.TotalDeductions < 0)
            {
                ModelState.AddModelError(nameof(model.TotalDeductions), "Deductions cannot be negative.");
            }

            Employee? employee = null;
            if (model.EmployeeId.HasValue && model.EmployeeId.Value > 0)
            {
                employee = await _context.Employees.FirstOrDefaultAsync(e => e.uid == model.EmployeeId.Value);
                if (employee == null)
                {
                    ModelState.AddModelError(nameof(model.EmployeeId), "Selected employee was not found.");
                }
            }

            if (ModelState.IsValid && employee != null)
            {
                bool exists = await _context.Payslips.AnyAsync(p =>
                    p.EmployeeId == employee.uid &&
                    p.Month == model.Month &&
                    p.Year == model.Year);

                if (exists)
                {
                    ModelState.AddModelError(string.Empty,
                        $"A payslip already exists for {employee.EmployeeName} for {GetMonthName(model.Month)} {model.Year}.");
                }
            }

            bool isMonthLocked = false;
            if (ModelState.IsValid)
            {
                isMonthLocked = await _context.Payslips.AnyAsync(p =>
                    p.Month == model.Month && p.Year == model.Year && p.IsLocked);
                if (isMonthLocked)
                {
                    ModelState.AddModelError(string.Empty,
                        $"Payroll for {GetMonthName(model.Month)} {model.Year} is locked. New payslips cannot be added.");
                }
            }

            if (!ModelState.IsValid || employee == null)
            {
                await PopulateLookups();
                return View(model);
            }

            var netSalary = model.GrossSalary - model.TotalDeductions;

            var payslip = new Payslip
            {
                EmployeeId = employee.uid,
                Month = model.Month,
                Year = model.Year,
                BasicSalary = model.BasicSalary,
                GrossSalary = model.GrossSalary,
                TotalDeductions = model.TotalDeductions,
                NetSalary = netSalary,
                WorkingDays = model.WorkingDays,
                LeaveDays = model.LeaveDays,
                LeaveBalance = model.LeaveBalance ?? string.Empty,
                Notes = model.Notes ?? string.Empty,
                CalculationDetails = string.Empty,
                GeneratedDate = DateTime.Now,
                GeneratedBy = User.Identity?.Name ?? "System",
                IsLocked = false
            };

            _context.Payslips.Add(payslip);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Payslip generated for {employee.EmployeeName} ({employee.EmployeeID}) — {GetMonthName(model.Month)} {model.Year}.";

            return RedirectToAction(nameof(Details), new { id = payslip.Id });
        }

        // GET: /Payslips/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            ViewData["Module"] = "Payroll";

            if (id == null) return NotFound();

            var payslip = await _context.Payslips
                .Include(p => p.Employee)
                .Include(p => p.PayslipDetails)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payslip == null) return NotFound();

            return View(payslip);
        }

        // GET: /Payslips/Print/5
        public async Task<IActionResult> Print(int? id)
        {
            ViewData["Module"] = "Payroll";

            if (id == null) return NotFound();

            var payslip = await _context.Payslips
                .Include(p => p.Employee)
                .Include(p => p.PayslipDetails)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payslip == null) return NotFound();

            return View(payslip);
        }

        // GET: /Payslips/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            ViewData["Module"] = "Payroll";

            if (id == null) return NotFound();

            var payslip = await _context.Payslips
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payslip == null) return NotFound();

            return View(payslip);
        }

        // POST: /Payslips/DeleteConfirmed/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ViewData["Module"] = "Payroll";

            var payslip = await _context.Payslips.FindAsync(id);
            if (payslip != null)
            {
                if (payslip.IsLocked)
                {
                    TempData["ErrorMessage"] = "This payslip is locked and cannot be deleted.";
                    return RedirectToAction(nameof(Index));
                }

                var details = _context.PayslipDetails.Where(d => d.PayslipId == id);
                _context.PayslipDetails.RemoveRange(details);
                _context.Payslips.Remove(payslip);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Payslip deleted.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ---------- AJAX ----------

        // GET: /Payslips/EmployeesByDepartment?department=Finance
        [HttpGet]
        public async Task<JsonResult> EmployeesByDepartment(string? department)
        {
            var query = _context.Employees.AsQueryable();

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(e => e.Department == department);
            }

            var list = await query
                .OrderBy(e => e.EmployeeName)
                .Select(e => new
                {
                    id = e.uid,
                    employeeId = e.EmployeeID,
                    name = e.EmployeeName,
                    department = e.Department,
                    designation = e.Designation,
                    status = e.EmployeeStatus
                })
                .ToListAsync();

            return Json(list);
        }

        // GET: /Payslips/EmployeeDetails?id=5
        [HttpGet]
        public async Task<JsonResult> EmployeeDetails(int id)
        {
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.uid == id);
            if (employee == null)
            {
                return Json(new { found = false });
            }

            return Json(new
            {
                found = true,
                id = employee.uid,
                employeeId = employee.EmployeeID,
                name = employee.EmployeeName,
                department = employee.Department,
                designation = employee.Designation,
                status = employee.EmployeeStatus,
                basicSalary = employee.BasicSalary ?? 0m
            });
        }

        // ---------- Helpers ----------

        private async Task PopulateLookups()
        {
            ViewBag.Departments = await _context.Employees
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department!)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            ViewBag.Employees = await _context.Employees
                .OrderBy(e => e.EmployeeName)
                .Select(e => new PayslipEmployeeOption
                {
                    Uid = e.uid,
                    EmployeeID = e.EmployeeID,
                    EmployeeName = e.EmployeeName,
                    Department = e.Department,
                    Designation = e.Designation,
                    BasicSalary = e.BasicSalary ?? 0m
                })
                .ToListAsync();
        }

        private static string GetMonthName(int month) =>
            month >= 1 && month <= 12
                ? System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)
                : month.ToString();
    }
}
