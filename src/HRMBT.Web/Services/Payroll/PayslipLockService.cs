using HRMBT.Web.Data;
using HRMBT.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Services.Payroll;

public interface IPayslipLockService
{
    /// <summary>
    /// Locks all unlocked payslips for the employee so stale payroll cannot be paid
    /// after allowance/deduction master data changes. Writes an AuditLogs row when any are locked.
    /// </summary>
    /// <returns>Number of payslips newly locked.</returns>
    Task<int> LockEmployeePayslipsAsync(
        int employeeId,
        string? moduleName = null,
        CancellationToken cancellationToken = default);
}

public class PayslipLockService : IPayslipLockService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLog;

    public PayslipLockService(ApplicationDbContext context, IAuditLogService auditLog)
    {
        _context = context;
        _auditLog = auditLog;
    }

    public async Task<int> LockEmployeePayslipsAsync(
        int employeeId,
        string? moduleName = null,
        CancellationToken cancellationToken = default)
    {
        if (employeeId <= 0)
            return 0;

        var unlocked = await _context.Payslips
            .Where(p => p.EmployeeId == employeeId && !p.IsLocked)
            .Select(p => new { p.Id, p.Month, p.Year })
            .ToListAsync(cancellationToken);

        if (unlocked.Count == 0)
            return 0;

        var ids = unlocked.Select(p => p.Id).ToList();
        var now = DateTime.Now;

        await _context.Payslips
            .Where(p => ids.Contains(p.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.IsLocked, true)
                    .SetProperty(p => p.LockedDate, now),
                cancellationToken);

        await _auditLog.WriteAsync(
            tableName: "Payslips",
            operation: "LOCK",
            recordId: string.Join(",", ids),
            employeeId: employeeId,
            oldData: new { IsLocked = false, PayslipIds = ids },
            newData: new
            {
                IsLocked = true,
                LockedDate = now.ToString("o"),
                Payslips = unlocked.Select(p => new { p.Id, p.Month, p.Year }).ToList()
            },
            moduleName: string.IsNullOrWhiteSpace(moduleName) ? "Payroll" : moduleName,
            cancellationToken);

        return unlocked.Count;
    }
}
