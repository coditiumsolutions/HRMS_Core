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
            if (ModelState.IsValid)
            {
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
