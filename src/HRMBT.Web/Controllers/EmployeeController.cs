using HRMBT.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace HRMBT.Web.Controllers;

public class EmployeeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IEmployeeDocumentService _documentService;

    public EmployeeController(ApplicationDbContext context, IEmployeeDocumentService documentService)
    {
        _context = context;
        _documentService = documentService;
    }

    // GET: Employee
    public async Task<IActionResult> Index(string? department, string? designation, string? project, string? search, string? employeeName, string? employeeID, string? year2024, string? applyTax, string? sortBy = "EmployeeID", string? sortOrder = "asc", int page = 1, int pageSize = 20)
    {
        ViewData["Module"] = "Employees";
        
        try
        {
            // Ensure valid pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100; // Max page size limit

            // Combined name/ID search (legacy employeeName / employeeID still accepted)
            var searchTerm = (search ?? employeeName ?? employeeID)?.Trim();
            var projectFilter = string.IsNullOrWhiteSpace(project) ? null : project.Trim();

            // Start with a query
            var query = _context.Employees.AsQueryable();

            // Apply filters only if they have values
            if (!string.IsNullOrEmpty(department))
            {
                query = query.Where(e => e.DepartmentInfo!.DepartmentName == department);
            }

            if (!string.IsNullOrEmpty(designation))
            {
                query = query.Where(e => e.DesignationInfo!.Name == designation);
            }

            if (!string.IsNullOrEmpty(projectFilter))
            {
                query = query.Where(e => e.ProjectInfo!.Name == projectFilter);
            }

            // Single search matches Employee Name OR Employee ID (partial)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(e =>
                    (e.EmployeeName != null && e.EmployeeName.Contains(searchTerm)) ||
                    (e.EmployeeID != null && e.EmployeeID.Contains(searchTerm)));
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
                "department" => sortOrder == "desc" ? query.OrderByDescending(e => e.DepartmentInfo!.DepartmentName) : query.OrderBy(e => e.DepartmentInfo!.DepartmentName),
                "designation" => sortOrder == "desc" ? query.OrderByDescending(e => e.DesignationInfo!.Name) : query.OrderBy(e => e.DesignationInfo!.Name),
                "project" => sortOrder == "desc" ? query.OrderByDescending(e => e.ProjectInfo!.Name) : query.OrderBy(e => e.ProjectInfo!.Name),
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
            ViewBag.Designations = await _context.Designations.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Rank).ThenBy(d => d.Name).Select(d => d.Name).ToListAsync();
            ViewBag.Projects = await _context.Projects.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ThenBy(p => p.Name).Select(p => p.Name).ToListAsync();
            ViewBag.Year2024Options = await _context.Employees.Select(e => e.Year2024).Distinct().Where(y => y != null).OrderBy(y => y).ToListAsync();
            
            // Preserve filter values
            ViewBag.CurrentDepartment = department;
            ViewBag.CurrentDesignation = designation;
            ViewBag.CurrentProject = projectFilter;
            ViewBag.CurrentSearch = searchTerm;
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
    public async Task<IActionResult> ExportExcel(string? department, string? designation, string? search, string? employeeName, string? employeeID, string? year2024, string? applyTax, string? sortBy = "EmployeeID", string? sortOrder = "asc")
    {
        var query = _context.Employees.AsQueryable();
        var searchTerm = (search ?? employeeName ?? employeeID)?.Trim();

        if (!string.IsNullOrEmpty(department))
            query = query.Where(e => e.DepartmentInfo!.DepartmentName == department);
        if (!string.IsNullOrEmpty(designation))
            query = query.Where(e => e.DesignationInfo!.Name == designation);
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(e =>
                (e.EmployeeName != null && e.EmployeeName.Contains(searchTerm)) ||
                (e.EmployeeID != null && e.EmployeeID.Contains(searchTerm)));
        }
        if (!string.IsNullOrEmpty(year2024) && int.TryParse(year2024, out int yearValue))
            query = query.Where(e => e.Year2024 == yearValue);
        if (!string.IsNullOrEmpty(applyTax))
            query = query.Where(e => e.ApplyTax == applyTax);

        query = sortBy?.ToLower() switch
        {
            "employeename" => sortOrder == "desc" ? query.OrderByDescending(e => e.EmployeeName) : query.OrderBy(e => e.EmployeeName),
            "department" => sortOrder == "desc" ? query.OrderByDescending(e => e.DepartmentInfo!.DepartmentName) : query.OrderBy(e => e.DepartmentInfo!.DepartmentName),
            "designation" => sortOrder == "desc" ? query.OrderByDescending(e => e.DesignationInfo!.Name) : query.OrderBy(e => e.DesignationInfo!.Name),
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
            ws.Cells[row, 9].Value = Fmt.ShortDate(e.DateOfJoining);
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

        ViewBag.EmployeeDocuments = await _documentService.BuildViewModelAsync(employee.uid, canUpload: false);
        ViewBag.EmployeeAllowances = await _context.Allowances
            .AsNoTracking()
            .Where(a => a.EmployeeId == employee.uid)
            .OrderByDescending(a => a.IsActive)
            .ThenBy(a => a.AllowanceType)
            .ThenBy(a => a.Name)
            .ToListAsync();
        ViewBag.EmployeeDeductions = await _context.Deductions
            .AsNoTracking()
            .Where(d => d.EmployeeId == employee.uid)
            .OrderByDescending(d => d.IsActive)
            .ThenBy(d => d.DeductionType)
            .ThenBy(d => d.DeductionName)
            .ToListAsync();
        return View(employee);
    }

    // GET: Employee/Create
    public async Task<IActionResult> Create()
    {
        ViewData["Module"] = "Employees";
        await LoadCreateFormOptionsAsync();
        return View(new Employee { EmployeeStatus = "Active", ApplyTax = "Yes", PayBasis = "Monthly" });
    }

    // POST: Employee/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EmployeeID,EmployeeName,FatherName,DOB,CNIC,MobileNo,ProjectId,DepartmentId,SubDepartmentId,DesignationId,DateOfJoining,DateOfLeaving,DateOfReturn,EmployeeStatus,ModifiedBy,Details,Notes,CarryForwardLeaves,Year2022,Year2023,AdjustedAjusted,Year2024,CarryForwardLeaves1,Year2023New,BasicSalary,ApplyTax,GenStatus")] Employee employee)
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

        await ValidateOrganisationAsync(employee);

        if (ModelState.IsValid)
        {
            employee.ModifiedOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            employee.CreatedBy = User.Identity?.Name;
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

        await LoadCreateFormOptionsAsync(employee);
        ViewBag.EmployeeDocuments = await _documentService.BuildViewModelAsync(employee.uid, canUpload: true);
        return View(employee);
    }

    // POST: Employee/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("RowVersion,uid,EmployeeID,EmployeeName,FatherName,DOB,CNIC,MobileNo,ProjectId,DepartmentId,SubDepartmentId,DesignationId,DateOfJoining,DateOfLeaving,DateOfReturn,EmployeeStatus,ModifiedBy,Details,Notes,CarryForwardLeaves,Year2022,Year2023,AdjustedAjusted,Year2024,CarryForwardLeaves1,Year2023New,BasicSalary,ApplyTax,GenStatus")] Employee employee)
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

        await ValidateOrganisationAsync(employee);

        if (ModelState.IsValid)
        {
            try
            {
                employee.ModifiedOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                _context.Attach(employee);
                foreach (var name in EditablePropertyNames)
                    _context.Entry(employee).Property(name).IsModified = true;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(employee.uid))
                    return NotFound();

                // S15: someone else saved this employee after the form was opened
                ModelState.AddModelError(string.Empty, "This employee was changed by someone else while you were editing. Reload the page and apply your changes again.");
                _context.Entry(employee).State = EntityState.Detached;
                await LoadCreateFormOptionsAsync(employee);
                ViewBag.EmployeeDocuments = await _documentService.BuildViewModelAsync(employee.uid, canUpload: true);
                return View(employee);
            }
            return RedirectToAction(nameof(Index));
        }

        await LoadCreateFormOptionsAsync(employee);
        ViewBag.EmployeeDocuments = await _documentService.BuildViewModelAsync(employee.uid, canUpload: true);
        return View(employee);
    }

    // GET: Employee/Delete/5
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        ViewData["Module"] = "Employees";
        // Employees are never hard-deleted: payslips, leave and attendance reference them. Mark inactive instead.
        var employee = await _context.Employees.FindAsync(id);
        if (employee != null && employee.EmployeeStatus != "Inactive")
        {
            employee.EmployeeStatus = "Inactive";
            employee.ModifiedBy = User.Identity?.Name;
            employee.ModifiedOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Properties the Employee edit form posts; everything else on the row is left as it is.</summary>
    private static readonly string[] EditablePropertyNames =
    {
        nameof(Employee.EmployeeID), nameof(Employee.EmployeeName), nameof(Employee.FatherName), nameof(Employee.DOB),
        nameof(Employee.CNIC), nameof(Employee.MobileNo), nameof(Employee.ProjectId), nameof(Employee.DepartmentId),
        nameof(Employee.SubDepartmentId), nameof(Employee.DesignationId), nameof(Employee.DateOfJoining),
        nameof(Employee.DateOfLeaving), nameof(Employee.DateOfReturn), nameof(Employee.EmployeeStatus),
        nameof(Employee.ModifiedBy), nameof(Employee.ModifiedOn), nameof(Employee.Details), nameof(Employee.Notes),
        nameof(Employee.CarryForwardLeaves), nameof(Employee.Year2022), nameof(Employee.Year2023),
        nameof(Employee.AdjustedAjusted), nameof(Employee.Year2024), nameof(Employee.CarryForwardLeaves1),
        nameof(Employee.Year2023New), nameof(Employee.BasicSalary), nameof(Employee.ApplyTax), nameof(Employee.GenStatus)
    };

    private static readonly string[] EmployeeStatusValues = { "Active", "Inactive", "Resigned", "Terminated" };

    /// <summary>Project and department are required; a sub-department must belong to the chosen department; leaving/return dates must be in order.</summary>
    private async Task ValidateOrganisationAsync(Employee employee)
    {
        if (employee.ProjectId <= 0)
            ModelState.AddModelError(nameof(Employee.ProjectId), "Please select a project.");
        else if (!await _context.Projects.AnyAsync(p => p.Id == employee.ProjectId))
            ModelState.AddModelError(nameof(Employee.ProjectId), "Unknown project.");

        if (employee.DepartmentId <= 0)
            ModelState.AddModelError(nameof(Employee.DepartmentId), "Please select a department.");
        else if (!await _context.Departments.AnyAsync(d => d.DepartmentID == employee.DepartmentId))
            ModelState.AddModelError(nameof(Employee.DepartmentId), "Unknown department.");

        if (employee.SubDepartmentId.HasValue &&
            !await _context.SubDepartments.AnyAsync(s => s.Id == employee.SubDepartmentId && s.DepartmentId == employee.DepartmentId))
            ModelState.AddModelError(nameof(Employee.SubDepartmentId), "The sub-department does not belong to the selected department.");

        if (employee.DesignationId.HasValue && !await _context.Designations.AnyAsync(d => d.Id == employee.DesignationId))
            ModelState.AddModelError(nameof(Employee.DesignationId), "Unknown designation.");

        if (!EmployeeStatusValues.Contains(employee.EmployeeStatus ?? ""))
            ModelState.AddModelError(nameof(Employee.EmployeeStatus), "Status must be Active, Inactive, Resigned or Terminated.");

        if (employee.DateOfLeaving.HasValue && employee.DateOfJoining.HasValue && employee.DateOfLeaving < employee.DateOfJoining)
            ModelState.AddModelError(nameof(Employee.DateOfLeaving), "Date of leaving cannot be before the date of joining.");
        if (employee.DateOfReturn.HasValue && (!employee.DateOfLeaving.HasValue || employee.DateOfReturn <= employee.DateOfLeaving))
            ModelState.AddModelError(nameof(Employee.DateOfReturn), "Date of return needs a date of leaving and must be after it.");
    }

    private async Task LoadCreateFormOptionsAsync(Employee? current = null)
    {
        ViewBag.ProjectOptions = await _context.Projects.AsNoTracking()
            .Where(p => p.IsActive || (current != null && p.Id == current.ProjectId))
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
            .ToListAsync();

        ViewBag.DepartmentOptions = await _context.Departments.AsNoTracking()
            .Where(d => (d.IsActive || (current != null && d.DepartmentID == current.DepartmentId)) && d.DepartmentName != null)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.DepartmentName)
            .Select(d => new SelectListItem { Value = d.DepartmentID.ToString(), Text = d.DepartmentName! })
            .ToListAsync();

        var subs = await _context.SubDepartments.AsNoTracking()
            .Where(s => s.IsActive || (current != null && s.Id == current.SubDepartmentId))
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new { s.Id, s.DepartmentId, s.Name })
            .ToListAsync();
        ViewBag.SubDepartmentOptions = subs.Select(s => (s.Id, s.DepartmentId, s.Name)).ToList();

        ViewBag.DesignationOptions = await _context.Designations.AsNoTracking()
            .Where(d => d.IsActive || (current != null && d.Id == current.DesignationId))
            .OrderBy(d => d.Rank).ThenBy(d => d.Name)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync();

        ViewBag.EmployeeStatusOptions = EmployeeStatusValues;
    }

    private bool EmployeeExists(int id)
    {
        return _context.Employees.Any(e => e.uid == id);
    }
}
