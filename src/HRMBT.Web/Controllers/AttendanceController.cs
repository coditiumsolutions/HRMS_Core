using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using HRMBT.Web.Services;
using HRMBT.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace HRMBT.Web.Controllers
{
    public class AttendanceController : Controller
    {
        private readonly AttendanceService _attendanceService;
        private readonly ApplicationDbContext _context;

        public AttendanceController(AttendanceService attendanceService, ApplicationDbContext context)
        {
            _attendanceService = attendanceService;
            _context = context;
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

        // GET: Upload CSV Form
        public IActionResult Upload()
        {
            ViewData["Module"] = "Attendance";
            return View();
        }

        // POST: Process CSV
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile csvFile)
        {
            ViewData["Module"] = "Attendance";
            if (csvFile == null || csvFile.Length == 0)
            {
                ViewBag.Error = "Please select a CSV file.";
                return View();
            }

            var result = await _attendanceService.ProcessCsvAsync(csvFile);
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
