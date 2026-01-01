using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Models;
using HRMBT.Web.Data;

namespace HRMBT.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Module"] = "Home";

        // Dashboard Statistics
        var totalEmployees = await _context.Employees.CountAsync();
        var activeEmployees = await _context.Employees.CountAsync(e => e.Status == "Active");
        var todayAttendance = await _context.Attendances.CountAsync(a => a.Date.Date == DateTime.Today);
        var pendingLeaves = await _context.LeaveRequests.CountAsync(l => l.Status == "Pending");
        var totalPayrolls = await _context.Payrolls.CountAsync();
        var totalTaxRules = await _context.TaxRules.CountAsync();

        ViewData["TotalEmployees"] = totalEmployees;
        ViewData["ActiveEmployees"] = activeEmployees;
        ViewData["TodayAttendance"] = todayAttendance;
        ViewData["PendingLeaves"] = pendingLeaves;
        ViewData["TotalPayrolls"] = totalPayrolls;
        ViewData["TotalTaxRules"] = totalTaxRules;

        return View();
    }

    public IActionResult Privacy()
    {
        ViewData["Module"] = "Home";
        return View();
    }

    public IActionResult Login()
    {
        ViewData["Module"] = "Home";
        return View();
    }

    public IActionResult Logout()
    {
        ViewData["Module"] = "Home";
        // TODO: Implement logout logic (clear session, sign out, etc.)
        return RedirectToAction("Index");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["Module"] = "Home";
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
