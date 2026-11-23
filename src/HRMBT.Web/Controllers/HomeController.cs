using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    private const string InvalidLoginMessage = "Invalid username or password.";

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext context,
        IWebHostEnvironment env,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _logger = logger;
        _context = context;
        _env = env;
        _cache = cache;
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

    /// <summary>Shown when a signed-in user lacks the role an area needs (cookie AccessDeniedPath).</summary>
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
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

        // Throttle by client address + username so one attacker cannot lock a known account for everyone.
        var lockKey = $"login:{HttpContext.Connection.RemoteIpAddress}:{name.ToLowerInvariant()}";
        if (_cache.TryGetValue(lockKey, out int failures) && failures >= MaxFailedLogins)
        {
            _logger.LogWarning("Login throttled for {Candidate} from {Ip}", name, HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty, "Too many failed attempts. Try again in 15 minutes.");
            return View(model);
        }

        try
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.Username.ToLower() == name.ToLower() && u.IsActive);

            if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash)
                || !LoginPasswordHasher.Verify(model.Password, user.PasswordHash))
            {
                _cache.Set(lockKey, failures + 1, LockoutWindow);
                _logger.LogWarning("Failed login for {Candidate} from {Ip}", name, HttpContext.Connection.RemoteIpAddress);
                ModelState.AddModelError(string.Empty, InvalidLoginMessage);
                return View(model);
            }

            _cache.Remove(lockKey);

            // Upgrade legacy plain-text / SHA-256 / low-iteration hashes now that we hold the correct password.
            if (LoginPasswordHasher.NeedsRehash(user.PasswordHash))
            {
                user.PasswordHash = LoginPasswordHasher.Hash(model.Password);
                await _context.SaveChangesAsync();
            }

            await SignInAsync(user);
            return RedirectToLocal(returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for username candidate {Candidate}", name);
            ModelState.AddModelError(string.Empty, "Sign-in is temporarily unavailable. Please try again or contact an administrator.");
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

        // One role claim per role: a user can hold several (e.g. HR and Admin).
        foreach (var role in user.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role.Role));

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
    [HttpPost]
    [ValidateAntiForgeryToken]
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
