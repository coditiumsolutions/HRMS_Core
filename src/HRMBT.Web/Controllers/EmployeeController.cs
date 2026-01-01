using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;

namespace HRMBT.Web.Controllers;

public class EmployeeController : Controller
{
    private readonly ApplicationDbContext _context;

    public EmployeeController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Employee
    public async Task<IActionResult> Index()
    {
        ViewData["Module"] = "Employees";
        var employees = await _context.Employees.ToListAsync();
        return View(employees);
    }

    // GET: Employee/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        ViewData["Module"] = "Employees";
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(m => m.uid == id);
        if (employee == null)
        {
            return NotFound();
        }

        return View(employee);
    }

    // GET: Employee/Create
    public IActionResult Create()
    {
        ViewData["Module"] = "Employees";
        return View();
    }

    // POST: Employee/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EmployeeID,EmployeeName,CNIC,Department,Designation,DateOfJoining,BasicSalary,ApplyTax,Status")] Employee employee)
    {
        ViewData["Module"] = "Employees";
        // Validate EmployeeID uniqueness
        if (!string.IsNullOrEmpty(employee.EmployeeID))
        {
            var existingEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeID == employee.EmployeeID);
            if (existingEmployee != null)
            {
                ModelState.AddModelError("EmployeeID", "Employee ID must be unique.");
            }
        }

        if (ModelState.IsValid)
        {
            _context.Add(employee);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(employee);
    }

    // GET: Employee/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        ViewData["Module"] = "Employees";
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees.FindAsync(id);
        if (employee == null)
        {
            return NotFound();
        }
        return View(employee);
    }

    // POST: Employee/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("uid,EmployeeID,EmployeeName,CNIC,Department,Designation,DateOfJoining,BasicSalary,ApplyTax,Status")] Employee employee)
    {
        ViewData["Module"] = "Employees";
        if (id != employee.uid)
        {
            return NotFound();
        }

        // Validate EmployeeID uniqueness (excluding current record)
        if (!string.IsNullOrEmpty(employee.EmployeeID))
        {
            var existingEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeID == employee.EmployeeID && e.uid != employee.uid);
            if (existingEmployee != null)
            {
                ModelState.AddModelError("EmployeeID", "Employee ID must be unique.");
            }
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(employee);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(employee.uid))
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
        return View(employee);
    }

    // GET: Employee/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        ViewData["Module"] = "Employees";
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(m => m.uid == id);
        if (employee == null)
        {
            return NotFound();
        }

        return View(employee);
    }

    // POST: Employee/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        ViewData["Module"] = "Employees";
        var employee = await _context.Employees.FindAsync(id);
        if (employee != null)
        {
            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private bool EmployeeExists(int id)
    {
        return _context.Employees.Any(e => e.uid == id);
    }
}

