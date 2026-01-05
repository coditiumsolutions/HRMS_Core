using HRMBT.Web.Models;
using HRMBT.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HRMBT.Web.Services
{
    public class AttendanceService
    {
        private readonly ApplicationDbContext _context;

        public AttendanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UploadResult> ProcessCsvAsync(IFormFile csvFile)
        {
            var result = new UploadResult();
            var errorMessages = new List<string>();
            var successfulRows = 0;
            var failedRows = 0;

            // Create upload log
            var uploadLog = new AttendanceUploadLog
            {
                UploadDate = DateTime.Now,
                UploadedBy = "System", // TODO: Get from current user
                FileName = csvFile.FileName,
                TotalRows = 0,
                SuccessCount = 0,
                FailureCount = 0,
                ErrorDetails = ""
            };

            _context.AttendanceUploadLogs.Add(uploadLog);
            await _context.SaveChangesAsync();

            try
            {
                using (var reader = new StreamReader(csvFile.OpenReadStream()))
                {
                    string? line;
                    int rowNumber = 0;
                    bool isFirstLine = true;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        rowNumber++;
                        result.TotalRows++;

                        // Skip header row
                        if (isFirstLine)
                        {
                            isFirstLine = false;
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var columns = line.Split(',');
                        
                        // Expected CSV format: EmployeeID,EmployeeName,DepartmentName,AttendanceDate,TimeIn,TimeOut,Status,Comments
                        if (columns.Length < 4)
                        {
                            failedRows++;
                            errorMessages.Add($"Row {rowNumber}: Insufficient columns. Expected at least 4 columns.");
                            continue;
                        }

                        var employeeID = columns[0]?.Trim();
                        var employeeName = columns.Length > 1 ? columns[1]?.Trim() : null;
                        var departmentName = columns.Length > 2 ? columns[2]?.Trim() : null;
                        var dateStr = columns.Length > 3 ? columns[3]?.Trim() : null;
                        var timeInStr = columns.Length > 4 ? columns[4]?.Trim() : null;
                        var timeOutStr = columns.Length > 5 ? columns[5]?.Trim() : null;
                        var status = columns.Length > 6 ? columns[6]?.Trim() : null;
                        var comments = columns.Length > 7 ? columns[7]?.Trim() : null;

                        // Validate required fields
                        if (string.IsNullOrWhiteSpace(employeeID))
                        {
                            failedRows++;
                            errorMessages.Add($"Row {rowNumber}: EmployeeID is required.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(employeeName))
                        {
                            failedRows++;
                            errorMessages.Add($"Row {rowNumber}: EmployeeName is required.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(departmentName))
                        {
                            failedRows++;
                            errorMessages.Add($"Row {rowNumber}: DepartmentName is required.");
                            continue;
                        }

                        // Parse date
                        if (string.IsNullOrWhiteSpace(dateStr) || !DateTime.TryParse(dateStr, out DateTime attendanceDate))
                        {
                            failedRows++;
                            errorMessages.Add($"Row {rowNumber}: Invalid date format '{dateStr}'.");
                            continue;
                        }

                        // Parse optional times
                        TimeSpan? timeIn = null;
                        TimeSpan? timeOut = null;

                        if (!string.IsNullOrWhiteSpace(timeInStr))
                        {
                            if (!TimeSpan.TryParse(timeInStr, out TimeSpan parsedTimeIn))
                            {
                                failedRows++;
                                errorMessages.Add($"Row {rowNumber}: Invalid TimeIn format '{timeInStr}'.");
                                continue;
                            }
                            timeIn = parsedTimeIn;
                        }

                        if (!string.IsNullOrWhiteSpace(timeOutStr))
                        {
                            if (!TimeSpan.TryParse(timeOutStr, out TimeSpan parsedTimeOut))
                            {
                                failedRows++;
                                errorMessages.Add($"Row {rowNumber}: Invalid TimeOut format '{timeOutStr}'.");
                                continue;
                            }
                            timeOut = parsedTimeOut;
                        }

                        // Validate time logic
                        if (timeIn.HasValue && timeOut.HasValue && timeOut.Value <= timeIn.Value)
                        {
                            failedRows++;
                            errorMessages.Add($"Row {rowNumber}: TimeOut must be later than TimeIn.");
                            continue;
                        }

                        // Check for duplicate (one record per employee per date)
                        var existing = await _context.Attendances
                            .FirstOrDefaultAsync(a => a.EmployeeID == employeeID && 
                                                      a.AttendanceDate.Date == attendanceDate.Date);

                        if (existing != null)
                        {
                            // Update existing record
                            existing.EmployeeName = employeeName ?? string.Empty;
                            existing.DepartmentName = departmentName ?? string.Empty;
                            existing.TimeIn = timeIn;
                            existing.TimeOut = timeOut;
                            existing.Status = status;
                            existing.Comments = comments;
                            _context.Attendances.Update(existing);
                        }
                        else
                        {
                            // Create new record
                            var attendance = new Attendance
                            {
                                EmployeeID = employeeID,
                                EmployeeName = employeeName ?? string.Empty,
                                DepartmentName = departmentName ?? string.Empty,
                                AttendanceDate = attendanceDate,
                                TimeIn = timeIn,
                                TimeOut = timeOut,
                                Status = status,
                                Comments = comments
                            };
                            _context.Attendances.Add(attendance);
                        }

                        successfulRows++;
                    }
                }

                // Save all changes
                await _context.SaveChangesAsync();

                // Update upload log
                uploadLog.TotalRows = result.TotalRows;
                uploadLog.SuccessCount = successfulRows;
                uploadLog.FailureCount = failedRows;
                uploadLog.ErrorDetails = string.Join("; ", errorMessages);
                _context.AttendanceUploadLogs.Update(uploadLog);
                await _context.SaveChangesAsync();

                result.SuccessfulRows = successfulRows;
                result.FailedRows = failedRows;
                result.ErrorMessages = errorMessages;
                result.UploadLogId = uploadLog.Id;
            }
            catch (Exception ex)
            {
                errorMessages.Add($"Error processing CSV: {ex.Message}");
                uploadLog.ErrorDetails = string.Join("; ", errorMessages);
                uploadLog.FailureCount = failedRows;
                _context.AttendanceUploadLogs.Update(uploadLog);
                await _context.SaveChangesAsync();

                result.ErrorMessages = errorMessages;
                result.FailedRows = failedRows;
            }

            return result;
        }

        public List<Attendance> GetMonthlyReport(int month, int year)
        {
            return _context.Attendances
                .Where(a => a.AttendanceDate.Month == month && a.AttendanceDate.Year == year)
                .OrderBy(a => a.AttendanceDate)
                .ThenBy(a => a.EmployeeID)
                .ToList();
        }

        public async Task<List<Attendance>> GetAttendanceSummaryAsync(string? department = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Attendances.AsQueryable();

            if (!string.IsNullOrEmpty(department))
            {
                query = query.Where(a => a.DepartmentName == department);
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.AttendanceDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.AttendanceDate <= endDate.Value);
            }

            return await query
                .OrderBy(a => a.AttendanceDate)
                .ThenBy(a => a.EmployeeID)
                .ToListAsync();
        }

        public async Task<Employee?> GetEmployeeByCodeAsync(string employeeID)
        {
            return await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeID == employeeID);
        }

        public async Task<Attendance?> GetAttendanceByIDAndDateAsync(string employeeID, DateTime date)
        {
            return await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeID == employeeID && 
                                         a.AttendanceDate.Date == date.Date);
        }

        public async Task CreateAttendanceAsync(Attendance attendance)
        {
            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAttendanceAsync(Attendance attendance)
        {
            _context.Attendances.Update(attendance);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAttendanceAsync(int attendanceID)
        {
            var attendance = await _context.Attendances.FindAsync(attendanceID);
            if (attendance != null)
            {
                _context.Attendances.Remove(attendance);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Attendance?> GetAttendanceByIdAsync(int attendanceID)
        {
            return await _context.Attendances.FindAsync(attendanceID);
        }
    }
}
