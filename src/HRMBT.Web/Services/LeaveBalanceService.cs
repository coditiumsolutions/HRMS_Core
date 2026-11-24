using HRMBT.Web.Data;
using HRMBT.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HRMBT.Web.Services;

public class LeaveTypeBalance
{
    public int LeaveTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Remaining { get; set; }
    public decimal Entitled { get; set; }
    public decimal BaseDays { get; set; }
    public decimal Adjustment { get; set; }
    public decimal CarryForward { get; set; }
    public decimal Taken { get; set; }
    public decimal Pending { get; set; }
}

public class LeaveHistoryYear
{
    public int Year { get; set; }
    /// <summary>One entry per leave type column of the history table (same order as <see cref="EmployeeLeaveBalanceViewModel.HistoryTypes"/>); null when the type has no data that year.</summary>
    public IReadOnlyList<LeaveTypeBalance?> Cells { get; set; } = Array.Empty<LeaveTypeBalance?>();
}

public class EmployeeLeaveBalanceViewModel
{
    public int EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    /// <summary>False when leave is not managed for this employee's project (for example Dastarkhuwans).</summary>
    public bool Managed { get; set; }
    public int Year { get; set; }
    public IReadOnlyList<LeaveTypeBalance> Current { get; set; } = Array.Empty<LeaveTypeBalance>();
    public IReadOnlyList<string> HistoryTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<LeaveHistoryYear> History { get; set; } = Array.Empty<LeaveHistoryYear>();
}

public class RolloverRow
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int LeaveTypeId { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public decimal Remaining { get; set; }
    /// <summary>What the rollover writes: max(remaining, 0), limited by the leave type's cap.</summary>
    public decimal Amount { get; set; }
    public decimal? Existing { get; set; }
    /// <summary>New, Update, Unchanged, or Manual (a hand-entered carry-forward row exists and is never touched).</summary>
    public string Action { get; set; } = string.Empty;
}

/// <summary>
/// The one place that reads leave balances (dbo.vLeaveBalances, db_changes.md). The employee Leave Balance tab and the LMS Balances page both use it,
/// so they cannot disagree. Leave is tracked in one decimal (half days = 0.5).
/// </summary>
public class LeaveBalanceService
{
    private readonly ApplicationDbContext _db;
    public LeaveBalanceService(ApplicationDbContext db) => _db = db;

    public async Task<EmployeeLeaveBalanceViewModel> BuildAsync(Employee e, int? year = null, CancellationToken ct = default)
    {
        var y = year ?? DateTime.Now.Year;
        var vm = new EmployeeLeaveBalanceViewModel { EmployeeId = e.uid, EmployeeCode = e.EmployeeID, Year = y };

        var projectManaged = await _db.Projects.AsNoTracking().Where(p => p.Id == e.ProjectId).Select(p => p.HasLeaveManagement).FirstOrDefaultAsync(ct);
        vm.Managed = e.IsLeaveManagedOverride ?? projectManaged;
        if (!vm.Managed) return vm;

        var rows = await _db.LeaveBalances.AsNoTracking().Where(r => r.EmployeeId == e.uid).ToListAsync(ct);
        var types = await _db.LeaveTypes.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToDictionaryAsync(t => t.Id, ct);
        var order = types.Values.Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i);

        LeaveTypeBalance Map(LeaveBalanceRow r) => new()
        {
            LeaveTypeId = r.LeaveTypeId, Name = types.TryGetValue(r.LeaveTypeId, out var t) ? t.Name : "Leave type " + r.LeaveTypeId,
            Remaining = r.Remaining, Entitled = r.Entitled, BaseDays = r.BaseDays, Adjustment = r.Adjustment, CarryForward = r.CarryForward, Taken = r.Taken, Pending = r.Pending
        };
        int Ord(int typeId) => order.TryGetValue(typeId, out var i) ? i : int.MaxValue;

        vm.Current = rows.Where(r => r.Year == y).OrderBy(r => Ord(r.LeaveTypeId)).Select(Map).ToList();

