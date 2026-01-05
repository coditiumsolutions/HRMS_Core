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

        try
        {
            // Dashboard Statistics
            var totalEmployees = await _context.Employees.CountAsync();
            var activeEmployees = await _context.Employees.CountAsync(e => e.EmployeeStatus == "Active");
            var todayAttendance = await _context.Attendances.CountAsync(a => a.AttendanceDate.Date == DateTime.Today);
            var pendingLeaves = await _context.LeaveRequests.CountAsync(l => l.Status == "Pending");
            var totalPayrolls = await _context.Payslips.CountAsync();
            var totalTaxRules = await _context.TaxRules.CountAsync();

            ViewData["TotalEmployees"] = totalEmployees;
            ViewData["ActiveEmployees"] = activeEmployees;
            ViewData["TodayAttendance"] = todayAttendance;
            ViewData["PendingLeaves"] = pendingLeaves;
            ViewData["TotalPayrolls"] = totalPayrolls;
            ViewData["TotalTaxRules"] = totalTaxRules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard statistics");
            // Set default values if database connection fails
            ViewData["TotalEmployees"] = 0;
            ViewData["ActiveEmployees"] = 0;
            ViewData["TodayAttendance"] = 0;
            ViewData["PendingLeaves"] = 0;
            ViewData["TotalPayrolls"] = 0;
            ViewData["TotalTaxRules"] = 0;
            ViewData["DatabaseError"] = "Unable to connect to database. Please check your connection settings.";
        }

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Test()
    {
        var results = new List<string>();
        var connection = _context.Database.GetDbConnection();
        try 
        {
            await connection.OpenAsync();
            results.Add($"Connected to: {connection.Database}");
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
                using (var reader = await command.ExecuteReaderAsync())
                {
                    results.Add("Tables found:");
                    while (await reader.ReadAsync())
                    {
                        results.Add("- " + reader.GetString(0));
                    }
                }
            }
            string[] tablesToCheck = { "Employee", "Employees", "Payroll", "Payrolls" };
            foreach (var table in tablesToCheck)
            {
                try 
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $"SELECT COUNT(*) FROM [{table}]";
                        var count = await command.ExecuteScalarAsync();
                        results.Add($"Table [{table}] count: {count}");
                    }
                }
                catch { }
            }
        }
        catch (System.Exception ex) { results.Add($"Error: {ex.Message}"); }
        finally { await connection.CloseAsync(); }
        return Ok(results);
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
