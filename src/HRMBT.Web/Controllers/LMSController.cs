using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;

namespace HRMBT.Web.Controllers
{
    public class LMSController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LMSController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: LMS
        public async Task<IActionResult> Index(string? employeeId, string? status, string? leaveType)
        {
            ViewData["Module"] = "LMS";
            var query = _context.EmployeeLeaves.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(employeeId))
            {
                query = query.Where(l => l.EmployeeID.Contains(employeeId));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(l => l.Status == status);
            }

            if (!string.IsNullOrEmpty(leaveType))
            {
                query = query.Where(l => l.LeaveTypeName == leaveType);
            }

            var leaves = await query
                .OrderByDescending(l => l.StartDate)
                .ThenByDescending(l => l.Id)
                .ToListAsync();

            // Populate filter dropdowns
            ViewBag.Statuses = new SelectList(new[] { "Applied", "Approved", "Rejected", "Cancelled" });
            ViewBag.LeaveTypes = await _context.LeaveQuotas
                .Select(lq => lq.LeaveTypeName)
                .Distinct()
                .OrderBy(lt => lt)
                .ToListAsync();
            ViewBag.Employees = await _context.Employees
                .Where(e => e.EmployeeStatus == "Active")
                .OrderBy(e => e.EmployeeName)
                .Select(e => new { e.EmployeeID, e.EmployeeName })
                .ToListAsync();

            ViewBag.CurrentEmployeeId = employeeId;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentLeaveType = leaveType;

