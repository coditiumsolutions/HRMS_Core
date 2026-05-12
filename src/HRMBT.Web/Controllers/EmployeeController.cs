using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace HRMBT.Web.Controllers;

public class EmployeeController : Controller
{
    private readonly ApplicationDbContext _context;

    public EmployeeController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Employee
    public async Task<IActionResult> Index(string? department, string? designation, string? employeeName, string? employeeID, string? year2024, string? applyTax, string? sortBy = "EmployeeID", string? sortOrder = "asc", int page = 1, int pageSize = 20)
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

            // Filter by Year2024 (FR-005)
            if (!string.IsNullOrEmpty(year2024))
            {
                if (int.TryParse(year2024, out int yearValue))
                {
                    query = query.Where(e => e.Year2024 == yearValue);
                }
            }

            // Filter by ApplyTax (FR-005)
            if (!string.IsNullOrEmpty(applyTax))
            {
                query = query.Where(e => e.ApplyTax == applyTax);
            }

            // Apply sorting
            query = sortBy?.ToLower() switch
            {
                "employeename" => sortOrder == "desc" ? query.OrderByDescending(e => e.EmployeeName) : query.OrderBy(e => e.EmployeeName),
                "department" => sortOrder == "desc" ? query.OrderByDescending(e => e.Department) : query.OrderBy(e => e.Department),
                "designation" => sortOrder == "desc" ? query.OrderByDescending(e => e.Designation) : query.OrderBy(e => e.Designation),
                "genstatus" => sortOrder == "desc" ? query.OrderByDescending(e => e.GenStatus) : query.OrderBy(e => e.GenStatus),
                "basicSalary" => sortOrder == "desc" ? query.OrderByDescending(e => e.BasicSalary) : query.OrderBy(e => e.BasicSalary),
                "year2024" => sortOrder == "desc" ? query.OrderByDescending(e => e.Year2024) : query.OrderBy(e => e.Year2024),
                "applytax" => sortOrder == "desc" ? query.OrderByDescending(e => e.ApplyTax) : query.OrderBy(e => e.ApplyTax),
                _ => query.OrderBy(e => e.EmployeeID) // Default sort by EmployeeID
            };

            // Get paginated results
            var paginatedEmployees = await PaginatedList<Employee>.CreateAsync(query, page, pageSize);

            // Get filter options from Departments table
            ViewBag.Departments = await _context.Departments
                .Where(d => d.DepartmentName != null)
                .OrderBy(d => d.DepartmentName)
                .ToListAsync();
            ViewBag.Designations = await _context.Employees.Select(e => e.Designation).Distinct().Where(d => d != null).OrderBy(d => d).ToListAsync();
            ViewBag.Year2024Options = await _context.Employees.Select(e => e.Year2024).Distinct().Where(y => y != null).OrderBy(y => y).ToListAsync();
            
            // Preserve filter values
            ViewBag.CurrentDepartment = department;
            ViewBag.CurrentDesignation = designation;
            ViewBag.CurrentEmployeeName = employeeName;
            ViewBag.CurrentEmployeeID = employeeID;
            ViewBag.CurrentYear2024 = year2024;
            ViewBag.CurrentApplyTax = applyTax;
            ViewBag.CurrentPageSize = pageSize;
            ViewBag.CurrentSortBy = sortBy;
            ViewBag.CurrentSortOrder = sortOrder;

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