        var past = rows.Where(r => r.Year < y).ToList();
        var typeIds = past.Select(r => r.LeaveTypeId).Distinct().OrderBy(Ord).ToList();
        vm.HistoryTypes = typeIds.Select(id => types.TryGetValue(id, out var t) ? t.Name : "Leave type " + id).ToList();
        vm.History = past.GroupBy(r => r.Year).OrderByDescending(g => g.Key)
            .Select(g => new LeaveHistoryYear
            {
                Year = g.Key,
                Cells = typeIds.Select(id => g.FirstOrDefault(r => r.LeaveTypeId == id) is { } r ? Map(r) : null).ToList()
            }).ToList();
        return vm;
    }

    /// <summary>Balances of the given employees for one year (the LMS Balances page).</summary>
    public async Task<(List<LeaveBalanceRow> Rows, Dictionary<int, string> TypeNames)> ForYearAsync(int year, IReadOnlyCollection<int> employeeIds, CancellationToken ct = default)
    {
        var rows = await _db.LeaveBalances.AsNoTracking().Where(r => r.Year == year && employeeIds.Contains(r.EmployeeId)).ToListAsync(ct);
        var names = await _db.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        return (rows, names);
    }

    public const string RolloverMarker = "Year-end rollover";

    /// <summary>Rows the year-end rollover from <paramref name="fromYear"/> into the next year would write (nothing is saved).</summary>
    public async Task<List<RolloverRow>> PreviewRolloverAsync(int fromYear, CancellationToken ct = default)
    {
        var rows = await (from r in _db.LeaveBalances.AsNoTracking()
                          join lt in _db.LeaveTypes.AsNoTracking() on r.LeaveTypeId equals lt.Id
                          join e in _db.Employees.AsNoTracking() on r.EmployeeId equals e.uid
                          where r.Year == fromYear && lt.AllowsCarryForward && e.EmployeeStatus == "Active"
                          orderby e.EmployeeID, lt.SortOrder, lt.Name
                          select new { r.EmployeeId, e.EmployeeID, e.EmployeeName, TypeId = lt.Id, TypeName = lt.Name, lt.MaxCarryForwardDays, r.Remaining }).ToListAsync(ct);

        var existing = await _db.CarryforwardLeaves.AsNoTracking().Where(c => c.FromYear == fromYear)
            .ToDictionaryAsync(c => (c.EmployeeId, c.LeaveTypeId), ct);

        var result = new List<RolloverRow>();
        foreach (var x in rows)
        {
            var amount = Math.Max(x.Remaining, 0m);
            if (x.MaxCarryForwardDays.HasValue) amount = Math.Min(amount, x.MaxCarryForwardDays.Value);
            existing.TryGetValue((x.EmployeeId, x.TypeId), out var ex);
            string action;
            if (ex == null) action = amount > 0 ? "New" : "";
            else if (!string.Equals(ex.Description, RolloverMarker, StringComparison.Ordinal)) action = "Manual";
            else action = ex.CarryForwardDays == amount ? "Unchanged" : "Update";
            if (action == "") continue;   // nothing to carry and nothing recorded
            result.Add(new RolloverRow
            {
                EmployeeId = x.EmployeeId, EmployeeCode = x.EmployeeID, EmployeeName = x.EmployeeName, LeaveTypeId = x.TypeId, LeaveType = x.TypeName,
                Remaining = x.Remaining, Amount = amount, Existing = ex?.CarryForwardDays, Action = action
            });
        }
        return result;
    }

    /// <summary>Writes the preview: inserts missing rows, updates rows an earlier rollover created, never touches hand-entered rows.</summary>
    public async Task<(int Inserted, int Updated, int Skipped)> ApplyRolloverAsync(int fromYear, string? actor, CancellationToken ct = default)
    {
        var preview = await PreviewRolloverAsync(fromYear, ct);
        var existing = await _db.CarryforwardLeaves.Where(c => c.FromYear == fromYear).ToDictionaryAsync(c => (c.EmployeeId, c.LeaveTypeId), ct);
        int ins = 0, upd = 0, skip = 0;
        foreach (var r in preview)
        {
            switch (r.Action)
            {
                case "New":
                    _db.CarryforwardLeaves.Add(new CarryforwardLeave { EmployeeId = r.EmployeeId, LeaveTypeId = r.LeaveTypeId, FromYear = fromYear, ToYear = fromYear + 1, CarryForwardDays = r.Amount, Description = RolloverMarker });
                    ins++; break;
                case "Update":
                    existing[(r.EmployeeId, r.LeaveTypeId)].CarryForwardDays = r.Amount; upd++; break;
                default:
                    skip++; break;
            }
        }
        await _db.SaveChangesAsync(ct);
        return (ins, upd, skip);
    }
}
