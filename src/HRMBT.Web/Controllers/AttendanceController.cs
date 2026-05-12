using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services;
using HRMBT.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace HRMBT.Web.Controllers
{
    public class AttendanceController : Controller
    {
        private const string LeaveTypesConfigKey = "LeaveTypes";

        private readonly AttendanceService _attendanceService;
        private readonly ApplicationDbContext _context;

        public AttendanceController(AttendanceService attendanceService, ApplicationDbContext context)
        {
            _attendanceService = attendanceService;
            _context = context;
        }

        private static IEnumerable<string> SplitConfigCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) yield break;
            foreach (var item in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(item))
                    yield return item;
            }
        }

        private static bool ConfigKeyMatches(string? key, string match) =>
            !string.IsNullOrWhiteSpace(key) && string.Equals(key.Trim(), match, StringComparison.OrdinalIgnoreCase);

        /// <summary>Leave types from dbo.Configuration (ConfigKey LeaveTypes, CSV) or LeaveQuota.</summary>
        private async Task<List<string>> GetOffDayLeaveTypesAsync()
        {
            var rows = await _context.HrConfigurations.AsNoTracking()
                .Where(c => c.ConfigKey != null && c.ConfigValue != null)
                .ToListAsync();

            var fromConfig = rows
                .Where(c => ConfigKeyMatches(c.ConfigKey, LeaveTypesConfigKey))
                .SelectMany(c => SplitConfigCsv(c.ConfigValue))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (fromConfig.Count > 0)
                return fromConfig;

            var yearStr = DateTime.Now.Year.ToString();
            var fromQuota = await _context.LeaveQuotas.AsNoTracking()
                .Where(lq => lq.Year == yearStr)
                .Select(lq => lq.LeaveTypeName)
                .Where(n => n != null && n != "")
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();

            if (fromQuota.Count > 0)
                return fromQuota!;

            return await _context.LeaveQuotas.AsNoTracking()
                .Select(lq => lq.LeaveTypeName)
                .Where(n => n != null && n != "")
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync()!;
        }

        // GET: Attendance Dashboard
        public async Task<IActionResult> Index()
        {
            ViewData["Module"] = "Attendance";
            var attendances = await _attendanceService.GetAttendanceSummaryAsync();
            return View(attendances);
        }

        // GET: Attendance/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            ViewData["Module"] = "Attendance";
            if (id == null)
            {
                return NotFound();
            }

            var attendance = await _attendanceService.GetAttendanceByIdAsync(id.Value);
            if (attendance == null)
            {
                return NotFound();
            }

            return View(attendance);
        }

        // GET: Attendance/Create
        public async Task<IActionResult> Create(string department, int page = 1, int pageSize = 20)
        {
            ViewData["Module"] = "Attendance";
            
            // Ensure valid pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100; // Max page size limit

            var query = _context.Employees.AsQueryable();

            // Filter by department if provided
            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(e => e.Department != null && e.Department == department);
            }

            // Apply ordering before pagination
            query = query.OrderBy(e => e.EmployeeName);

            // Get paginated results
            var paginatedEmployees = await PaginatedList<Employee>.CreateAsync(query, page, pageSize);

            // Get distinct departments for dropdown
            var departments = await _context.Employees
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            ViewBag.Departments = departments;
            ViewBag.SelectedDepartment = department;
            ViewBag.CurrentPageSize = pageSize;

            return View(paginatedEmployees);
        }

        // POST: Attendance/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [FromForm] List<string> selectedEmployeeIds, 
            [FromForm] DateTime attendanceDate,
            [FromForm] TimeSpan? timeIn,
            [FromForm] TimeSpan? timeOut,
            [FromForm] string? status,
            [FromForm] string? comments,
            string department,
            int page = 1,
            int pageSize = 20)
        {
            ViewData["Module"] = "Attendance";

            if (selectedEmployeeIds == null || !selectedEmployeeIds.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one employee.";
                return RedirectToAction(nameof(Create), new { department, page, pageSize });
            }

            if (attendanceDate == default(DateTime))
            {
                TempData["ErrorMessage"] = "Please select a valid attendance date.";
                return RedirectToAction(nameof(Create), new { department, page, pageSize });
            }

            // Validate time logic
            if (timeIn.HasValue && timeOut.HasValue && timeOut.Value <= timeIn.Value)
            {
                TempData["ErrorMessage"] = "Time Out must be later than Time In.";
                return RedirectToAction(nameof(Create), new { department, page, pageSize });
            }

            int successCount = 0;
            int skipCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            foreach (var employeeId in selectedEmployeeIds)
            {
                try
                {
                    // Get employee details
                    var employee = await _context.Employees
                        .FirstOrDefaultAsync(e => e.EmployeeID == employeeId);

                    if (employee == null)
                    {
                        errorCount++;
                        errors.Add($"Employee ID '{employeeId}' not found.");
                        continue;
                    }

                    // Check if attendance already exists for this employee on this date
                    var existing = await _attendanceService.GetAttendanceByIDAndDateAsync(employeeId, attendanceDate);
                    if (existing != null)
                    {
                        skipCount++;
                        errors.Add($"Attendance already exists for {employee.EmployeeName} ({employeeId}) on {attendanceDate:yyyy-MM-dd}.");
                        continue;
                    }

                    // Create attendance record
                    var attendance = new Attendance
                    {
                        EmployeeID = employee.EmployeeID ?? employeeId,
                        EmployeeName = employee.EmployeeName ?? "",
                        DepartmentName = employee.Department ?? "",
                        AttendanceDate = attendanceDate,
                        TimeIn = timeIn,
                        TimeOut = timeOut,
                        Status = status,
                        Comments = comments
                    };

                    await _attendanceService.CreateAttendanceAsync(attendance);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errors.Add($"Error creating attendance for Employee ID '{employeeId}': {ex.Message}");
                }
            }

            // Build result message
            var message = $"Attendance created: {successCount} successful";
            if (skipCount > 0)
            {
                message += $", {skipCount} skipped (already exists)";
            }
            if (errorCount > 0)
            {
                message += $", {errorCount} failed";
            }

            if (successCount > 0)
            {
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            if (errors.Any())
            {
                TempData["ErrorDetails"] = string.Join("<br/>", errors.Take(20)); // Limit to first 20 errors
                if (errors.Count > 20)
                {
                    TempData["ErrorDetails"] += $"<br/><small class='text-muted'>... and {errors.Count - 20} more errors</small>";
                }
            }

            return RedirectToAction(nameof(Create), new { department, page, pageSize });
        }

        // GET: Attendance/OffDay — bulk leave rows in dbo.LeaveRequests (off-day / single day).
        public async Task<IActionResult> OffDay(string? department, int page = 1, int pageSize = 20)
        {
            ViewData["Module"] = "Attendance";

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Employees.AsQueryable();
            if (!string.IsNullOrWhiteSpace(department))
                query = query.Where(e => e.Department != null && e.Department == department);

            query = query.OrderBy(e => e.EmployeeName);
            var paginatedEmployees = await PaginatedList<Employee>.CreateAsync(query, page, pageSize);

            var departments = await _context.Employees
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            ViewBag.Departments = departments;
            ViewBag.SelectedDepartment = department;
            ViewBag.CurrentPageSize = pageSize;
            ViewBag.LeaveTypeOptions = await GetOffDayLeaveTypesAsync();

            return View(paginatedEmployees);
        }

        // POST: Attendance/OffDay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OffDay(
            [FromForm] List<string>? selectedEmployeeIds,
            [FromForm] string? leaveType,
            [FromForm] DateTime offDayDate,
            string? department,
            int page = 1,
            int pageSize = 20)
        {
            ViewData["Module"] = "Attendance";

            selectedEmployeeIds = selectedEmployeeIds?
                .Select(s => (s ?? "").Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList() ?? new List<string>();

            if (!selectedEmployeeIds.Any())
            {
                TempData["ErrorMessage"] = "No employees were submitted. Select at least one row in the grid, choose leave type and date, then click Create off-day records again.";
                return RedirectToAction(nameof(OffDay), new { department, page, pageSize });
            }

            if (offDayDate == default)
            {
                TempData["ErrorMessage"] = "Please select a valid date.";
                return RedirectToAction(nameof(OffDay), new { department, page, pageSize });
            }

            var allowedTypes = await GetOffDayLeaveTypesAsync();
            var normalizedType = (leaveType ?? "").Trim();
            if (string.IsNullOrEmpty(normalizedType) || !allowedTypes.Contains(normalizedType, StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(leaveType)
                    ? "Please select a leave type."
                    : "The selected leave type is not allowed. Configure types under Configuration (LeaveTypes) or LeaveQuota.";
                return RedirectToAction(nameof(OffDay), new { department, page, pageSize });
            }

            var canonicalType = allowedTypes.First(t => string.Equals(t, normalizedType, StringComparison.OrdinalIgnoreCase));
            if (canonicalType.Length > 50)
            {
                TempData["ErrorMessage"] = "Leave type exceeds 50 characters.";
                return RedirectToAction(nameof(OffDay), new { department, page, pageSize });
            }

            var day = offDayDate.Date;
            var dayEnd = day.AddDays(1);

            var employees = await _context.Employees
                .Where(e => e.EmployeeID != null && selectedEmployeeIds.Contains(e.EmployeeID))
                .ToListAsync();

            var foundIds = new HashSet<string>(employees.Select(e => e.EmployeeID!), StringComparer.OrdinalIgnoreCase);
            foreach (var sid in selectedEmployeeIds.Distinct())
            {
                if (foundIds.Contains(sid)) continue;
                var one = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeID != null && e.EmployeeID.ToLower() == sid.ToLower());
                if (one != null)
                {
                    employees.Add(one);
                    foundIds.Add(one.EmployeeID!);
                }
            }

            Employee? FindEmployee(string id) =>
                employees.FirstOrDefault(e => string.Equals(e.EmployeeID, id, StringComparison.OrdinalIgnoreCase));

            var uids = employees.Select(e => e.uid).ToList();
            var existingEmployeeIds = await _context.LeaveRequests.AsNoTracking()
                .Where(l => uids.Contains(l.EmployeeId)
                    && l.FromDate >= day && l.FromDate < dayEnd
                    && l.LeaveType == canonicalType)
                .Select(l => l.EmployeeId)
                .ToListAsync();
            var existingSet = existingEmployeeIds.ToHashSet();

            var toAdd = new List<LeaveRequest>();
            var skipped = new List<string>();
            var notFound = new List<string>();

            foreach (var id in selectedEmployeeIds.Distinct())
            {
                var emp = FindEmployee(id);
                if (emp == null)
                {
                    notFound.Add(id);
                    continue;
                }

                if (existingSet.Contains(emp.uid))
                {
                    skipped.Add($"{emp.EmployeeName} ({id})");
                    continue;
                }

                toAdd.Add(new LeaveRequest
                {
                    EmployeeId = emp.uid,
                    LeaveType = canonicalType,
                    FromDate = day,
                    ToDate = day,
                    Status = "Approved"
                });
                existingSet.Add(emp.uid);
            }

            var summaryLine = $"Off-day for {day:yyyy-MM-dd}, leave type \"{canonicalType}\": {toAdd.Count} new row(s) to save, {skipped.Count} skipped (duplicate), {notFound.Count} unknown ID(s), {selectedEmployeeIds.Count} checkbox(es) posted.";

            var detailLines = new List<string>();
            if (skipped.Count > 0)
                detailLines.AddRange(skipped.Take(20).Select(s => $"Already exists: {s}"));
            if (notFound.Count > 0)
                detailLines.Add("Not found: " + string.Join(", ", notFound.Take(20)));

            if (toAdd.Count > 0)
            {
                try
                {
                    _context.LeaveRequests.AddRange(toAdd);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = summaryLine + " Database save completed (dbo.LeaveRequests).";
                }
                catch (Exception ex)
                {
                    _context.ChangeTracker.Clear();
                    var inner = ex.InnerException?.Message ?? ex.Message;
                    TempData["ErrorMessage"] = summaryLine + " Save to LeaveRequests failed; no rows were committed.";
                    TempData["ErrorDetails"] = System.Net.WebUtility.HtmlEncode(inner);
                }
            }
            else
            {
                TempData["ErrorMessage"] = summaryLine + " Nothing was written. If every employee was skipped, they already have this leave type on that date. If IDs were not found, the EmployeeID in the grid does not match dbo.Employee. If no checkboxes appeared, set EmployeeStatus to Active.";
                if (detailLines.Count > 0)
                    TempData["ErrorDetails"] = string.Join("<br/>", detailLines);
            }

            return RedirectToAction(nameof(OffDay), new { department, page, pageSize });
        }

        // GET: Attendance/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            ViewData["Module"] = "Attendance";
            if (id == null)
            {
                return NotFound();
            }

            var attendance = await _attendanceService.GetAttendanceByIdAsync(id.Value);
            if (attendance == null)
            {
                return NotFound();
            }

            return View(attendance);
        }

        // POST: Attendance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Attendance attendance)
        {
            ViewData["Module"] = "Attendance";
            
            if (id != attendance.AttendanceID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Check for duplicate (excluding current record)
                var existing = await _attendanceService.GetAttendanceByIDAndDateAsync(attendance.EmployeeID, attendance.AttendanceDate);
                if (existing != null && existing.AttendanceID != attendance.AttendanceID)
                {
                    ModelState.AddModelError("", "Attendance record already exists for this employee on this date.");
                    return View(attendance);
                }

                // Validate time logic
                if (attendance.TimeIn.HasValue && attendance.TimeOut.HasValue && attendance.TimeOut.Value <= attendance.TimeIn.Value)
                {
                    ModelState.AddModelError("TimeOut", "Time Out must be later than Time In.");
                    return View(attendance);
                }

                try
                {
                    await _attendanceService.UpdateAttendanceAsync(attendance);
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    ModelState.AddModelError("", "An error occurred while updating the attendance record.");
                    return View(attendance);
                }
            }
            
            return View(attendance);
        }

        // GET: Attendance/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            ViewData["Module"] = "Attendance";
            if (id == null)
            {
                return NotFound();
            }

            var attendance = await _attendanceService.GetAttendanceByIdAsync(id.Value);
            if (attendance == null)
            {
                return NotFound();
            }

            return View(attendance);
        }

        // POST: Attendance/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ViewData["Module"] = "Attendance";
            await _attendanceService.DeleteAttendanceAsync(id);
            return RedirectToAction(nameof(Index));
        }

        // GET: Upload Excel Form
        public IActionResult Upload()
        {
            ViewData["Module"] = "Attendance";
            return View();
        }

        // POST: Process Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile excelFile)
        {
            ViewData["Module"] = "Attendance";
            if (excelFile == null || excelFile.Length == 0)
            {
                ViewBag.Error = "Please select an Excel file.";
                return View();
            }

            // Validate file extension
            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var fileExtension = Path.GetExtension(excelFile.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
            {
                ViewBag.Error = "Invalid file type. Please upload an Excel file (.xlsx or .xls).";
                return View();
            }

            var result = await _attendanceService.ProcessExcelAsync(excelFile);
            return View("UploadResult", result);
        }

        // GET: Monthly Attendance Report
        public async Task<IActionResult> MonthlyReport(int month, int year, string? department)
        {
            ViewData["Module"] = "Attendance";
            
            // Default to current month/year if not provided
            if (month == 0) month = DateTime.Now.Month;
            if (year == 0) year = DateTime.Now.Year;
            
            var report = _attendanceService.GetMonthlyReport(month, year);
            
            // Filter by department if provided
            if (!string.IsNullOrWhiteSpace(department))
            {
                report = report.Where(a => a.DepartmentName == department).ToList();
            }
            
            // Group by employee and calculate totals
            var employeeSummaries = report
                .GroupBy(a => new { a.EmployeeID, a.EmployeeName, a.DepartmentName })
                .Select(g => new EmployeeMonthlySummary
                {
                    EmployeeID = g.Key.EmployeeID,
                    EmployeeName = g.Key.EmployeeName,
                    DepartmentName = g.Key.DepartmentName,
                    TotalPresent = g.Count(a => a.Status == "P"),
                    TotalAbsent = g.Count(a => a.Status == "A"),
                    TotalLate = g.Count(a => a.Status == "L"),
                    TotalHoliday = g.Count(a => a.Status == "H")
                })
                .OrderBy(e => e.DepartmentName)
                .ThenBy(e => e.EmployeeName)
                .ToList();
            
            // Get distinct departments for dropdown
            var departments = await _context.Employees
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();
            
            ViewBag.Month = month;
            ViewBag.Year = year;
            ViewBag.Departments = departments;
            ViewBag.SelectedDepartment = department;
            
            return View(employeeSummaries);
        }

        // GET: Attendance/AttendanceByDepartment
        public async Task<IActionResult> AttendanceByDepartment(DateTime? date)
        {
            ViewData["Module"] = "Attendance";
            
            // Default to today if not provided
            var attendanceDate = date ?? DateTime.Now.Date;
            
            // Get all departments
            var departments = await _context.Employees
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            // Get attendance records for the selected date
            var attendances = await _context.Attendances
                .Where(a => a.AttendanceDate.Date == attendanceDate.Date)
                .ToListAsync();

            // Calculate summary for each department
            var departmentSummaries = new List<DepartmentAttendanceSummary>();
            
            foreach (var dept in departments)
            {
                var deptAttendances = attendances.Where(a => a.DepartmentName == dept).ToList();
                
                var summary = new DepartmentAttendanceSummary
                {
                    DepartmentName = dept ?? string.Empty,
                    TotalEmployees = await _context.Employees
                        .CountAsync(e => e.Department == dept && e.EmployeeStatus == "Active"),
                    Present = deptAttendances.Count(a => a.Status == "P"),
                    Absent = deptAttendances.Count(a => a.Status == "A"),
                    Late = deptAttendances.Count(a => a.Status == "L"),
                    Holiday = deptAttendances.Count(a => a.Status == "H"),
                    NotMarked = 0 // Will be calculated
                };
                
                // Calculate not marked (total active employees - marked attendance)
                summary.NotMarked = summary.TotalEmployees - (summary.Present + summary.Absent + summary.Late + summary.Holiday);
                
                departmentSummaries.Add(summary);
            }

            ViewBag.AttendanceDate = attendanceDate;
            ViewBag.SelectedDate = attendanceDate.ToString("yyyy-MM-dd");
            
            return View(departmentSummaries);
        }

    }
}
