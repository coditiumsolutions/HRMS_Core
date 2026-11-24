using HRMBT.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMBT.Web.Controllers;

/// <summary>Record or void a key employment event (promotion, transfer, resignation...) from Edit Employee > Employee History.</summary>
[Route("employees/{id:int}/events")]
public class EmployeeEventsController : Controller
{
    private readonly EmployeeEventService _events;
    private readonly IAuditLogService _audit;
    public EmployeeEventsController(EmployeeEventService events, IAuditLogService audit) { _events = events; _audit = audit; }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Record(int id, EmployeeEventInput input)
    {
        var (ok, error) = await _events.RecordAsync(id, input, User.Identity?.Name);
        if (ok) TempData["EventOk"] = "Event recorded."; else TempData["EventError"] = error;
        return RedirectToAction("Edit", "Employee", new { id, tab = "history" });
    }

    [HttpPost("{eventId:int}/void")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(int id, int eventId, string? reason)
    {
        var (ok, message) = await _events.VoidAsync(id, eventId, reason, User.Identity?.Name);
        if (ok) TempData["EventOk"] = message; else TempData["EventError"] = message;
        return RedirectToAction("Edit", "Employee", new { id, tab = "history" });
    }

    /// <summary>Admin only: remove a voided entry for good (it disappears from the history). Logged in AuditLogs.</summary>
    [HttpPost("{eventId:int}/delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteVoided(int id, int eventId)
    {
        var (ok, message, removed) = await _events.DeleteVoidedAsync(id, eventId);
        if (ok && removed != null)
            await _audit.WriteAsync("EmployeeEvents", "Delete", eventId.ToString(), id, removed, null, "Employees");
        if (ok) TempData["EventOk"] = message; else TempData["EventError"] = message;
        return RedirectToAction("Edit", "Employee", new { id, tab = "history" });
    }
}
