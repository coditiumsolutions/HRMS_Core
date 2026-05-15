using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Infrastructure;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services;

namespace HRMBT.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext context,
        IWebHostEnvironment env)
    {
        _logger = logger;
        _context = context;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Module"] = "Home";

        try
        {
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
                    using var command = connection.CreateCommand();
                    command.CommandText = $"SELECT COUNT(*) FROM [{table}]";
                    var count = await command.ExecuteScalarAsync();
                    results.Add($"Table [{table}] count: {count}");
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

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);

        ViewData["Module"] = "Home";
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl)
    {
        ViewData["Module"] = "Home";
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var name = model.Username.Trim();
        if (string.IsNullOrEmpty(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
            return View(model);
        }

        try
        {
            var user = await _context.AppUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username.ToLower() == name.ToLower());

            if (user == null)
            {
                ModelState.AddModelError(string.Empty,
                    $"No row in Users for username \"{name}\". Either the name does not exist in dbo.Users, or it does not match when compared case-insensitive. Add or fix the row under Setup → Users, or check the database your app connects to.");
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty,
                    $"User \"{user.Username}\" was found, but PasswordHash is empty or whitespace. Edit this user in Setup → Users and set a password, or UPDATE dbo.Users SET PasswordHash = ... ");
                return View(model);
            }

            if (!LoginPasswordHasher.Verify(model.Password, user.PasswordHash))
            {
                var hint = LoginPasswordHasher.DescribeStoredFormat(user.PasswordHash);
                ModelState.AddModelError(string.Empty,
                    $"Password was rejected for user \"{user.Username}\". {hint}");
                return View(model);
            }

            await SignInAsync(user);
            return RedirectToLocal(returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for username candidate {Candidate}", name);
            var root = ex.GetBaseException().Message;
            var hint = _env.IsDevelopment()
                ? $" Root cause: {root}"
                : " Check server logs for the full exception.";
            ModelState.AddModelError(string.Empty,
                $"Sign-in could not talk to the database or complete processing: {ex.Message}.{hint} Confirm SQL Server is reachable and table dbo.Users exists for this connection.");
            return View(model);
        }
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    private async Task SignInAsync(AppUser user)
    {
        var loggedInAt = DateTime.Now;
        var loginAtStr = loggedInAt.ToString("dd MMM yyyy HH:mm", CultureInfo.CurrentCulture);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(AuthClaims.DisplayName, user.Username.Trim()),
            new Claim(AuthClaims.LoginAt, loginAtStr),
        };

        if (!string.IsNullOrWhiteSpace(user.Role))
            claims.Add(new Claim(ClaimTypes.Role, user.Role));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true,
                IsPersistent = true,
            });
    }

    /// <summary>Development-only: open a DB connection and report success or the real SqlClient error.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> DbHealth()
    {
        if (!_env.IsDevelopment())
            return NotFound();

        try
        {
            var ok = await _context.Database.CanConnectAsync();
            return Content(ok ? "Database: CanConnect OK." : "Database: CanConnect returned false.", "text/plain");
        }
        catch (Exception ex)
        {
            var root = ex.GetBaseException();
            return Content(
                $"CanConnect failed.\n\n{root.GetType().Name}: {root.Message}",
                "text/plain");
        }
    }

    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["Module"] = "Home";
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
