using HRMBT.Web.Data;
using HRMBT.Web.Infrastructure;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers
{
[Route("payslips")]
    public class PayslipsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly HRMBT.Web.Services.IAuditLogService _auditLog;

        private readonly HRMBT.Web.Services.Payroll.PayrollCalculationService _payrollService;

        public PayslipsController(ApplicationDbContext context, HRMBT.Web.Services.IAuditLogService auditLog,
            HRMBT.Web.Services.Payroll.PayrollCalculationService payrollService)
        {
            _context = context;
            _auditLog = auditLog;
            _payrollService = payrollService;
        }

        private static object PayslipSnapshot(Payslip p) => new
        {
            p.Id, p.EmployeeId, p.Month, p.Year, p.BasicSalary, p.TotalAllowances, p.GrossSalary,
            p.TaxAmount, p.TotalDeductions, p.NetSalary, p.IsLocked, p.GeneratedBy
        };

        private static object PayslipWithLines(Payslip p) => new
        {
            Payslip = PayslipSnapshot(p),
            Lines = p.PayslipDetails.OrderBy(d => d.SortOrder)
                .Select(d => new { d.ItemType, d.ItemName, d.Quantity, d.UnitAmount, d.Amount }).ToList()
        };

        // GET: /Payslips
        [HttpGet("")]
        public async Task<IActionResult> Index(
            string? employeeQuery,
            string? department,
            int? employeeId,
            string? month,
            int? year,
            string? status,
            int page = 1,
            int pageSize = 20)
        {
            ViewData["Module"] = "Payroll";

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            string? monthFilter = null;
            if (!string.IsNullOrWhiteSpace(month))
            {
                monthFilter = PayrollMonthHelper.Normalize(month);
            }

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
                query = query.Where(p => p.Employee != null && p.Employee.DepartmentInfo!.DepartmentName == department);
            }

            if (employeeId.HasValue && employeeId.Value > 0)
            {
                query = query.Where(p => p.EmployeeId == employeeId.Value);
            }

            if (monthFilter != null)
            {
                query = query.Where(p => p.Month == monthFilter);
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

            query = query.OrderByPeriodDescending();

            var paginated = await PaginatedList<Payslip>.CreateAsync(query, page, pageSize);

            ViewBag.Departments = await _context.Employees
                .Where(e => e.DepartmentInfo!.DepartmentName != null && e.DepartmentInfo!.DepartmentName != "")
                .Select(e => e.DepartmentInfo!.DepartmentName!)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            ViewBag.EmployeeQuery = employeeQuery;
            ViewBag.Department = department;
            ViewBag.EmployeeId = employeeId;
            ViewBag.Month = monthFilter ?? month;
            ViewBag.Year = year;
            ViewBag.Status = status;
            ViewBag.CurrentPageSize = pageSize;

            return View(paginated);
        }

        // GET: /Payslips/Create
        [HttpGet("new")]
        public async Task<IActionResult> Create()
        {
            ViewData["Module"] = "Payroll";

            await PopulateLookups();

            return View(new PayslipCreateVM
            {
                Month = PayrollMonthHelper.CurrentMonthName(),
                Year = DateTime.Now.Year
            });
        }

        // GET: /Payslips/Generate (alias for Create)
        [HttpGet("generate")]
        public IActionResult Generate() => RedirectToAction(nameof(Create));

        // POST: /Payslips/Create
        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PayslipCreateVM model)
        {
            ViewData["Module"] = "Payroll";

            model.Month = PayrollMonthHelper.Normalize(model.Month);

            if (!model.EmployeeId.HasValue || model.EmployeeId.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "Please select an employee.");
            }

            if (!PayrollMonthHelper.IsValid(model.Month))
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
                        $"A payslip already exists for {employee.EmployeeName} for {PayrollMonthHelper.FormatPeriod(model.Month, model.Year)}.");
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
            await _auditLog.WriteAsync("Payslips", "INSERT", payslip.Id.ToString(), employee.uid,
                null, PayslipSnapshot(payslip), "Payroll (manual payslip)");

            TempData["SuccessMessage"] =
                $"Payslip generated for {employee.EmployeeName} ({employee.EmployeeID}) — {PayrollMonthHelper.FormatPeriod(model.Month, model.Year)}.";

            return RedirectToAction(nameof(Details), new { id = payslip.Id });
        }

        // GET: /Payslips/Details/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            ViewData["Module"] = "Payroll";

            if (id == null) return NotFound();

            var payslip = await _context.Payslips
                .Include(p => p.Employee)
                .Include(p => p.PayslipDetails)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payslip == null) return NotFound();

            ViewBag.CanRegenerate = !payslip.IsLocked && !HRMBT.Web.Services.Payroll.PayrollCalculationService.IsManual(payslip);
            return View(payslip);
        }

        // POST: /payslips/5/regenerate  (T4: update the same payslip and replace its lines)
        [HttpPost("{id:int}/regenerate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Regenerate(int id)
        {
            try
            {
                var refusal = await _payrollService.RegeneratePayslipAsync(id, User.Identity?.Name ?? "System", PayslipWithLines,
                    (p, before) => _auditLog.WriteAsync("Payslips", "UPDATE", p.Id.ToString(), p.EmployeeId, before, PayslipWithLines(p), "Payroll (regenerate)"));
                if (refusal == null)
                    TempData["SuccessMessage"] = "Payslip regenerated from the current salary, allowances, deductions and tax rules.";
                else
                    TempData["ErrorMessage"] = refusal;
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = "The payslip could not be regenerated. It was left as it was.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Payslips/Print/5
        [HttpGet("{id}/print")]
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
        [HttpGet("{id}/delete")]
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
        [HttpPost("{id}/delete"), ActionName("Delete")]
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

                var oldSnapshot = PayslipSnapshot(payslip);
                var employeeId = payslip.EmployeeId;
                var details = _context.PayslipDetails.Where(d => d.PayslipId == id);
                _context.PayslipDetails.RemoveRange(details);
                _context.Payslips.Remove(payslip);
                await _context.SaveChangesAsync();
                await _auditLog.WriteAsync("Payslips", "DELETE", id.ToString(), employeeId,
                    oldSnapshot, null, "Payroll");
                TempData["SuccessMessage"] = "Payslip deleted.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ---------- AJAX ----------

        // GET: /Payslips/EmployeesByDepartment?department=Finance
        [HttpGet("lookup/employees-by-department")]
        public async Task<JsonResult> EmployeesByDepartment(string? department)
        {
            var query = _context.Employees.AsQueryable();

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(e => e.DepartmentInfo!.DepartmentName == department);
            }

            var list = await query
                .OrderBy(e => e.EmployeeName)
                .Select(e => new
                {
                    id = e.uid,
                    employeeId = e.EmployeeID,
                    name = e.EmployeeName,
                    department = e.DepartmentInfo!.DepartmentName,
                    designation = e.DesignationInfo!.Name,
                    status = e.EmployeeStatus
                })
                .ToListAsync();

            return Json(list);
        }

        // GET: /Payslips/EmployeeDetails?id=5
        [HttpGet("lookup/employees/{id}")]
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
                .Where(e => e.DepartmentInfo!.DepartmentName != null && e.DepartmentInfo!.DepartmentName != "")
                .Select(e => e.DepartmentInfo!.DepartmentName!)
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
                    Department = e.DepartmentInfo!.DepartmentName,
                    Designation = e.DesignationInfo!.Name,
                    BasicSalary = e.BasicSalary ?? 0m
                })
                .ToListAsync();
        }
    }
}
