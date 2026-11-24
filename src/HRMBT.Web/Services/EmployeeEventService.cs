using HRMBT.Web.Data;
using HRMBT.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Services;

/// <summary>Input for a new employee event (the Edit Employee > Employee History form).</summary>
public class EmployeeEventInput
{
    public string EventType { get; set; } = string.Empty;
    public DateTime? EventDate { get; set; }
    public int? ToProjectId { get; set; }
    public int? ToDepartmentId { get; set; }
    public int? ToDesignationId { get; set; }
    public string? Remarks { get; set; }
    public Guid? RequestId { get; set; }
}

public class EmployeeEventRow
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public bool IsVoided { get; set; }
    public string? VoidReason { get; set; }
    public string? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public bool CanVoid { get; set; }
}

public class EmployeeHistoryViewModel
{
    public int EmployeeId { get; set; }
    public bool CanRecord { get; set; }
    public bool HasLiveJoined { get; set; }
    /// <summary>True when the employee has left and not come back (Rejoin is then the only way back).</summary>
    public bool IsCurrentlyLeft { get; set; }
    public DateTime? LastEventDate { get; set; }
    public string CurrentPosition { get; set; } = string.Empty;
    public IReadOnlyList<EmployeeEventRow> Events { get; set; } = Array.Empty<EmployeeEventRow>();
}

/// <summary>
/// Records key employment events and applies their effect to Employee in the same transaction (decisions B14/T1):
/// Promotion/Transfer change designation/department/project; Resignation/Termination set DateOfLeaving and status and clear DateOfReturn
/// (Employee always describes the latest stint; older stints live in this timeline); Rejoin sets DateOfReturn and Active.
/// </summary>
public class EmployeeEventService
{
    private readonly ApplicationDbContext _db;
    public EmployeeEventService(ApplicationDbContext db) => _db = db;

    public static bool IsLeft(Employee e) => e.DateOfLeaving.HasValue && !e.DateOfReturn.HasValue;

    /// <summary>Adds the Joined row for a brand-new employee (call after the employee row exists; the caller saves).</summary>
    public void AddJoined(Employee e, string? actor)
    {
        if (!e.DateOfJoining.HasValue) return;
        _db.EmployeeEvents.Add(new EmployeeEvent
        {
            EmployeeId = e.uid, EventType = EmployeeEvent.Joined, EventDate = e.DateOfJoining.Value.Date,
            ToProjectId = e.ProjectId, ToDepartmentId = e.DepartmentId, ToDesignationId = e.DesignationId, CreatedBy = actor
        });
    }

