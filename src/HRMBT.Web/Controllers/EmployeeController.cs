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
    public async Task<IActionResult> Index(string? department, string? designation, string? employeeName, string? employeeID, int page = 1, int pageSize = 20)
    {
        ViewData["Module"] = "Employees";
        
        try
        {
            // Ensure valid pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100; // Max page size limit

            // Start with a query
            var query = _context.Employees.AsQueryable();

            // Apply filters only if they have values
            if (!string.IsNullOrEmpty(department))
            {
                query = query.Where(e => e.Department == department);
            }

            if (!string.IsNullOrEmpty(designation))
            {
                query = query.Where(e => e.Designation == designation);
            }

            // Filter by Employee Name (partial match)
            if (!string.IsNullOrEmpty(employeeName))
            {
                query = query.Where(e => e.EmployeeName != null && e.EmployeeName.Contains(employeeName));
            }

            // Filter by Employee ID (partial match)
            if (!string.IsNullOrEmpty(employeeID))
            {
                query = query.Where(e => e.EmployeeID != null && e.EmployeeID.Contains(employeeID));
            }

            // Order by EmployeeID for consistent pagination
            query = query.OrderBy(e => e.EmployeeID);

            // Get paginated results
            var paginatedEmployees = await PaginatedList<Employee>.CreateAsync(query, page, pageSize);

            // Get filter options
            ViewBag.Departments = await _context.Employees.Select(e => e.Department).Distinct().Where(d => d != null).OrderBy(d => d).ToListAsync();
            ViewBag.Designations = await _context.Employees.Select(e => e.Designation).Distinct().Where(d => d != null).OrderBy(d => d).ToListAsync();
            
            // Preserve filter values
            ViewBag.CurrentDepartment = department;
            ViewBag.CurrentDesignation = designation;
            ViewBag.CurrentEmployeeName = employeeName;
            ViewBag.CurrentEmployeeID = employeeID;
            ViewBag.CurrentPageSize = pageSize;

            // Page size options
            ViewBag.PageSizeOptions = new List<int> { 10, 20, 50, 100 };

            return View(paginatedEmployees);
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = ex.Message;
            ViewBag.ErrorStackTrace = ex.StackTrace;
            // Return empty paginated list on error
            return View(new PaginatedList<Employee>(new List<Employee>(), 0, 1, pageSize));
        }
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
    public async Task<IActionResult> Create([Bind("EmployeeID,EmployeeName,FatherName,DOB,CNIC,MobileNo,Department,Designation,DateOfJoining,EmployeeStatus,ModifiedBy,ModifiedOn,Details,Project,CarryForwardLeaves,Year2022,Year2023,AdjustedAjusted,Year2024,CarryForwardLeaves1,Year2023New,BasicSalary,ApplyTax")] Employee employee)
    {
        ViewData["Module"] = "Employees";
        
        // FR-007: Validate EmployeeID uniqueness
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
            employee.ModifiedOn = DateTime.Now.ToString();
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
    public async Task<IActionResult> Edit(int id, [Bind("uid,EmployeeID,EmployeeName,FatherName,DOB,CNIC,MobileNo,Department,Designation,DateOfJoining,EmployeeStatus,ModifiedBy,ModifiedOn,Details,Project,CarryForwardLeaves,Year2022,Year2023,AdjustedAjusted,Year2024,CarryForwardLeaves1,Year2023New,BasicSalary,ApplyTax")] Employee employee)
    {
        ViewData["Module"] = "Employees";
        if (id != employee.uid)
        {
            return NotFound();
        }

        // FR-007: Validate EmployeeID uniqueness (excluding current record)
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
                employee.ModifiedOn = DateTime.Now.ToString();
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
