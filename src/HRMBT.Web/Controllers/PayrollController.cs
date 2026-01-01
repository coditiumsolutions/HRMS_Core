using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;

namespace HRMBT.Web.Controllers;

public class PayrollController : Controller
{
    private readonly ApplicationDbContext _context;

    public PayrollController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Payroll
    public async Task<IActionResult> Index()
    {
        ViewData["Module"] = "Payroll";
        var payrolls = await _context.Payrolls.ToListAsync();
        return View(payrolls);
    }

    // GET: Payroll/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        ViewData["Module"] = "Payroll";
        if (id == null)
        {
            return NotFound();
        }

        var payroll = await _context.Payrolls
            .FirstOrDefaultAsync(m => m.Id == id);
        if (payroll == null)
        {
            return NotFound();
        }

        return View(payroll);
    }

    // GET: Payroll/Create
    public IActionResult Create()
    {
        ViewData["Module"] = "Payroll";
        return View();
    }

    // POST: Payroll/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EmployeeId,BasicSalary,Month,Year")] Payroll payroll)
    {
        ViewData["Module"] = "Payroll";
        if (ModelState.IsValid)
        {
            _context.Add(payroll);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(payroll);
    }

    // GET: Payroll/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        ViewData["Module"] = "Payroll";
        if (id == null)
        {
            return NotFound();
        }

        var payroll = await _context.Payrolls.FindAsync(id);
        if (payroll == null)
        {
            return NotFound();
        }
        return View(payroll);
    }

    // POST: Payroll/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeId,BasicSalary,Month,Year")] Payroll payroll)
    {
        ViewData["Module"] = "Payroll";
        if (id != payroll.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(payroll);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PayrollExists(payroll.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(payroll);
    }

    // GET: Payroll/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        ViewData["Module"] = "Payroll";
        if (id == null)
        {
            return NotFound();
        }

        var payroll = await _context.Payrolls
            .FirstOrDefaultAsync(m => m.Id == id);
        if (payroll == null)
        {
            return NotFound();
        }

        return View(payroll);
    }

    // POST: Payroll/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        ViewData["Module"] = "Payroll";
        var payroll = await _context.Payrolls.FindAsync(id);
        if (payroll != null)
        {
            _context.Payrolls.Remove(payroll);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private bool PayrollExists(int id)
    {
        return _context.Payrolls.Any(e => e.Id == id);
    }
}

