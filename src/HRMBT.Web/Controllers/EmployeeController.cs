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
    public async Task<IActionResult> Index(string? department, string? designation, int? year2024, string? applyTax)
    {
        ViewData["Module"] = "Employees";
        
        try
        {
            // Get total count first for debugging
            int totalCount = 0;
            try
            {
                totalCount = await _context.Employees.CountAsync();
            }
            catch (Exception countEx)
            {
                ViewBag.CountError = countEx.Message;
                ViewBag.CountStackTrace = countEx.StackTrace;
            }
            ViewBag.TotalEmployeeCount = totalCount;

            // Start with a simple query to get all employees
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

            if (year2024.HasValue)
            {
                query = query.Where(e => e.Year2024 == year2024.Value);
            }

            // Only filter by ApplyTax if explicitly provided
            // ApplyTax is stored as string "1" or "0" in database
            if (!string.IsNullOrEmpty(applyTax))
            {
                query = query.Where(e => e.ApplyTax == applyTax);
            }

            // Execute the query and get all results
            List<Employee> employees = new List<Employee>();
            try
            {
                employees = await query.ToListAsync();
            }
            catch (Exception queryEx)
            {
                ViewBag.QueryError = queryEx.Message;
                ViewBag.QueryStackTrace = queryEx.StackTrace;
                // Log the full exception details
                ViewBag.QueryInnerException = queryEx.InnerException?.Message;
            }
            ViewBag.FilteredEmployeeCount = employees.Count;
            ViewBag.QueryExecuted = true;

            // Test direct SQL query to verify record count from Employee table (singular)
            var connection = _context.Database.GetDbConnection();
            int sqlCount = 0;
            string dbName = "";
            try
            {
                await connection.OpenAsync();
                dbName = connection.Database;
                using (var cmd = connection.CreateCommand())
                {
                    // Use fully qualified table name with schema
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.Employee";
                    var result = await cmd.ExecuteScalarAsync();
                    sqlCount = Convert.ToInt32(result);
                }
                
                // Also check if Employees table exists and its count
                int employeesTableCount = 0;
                try
                {
                    using (var cmd2 = connection.CreateCommand())
                    {
                        cmd2.CommandText = "SELECT COUNT(*) FROM dbo.Employees";
                        var result2 = await cmd2.ExecuteScalarAsync();
                        employeesTableCount = Convert.ToInt32(result2);
                    }
                }
                catch { }
                ViewBag.EmployeesTableCount = employeesTableCount;
            }
            catch (Exception sqlEx)
            {
                ViewBag.SqlError = sqlEx.Message;
            }
            finally
            {
                await connection.CloseAsync();
            }
            ViewBag.SqlRecordCount = sqlCount;
            ViewBag.DatabaseName = dbName;

            // Get filter options
            ViewBag.Departments = await _context.Employees.Select(e => e.Department).Distinct().Where(d => d != null).OrderBy(d => d).ToListAsync();
            ViewBag.Designations = await _context.Employees.Select(e => e.Designation).Distinct().Where(d => d != null).OrderBy(d => d).ToListAsync();
            
            // Preserve filter values
            ViewBag.CurrentDepartment = department;
            ViewBag.CurrentDesignation = designation;
            ViewBag.CurrentYear2024 = year2024;
            ViewBag.CurrentApplyTax = applyTax;

            return View(employees);
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = ex.Message;
            ViewBag.ErrorStackTrace = ex.StackTrace;
            // Return empty list on error so page still loads
            return View(new List<Employee>());
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
            employee.ModifiedOn = DateTime.Now;
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
                employee.ModifiedOn = DateTime.Now;
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
