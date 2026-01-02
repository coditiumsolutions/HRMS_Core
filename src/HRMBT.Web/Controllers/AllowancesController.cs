using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;

namespace HRMBT.Web.Controllers;

public class AllowancesController : Controller
{
    private readonly ApplicationDbContext _context;

    public AllowancesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Allowances
    public async Task<IActionResult> Index(int? employeeId)
    {
        ViewData["Module"] = "Payroll";
        var query = _context.Allowances.AsQueryable();

        if (employeeId.HasValue)
        {
            query = query.Where(a => a.EmployeeId == employeeId.Value);
        }

        var allowances = await query
            .OrderBy(a => a.EmployeeId)
            .ThenBy(a => a.Name)
            .ToListAsync();

        // Populate employee dropdown
        ViewData["EmployeeId"] = new SelectList(
            await _context.Employees
                .Where(e => e.EmployeeStatus == "Active")
                .OrderBy(e => e.EmployeeName)
                .ToListAsync(),
            "uid",
            "EmployeeName",
            employeeId);

        return View(allowances);
    }

    // GET: Allowances/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        ViewData["Module"] = "Payroll";
        if (id == null) return NotFound();

        var allowance = await _context.Allowances
            .FirstOrDefaultAsync(m => m.Id == id);
        
        if (allowance == null) return NotFound();

        return View(allowance);
    }

    // GET: Allowances/Create
    public async Task<IActionResult> Create()
    {
        ViewData["Module"] = "Payroll";
        ViewData["EmployeeId"] = new SelectList(
            await _context.Employees
                .Where(e => e.EmployeeStatus == "Active")
                .OrderBy(e => e.EmployeeName)
                .ToListAsync(),
            "uid",
            "EmployeeName");
        
        ViewData["AllowanceType"] = new SelectList(new[]
        {
            "Transport",
            "Medical",
            "Housing",
            "Food",
            "Other"
        });

        return View();
    }

    // POST: Allowances/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EmployeeId,AllowanceType,Name,Amount,IsPercentage,PercentageValue,EffectiveDate,EndDate,IsActive")] Allowance allowance)
    {
        ViewData["Module"] = "Payroll";
        
        if (ModelState.IsValid)
        {
            allowance.CreatedDate = DateTime.Now;
            allowance.CreatedBy = User.Identity?.Name ?? "System";
            _context.Add(allowance);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        
        ViewData["EmployeeId"] = new SelectList(
            await _context.Employees
                .Where(e => e.EmployeeStatus == "Active")
                .OrderBy(e => e.EmployeeName)
                .ToListAsync(),
            "uid",
            "EmployeeName",
            allowance.EmployeeId);
        
        ViewData["AllowanceType"] = new SelectList(new[]
        {
            "Transport",
            "Medical",
            "Housing",
            "Food",
            "Other"
        }, allowance.AllowanceType);

        return View(allowance);
    }

    // GET: Allowances/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        ViewData["Module"] = "Payroll";
        if (id == null) return NotFound();

        var allowance = await _context.Allowances.FindAsync(id);
        if (allowance == null) return NotFound();

        ViewData["EmployeeId"] = new SelectList(
            await _context.Employees
                .Where(e => e.EmployeeStatus == "Active")
                .OrderBy(e => e.EmployeeName)
                .ToListAsync(),
            "uid",
            "EmployeeName",
            allowance.EmployeeId);
        
        ViewData["AllowanceType"] = new SelectList(new[]
        {
            "Transport",
            "Medical",
            "Housing",
            "Food",
            "Other"
        }, allowance.AllowanceType);

        return View(allowance);
    }

    // POST: Allowances/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeId,AllowanceType,Name,Amount,IsPercentage,PercentageValue,EffectiveDate,EndDate,IsActive,CreatedDate,CreatedBy")] Allowance allowance)
    {
        ViewData["Module"] = "Payroll";
        if (id != allowance.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                allowance.ModifiedDate = DateTime.Now;
                allowance.ModifiedBy = User.Identity?.Name ?? "System";
                _context.Update(allowance);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AllowanceExists(allowance.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        
        ViewData["EmployeeId"] = new SelectList(
            await _context.Employees
                .Where(e => e.EmployeeStatus == "Active")
                .OrderBy(e => e.EmployeeName)
                .ToListAsync(),
            "uid",
            "EmployeeName",
            allowance.EmployeeId);
        
        ViewData["AllowanceType"] = new SelectList(new[]
        {
            "Transport",
            "Medical",
            "Housing",
            "Food",
            "Other"
        }, allowance.AllowanceType);

        return View(allowance);
    }

    // GET: Allowances/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        ViewData["Module"] = "Payroll";
        if (id == null) return NotFound();

        var allowance = await _context.Allowances
            .FirstOrDefaultAsync(m => m.Id == id);
        
        if (allowance == null) return NotFound();

        return View(allowance);
    }

    // POST: Allowances/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        ViewData["Module"] = "Payroll";
        var allowance = await _context.Allowances.FindAsync(id);
        if (allowance != null)
        {
            _context.Allowances.Remove(allowance);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool AllowanceExists(int id)
    {
        return _context.Allowances.Any(e => e.Id == id);
    }
}

