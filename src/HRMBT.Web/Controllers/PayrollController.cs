using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Services.Payroll;

namespace HRMBT.Web.Controllers;

public class PayrollController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PayrollCalculationService _payrollService;

    public PayrollController(ApplicationDbContext context, PayrollCalculationService payrollService)
    {
        _context = context;
        _payrollService = payrollService;
    }

    // GET: Payroll
    public async Task<IActionResult> Index()
    {
        ViewData["Module"] = "Payroll";
        var payslips = await _context.Payslips
            .Include(p => p.PayslipDetails)
            .ToListAsync();
        return View(payslips);
    }

    // GET: Payroll/Create
    public IActionResult Create()
    {
        ViewData["Module"] = "Payroll";
        
        // Clear any previous errors
        ModelState.Clear();
        
        // Initialize values to empty/default
        ViewData["EmployeeId"] = string.Empty;
        ViewData["Month"] = DateTime.Now.Month;
        ViewData["Year"] = DateTime.Now.Year;
        
        return View();
    }

    // POST: Payroll/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string employeeId, int? month, int? year)
    {
        ViewData["Module"] = "Payroll";
        
        // Clear ALL model binding errors since we're not using model binding
        // We handle validation manually for all fields
        ModelState.Clear();
        
        // Store values in ViewData to preserve them on error
        ViewData["EmployeeId"] = employeeId ?? string.Empty;
        ViewData["Month"] = month ?? DateTime.Now.Month;
        ViewData["Year"] = year ?? DateTime.Now.Year;
        
        // Validate Employee ID
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            ModelState.AddModelError("EmployeeId", "Employee ID is required.");
            return View();
        }
        
        // Validate month and year
        if (!month.HasValue || month < 1 || month > 12)
        {
            ModelState.AddModelError("Month", "Please select a valid month.");
            return View();
        }
        
        if (!year.HasValue || year < 2000 || year > 2100)
        {
            ModelState.AddModelError("Year", "Please enter a valid year.");
            return View();
        }
        
        int monthValue = month.Value;
        int yearValue = year.Value;
        
        // Look up employee by EmployeeID (string) - case-insensitive
        var trimmedEmployeeId = employeeId.Trim();
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.EmployeeID != null && 
                                     e.EmployeeID.Trim().ToLower() == trimmedEmployeeId.ToLower() && 
                                     e.EmployeeStatus == "Active");
        
        if (employee == null)
        {
            // Check if employee exists but is not active
            var inactiveEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeID != null && 
                                         e.EmployeeID.Trim().ToLower() == trimmedEmployeeId.ToLower());
            
            if (inactiveEmployee != null)
            {
                ModelState.AddModelError("EmployeeId", $"Employee with ID '{employeeId}' exists but is not active.");
            }
            else
            {
                ModelState.AddModelError("EmployeeId", $"Employee with ID '{employeeId}' not found.");
            }
            return View();
        }
        
        try
        {
            // Check if payslip already exists
            var existing = await _context.Payslips
                .FirstOrDefaultAsync(p => p.EmployeeId == employee.uid && 
                                         p.Month == monthValue && 
                                         p.Year == yearValue);
            
            if (existing != null)
            {
                ModelState.AddModelError("", "A payslip already exists for this employee for the selected month and year.");
                return View();
            }
            
            // Use PayrollCalculationService to generate the payslip
            var generatedPayslip = _payrollService.GeneratePayslip(
                employee.uid, 
                monthValue, 
                yearValue, 
                User.Identity?.Name ?? "System"
            );
            
            if (generatedPayslip != null && generatedPayslip.Id > 0)
            {
                TempData["SuccessMessage"] = $"Payslip generated successfully for Employee ID: {employeeId}! Payslip ID: {generatedPayslip.Id}";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                ModelState.AddModelError("", "Payslip was created but could not be retrieved. Please check the payroll list.");
                return View();
            }
        }
        catch (Exception ex)
        {
            // Log the full exception for debugging
            ModelState.AddModelError("", $"Error generating payslip: {ex.Message}");
            if (ex.InnerException != null)
            {
                ModelState.AddModelError("", $"Details: {ex.InnerException.Message}");
            }
            return View();
        }
    }

    // GET: Payroll/Details/5
    public IActionResult Details(int id)
    {
        ViewData["Module"] = "Payroll";
        var payslip = _context.Payslips
            .Include(p => p.PayslipDetails)
            .FirstOrDefault(p => p.Id == id);

        if (payslip == null)
            return NotFound();

        return View(payslip);
    }

    // POST: Payroll/Lock/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Lock(int id)
    {
        ViewData["Module"] = "Payroll";
        var payslip = _context.Payslips.Find(id);
        
        if (payslip == null)
            return NotFound();

        if (payslip.IsLocked)
        {
            TempData["ErrorMessage"] = "Payslip is already locked.";
            return RedirectToAction(nameof(Details), new { id });
        }

        payslip.IsLocked = true;
        payslip.LockedDate = DateTime.Now;
        _context.SaveChanges();

        TempData["SuccessMessage"] = "Payslip has been locked successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: Payroll/Unlock/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Unlock(int id)
    {
        ViewData["Module"] = "Payroll";
        var payslip = _context.Payslips.Find(id);
        
        if (payslip == null)
            return NotFound();

        if (!payslip.IsLocked)
        {
            TempData["ErrorMessage"] = "Payslip is not locked.";
            return RedirectToAction(nameof(Details), new { id });
        }

        payslip.IsLocked = false;
        payslip.LockedDate = null;
        _context.SaveChanges();

        TempData["SuccessMessage"] = "Payslip has been unlocked successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }



// GET: Payroll/GeneratePayslips
public IActionResult GeneratePayslips()
{
    ViewData["Module"] = "Payroll";
    
    // Set default values
    var model = new Payslip
    {
        Year = DateTime.Now.Year,
        Month = DateTime.Now.Month
    };
    
    return View(model);
}

// POST: Payroll/GeneratePayslips
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> GeneratePayslips(IFormCollection form)
{
    ViewData["Module"] = "Payroll";

    // Clear ALL model state
    ModelState.Clear();

    // Get values directly from form
    string monthStr = form["Month"];
    if (string.IsNullOrEmpty(monthStr)) monthStr = form["month"];
    
    string yearStr = form["Year"];
    if (string.IsNullOrEmpty(yearStr)) yearStr = form["year"];

    int? monthValue = null;
    int? yearValue = null;

    if (int.TryParse(monthStr, out int m)) monthValue = m;
    if (int.TryParse(yearStr, out int y)) yearValue = y;

    // Create model for view
    var model = new Payslip
    {
        Month = monthValue ?? DateTime.Now.Month,
        Year = yearValue ?? DateTime.Now.Year
    };

    // Validate
    bool hasErrors = false;
    if (!monthValue.HasValue || monthValue < 1 || monthValue > 12)
    {
        ModelState.AddModelError("Month", "Please select a valid month.");
        hasErrors = true;
    }
    if (!yearValue.HasValue || yearValue < 2000 || yearValue > 2100)
    {
        ModelState.AddModelError("Year", "Please enter a valid year.");
        hasErrors = true;
    }

    if (hasErrors)
    {
        // Add a general error message if both are missing
        if (!monthValue.HasValue && !yearValue.HasValue)
        {
            ModelState.AddModelError("", "Please select both Month and Year.");
        }
        return View(model);
    }
    
    int monthInt = monthValue.Value;
    int yearInt = yearValue.Value;
    
    try
    {
        var employees = await _context.Employees
            .Where(e => e.EmployeeStatus == "Active")
            .ToListAsync();

        if (employees == null || employees.Count == 0)
        {
            ModelState.AddModelError("", "No active employees found. Please ensure there are active employees in the system.");
            return View(model);
        }

        int generatedCount = 0;
        int skippedCount = 0;
        int errorCount = 0;
        var errors = new List<string>();

        foreach (var emp in employees)
        {
            try
            {
                // Check if payslip already exists for this month/year
                var existing = await _context.Payslips
                    .FirstOrDefaultAsync(p => p.EmployeeId == emp.uid && p.Month == monthInt && p.Year == yearInt);

                if (existing == null)
                {
                    // Use PayrollCalculationService to generate the payslip (handles percentage-based calculations)
                    var generatedPayslip = _payrollService.GeneratePayslip(
                        emp.uid,
                        monthInt,
                        yearInt,
                        User.Identity?.Name ?? "System"
                    );
                    
                    if (generatedPayslip != null && generatedPayslip.Id > 0)
                    {
                        generatedCount++;
                    }
                    else
                    {
                        errorCount++;
                        errors.Add($"Failed to generate payslip for Employee ID: {emp.EmployeeID}");
                    }
                }
                else
                {
                    skippedCount++;
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                errors.Add($"Error generating payslip for Employee ID {emp.EmployeeID}: {ex.Message}");
            }
        }

        // Build success message
        var message = $"Payslips generation completed! Generated: {generatedCount}, Skipped (already exists): {skippedCount}";
        if (errorCount > 0)
        {
            message += $", Errors: {errorCount}";
            TempData["ErrorMessage"] = string.Join("; ", errors.Take(5)); // Show first 5 errors
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Index));
    }
    catch (Exception ex)
    {
        ModelState.AddModelError("", $"Error generating payslips: {ex.Message}");
        if (ex.InnerException != null)
        {
            ModelState.AddModelError("", $"Details: {ex.InnerException.Message}");
        }
        return View(model);
    }
}





}
