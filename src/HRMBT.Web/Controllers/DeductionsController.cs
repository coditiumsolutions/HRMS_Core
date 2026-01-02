using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;

namespace HRMBT.Web.Controllers;

public class DeductionsController : Controller
{
    private readonly ApplicationDbContext _context;

    public DeductionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Deductions
    public async Task<IActionResult> Index(int? employeeId)
    {
        ViewData["Module"] = "Payroll";
        var query = _context.Deductions.AsQueryable();

        if (employeeId.HasValue)
        {
            query = query.Where(d => d.EmployeeId == employeeId.Value);
        }

        var deductions = await query
            .OrderBy(d => d.EmployeeId)
            .ThenBy(d => d.Name)
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

        return View(deductions);
    }

    // GET: Deductions/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        ViewData["Module"] = "Payroll";
        if (id == null) return NotFound();

        var deduction = await _context.Deductions
            .FirstOrDefaultAsync(m => m.Id == id);
        
        if (deduction == null) return NotFound();

        return View(deduction);
    }

    // GET: Deductions/Create
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
        
        ViewData["DeductionType"] = new SelectList(new[]
        {
            "Tax",
            "Provident Fund",
            "Loan",
            "Insurance",
            "Other"
        });

        ViewData["CalculationMethod"] = new SelectList(new[]
        {
            "Fixed",
            "Percentage"
        });

        return View();
    }

    // POST: Deductions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EmployeeId,DeductionType,Name,Amount,CalculationMethod,PercentageValue,IsMandatory,EffectiveDate,EndDate,IsActive")] Deduction deduction)
    {
        ViewData["Module"] = "Payroll";
        
        if (ModelState.IsValid)
        {
            deduction.CreatedDate = DateTime.Now;
            deduction.CreatedBy = User.Identity?.Name ?? "System";
            _context.Add(deduction);
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
            deduction.EmployeeId);
        
        ViewData["DeductionType"] = new SelectList(new[]
        {
            "Tax",
            "Provident Fund",
            "Loan",
            "Insurance",
            "Other"
        }, deduction.DeductionType);

        ViewData["CalculationMethod"] = new SelectList(new[]
        {
            "Fixed",
            "Percentage"
        }, deduction.CalculationMethod);

        return View(deduction);
    }

    // GET: Deductions/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        ViewData["Module"] = "Payroll";
        if (id == null) return NotFound();

        var deduction = await _context.Deductions.FindAsync(id);
        if (deduction == null) return NotFound();

        ViewData["EmployeeId"] = new SelectList(
            await _context.Employees
                .Where(e => e.EmployeeStatus == "Active")
                .OrderBy(e => e.EmployeeName)
                .ToListAsync(),
            "uid",
            "EmployeeName",
            deduction.EmployeeId);
        
        ViewData["DeductionType"] = new SelectList(new[]
        {
            "Tax",
            "Provident Fund",
            "Loan",
            "Insurance",
            "Other"
        }, deduction.DeductionType);

        ViewData["CalculationMethod"] = new SelectList(new[]
        {
            "Fixed",
            "Percentage"
        }, deduction.CalculationMethod);

        return View(deduction);
    }

    // POST: Deductions/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeId,DeductionType,Name,Amount,CalculationMethod,PercentageValue,IsMandatory,EffectiveDate,EndDate,IsActive,CreatedDate,CreatedBy")] Deduction deduction)
    {
        ViewData["Module"] = "Payroll";
        if (id != deduction.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                deduction.ModifiedDate = DateTime.Now;
                deduction.ModifiedBy = User.Identity?.Name ?? "System";
                _context.Update(deduction);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DeductionExists(deduction.Id))
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
            deduction.EmployeeId);
        
        ViewData["DeductionType"] = new SelectList(new[]
        {
            "Tax",
            "Provident Fund",
            "Loan",
            "Insurance",
            "Other"
        }, deduction.DeductionType);

        ViewData["CalculationMethod"] = new SelectList(new[]
        {
            "Fixed",
            "Percentage"
        }, deduction.CalculationMethod);

        return View(deduction);
    }

    // GET: Deductions/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        ViewData["Module"] = "Payroll";
        if (id == null) return NotFound();

        var deduction = await _context.Deductions
            .FirstOrDefaultAsync(m => m.Id == id);
        
        if (deduction == null) return NotFound();

        return View(deduction);
    }

    // POST: Deductions/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        ViewData["Module"] = "Payroll";
        var deduction = await _context.Deductions.FindAsync(id);
        if (deduction != null)
        {
            _context.Deductions.Remove(deduction);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool DeductionExists(int id)
    {
        return _context.Deductions.Any(e => e.Id == id);
    }
}