    public async Task<(bool Ok, string? Error)> RecordAsync(int employeeId, EmployeeEventInput input, string? actor, CancellationToken ct = default)
    {
        var type = input.EventType ?? string.Empty;
        if (!EmployeeEvent.Recordable.Contains(type)) return (false, "Choose the type of event.");
        if (!input.EventDate.HasValue) return (false, "Enter the date the event took effect.");
        var date = input.EventDate.Value.Date;
        var remarks = string.IsNullOrWhiteSpace(input.Remarks) ? null : input.Remarks.Trim();
        if (remarks is { Length: > 1000 }) return (false, "Remarks can be at most 1000 characters.");

        var e = await _db.Employees.Include(x => x.DesignationInfo).Include(x => x.DepartmentInfo).Include(x => x.ProjectInfo)
            .FirstOrDefaultAsync(x => x.uid == employeeId, ct);
        if (e == null) return (false, "Employee not found.");

        // the same form submitted twice: already recorded
        if (input.RequestId.HasValue && await _db.EmployeeEvents.AnyAsync(v => v.RequestId == input.RequestId, ct)) return (true, null);

        if (e.DateOfJoining.HasValue && date < e.DateOfJoining.Value.Date) return (false, "The date cannot be before the joining date.");
        var last = await _db.EmployeeEvents.Where(v => v.EmployeeId == employeeId && !v.IsVoided).MaxAsync(v => (DateTime?)v.EventDate, ct);
        if (type != EmployeeEvent.Other && last.HasValue && date < last.Value.Date)
            return (false, "Events are recorded in date order: the latest entry is dated " + last.Value.ToString("dd/MM/yyyy") + ". Use a later date, or void that entry first.");

        var ev = new EmployeeEvent
        {
            EmployeeId = employeeId, EventType = type, EventDate = date, Remarks = remarks, CreatedBy = actor, RequestId = input.RequestId,
            FromProjectId = e.ProjectId, FromDepartmentId = e.DepartmentId, FromDesignationId = e.DesignationId
        };

        switch (type)
        {
            case EmployeeEvent.Promotion:
                if (!input.ToDesignationId.HasValue) return (false, "Choose the new designation.");
                if (IsLeft(e)) return (false, "This employee has left. Record a Rejoin first.");
                if (input.ToDesignationId == e.DesignationId) return (false, "The new designation is the same as the current one.");
                if (!await _db.Designations.AnyAsync(d => d.Id == input.ToDesignationId && d.IsActive, ct)) return (false, "Choose an active designation.");
                ev.ToDesignationId = input.ToDesignationId;
                e.DesignationId = input.ToDesignationId;
                break;

            case EmployeeEvent.Transfer:
                if (IsLeft(e)) return (false, "This employee has left. Record a Rejoin first.");
                var newProject = input.ToProjectId.HasValue && input.ToProjectId != e.ProjectId ? input.ToProjectId : null;
                var newDept = input.ToDepartmentId.HasValue && input.ToDepartmentId != e.DepartmentId ? input.ToDepartmentId : null;
                var newDesig = input.ToDesignationId.HasValue && input.ToDesignationId != e.DesignationId ? input.ToDesignationId : null;
                if (newProject == null && newDept == null && newDesig == null) return (false, "Choose at least one new project, department or designation that differs from the current one.");
                if (newProject != null && !await _db.Projects.AnyAsync(p => p.Id == newProject && p.IsActive, ct)) return (false, "Choose an active project.");
                if (newDept != null && !await _db.Departments.AnyAsync(d => d.DepartmentID == newDept && d.IsActive, ct)) return (false, "Choose an active department.");
                if (newDesig != null && !await _db.Designations.AnyAsync(d => d.Id == newDesig && d.IsActive, ct)) return (false, "Choose an active designation.");
                ev.ToProjectId = newProject; ev.ToDepartmentId = newDept; ev.ToDesignationId = newDesig;
                if (newProject != null) e.ProjectId = newProject.Value;
                if (newDept != null) { e.DepartmentId = newDept.Value; e.SubDepartmentId = null; }   // a sub-department belongs to one department
                if (newDesig != null) e.DesignationId = newDesig;
                break;

            case EmployeeEvent.Resignation:
            case EmployeeEvent.Termination:
                if (IsLeft(e)) return (false, "This employee has already left. Record a Rejoin before another resignation or termination.");
                e.DateOfLeaving = date;
                e.DateOfReturn = null;   // Employee describes the latest stint; the earlier return stays in the timeline
                e.EmployeeStatus = type == EmployeeEvent.Resignation ? "Resigned" : "Terminated";
                break;

            case EmployeeEvent.Rejoin:
                if (!IsLeft(e)) return (false, "Only an employee who has resigned or been terminated can rejoin.");
                if (date <= e.DateOfLeaving!.Value.Date) return (false, "The rejoin date must be after the leaving date.");
                e.DateOfReturn = date;
                e.EmployeeStatus = "Active";
                break;

            case EmployeeEvent.Other:
                if (remarks == null) return (false, "Describe the event in the remarks.");
                break;
        }

        if (type != EmployeeEvent.Other)
        {
            e.ModifiedOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            e.ModifiedBy = actor;
        }
        _db.EmployeeEvents.Add(ev);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return (false, "This employee was changed by someone else a moment ago. Reload the page and try again."); }
        return (true, null);
    }

    /// <summary>
    /// Voids the LATEST live entry and reverts its effect on Employee (decision T8): a promotion or transfer restores the position it replaced,
    /// a resignation, termination or rejoin is undone by replaying the remaining entries. Older entries cannot be voided first, so the
    /// timeline and the employee record keep agreeing. A field that was changed by hand since the event is left alone and reported.
    /// </summary>
    public async Task<(bool Ok, string? Message)> VoidAsync(int employeeId, int eventId, string? reason, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "Give a reason for voiding the entry.");
        var ev = await _db.EmployeeEvents.FirstOrDefaultAsync(v => v.Id == eventId && v.EmployeeId == employeeId, ct);
        if (ev == null) return (false, "Entry not found.");
        if (ev.IsVoided) return (true, "Entry was already voided.");

        var live = await _db.EmployeeEvents.Where(v => v.EmployeeId == employeeId && !v.IsVoided).ToListAsync(ct);
        var latest = live.OrderByDescending(v => v.EventDate).ThenByDescending(v => v.Id).First();
        if (latest.Id != ev.Id) return (false, "Only the latest entry can be voided, so the history stays consistent. Void the newer entries first.");

        var e = await _db.Employees.FirstOrDefaultAsync(x => x.uid == employeeId, ct);
        if (e == null) return (false, "Employee not found.");

        var notes = new List<string>();
        switch (ev.EventType)
        {
            case EmployeeEvent.Promotion:
                if (e.DesignationId == ev.ToDesignationId) e.DesignationId = ev.FromDesignationId;
                else notes.Add("the designation was changed by hand since, so it was left as it is");
                break;

            case EmployeeEvent.Transfer:
                if (ev.ToProjectId.HasValue)
                {
                    if (e.ProjectId == ev.ToProjectId) e.ProjectId = ev.FromProjectId ?? e.ProjectId;
                    else notes.Add("the project was left as it is");
                }
                if (ev.ToDepartmentId.HasValue)
                {
                    if (e.DepartmentId == ev.ToDepartmentId)
                    {
                        e.DepartmentId = ev.FromDepartmentId ?? e.DepartmentId;
                        e.SubDepartmentId = null;
                        notes.Add("choose the sub-department again if the employee had one");
                    }
                    else notes.Add("the department was left as it is");
                }
                if (ev.ToDesignationId.HasValue)
                {
                    if (e.DesignationId == ev.ToDesignationId) e.DesignationId = ev.FromDesignationId;
                    else notes.Add("the designation was left as it is");
                }
                break;

            case EmployeeEvent.Resignation:
            case EmployeeEvent.Termination:
            case EmployeeEvent.Rejoin:
                // replay what is left: the latest leaving / return entry decides the dates and the status
                var rest = live.Where(v => v.Id != ev.Id).ToList();
                var lastLeave = rest.Where(v => v.EventType is EmployeeEvent.Resignation or EmployeeEvent.Termination)
                    .OrderBy(v => v.EventDate).ThenBy(v => v.Id).LastOrDefault();
                var lastRejoin = rest.Where(v => v.EventType == EmployeeEvent.Rejoin)
                    .OrderBy(v => v.EventDate).ThenBy(v => v.Id).LastOrDefault();
                if (lastLeave == null)
                {
                    e.DateOfLeaving = null;
                    e.DateOfReturn = null;
                    if (e.EmployeeStatus is "Resigned" or "Terminated") e.EmployeeStatus = "Active";
                }
                else if (lastRejoin != null && (lastRejoin.EventDate > lastLeave.EventDate || (lastRejoin.EventDate == lastLeave.EventDate && lastRejoin.Id > lastLeave.Id)))
                {
                    e.DateOfLeaving = lastLeave.EventDate;
                    e.DateOfReturn = lastRejoin.EventDate;
                    e.EmployeeStatus = "Active";
                }
                else
                {
                    e.DateOfLeaving = lastLeave.EventDate;
                    e.DateOfReturn = null;
                    e.EmployeeStatus = lastLeave.EventType == EmployeeEvent.Resignation ? "Resigned" : "Terminated";
                }
                break;
        }

        var text = reason.Trim();
        ev.IsVoided = true;
        ev.VoidReason = text.Length > 500 ? text[..500] : text;
        ev.VoidedAt = DateTime.Now;
        ev.VoidedBy = actor;
        if (ev.EventType != EmployeeEvent.Other && ev.EventType != EmployeeEvent.Joined)
        {
            e.ModifiedOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            e.ModifiedBy = actor;
        }
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return (false, "This employee was changed by someone else a moment ago. Reload the page and try again."); }
        return (true, notes.Count == 0 ? "Entry voided and its change to the employee was reverted." : "Entry voided and reverted; note: " + string.Join("; ", notes) + ".");
    }

    /// <summary>Admin only (the controller enforces it): permanently removes a voided entry so it no longer appears in the history.</summary>
    public async Task<(bool Ok, string? Message, EmployeeEvent? Removed)> DeleteVoidedAsync(int employeeId, int eventId, CancellationToken ct = default)
    {
        var ev = await _db.EmployeeEvents.FirstOrDefaultAsync(v => v.Id == eventId && v.EmployeeId == employeeId, ct);
        if (ev == null) return (false, "Entry not found.", null);
        if (!ev.IsVoided) return (false, "Only a voided entry can be deleted. Void it first.", null);
        _db.EmployeeEvents.Remove(ev);
        await _db.SaveChangesAsync(ct);
        return (true, "Voided entry deleted.", ev);
    }

    public async Task<EmployeeHistoryViewModel> BuildAsync(Employee e, bool canRecord, CancellationToken ct = default)
    {
        var raw = await _db.EmployeeEvents.AsNoTracking().Where(v => v.EmployeeId == e.uid)
            .OrderByDescending(v => v.EventDate).ThenByDescending(v => v.Id).ToListAsync(ct);

        var projects = await _db.Projects.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var depts = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.DepartmentID, d => d.DepartmentName ?? "", ct);
        var desigs = await _db.Designations.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        static string N(Dictionary<int, string> map, int? id) => id.HasValue && map.TryGetValue(id.Value, out var n) ? n : "";
        static string Change(string label, Dictionary<int, string> map, int? from, int? to) =>
            to.HasValue ? label + ": " + (N(map, from) is { Length: > 0 } f ? f : "—") + " → " + N(map, to) : "";

        var live = raw.Where(v => !v.IsVoided).ToList();
        var latestLiveId = live.Count == 0 ? (int?)null : live.OrderByDescending(v => v.EventDate).ThenByDescending(v => v.Id).First().Id;

        var rows = raw.Select(v =>
        {
            string summary = v.EventType switch
            {
                EmployeeEvent.Joined => string.Join(" · ", new[] { N(desigs, v.ToDesignationId), N(depts, v.ToDepartmentId), N(projects, v.ToProjectId) }.Where(s => s.Length > 0)),
                EmployeeEvent.Promotion => Change("Designation", desigs, v.FromDesignationId, v.ToDesignationId),
                EmployeeEvent.Transfer => string.Join(" · ", new[]
                {
                    Change("Project", projects, v.FromProjectId, v.ToProjectId),
                    Change("Department", depts, v.FromDepartmentId, v.ToDepartmentId),
                    Change("Designation", desigs, v.FromDesignationId, v.ToDesignationId)
                }.Where(s => s.Length > 0)),
                EmployeeEvent.Resignation or EmployeeEvent.Termination => "Last working day",
                EmployeeEvent.Rejoin => "Returned to work",
                _ => ""
            };
            return new EmployeeEventRow
            {
                Id = v.Id, EventType = v.EventType, EventDate = v.EventDate, Summary = summary, Remarks = v.Remarks,
                IsVoided = v.IsVoided, VoidReason = v.VoidReason, VoidedBy = v.VoidedBy, VoidedAt = v.VoidedAt,
                CreatedAt = v.CreatedAt, CreatedBy = v.CreatedBy, CanVoid = canRecord && !v.IsVoided && v.Id == latestLiveId
            };
        }).ToList();

        return new EmployeeHistoryViewModel
        {
            EmployeeId = e.uid,
            CanRecord = canRecord,
            HasLiveJoined = live.Any(v => v.EventType == EmployeeEvent.Joined),
            IsCurrentlyLeft = IsLeft(e),
            LastEventDate = live.Count == 0 ? null : live.Max(v => v.EventDate),
            CurrentPosition = string.Join(" · ", new[] { e.Designation, e.Department, e.Project }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Events = rows
        };
    }
}
