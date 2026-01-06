using HRMBT.Web.Models;
using HRMBT.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OfficeOpenXml;

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

        public async Task<UploadResult> ProcessExcelAsync(IFormFile excelFile)
        {
            var result = new UploadResult();
            var errorMessages = new List<string>();
            var successfulRows = 0;
            var failedRows = 0;
            var updatedRows = 0;
            var insertedRows = 0;

            // Create upload log (optional - continue if table doesn't exist)
            AttendanceUploadLog? uploadLog = null;
            try
            {
                uploadLog = new AttendanceUploadLog
                {
                    UploadDate = DateTime.Now,
                    UploadedBy = "System", // TODO: Get from current user
                    FileName = excelFile.FileName,
                    TotalRows = 0,
                    SuccessCount = 0,
                    FailureCount = 0,
                    ErrorDetails = ""
                };

                _context.AttendanceUploadLogs.Add(uploadLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // Table doesn't exist or other error - continue without logging
                uploadLog = null;
            }

            try
            {
                // Set EPPlus license context (required for non-commercial use)
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var stream = new MemoryStream())
                {
                    await excelFile.CopyToAsync(stream);
                    stream.Position = 0;

                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        if (worksheet == null)
                        {
                            errorMessages.Add("Excel file does not contain any worksheets.");
                            if (uploadLog != null)
                            {
                                uploadLog.ErrorDetails = string.Join("; ", errorMessages);
                                uploadLog.FailureCount = 1;
                                _context.AttendanceUploadLogs.Update(uploadLog);
                                await _context.SaveChangesAsync();
                            }
                            result.ErrorMessages = errorMessages;
                            result.FailedRows = 1;
                            return result;
                        }

                        // Find header row dynamically (search first 20 rows for flexibility)
                        // Define expected headers with possible variations
                        var headerVariations = new Dictionary<int, string[]>
                        {
                            { 0, new[] { "Employee ID", "EmployeeID", "Employee_Id", "Emp ID", "EmpID" } },
                            { 1, new[] { "Employee Name", "EmployeeName", "Employee_Name", "Name", "Emp Name" } },
                            { 2, new[] { "Department", "DepartmentName", "Department_Name", "Dept", "Dept Name" } },
                            { 3, new[] { "Date", "AttendanceDate", "Attendance_Date", "AttDate", "Att Date" } },
                            { 4, new[] { "TimeIn", "Time In", "Time_In", "InTime", "In Time", "TimeIn", "Check In" } },
                            { 5, new[] { "TimeOut", "Time Out", "Time_Out", "OutTime", "Out Time", "TimeOut", "Check Out" } },
                            { 6, new[] { "Status", "AttendanceStatus", "Attendance_Status", "AttStatus" } }
                        };

                        int headerRow = 0;
                        int dataStartRow = 0;
                        var lastRow = worksheet.Dimension?.End.Row ?? 0;
                        int maxSearchRows = Math.Min(20, lastRow);

                        // Search for header row
                        for (int searchRow = 1; searchRow <= maxSearchRows; searchRow++)
                        {
                            bool isHeaderRow = true;
                            for (int col = 1; col <= headerVariations.Count; col++)
                            {
                                var headerValue = worksheet.Cells[searchRow, col].Text?.Trim();
                                if (string.IsNullOrWhiteSpace(headerValue))
                                {
                                    isHeaderRow = false;
                                    break;
                                }

                                // Check if header matches any variation
                                bool matches = false;
                                if (headerVariations.ContainsKey(col - 1))
                                {
                                    foreach (var variation in headerVariations[col - 1])
                                    {
                                        if (headerValue.Equals(variation, StringComparison.OrdinalIgnoreCase))
                                        {
                                            matches = true;
                                            break;
                                        }
                                    }
                                }

                                if (!matches)
                                {
                                    isHeaderRow = false;
                                    break;
                                }
                            }

                            if (isHeaderRow)
                            {
                                headerRow = searchRow;
                                dataStartRow = searchRow + 1;
                                break;
                            }
                        }

                        // If header row not found, provide helpful error message
                        if (headerRow == 0)
                        {
                            errorMessages.Add("Could not find the expected header row in the first 20 rows of your Excel file.");
                            errorMessages.Add("Expected headers (in order): Employee ID, Employee Name, Department, Date, TimeIn, TimeOut, Status");
                            errorMessages.Add("Please ensure your Excel file has these exact headers (case-insensitive) in one of the first 20 rows.");
                            errorMessages.Add("Note: Headers can have variations like 'EmployeeID', 'Employee ID', 'Employee_Id', etc.");
                            
                            // Show what was found in first few rows for debugging
                            errorMessages.Add("");
                            errorMessages.Add("First few rows found in your file:");
                            for (int debugRow = 1; debugRow <= Math.Min(5, lastRow); debugRow++)
                            {
                                var rowData = new List<string>();
                                for (int col = 1; col <= 7; col++)
                                {
                                    var cellValue = worksheet.Cells[debugRow, col].Text?.Trim();
                                    rowData.Add(string.IsNullOrWhiteSpace(cellValue) ? "(empty)" : cellValue);
                                }
                                errorMessages.Add($"Row {debugRow}: {string.Join(" | ", rowData)}");
                            }

                            if (uploadLog != null)
                            {
                                uploadLog.ErrorDetails = "Header validation failed: " + string.Join("; ", errorMessages);
                                uploadLog.FailureCount = 1;
                                _context.AttendanceUploadLogs.Update(uploadLog);
                                await _context.SaveChangesAsync();
                            }
                            result.ErrorMessages = errorMessages;
                            result.FailedRows = 1;
                            return result;
                        }

                        // Process data rows
                        result.TotalRows = lastRow - dataStartRow + 1; // Count data rows only

                        for (int row = dataStartRow; row <= lastRow; row++)
                        {
                            try
                            {
                                // Skip empty rows
                                var employeeIdCell = worksheet.Cells[row, 1].Text?.Trim();
                                if (string.IsNullOrWhiteSpace(employeeIdCell))
                                {
                                    continue;
                                }

                                // Read columns in fixed order
                                var employeeID = worksheet.Cells[row, 1].Text?.Trim() ?? string.Empty;
                                var employeeName = worksheet.Cells[row, 2].Text?.Trim() ?? string.Empty;
                                var department = worksheet.Cells[row, 3].Text?.Trim() ?? string.Empty;
                                var dateCell = worksheet.Cells[row, 4];
                                var timeInCell = worksheet.Cells[row, 5];
                                var timeOutCell = worksheet.Cells[row, 6];
                                var status = worksheet.Cells[row, 7].Text?.Trim();

                                // Validate required fields
                                if (string.IsNullOrWhiteSpace(employeeID))
                                {
                                    failedRows++;
                                    errorMessages.Add($"Row {row}: Employee ID is required.");
                                    continue;
                                }

                                if (string.IsNullOrWhiteSpace(employeeName))
                                {
                                    failedRows++;
                                    errorMessages.Add($"Row {row}: Employee Name is required.");
                                    continue;
                                }

                                if (string.IsNullOrWhiteSpace(department))
                                {
                                    failedRows++;
                                    errorMessages.Add($"Row {row}: Department is required.");
                                    continue;
                                }

                                // Parse Date - handle both DateTime and string formats
                                DateTime attendanceDate;
                                if (dateCell.Value is DateTime dateTimeValue)
                                {
                                    attendanceDate = dateTimeValue.Date;
                                }
                                else if (dateCell.Value is double dateDouble)
                                {
                                    // Excel date serial number
                                    attendanceDate = DateTime.FromOADate(dateDouble).Date;
                                }
                                else
                                {
                                    var dateStr = dateCell.Text?.Trim();
                                    if (string.IsNullOrWhiteSpace(dateStr) || 
                                        !DateTime.TryParse(dateStr, out attendanceDate))
                                    {
                                        failedRows++;
                                        errorMessages.Add($"Row {row}: Invalid Date format '{dateStr}'. Expected date format (e.g., 1-Jan-26 or 2026-01-01).");
                                        continue;
                                    }
                                    attendanceDate = attendanceDate.Date;
                                }

                                // Parse TimeIn - handle both TimeSpan and string formats
                                TimeSpan? timeIn = null;
                                if (timeInCell.Value != null)
                                {
                                    if (timeInCell.Value is TimeSpan timeSpanValue)
                                    {
                                        timeIn = timeSpanValue;
                                    }
                                    else if (timeInCell.Value is DateTime dateTimeIn)
                                    {
                                        timeIn = dateTimeIn.TimeOfDay;
                                    }
                                    else if (timeInCell.Value is double timeDouble)
                                    {
                                        // Excel time serial number (fraction of day)
                                        var timeDateTime = DateTime.FromOADate(timeDouble);
                                        timeIn = timeDateTime.TimeOfDay;
                                    }
                                    else
                                    {
                                        var timeInStr = timeInCell.Text?.Trim();
                                        if (!string.IsNullOrWhiteSpace(timeInStr))
                                        {
                                            if (TimeSpan.TryParse(timeInStr, out TimeSpan parsedTimeIn))
                                            {
                                                timeIn = parsedTimeIn;
                                            }
                                            else if (DateTime.TryParse(timeInStr, out DateTime parsedDateTime))
                                            {
                                                timeIn = parsedDateTime.TimeOfDay;
                                            }
                                            else
                                            {
                                                failedRows++;
                                                errorMessages.Add($"Row {row}: Invalid TimeIn format '{timeInStr}'. Expected time format (e.g., 9:00 or 09:00:00).");
                                                continue;
                                            }
                                        }
                                    }
                                }

                                // Parse TimeOut - handle both TimeSpan and string formats
                                TimeSpan? timeOut = null;
                                if (timeOutCell.Value != null)
                                {
                                    if (timeOutCell.Value is TimeSpan timeSpanValue)
                                    {
                                        timeOut = timeSpanValue;
                                    }
                                    else if (timeOutCell.Value is DateTime dateTimeOut)
                                    {
                                        timeOut = dateTimeOut.TimeOfDay;
                                    }
                                    else if (timeOutCell.Value is double timeDouble)
                                    {
                                        // Excel time serial number (fraction of day)
                                        var timeDateTime = DateTime.FromOADate(timeDouble);
                                        timeOut = timeDateTime.TimeOfDay;
                                    }
                                    else
                                    {
                                        var timeOutStr = timeOutCell.Text?.Trim();
                                        if (!string.IsNullOrWhiteSpace(timeOutStr))
                                        {
                                            if (TimeSpan.TryParse(timeOutStr, out TimeSpan parsedTimeOut))
                                            {
                                                timeOut = parsedTimeOut;
                                            }
                                            else if (DateTime.TryParse(timeOutStr, out DateTime parsedDateTime))
                                            {
                                                timeOut = parsedDateTime.TimeOfDay;
                                            }
                                            else
                                            {
                                                failedRows++;
                                                errorMessages.Add($"Row {row}: Invalid TimeOut format '{timeOutStr}'. Expected time format (e.g., 17:00 or 17:00:00).");
                                                continue;
                                            }
                                        }
                                    }
                                }

                                // Validate time logic
                                if (timeIn.HasValue && timeOut.HasValue && timeOut.Value <= timeIn.Value)
                                {
                                    failedRows++;
                                    errorMessages.Add($"Row {row}: TimeOut must be later than TimeIn.");
                                    continue;
                                }

                                // Upsert logic: Check if record exists for same EmployeeID and AttendanceDate
                                var existing = await _context.Attendances
                                    .FirstOrDefaultAsync(a => a.EmployeeID == employeeID && 
                                                              a.AttendanceDate.Date == attendanceDate.Date);

                                if (existing != null)
                                {
                                    // Update existing record
                                    existing.EmployeeName = employeeName;
                                    existing.DepartmentName = department;
                                    existing.TimeIn = timeIn;
                                    existing.TimeOut = timeOut;
                                    existing.Status = status;
                                    existing.Comments = null; // Leave Comments as NULL by default
                                    _context.Attendances.Update(existing);
                                    updatedRows++;
                                }
                                else
                                {
                                    // Insert new record
                                    var attendance = new Attendance
                                    {
                                        EmployeeID = employeeID,
                                        EmployeeName = employeeName,
                                        DepartmentName = department,
                                        AttendanceDate = attendanceDate,
                                        TimeIn = timeIn,
                                        TimeOut = timeOut,
                                        Status = status,
                                        Comments = null // Leave Comments as NULL by default
                                    };
                                    _context.Attendances.Add(attendance);
                                    insertedRows++;
                                }

                                successfulRows++;
                            }
                            catch (Exception ex)
                            {
                                failedRows++;
                                errorMessages.Add($"Row {row}: Error processing row - {ex.Message}");
                                // Continue processing other rows
                            }
                        }

                        // Save all changes in batch
                        await _context.SaveChangesAsync();

                        // Update upload log (if it exists)
                        if (uploadLog != null)
                        {
                            try
                            {
                                uploadLog.TotalRows = result.TotalRows;
                                uploadLog.SuccessCount = successfulRows;
                                uploadLog.FailureCount = failedRows;
                                uploadLog.ErrorDetails = string.Join("; ", errorMessages.Take(50)); // Limit error details
                                _context.AttendanceUploadLogs.Update(uploadLog);
                                await _context.SaveChangesAsync();
                                result.UploadLogId = uploadLog.Id;
                            }
                            catch (Exception)
                            {
                                // Log update failed - continue without logging
                            }
                        }

                        result.SuccessfulRows = successfulRows;
                        result.FailedRows = failedRows;
                        result.ErrorMessages = errorMessages;
                        if (uploadLog != null)
                        {
                            result.UploadLogId = uploadLog.Id;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessages.Add($"Error processing Excel file: {ex.Message}");
                if (ex.InnerException != null)
                {
                    errorMessages.Add($"Inner exception: {ex.InnerException.Message}");
                }
                if (uploadLog != null)
                {
                    try
                    {
                        uploadLog.ErrorDetails = string.Join("; ", errorMessages.Take(50));
                        uploadLog.FailureCount = failedRows;
                        _context.AttendanceUploadLogs.Update(uploadLog);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception)
                    {
                        // Log update failed - continue without logging
                    }
                }

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
