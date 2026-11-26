using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Controllers;

/// <summary>
/// Dated notes against a record (decision T7): Admin and HR write, edit and remove them.
/// A removed note is kept (IsDeleted) and every change is in AuditLogs. Notes never affect days, balances or pay.
/// Each kind of record that takes notes has its own route and says which column of RecordNotes it fills.
/// </summary>
[Authorize(Roles = "Admin,HR")]
public abstract class RecordNotesControllerBase : Controller
{
    private const int MaxLength = 1000;

    protected readonly ApplicationDbContext Db;
    private readonly IAuditLogService _audit;

    protected RecordNotesControllerBase(ApplicationDbContext context, IAuditLogService audit)
    {
        Db = context;
        _audit = audit;
    }

    protected abstract string Module { get; }
    /// <summary>TempData keys the record's page shows.</summary>
    protected virtual string SuccessKey => "SuccessMessage";
    protected virtual string ErrorKey => "ErrorMessage";

    /// <summary>The employee the record belongs to, or null when there is no such record.</summary>
    protected abstract Task<int?> EmployeeOfAsync(int recordId);
    protected abstract void Attach(RecordNote note, int recordId);
    protected abstract IQueryable<RecordNote> NotesOf(int recordId);
    protected abstract IActionResult Back(int recordId);

    /// <summary>The trimmed note, or null with a message set when it cannot be saved.</summary>
    private string? Clean(string? note)
    {
        var text = note?.Replace("\r\n", "\n").Trim();
        if (string.IsNullOrEmpty(text))
            TempData[ErrorKey] = "Write the note first.";
        else if (text.Length > MaxLength)
            TempData[ErrorKey] = $"A note can be at most {MaxLength} characters.";
        else
            return text;
        return null;
    }

    private Task AuditAsync(string operation, int noteId, int employeeId, object? before, object? after) =>
        _audit.WriteAsync("RecordNotes", operation, noteId.ToString(), employeeId, before, after, Module);

    private Task<RecordNote?> FindAsync(int recordId, int noteId) =>
        NotesOf(recordId).FirstOrDefaultAsync(n => n.Id == noteId && !n.IsDeleted);

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int recordId, string? note, Guid requestId)
    {
        if (await EmployeeOfAsync(recordId) is not { } employeeId) return NotFound();
        if (Clean(note) is not { } text) return Back(recordId);
        // the form's key: a double click or a reload does not write the note twice
        if (requestId != Guid.Empty && await Db.RecordNotes.AnyAsync(n => n.RequestId == requestId))
            return Back(recordId);

        var row = new RecordNote { Note = text, CreatedBy = User.Identity?.Name, RequestId = requestId == Guid.Empty ? null : requestId };
        Attach(row, recordId);
        Db.RecordNotes.Add(row);
        await Db.SaveChangesAsync();
        await AuditAsync("INSERT", row.Id, employeeId, null, new { row.EmployeeId, row.LeaveRequestId, row.Note });
        TempData[SuccessKey] = "Note added.";
        return Back(recordId);
    }

    [HttpPost("{noteId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int recordId, int noteId, string? note)
    {
        if (await EmployeeOfAsync(recordId) is not { } employeeId) return NotFound();
        var row = await FindAsync(recordId, noteId);
        if (row == null) return NotFound();
        if (Clean(note) is not { } text || text == row.Note) return Back(recordId);

        var before = new { row.Note };
        row.Note = text;
        await Db.SaveChangesAsync();
        await AuditAsync("UPDATE", row.Id, employeeId, before, new { row.Note });
        TempData[SuccessKey] = "Note updated.";
        return Back(recordId);
    }

    [HttpPost("{noteId:int}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int recordId, int noteId)
    {
        if (await EmployeeOfAsync(recordId) is not { } employeeId) return NotFound();
        if (await FindAsync(recordId, noteId) is { } row)
        {
            row.IsDeleted = true;
            await Db.SaveChangesAsync();
            await AuditAsync("DELETE", row.Id, employeeId, new { row.Note }, new { row.IsDeleted });
        }
        TempData[SuccessKey] = "Note removed.";
        return Back(recordId);
    }
}

[Route("leave/{recordId:int}/notes")]
public class LeaveNotesController : RecordNotesControllerBase
{
    public LeaveNotesController(ApplicationDbContext context, IAuditLogService audit) : base(context, audit) { }

    protected override string Module => "LMS";

    protected override Task<int?> EmployeeOfAsync(int recordId) =>
        Db.LeaveRequests.AsNoTracking().Where(l => l.Id == recordId).Select(l => (int?)l.EmployeeId).FirstOrDefaultAsync();

    protected override void Attach(RecordNote note, int recordId) => note.LeaveRequestId = recordId;
    protected override IQueryable<RecordNote> NotesOf(int recordId) => Db.RecordNotes.Where(n => n.LeaveRequestId == recordId);
    protected override IActionResult Back(int recordId) => RedirectToAction("Details", "LMS", new { id = recordId }, "notes");
}

[Route("employees/{recordId:int}/notes")]
public class EmployeeNotesController : RecordNotesControllerBase
{
    public EmployeeNotesController(ApplicationDbContext context, IAuditLogService audit) : base(context, audit) { }

    protected override string Module => "Employees";
    protected override string SuccessKey => "NoteOk";
    protected override string ErrorKey => "NoteError";

    protected override Task<int?> EmployeeOfAsync(int recordId) =>
        Db.Employees.AsNoTracking().Where(e => e.uid == recordId).Select(e => (int?)e.uid).FirstOrDefaultAsync();

    protected override void Attach(RecordNote note, int recordId) => note.EmployeeId = recordId;
    protected override IQueryable<RecordNote> NotesOf(int recordId) => Db.RecordNotes.Where(n => n.EmployeeId == recordId);
    protected override IActionResult Back(int recordId) => RedirectToAction("Details", "Employee", new { id = recordId, tab = "notes" });
}
