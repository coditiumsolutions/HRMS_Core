using HRMBT.Web.Infrastructure;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("setup")]
public class SetupController : Controller
{
    private readonly ApplicationDbContext _context;

    public SetupController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("config")]
    public async Task<IActionResult> Config()
    {
        var rows = await _context.HrConfigurations.AsNoTracking().OrderBy(c => c.ConfigKey).ToListAsync();
        return View(rows);
    }

    [HttpGet("config/new")]
    public IActionResult CreateConfig()
    {
        return View(new ConfigEditVm());
    }

    [HttpPost("config/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateConfig(ConfigEditVm model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var row = new HrConfiguration
        {
            ConfigKey = string.IsNullOrWhiteSpace(model.ConfigKey) ? null : model.ConfigKey.Trim(),
            ConfigValue = string.IsNullOrWhiteSpace(model.ConfigValue) ? null : model.ConfigValue.Trim()
        };
        _context.HrConfigurations.Add(row);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Configuration was created (UID {row.UID}).";
        return RedirectToAction(nameof(Config));
    }

    [HttpGet("config/{id}/edit")]
    public async Task<IActionResult> EditConfig(int id)
    {
        var row = await _context.HrConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.UID == id);
        if (row == null)
            return NotFound();

        var vm = new ConfigEditVm
        {
            UID = row.UID,
            ConfigKey = row.ConfigKey,
            ConfigValue = row.ConfigValue
        };
        return View(vm);
    }

    [HttpPost("config/{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditConfig(int id, ConfigEditVm model)
    {
        if (id != model.UID)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        var row = await _context.HrConfigurations.FindAsync(id);
        if (row == null)
            return NotFound();

        row.ConfigKey = string.IsNullOrWhiteSpace(model.ConfigKey) ? null : model.ConfigKey.Trim();
        row.ConfigValue = string.IsNullOrWhiteSpace(model.ConfigValue) ? null : model.ConfigValue.Trim();
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Configuration UID {row.UID} was updated.";
        return RedirectToAction(nameof(Config));
    }

    [HttpGet("config/{id}/delete")]
    public async Task<IActionResult> DeleteConfig(int id)
    {
        var row = await _context.HrConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.UID == id);
        if (row == null)
            return NotFound();
        return View(row);
    }

    [HttpPost("config/{id}/delete"), ActionName("DeleteConfig")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfigConfirmed(int id)
    {
        var row = await _context.HrConfigurations.FindAsync(id);
        if (row == null)
            return NotFound();

        var uid = row.UID;
        var key = row.ConfigKey ?? "(no key)";
        _context.HrConfigurations.Remove(row);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Configuration UID {uid} ({key}) was deleted.";
        return RedirectToAction(nameof(Config));
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = await _context.AppUsers.AsNoTracking().OrderBy(u => u.Username).ToListAsync(); // roles are auto-included
        return View(users);
    }

    [HttpGet("users/new")]
    public IActionResult CreateUser()
    {
        return View(new SetupUserFormVm());
    }

    [HttpPost("users/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(SetupUserFormVm model)
    {
        ValidatePasswordForCreate(model);
        if (!ModelState.IsValid)
            return View(model);

        if (await _context.AppUsers.AnyAsync(u => u.Username == model.Username))
        {
            ModelState.AddModelError(nameof(model.Username), "This username is already in use.");
            return View(model);
        }

        var actor = User.Identity?.Name ?? "Setup";
        var now = DateTime.Now;

        var (employeeUid, employeeError) = await ResolveEmployeeUidAsync(model.EmployeeId, null);
        if (employeeError != null)
        {
            ModelState.AddModelError(nameof(model.EmployeeId), employeeError);
            return View(model);
        }
        var roleError = ValidateRoles(model.Roles);
        if (roleError != null)
        {
            ModelState.AddModelError(nameof(model.Roles), roleError);
            return View(model);
        }

        var user = new AppUser
        {
            Username = model.Username.Trim(),
            EmployeeUid = employeeUid,
            Roles = model.Roles.Distinct().Select(r => new UserRole { Role = r }).ToList(),
            PasswordHash = LoginPasswordHasher.Hash(model.Password!),
            IsActive = true,
            CreatedBy = actor,
            CreatedOn = now,
            History = null
        };

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"User \"{user.Username}\" was created.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet("users/{id}/edit")]
    public async Task<IActionResult> EditUser(int id)
    {
        var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Uid == id);
        if (user == null)
            return NotFound();

        var vm = new SetupUserFormVm
        {
            Uid = user.Uid,
            Username = user.Username,
            EmployeeId = user.EmployeeId,
            Roles = user.Roles.Select(r => r.Role).ToList()
        };
        return View(vm);
    }

    [HttpPost("users/{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(int id, SetupUserFormVm model)
    {
        if (id != model.Uid)
            return BadRequest();

        var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Uid == id);
        if (user == null)
            return NotFound();

        ValidatePasswordForEdit(model);
        if (!ModelState.IsValid)
            return View(model);

        if (await _context.AppUsers.AnyAsync(u => u.Username == model.Username && u.Uid != id))
        {
            ModelState.AddModelError(nameof(model.Username), "This username is already in use.");
            return View(model);
        }

        var (employeeUid, employeeError) = await ResolveEmployeeUidAsync(model.EmployeeId, id);
        if (employeeError != null)
        {
            ModelState.AddModelError(nameof(model.EmployeeId), employeeError);
            return View(model);
        }
        var roleError = ValidateRoles(model.Roles);
        if (roleError == null && user.IsActive && user.Roles.Any(r => r.Role == AppRoles.Admin) && !model.Roles.Contains(AppRoles.Admin)
            && !await AnotherActiveAdminExistsAsync(id))
            roleError = "At least one active user must keep the Admin role.";
        if (roleError != null)
        {
            ModelState.AddModelError(nameof(model.Roles), roleError);
            return View(model);
        }

        user.Username = model.Username.Trim();
        user.EmployeeUid = employeeUid;
        var wanted = model.Roles.Distinct().ToHashSet();
        foreach (var existing in user.Roles.Where(r => !wanted.Contains(r.Role)).ToList())
            user.Roles.Remove(existing);
        foreach (var role in wanted.Where(w => user.Roles.All(r => r.Role != w)))
            user.Roles.Add(new UserRole { UserId = user.Uid, Role = role });
        user.ModifiedAt = DateTime.Now;
        user.ModifiedBy = User.Identity?.Name ?? "Setup";

        if (!string.IsNullOrEmpty(model.Password))
            user.PasswordHash = LoginPasswordHasher.Hash(model.Password);

        var actor = User.Identity?.Name ?? "Setup";
        var line = $"{Fmt.DateAndTime(DateTime.Now)} updated by {actor}";
        user.History = string.IsNullOrEmpty(user.History) ? line : user.History + Environment.NewLine + line;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"User \"{user.Username}\" was saved.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet("users/{id}/delete")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Uid == id);
        if (user == null)
            return NotFound();
        return View(user);
    }

    [HttpPost("users/{id}/delete"), ActionName("DeleteUser")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserConfirmed(int id)
    {
        var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Uid == id);
        if (user == null)
            return NotFound();

        var name = user.Username;
        if (string.Equals(name, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "You cannot disable the account you are signed in with.";
            return RedirectToAction(nameof(Users));
        }

        if (user.IsActive && user.Roles.Any(r => r.Role == AppRoles.Admin) && !await AnotherActiveAdminExistsAsync(id))
        {
            TempData["ErrorMessage"] = "You cannot disable the last active Admin.";
            return RedirectToAction(nameof(Users));
        }

        // Logins are never deleted (db_standards S6/S14): disabling keeps the history and blocks sign-in.
        user.IsActive = false;
        user.ModifiedAt = DateTime.Now;
        user.ModifiedBy = User.Identity?.Name ?? "Setup";
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"User \"{name}\" was disabled. The account is kept and can no longer sign in.";
        return RedirectToAction(nameof(Users));
    }

    /// <summary>Maps the employee code typed in the form (Employee.EmployeeID) to Employee.uid. Blank = no employee.</summary>
    private async Task<(int? Uid, string? Error)> ResolveEmployeeUidAsync(string? code, int? userId)
    {
        var c = code?.Trim();
        if (string.IsNullOrEmpty(c)) return (null, null);

        var uid = await _context.Employees.AsNoTracking()
            .Where(e => e.EmployeeID == c)
            .Select(e => (int?)e.uid)
            .FirstOrDefaultAsync();
        if (uid == null) return (null, $"No employee with ID \"{c}\".");

        var taken = await _context.AppUsers.AsNoTracking()
            .AnyAsync(u => u.EmployeeUid == uid && (userId == null || u.Uid != userId));
        if (taken) return (null, "This employee already has a login.");
        return (uid, null);
    }

    private static string? ValidateRoles(IEnumerable<string>? roles)
    {
        var bad = (roles ?? Enumerable.Empty<string>()).FirstOrDefault(r => !AppRoles.All.Contains(r));
        return bad == null ? null : $"Unknown role \"{bad}\". Allowed: {string.Join(", ", AppRoles.All)}.";
    }

    private Task<bool> AnotherActiveAdminExistsAsync(int exceptUserId) =>
        _context.AppUsers.AnyAsync(u => u.Uid != exceptUserId && u.IsActive && u.Roles.Any(r => r.Role == AppRoles.Admin));

    private void ValidatePasswordForCreate(SetupUserFormVm model)
    {
        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
            ModelState.AddModelError(nameof(model.Password), "Password is required (minimum 6 characters).");
        if (model.Password != model.ConfirmPassword)
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
    }

    private void ValidatePasswordForEdit(SetupUserFormVm model)
    {
        if (string.IsNullOrEmpty(model.Password) && string.IsNullOrEmpty(model.ConfirmPassword))
            return;
        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
            ModelState.AddModelError(nameof(model.Password), "New password must be at least 6 characters.");
        if (model.Password != model.ConfirmPassword)
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
    }
}
