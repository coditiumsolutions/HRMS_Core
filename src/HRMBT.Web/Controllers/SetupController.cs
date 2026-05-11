using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers;

[AllowAnonymous]
public class SetupController : Controller
{
    private readonly ApplicationDbContext _context;

    public SetupController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Config()
    {
        var rows = await _context.HrConfigurations.AsNoTracking().OrderBy(c => c.ConfigKey).ToListAsync();
        return View(rows);
    }

    [HttpGet]
    public IActionResult CreateConfig()
    {
        return View(new ConfigEditVm());
    }

    [HttpPost]
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

    [HttpGet]
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

    [HttpPost]
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

    [HttpGet]
    public async Task<IActionResult> DeleteConfig(int id)
    {
        var row = await _context.HrConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.UID == id);
        if (row == null)
            return NotFound();
        return View(row);
    }

    [HttpPost, ActionName("DeleteConfig")]
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

    public async Task<IActionResult> Users()
    {
        var users = await _context.AppUsers.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
        return View(users);
    }

    [HttpGet]
    public IActionResult CreateUser()
    {
        return View(new SetupUserFormVm());
    }

    [HttpPost]
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
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        var user = new AppUser
        {
            Username = model.Username.Trim(),
            EmployeeId = string.IsNullOrWhiteSpace(model.EmployeeId) ? null : model.EmployeeId.Trim(),
            Role = string.IsNullOrWhiteSpace(model.Role) ? null : model.Role.Trim(),
            PasswordHash = LoginPasswordHasher.Hash(model.Password!),
            CreatedBy = actor,
            CreatedOn = now,
            History = null
        };

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"User \"{user.Username}\" was created.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(int id)
    {
        var user = await _context.AppUsers.FindAsync(id);
        if (user == null)
            return NotFound();

        var vm = new SetupUserFormVm
        {
            Uid = user.Uid,
            Username = user.Username,
            EmployeeId = user.EmployeeId,
            Role = user.Role
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(int id, SetupUserFormVm model)
    {
        if (id != model.Uid)
            return BadRequest();

        var user = await _context.AppUsers.FindAsync(id);
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

        user.Username = model.Username.Trim();
        user.EmployeeId = string.IsNullOrWhiteSpace(model.EmployeeId) ? null : model.EmployeeId.Trim();
        user.Role = string.IsNullOrWhiteSpace(model.Role) ? null : model.Role.Trim();

        if (!string.IsNullOrEmpty(model.Password))
            user.PasswordHash = LoginPasswordHasher.Hash(model.Password);

        var actor = User.Identity?.Name ?? "Setup";
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm} updated by {actor}";
        user.History = string.IsNullOrEmpty(user.History) ? line : user.History + Environment.NewLine + line;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"User \"{user.Username}\" was saved.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Uid == id);
        if (user == null)
            return NotFound();
        return View(user);
    }

    [HttpPost, ActionName("DeleteUser")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserConfirmed(int id)
    {
        var user = await _context.AppUsers.FindAsync(id);
        if (user == null)
            return NotFound();

        var name = user.Username;
        _context.AppUsers.Remove(user);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"User \"{name}\" was deleted.";
        return RedirectToAction(nameof(Users));
    }

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