    // GET: Employee/Dashboard
    public async Task<IActionResult> Dashboard()
    {
        ViewData["Module"] = "Employees";

        var employees = await _context.Employees.AsNoTracking().ToListAsync();

        var byDept = employees
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Department) ? "Unassigned" : e.Department!.Trim())
            .Select(g => new DepartmentEmployeeCount { Department = g.Key, Count = g.Count() })
            .OrderByDescending(d => d.Count)
            .ThenBy(d => d.Department)
            .ToList();

        bool IsActive(string? s) => string.Equals((s ?? string.Empty).Trim(), "Active", StringComparison.OrdinalIgnoreCase);
        bool IsLeave(string? s)
        {
            var t = (s ?? string.Empty).Trim();
            return t.Equals("On Leave", StringComparison.OrdinalIgnoreCase) || t.Equals("Leave", StringComparison.OrdinalIgnoreCase);
        }
        bool IsInactive(string? s)
        {
            var t = (s ?? string.Empty).Trim();
            return t.Equals("Inactive", StringComparison.OrdinalIgnoreCase)
                || t.Equals("Terminated", StringComparison.OrdinalIgnoreCase)
                || t.Equals("Resigned", StringComparison.OrdinalIgnoreCase);
        }

        var vm = new EmployeeDashboardVM
        {
            TotalEmployees = employees.Count,
            ActiveEmployees = employees.Count(e => IsActive(e.EmployeeStatus)),
            InactiveEmployees = employees.Count(e => IsInactive(e.EmployeeStatus)),
            OnLeaveEmployees = employees.Count(e => IsLeave(e.EmployeeStatus)),
            DepartmentCount = byDept.Count,
            ByDepartment = byDept,
            TopDepartment = byDept.FirstOrDefault()
        };

        return View(vm);
    }

    // GET: Employee/ExportExcel
    public async Task<IActionResult> ExportExcel(string? department, string? designation, string? employeeName, string? employeeID, string? year2024, string? applyTax, string? sortBy = "EmployeeID", string? sortOrder = "asc")
    {
        var query = _context.Employees.AsQueryable();

        if (!string.IsNullOrEmpty(department))
            query = query.Where(e => e.Department == department);
        if (!string.IsNullOrEmpty(designation))
            query = query.Where(e => e.Designation == designation);
        if (!string.IsNullOrEmpty(employeeName))
            query = query.Where(e => e.EmployeeName != null && e.EmployeeName.Contains(employeeName));
        if (!string.IsNullOrEmpty(employeeID))
            query = query.Where(e => e.EmployeeID != null && e.EmployeeID.Contains(employeeID));
        if (!string.IsNullOrEmpty(year2024) && int.TryParse(year2024, out int yearValue))
            query = query.Where(e => e.Year2024 == yearValue);
        if (!string.IsNullOrEmpty(applyTax))
            query = query.Where(e => e.ApplyTax == applyTax);

        query = sortBy?.ToLower() switch
        {
            "employeename" => sortOrder == "desc" ? query.OrderByDescending(e => e.EmployeeName) : query.OrderBy(e => e.EmployeeName),
            "department" => sortOrder == "desc" ? query.OrderByDescending(e => e.Department) : query.OrderBy(e => e.Department),
            "designation" => sortOrder == "desc" ? query.OrderByDescending(e => e.Designation) : query.OrderBy(e => e.Designation),
            "basicSalary" => sortOrder == "desc" ? query.OrderByDescending(e => e.BasicSalary) : query.OrderBy(e => e.BasicSalary),
            _ => query.OrderBy(e => e.EmployeeID)
        };

        var employees = await query.ToListAsync();

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Employees");

        string[] headers = { "Employee ID", "Employee Name", "Father Name", "CNIC", "Mobile No", "Department", "Designation", "Project", "Date of Joining", "Status", "Basic Salary", "Apply Tax" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = headers[i];
        }

        using (var headerRange = ws.Cells[1, 1, 1, headers.Length])
        {
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.Color.SetColor(Color.White);
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(ColorTranslator.FromHtml("#00203F"));
            headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            headerRange.Style.Border.Bottom.Color.SetColor(ColorTranslator.FromHtml("#D4AF37"));
        }

        int row = 2;
        foreach (var e in employees)
        {
            ws.Cells[row, 1].Value = e.EmployeeID;
            ws.Cells[row, 2].Value = e.EmployeeName;
            ws.Cells[row, 3].Value = e.FatherName;
            ws.Cells[row, 4].Value = e.CNIC;
            ws.Cells[row, 5].Value = e.MobileNo;
            ws.Cells[row, 6].Value = e.Department;
            ws.Cells[row, 7].Value = e.Designation;
            ws.Cells[row, 8].Value = e.Project;
            ws.Cells[row, 9].Value = e.DateOfJoining?.ToString("yyyy-MM-dd");
            ws.Cells[row, 10].Value = e.EmployeeStatus;
            ws.Cells[row, 11].Value = e.BasicSalary;
            ws.Cells[row, 11].Style.Numberformat.Format = "#,##0.00";
            ws.Cells[row, 12].Value = e.ApplyTax;
            row++;
        }

        ws.Cells[ws.Dimension?.Address ?? "A1"].AutoFitColumns();

        var bytes = package.GetAsByteArray();
        var fileName = $"Employees_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
    public async Task<IActionResult> Create()
    {
        ViewData["Module"] = "Employees";
        await LoadCreateFormOptionsAsync();
        return View();
    }

    // POST: Employee/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EmployeeID,EmployeeName,FatherName,DOB,CNIC,MobileNo,Department,Designation,DateOfJoining,EmployeeStatus,ModifiedBy,ModifiedOn,Details,Project,CarryForwardLeaves,Year2022,Year2023,AdjustedAjusted,Year2024,CarryForwardLeaves1,Year2023New,BasicSalary,ApplyTax,GenStatus")] Employee employee)
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
        await LoadCreateFormOptionsAsync();
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
    public async Task<IActionResult> Edit(int id, [Bind("uid,EmployeeID,EmployeeName,FatherName,DOB,CNIC,MobileNo,Department,Designation,DateOfJoining,EmployeeStatus,ModifiedBy,ModifiedOn,Details,Project,CarryForwardLeaves,Year2022,Year2023,AdjustedAjusted,Year2024,CarryForwardLeaves1,Year2023New,BasicSalary,ApplyTax,GenStatus")] Employee employee)
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

    private async Task LoadCreateFormOptionsAsync()
    {
        var configRows = await _context.HrConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey != null && c.ConfigValue != null)
            .ToListAsync();

        static IEnumerable<string> SplitCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) yield break;
            foreach (var item in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(item))
                    yield return item;
            }
        }

        static bool KeyMatches(string? key, params string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return aliases.Any(a => string.Equals(key.Trim(), a, StringComparison.OrdinalIgnoreCase));
        }

        var departmentOptions = configRows
            .Where(c => KeyMatches(c.ConfigKey, "Department", "Departments"))
            .SelectMany(c => SplitCsv(c.ConfigValue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        var designationOptions = configRows
            .Where(c => KeyMatches(c.ConfigKey, "Designation", "Designations"))
            .SelectMany(c => SplitCsv(c.ConfigValue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        var employeeStatusOptions = configRows
            .Where(c => KeyMatches(c.ConfigKey, "EmployeeStatus"))
            .SelectMany(c => SplitCsv(c.ConfigValue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        var projectOptions = configRows
            .Where(c => KeyMatches(c.ConfigKey, "Project", "Projects"))
            .SelectMany(c => SplitCsv(c.ConfigValue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        ViewBag.DepartmentOptions = departmentOptions;
        ViewBag.DesignationOptions = designationOptions;
        ViewBag.EmployeeStatusOptions = employeeStatusOptions;
        ViewBag.ProjectOptions = projectOptions;
    }

    private bool EmployeeExists(int id)
    {
        return _context.Employees.Any(e => e.uid == id);
    }
}
