using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;

namespace HRMBT.Web.Controllers;

public class AttendanceController : Controller
{
    private readonly ApplicationDbContext _context;

    public AttendanceController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Attendance
    public async Task<IActionResult> Index()
    {
        ViewData["Module"] = "Attendance";
        var attendances = await _context.Attendances.ToListAsync();
        return View(attendances);
    }

    // GET: Attendance/Create
    public IActionResult Create()
    {
        ViewData["Module"] = "Attendance";
        return View();
    }

    // POST: Attendance/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EmployeeId,Date,Status,Department")] Attendance attendance)
    {
        ViewData["Module"] = "Attendance";
        if (ModelState.IsValid)
        {
            _context.Add(attendance);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(attendance);
    }
}