            return View(leaves);
        }

        // GET: LMS/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var employeeLeave = await _context.EmployeeLeaves
                .FirstOrDefaultAsync(m => m.Id == id);

            if (employeeLeave == null) return NotFound();

            return View(employeeLeave);
        }

        // GET: LMS/Create
        public async Task<IActionResult> Create()
        {
            ViewData["Module"] = "LMS";
            
            // Populate dropdowns
            ViewBag.Employees = new SelectList(
                await _context.Employees
                    .Where(e => e.EmployeeStatus == "Active")
                    .OrderBy(e => e.EmployeeName)
                    .Select(e => new { e.EmployeeID, DisplayName = $"{e.EmployeeID} - {e.EmployeeName}" })
                    .ToListAsync(),
                "EmployeeID",
                "DisplayName");

            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList();

            ViewBag.Statuses = new SelectList(new[] { "Applied", "Approved", "Rejected", "Cancelled" }, "Applied");

            return View();
        }

        // POST: LMS/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EmployeeID,LeaveTypeName,StartDate,EndDate,TotalDays,Short_Adj,AddDays,ExcludeDays,Status,ApprovedBy,ApprovedOn,Comments")] EmployeeLeave employeeLeave)
        {
            ViewData["Module"] = "LMS";

            // Validate dates
            if (employeeLeave.EndDate < employeeLeave.StartDate)
            {
                ModelState.AddModelError("EndDate", "End date must be greater than or equal to start date.");
            }

            // Set AppliedDate and Year
            employeeLeave.AppliedDate = DateTime.Now;
            if (string.IsNullOrEmpty(employeeLeave.Year))
            {
                employeeLeave.Year = DateTime.Now.Year.ToString();
            }

            // Calculate TotalDays if not provided
            if (!employeeLeave.TotalDays.HasValue)
            {
                var days = (employeeLeave.EndDate - employeeLeave.StartDate).Days + 1;
                
                // Exclude gazetted holidays
                var holidays = await _context.GazettedHolidays
                    .Where(h => h.HolidayDate >= employeeLeave.StartDate && h.HolidayDate <= employeeLeave.EndDate)
                    .CountAsync();
                
                days -= holidays;

                // Apply adjustments
                if (employeeLeave.AddDays.HasValue)
                    days += employeeLeave.AddDays.Value;
                if (employeeLeave.ExcludeDays.HasValue)
                    days -= employeeLeave.ExcludeDays.Value;

                employeeLeave.TotalDays = Math.Max(0, days);
            }

            // Set default status if not provided
            if (string.IsNullOrEmpty(employeeLeave.Status))
            {
                employeeLeave.Status = "Applied";
            }

            // If status is Approved, set ApprovedBy and ApprovedOn
            if (employeeLeave.Status == "Approved" && string.IsNullOrEmpty(employeeLeave.ApprovedBy))
            {
                employeeLeave.ApprovedBy = User.Identity?.Name ?? "System";
                employeeLeave.ApprovedOn = DateTime.Now;
            }

            if (ModelState.IsValid)
            {
                _context.Add(employeeLeave);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Leave application created successfully.";
                return RedirectToAction(nameof(Index));
            }

            // Repopulate dropdowns on error
            ViewBag.Employees = new SelectList(
                await _context.Employees
                    .Where(e => e.EmployeeStatus == "Active")
                    .OrderBy(e => e.EmployeeName)
                    .Select(e => new { e.EmployeeID, DisplayName = $"{e.EmployeeID} - {e.EmployeeName}" })
                    .ToListAsync(),
                "EmployeeID",
                "DisplayName",
                employeeLeave.EmployeeID);

            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList(employeeLeave.LeaveTypeName);

            ViewBag.Statuses = new SelectList(new[] { "Applied", "Approved", "Rejected", "Cancelled" }, employeeLeave.Status);

            return View(employeeLeave);
        }

        // GET: LMS/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var employeeLeave = await _context.EmployeeLeaves.FindAsync(id);
            if (employeeLeave == null) return NotFound();

            // Populate dropdowns
            ViewBag.Employees = new SelectList(
                await _context.Employees
                    .Where(e => e.EmployeeStatus == "Active")
                    .OrderBy(e => e.EmployeeName)
                    .Select(e => new { e.EmployeeID, DisplayName = $"{e.EmployeeID} - {e.EmployeeName}" })
                    .ToListAsync(),
                "EmployeeID",
                "DisplayName",
                employeeLeave.EmployeeID);

            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList(employeeLeave.LeaveTypeName);

            ViewBag.Statuses = new SelectList(new[] { "Applied", "Approved", "Rejected", "Cancelled" }, employeeLeave.Status);

            return View(employeeLeave);
        }

        // POST: LMS/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeID,LeaveTypeName,StartDate,EndDate,TotalDays,Short_Adj,AddDays,ExcludeDays,Status,ApprovedBy,ApprovedOn,Comments")] EmployeeLeave employeeLeave)
        {
            ViewData["Module"] = "LMS";
            if (id != employeeLeave.Id) return NotFound();

            // Validate dates
            if (employeeLeave.EndDate < employeeLeave.StartDate)
            {
                ModelState.AddModelError("EndDate", "End date must be greater than or equal to start date.");
            }

            // Update Year if not set
            if (string.IsNullOrEmpty(employeeLeave.Year))
            {
                employeeLeave.Year = DateTime.Now.Year.ToString();
            }

            // Recalculate TotalDays if dates changed
            if (ModelState.IsValid)
            {
                var days = (employeeLeave.EndDate - employeeLeave.StartDate).Days + 1;
                
                // Exclude gazetted holidays
                var holidays = await _context.GazettedHolidays
                    .Where(h => h.HolidayDate >= employeeLeave.StartDate && h.HolidayDate <= employeeLeave.EndDate)
                    .CountAsync();
                
                days -= holidays;

                // Apply adjustments
                if (employeeLeave.AddDays.HasValue)
                    days += employeeLeave.AddDays.Value;
                if (employeeLeave.ExcludeDays.HasValue)
                    days -= employeeLeave.ExcludeDays.Value;

                employeeLeave.TotalDays = Math.Max(0, days);
            }

            // If status changed to Approved, set ApprovedBy and ApprovedOn
            if (employeeLeave.Status == "Approved")
            {
                var existing = await _context.EmployeeLeaves.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
                if (existing?.Status != "Approved")
                {
                    employeeLeave.ApprovedBy = User.Identity?.Name ?? "System";
                    employeeLeave.ApprovedOn = DateTime.Now;
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(employeeLeave);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Leave application updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeLeaveExists(employeeLeave.Id))
                        return NotFound();
                    else
                        throw;
                }
            }

            // Repopulate dropdowns on error
            ViewBag.Employees = new SelectList(
                await _context.Employees
                    .Where(e => e.EmployeeStatus == "Active")
                    .OrderBy(e => e.EmployeeName)
                    .Select(e => new { e.EmployeeID, DisplayName = $"{e.EmployeeID} - {e.EmployeeName}" })
                    .ToListAsync(),
                "EmployeeID",
                "DisplayName",
                employeeLeave.EmployeeID);

            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList(employeeLeave.LeaveTypeName);

            ViewBag.Statuses = new SelectList(new[] { "Applied", "Approved", "Rejected", "Cancelled" }, employeeLeave.Status);

            return View(employeeLeave);
        }

        // GET: LMS/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var employeeLeave = await _context.EmployeeLeaves
                .FirstOrDefaultAsync(m => m.Id == id);

            if (employeeLeave == null) return NotFound();

            return View(employeeLeave);
        }

        // POST: LMS/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ViewData["Module"] = "LMS";
            var employeeLeave = await _context.EmployeeLeaves.FindAsync(id);
            if (employeeLeave != null)
            {
                _context.EmployeeLeaves.Remove(employeeLeave);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Leave application deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool EmployeeLeaveExists(int id)
        {
            return _context.EmployeeLeaves.Any(e => e.Id == id);
        }

        private async Task<SelectList> BuildLeaveTypesSelectList(string? selected = null)
        {
            var currentYear = DateTime.Now.Year;
            var typesForYear = await _context.LeaveQuotas
                .Where(lq => lq.Year == currentYear)
                .Select(lq => lq.LeaveTypeName)
                .Distinct()
                .OrderBy(lt => lt)
                .ToListAsync();

            var types = typesForYear.Any()
                ? typesForYear
                : await _context.LeaveQuotas
                    .Select(lq => lq.LeaveTypeName)
                    .Distinct()
                    .OrderBy(lt => lt)
                    .ToListAsync();

            return new SelectList(types, selected);
        }
    }
}
