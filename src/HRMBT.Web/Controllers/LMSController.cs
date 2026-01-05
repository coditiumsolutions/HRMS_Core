using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> Index()
        {
            ViewData["Module"] = "LMS";
            var leaves = await _context.LeaveRequests.ToListAsync();
            return View(leaves);
        }

        // GET: LMS/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var leave = await _context.LeaveRequests
                .FirstOrDefaultAsync(m => m.Id == id);

            if (leave == null) return NotFound();

            return View(leave);
        }

        // GET: LMS/Create
        public IActionResult Create()
        {
            ViewData["Module"] = "LMS";
            return View();
        }

        // POST: LMS/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EmployeeId,LeaveType,FromDate,ToDate,Status")] LeaveRequest leaveRequest)
        {
            ViewData["Module"] = "LMS";
            
            // Validate leave balance (FR-002)
            if (ModelState.IsValid)
            {
                // Calculate leave days requested
                var leaveDays = (leaveRequest.ToDate - leaveRequest.FromDate).Days + 1;
                
                // Get employee leave balance
                var employee = await _context.Employees.FindAsync(leaveRequest.EmployeeId);
                if (employee == null)
                {
                    ModelState.AddModelError("EmployeeId", "Employee not found.");
                    return View(leaveRequest);
                }
                
                // Get current year leave balance (Year2024)
                var availableLeaves = employee.Year2024 ?? 0;
                
                // Calculate used leaves (approved leaves for current year)
                var currentYear = DateTime.Now.Year;
                var usedLeaves = await _context.LeaveRequests
                    .Where(l => l.EmployeeId == leaveRequest.EmployeeId 
                        && l.Status == "Approved" 
                        && l.FromDate.Year == currentYear)
                    .SumAsync(l => (l.ToDate - l.FromDate).Days + 1);
                
                var remainingLeaves = availableLeaves - usedLeaves;
                
                // Validate leave balance
                if (leaveDays > remainingLeaves)
                {
                    ModelState.AddModelError("", 
                        $"Insufficient leave balance. Available: {remainingLeaves} days, Requested: {leaveDays} days.");
                    ViewBag.AvailableLeaves = remainingLeaves;
                    return View(leaveRequest);
                }
                
                // Set default status if not provided
                if (string.IsNullOrEmpty(leaveRequest.Status))
                {
                    leaveRequest.Status = "Pending";
                }
                
                _context.Add(leaveRequest);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(leaveRequest);
        }

        // GET: LMS/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var leave = await _context.LeaveRequests.FindAsync(id);
            if (leave == null) return NotFound();
            return View(leave);
        }

        // POST: LMS/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeId,LeaveType,FromDate,ToDate,Status")] LeaveRequest leaveRequest)
        {
            ViewData["Module"] = "LMS";
            if (id != leaveRequest.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(leaveRequest);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeaveRequestExists(leaveRequest.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(leaveRequest);
        }

        // GET: LMS/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var leave = await _context.LeaveRequests
                .FirstOrDefaultAsync(m => m.Id == id);
            if (leave == null) return NotFound();

            return View(leave);
        }

        // POST: LMS/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ViewData["Module"] = "LMS";
            var leave = await _context.LeaveRequests.FindAsync(id);
            if (leave != null)
            {
                _context.LeaveRequests.Remove(leave);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool LeaveRequestExists(int id)
        {
            return _context.LeaveRequests.Any(e => e.Id == id);
        }
    }
}
