using System.Text.Json;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using Microsoft.AspNetCore.Http;

namespace HRMBT.Web.Services;

public interface IAuditLogService
{
    Task WriteAsync(
        string tableName,
        string operation,
        string? recordId,
        int? employeeId,
        object? oldData,
        object? newData,
        string? moduleName,
        CancellationToken cancellationToken = default);
}

public class AuditLogService : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task WriteAsync(
        string tableName,
        string operation,
        string? recordId,
        int? employeeId,
        object? oldData,
        object? newData,
        string? moduleName,
        CancellationToken cancellationToken = default)
    {
        var http = _httpContextAccessor.HttpContext;
        var changedBy = http?.User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(changedBy))
            changedBy = "System";

        var ip = http?.Connection?.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(ip) && http?.Request?.Headers.ContainsKey("X-Forwarded-For") == true)
            ip = http.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();

        _context.AuditLogs.Add(new AuditLog
        {
            TableName = tableName.Length <= 128 ? tableName : tableName[..128],
            Operation = operation.Length <= 20 ? operation : operation[..20],
            RecordId = string.IsNullOrWhiteSpace(recordId) ? null : (recordId.Length <= 100 ? recordId : recordId[..100]),
            EmployeeId = employeeId,
            OldData = oldData == null ? null : JsonSerializer.Serialize(oldData, JsonOptions),
            NewData = newData == null ? null : JsonSerializer.Serialize(newData, JsonOptions),
            ChangedBy = changedBy.Length <= 128 ? changedBy : changedBy[..128],
            ChangedAt = DateTime.Now,
            ModuleName = string.IsNullOrWhiteSpace(moduleName)
                ? null
                : (moduleName.Length <= 100 ? moduleName : moduleName[..100]),
            IPAddress = string.IsNullOrWhiteSpace(ip)
                ? null
                : (ip.Length <= 50 ? ip : ip[..50])
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
