using System.Linq.Expressions;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Services;

public static class RecordNoteQueries
{
    /// <summary>The notes shown for one record, newest first. "Edited by" comes from the audit log.</summary>
    public static async Task<List<RecordNoteVm>> LoadNotesAsync(this ApplicationDbContext db, Expression<Func<RecordNote, bool>> record)
    {
        var notes = await db.RecordNotes.AsNoTracking()
            .Where(record).Where(n => !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Select(n => new RecordNoteVm { Id = n.Id, Note = n.Note, CreatedAt = n.CreatedAt, CreatedBy = n.CreatedBy })
            .ToListAsync();
        var ids = notes.Select(n => n.Id.ToString()).ToList();
        var edits = (await db.AuditLogs.AsNoTracking()
                .Where(a => a.TableName == "RecordNotes" && a.Operation == "UPDATE" && ids.Contains(a.RecordId!))
                .Select(a => new { a.RecordId, a.ChangedAt, a.ChangedBy })
                .ToListAsync())
            .GroupBy(a => a.RecordId)
            .ToDictionary(g => g.Key!, g => g.OrderByDescending(a => a.ChangedAt).First());
        foreach (var n in notes)
            if (edits.TryGetValue(n.Id.ToString(), out var edit))
                (n.EditedAt, n.EditedBy) = (edit.ChangedAt, edit.ChangedBy);
        return notes;
    }
}
