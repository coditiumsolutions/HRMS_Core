using HRMBT.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;

namespace HRMBT.Web.Controllers
{
[Route("leave")]
    public class LMSController : Controller
    {
        private const string LeaveTypesConfigKey = "LeaveTypes";
        private const string DepartmentConfigKey = "Department";

        private readonly ApplicationDbContext _context;
        private readonly HRMBT.Web.Services.LeaveBalanceService _leaveBalances;
        private readonly HRMBT.Web.Services.IAuditLogService _audit;

        public LMSController(ApplicationDbContext context, HRMBT.Web.Services.LeaveBalanceService leaveBalances, HRMBT.Web.Services.IAuditLogService audit)
        {
            _context = context;
            _leaveBalances = leaveBalances;
            _audit = audit;
        }

        private static IEnumerable<string> SplitConfigCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) yield break;
            foreach (var item in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(item))
                    yield return item;
            }
        }

        private static bool ConfigKeyMatches(string? key, string match) =>
            !string.IsNullOrWhiteSpace(key) && string.Equals(key.Trim(), match, StringComparison.OrdinalIgnoreCase);

        /// <summary>Active leave type names from dbo.LeaveTypes.</summary>
        private async Task<List<string>> GetLeaveTypeNamesAsync()
        {
            return await _context.LeaveTypes.AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
                .Select(t => t.Name)
                .ToListAsync();
        }

        /// <summary>Resolves a leave type name to its LeaveTypes.Id (null when there is no such type).</summary>
        private async Task<int?> ResolveLeaveTypeIdAsync(string? name)
        {
            var n = name?.Trim();
            if (string.IsNullOrEmpty(n)) return null;
            var id = await _context.LeaveTypes.AsNoTracking()
                .Where(t => t.Name == n)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync();
            return id;
        }

        /// <summary>Department names from dbo.Departments.</summary>
        private async Task<List<string>> GetDepartmentsFromConfigurationAsync()
        {
            return await _context.Departments.AsNoTracking()
                .Where(d => d.DepartmentName != null && d.IsActive)
                .OrderBy(d => d.DepartmentName)
                .Select(d => d.DepartmentName!)
                .ToListAsync();
        }

        /// <summary>
        /// An employee cannot have two open (Pending or Approved) leave requests covering the same day.
        /// Returns a message naming the conflicting request, or null. Rejected and Cancelled requests never conflict.
        /// </summary>
        private async Task<string?> FindLeaveOverlapAsync(int employeeUid, DateTime from, DateTime to, string? status, int? ignoreRequestId)
        {
            if (!LeaveRequestCountsAsAvailed(status))
                return null;

            var other = await _context.LeaveRequests.AsNoTracking()
                .Where(l => l.EmployeeId == employeeUid
                            && (ignoreRequestId == null || l.Id != ignoreRequestId)
                            && (l.Status == "Pending" || l.Status == "Approved")
                            && l.FromDate <= to && l.ToDate >= from)
                .OrderBy(l => l.FromDate)
                .Select(l => new { l.Id, l.FromDate, l.ToDate, l.Status })
                .FirstOrDefaultAsync();

            return other == null
                ? null
                : $"This employee already has a {other.Status} leave request (#{other.Id}) from {Fmt.ShortDate(other.FromDate)} to {Fmt.ShortDate(other.ToDate)} that overlaps these dates.";
        }

        /// <summary>Leave rows that consume quota (adjust if your workflow uses other statuses).</summary>
        private static bool LeaveRequestCountsAsAvailed(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var s = status.Trim();
            return string.Equals(s, "Approved", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Pending", StringComparison.OrdinalIgnoreCase);
        }

        // GET: LMS — lists dbo.LeaveRequests joined with Employee for display.
        [HttpGet("")]
        public async Task<IActionResult> Index(string? search, string? employeeId, string? status, string? leaveType)
        {
            ViewData["Module"] = "LMS";
            var searchTerm = (search ?? employeeId)?.Trim();
            if (string.IsNullOrWhiteSpace(searchTerm)) searchTerm = null;
            status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
            leaveType = string.IsNullOrWhiteSpace(leaveType) ? null : leaveType.Trim();

            var query =
                from lr in _context.LeaveRequests.AsNoTracking()
                join e in _context.Employees.AsNoTracking() on lr.EmployeeId equals e.uid into gj
                from e in gj.DefaultIfEmpty()
                select new { lr, e };

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(x =>
                    x.e != null &&
                    ((x.e.EmployeeID != null && x.e.EmployeeID.Contains(searchTerm)) ||
                     (x.e.EmployeeName != null && x.e.EmployeeName.Contains(searchTerm))));
            }

            if (!string.IsNullOrEmpty(status))
                query = query.Where(x => x.lr.Status == status);

            if (!string.IsNullOrEmpty(leaveType))
                query = query.Where(x => x.lr.LeaveTypeInfo!.Name == leaveType);

            var rows = await query
                .OrderByDescending(x => x.lr.FromDate)
                .ThenByDescending(x => x.lr.Id)
                .Select(x => new LmsLeaveRowVm
                {
                    Id = x.lr.Id,
                    EmployeeUid = x.lr.EmployeeId,
                    EmployeeCode = x.e != null ? x.e.EmployeeID : "?",
                    EmployeeName = x.e != null ? x.e.EmployeeName : null,
                    EmployeeDesignation = x.e != null && x.e.DesignationInfo != null ? x.e.DesignationInfo.Name : null,
                    LeaveType = x.lr.LeaveTypeInfo!.Name,
                    FromDate = x.lr.FromDate,
                    ToDate = x.lr.ToDate,
                    Status = x.lr.Status
                })
                .ToListAsync();

            ViewBag.LeaveTypes = await GetLeaveTypeNamesAsync();
            ViewBag.CurrentSearch = searchTerm;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentLeaveType = leaveType;

            return View(rows);
        }

        // GET: LMS/LeaveQuota — lists dbo.LeaveQuota (leave types and yearly quotas).
        [HttpGet("quotas")]
        public async Task<IActionResult> LeaveQuota()
        {
            ViewData["Module"] = "LMS";
            var rows = await _context.LeaveQuotas.AsNoTracking()
                .OrderByDescending(lq => lq.Year)
                .ThenBy(lq => lq.LeaveTypeInfo!.Name)
                .ToListAsync();
            return View(rows);
        }

        // GET: LMS/CreateLeaveQuota
        [HttpGet("quotas/new")]
        public async Task<IActionResult> CreateLeaveQuota()
        {
            ViewData["Module"] = "LMS";
            await PopulateLeaveQuotaFormLookupsAsync();
            return View(new LeaveQuota
            {
                Year = DateTime.Now.Year.ToString(),
                TotalLeaves = 0
            });
        }

        // POST: LMS/CreateLeaveQuota
        [HttpPost("quotas/new")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLeaveQuota([Bind("LeaveTypeName,TotalLeaves,Year")] LeaveQuota model)
        {
            ViewData["Module"] = "LMS";
            NormalizeLeaveQuota(model);
            await ValidateLeaveQuotaModelAsync(model, excludeUid: null);

            if (ModelState.IsValid)
            {
                _context.LeaveQuotas.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Leave quota for {model.LeaveTypeName} ({model.Year}) created.";
                return RedirectToAction(nameof(LeaveQuota));
            }

            await PopulateLeaveQuotaFormLookupsAsync(model.LeaveTypeName, model.Year);
            return View(model);
        }

        // GET: LMS/EditLeaveQuota/5
        [HttpGet("quotas/{id}/edit")]
        public async Task<IActionResult> EditLeaveQuota(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var quota = await _context.LeaveQuotas.FindAsync(id);
            if (quota == null) return NotFound();

            await PopulateLeaveQuotaFormLookupsAsync(quota.LeaveTypeName, quota.Year);
            return View(quota);
        }

        // POST: LMS/EditLeaveQuota/5
        [HttpPost("quotas/{id}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLeaveQuota(int id, [Bind("UID,LeaveTypeName,TotalLeaves,Year")] LeaveQuota model)
        {
            ViewData["Module"] = "LMS";
            if (id != model.UID) return NotFound();

            NormalizeLeaveQuota(model);
            await ValidateLeaveQuotaModelAsync(model, excludeUid: model.UID);

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Leave quota for {model.LeaveTypeName} ({model.Year}) updated.";
                    return RedirectToAction(nameof(LeaveQuota));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeaveQuotaExists(model.UID))
                        return NotFound();
                    throw;
                }
            }

            await PopulateLeaveQuotaFormLookupsAsync(model.LeaveTypeName, model.Year);
            return View(model);
        }

        // GET: LMS/DeleteLeaveQuota/5
        [HttpGet("quotas/{id}/delete")]
        public async Task<IActionResult> DeleteLeaveQuota(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var quota = await _context.LeaveQuotas.AsNoTracking()
                .FirstOrDefaultAsync(m => m.UID == id);
            if (quota == null) return NotFound();

            return View(quota);
        }

        // POST: LMS/DeleteLeaveQuota/5
        [HttpPost("quotas/{id}/delete"), ActionName("DeleteLeaveQuota")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLeaveQuotaConfirmed(int id)
        {
            ViewData["Module"] = "LMS";
            var quota = await _context.LeaveQuotas.FindAsync(id);
            if (quota != null)
            {
                _context.LeaveQuotas.Remove(quota);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Leave quota for {quota.LeaveTypeName} ({quota.Year}) deleted.";
            }

            return RedirectToAction(nameof(LeaveQuota));
        }

        // GET: LMS/LeaveBalance: entitled (quota + carry-forward + adjustments) minus approved leave for the selected year.
        // Reads dbo.vLeaveBalances through LeaveBalanceService, the same source as the employee Leave Balance tab.
        [HttpGet("balances")]
        public async Task<IActionResult> LeaveBalance(string? year, string? department)
        {
            ViewData["Module"] = "LMS";
            var yearStr = string.IsNullOrWhiteSpace(year) ? DateTime.Now.Year.ToString() : year!.Trim();
            var deptFilter = string.IsNullOrWhiteSpace(department) ? null : department!.Trim();
            if (!int.TryParse(yearStr, out var yearInt)) { yearInt = DateTime.Now.Year; yearStr = yearInt.ToString(); }

            var yearChoices = await BuildLeaveBalanceYearChoicesAsync();
            if (!yearChoices.Exists(y => string.Equals(y, yearStr, StringComparison.OrdinalIgnoreCase)))
            {
                yearChoices = yearChoices
                    .Concat(new[] { yearStr })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(y => int.TryParse(y, out var yi) ? yi : 0)
                    .ThenBy(y => y, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var configuredDepartments = await GetDepartmentsFromConfigurationAsync();
            var departmentChoices = new List<(string Value, string Label)> { ("", "All departments") };
            foreach (var d in configuredDepartments)
                departmentChoices.Add((d, d));

            var activeEmployees = await _context.Employees.AsNoTracking()
                .Where(e => e.EmployeeStatus == "Active")
                .OrderBy(e => e.EmployeeName)
                .Select(e => new { e.uid, e.EmployeeID, e.EmployeeName, Department = e.DepartmentInfo!.DepartmentName })
                .ToListAsync();

            var allActive = activeEmployees;
            if (!string.IsNullOrEmpty(deptFilter))
            {
                allActive = activeEmployees
                    .Where(e => e.Department != null && string.Equals(e.Department.Trim(), deptFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var uids = allActive.Select(e => e.uid).ToList();
            var (balances, typeNames) = await _leaveBalances.ForYearAsync(yearInt, uids);
            var typeOrder = await _context.LeaveTypes.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Name).Select(t => t.Id).ToListAsync();
            var byEmployee = balances.ToLookup(r => r.EmployeeId);

            var rows = new List<LmsLeaveBalanceRowVm>();
            foreach (var emp in allActive)
            {
                foreach (var r in byEmployee[emp.uid].OrderBy(r => typeOrder.IndexOf(r.LeaveTypeId)))
                {
                    rows.Add(new LmsLeaveBalanceRowVm
                    {
                        EmployeeCode = emp.EmployeeID,
                        EmployeeName = emp.EmployeeName,
                        LeaveTypeName = typeNames.TryGetValue(r.LeaveTypeId, out var n) ? n : "Leave type " + r.LeaveTypeId,
                        CarryForwardLeaves = r.CarryForward,
                        AvailableLeaves = r.Entitled,
                        AvailedLeaves = r.Taken,
                        PendingLeaves = r.Pending,
                        BalanceLeaves = r.Remaining
                    });
                }
            }

            var model = new LmsLeaveBalancePageVm
            {
                SelectedYear = yearStr,
                SelectedDepartment = deptFilter ?? "",
                YearChoices = yearChoices,
                DepartmentChoices = departmentChoices,
                Rows = rows
            };

            return View(model);
        }

        // GET: LMS/Rollover: preview of the year-end carry-forward (nothing is saved until Apply).
        [HttpGet("balances/rollover")]
        public async Task<IActionResult> Rollover(int? year)
        {
            ViewData["Module"] = "LMS";
            var y = year ?? DateTime.Now.Year - 1;
            ViewBag.Year = y;
            return View(await _leaveBalances.PreviewRolloverAsync(y));
        }

        // POST: LMS/Rollover: writes CarryforwardLeaves rows for year -> year + 1 and logs one audit row.
        [HttpPost("balances/rollover")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rollover(int year, bool apply)
        {
            var (inserted, updated, skipped) = await _leaveBalances.ApplyRolloverAsync(year, User.Identity?.Name);
            await _audit.WriteAsync("CarryforwardLeaves", "Rollover", year + "->" + (year + 1), null, null,
                new { FromYear = year, ToYear = year + 1, Inserted = inserted, Updated = updated, Skipped = skipped }, "LMS");
            TempData["SuccessMessage"] = $"Year-end rollover {year} to {year + 1}: {inserted} added, {updated} updated, {skipped} left as they were.";
            return RedirectToAction(nameof(LeaveBalance), new { year = year + 1 });
        }

        private async Task<List<string>> BuildLeaveBalanceYearChoicesAsync()
        {
            var fromQuota = await _context.LeaveQuotas.AsNoTracking()
                .Select(lq => lq.Year)
                .Where(y => y != null && y != "")
                .ToListAsync();
            var fromLeaves = (await _context.LeaveRequests.AsNoTracking()
                .Select(lr => lr.FromDate.Year)
                .Distinct()
                .ToListAsync())
                .Select(y => y.ToString())
                .ToList();

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var y in fromQuota)
            {
                if (!string.IsNullOrWhiteSpace(y))
                    set.Add(y!.Trim());
            }

            foreach (var y in fromLeaves)
            {
                if (!string.IsNullOrWhiteSpace(y))
                    set.Add(y!.Trim());
            }

            set.Add(DateTime.Now.Year.ToString());
            return set
                .OrderByDescending(y => int.TryParse(y, out var i) ? i : 0)
                .ThenBy(y => y, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // GET: LMS/Details/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var leaveRequest = await _context.LeaveRequests.FirstOrDefaultAsync(m => m.Id == id);
            if (leaveRequest == null) return NotFound();

            var emp = await _context.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.uid == leaveRequest.EmployeeId);
            ViewBag.EmployeeCode = emp?.EmployeeID ?? "(unknown)";
            ViewBag.EmployeeName = emp?.EmployeeName;

            return View(leaveRequest);
        }

        // GET: LMS/Create
        [HttpGet("new")]
        public async Task<IActionResult> Create()
        {
            ViewData["Module"] = "LMS";

            ViewBag.Employees = await BuildEmployeeUidSelectListAsync();
            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList();
            ViewBag.Statuses = new SelectList(new[] { "Pending", "Approved", "Rejected", "Cancelled" }, "Pending");

            var model = new LeaveRequest
            {
                FromDate = DateTime.Today,
                ToDate = DateTime.Today,
                Status = "Pending"
            };

            return View(model);
        }

        // POST: LMS/Create — persists dbo.LeaveRequests
        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EmployeeId,LeaveType,FromDate,ToDate,Status")] LeaveRequest model)
        {
            ViewData["Module"] = "LMS";

            if (model.EmployeeId <= 0)
                ModelState.AddModelError(nameof(LeaveRequest.EmployeeId), "Please select an employee.");

            if (model.ToDate.Date < model.FromDate.Date)
                ModelState.AddModelError(nameof(LeaveRequest.ToDate), "To date must be on or after the from date.");
            else if (model.ToDate.Year != model.FromDate.Year)
                ModelState.AddModelError(nameof(LeaveRequest.ToDate), "A leave request cannot cross 31 December. Apply for the two parts separately so each counts in its own year.");

            if (string.IsNullOrWhiteSpace(model.Status))
                model.Status = "Pending";

            var leaveTypeId = await ResolveLeaveTypeIdAsync(model.LeaveType);
            if (leaveTypeId == null)
                ModelState.AddModelError(nameof(LeaveRequest.LeaveType), "Please select a valid leave type.");

            if (ModelState.IsValid && model.ToDate.Date >= model.FromDate.Date)
            {
                var overlap = await FindLeaveOverlapAsync(model.EmployeeId, model.FromDate.Date, model.ToDate.Date, model.Status, null);
                if (overlap != null)
                    ModelState.AddModelError(string.Empty, overlap);
            }

            if (ModelState.IsValid)
            {
                model.LeaveTypeId = leaveTypeId!.Value;
                model.FromDate = model.FromDate.Date;
                model.ToDate = model.ToDate.Date;
                model.TotalDays = (model.ToDate - model.FromDate).Days + 1;   // calendar days; weekly offs/holidays excluded once schedules exist (B5)
                model.CreatedBy = User.Identity?.Name;
                try
                {
                    _context.Add(model);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Leave request saved to LeaveRequests.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    _context.Entry(model).State = EntityState.Detached;
                    ModelState.AddModelError(string.Empty, "An identical open leave request already exists for this employee, dates and leave type.");
                }
            }

            ViewBag.Employees = await BuildEmployeeUidSelectListAsync(model.EmployeeId);
            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList(model.LeaveType);
            ViewBag.Statuses = new SelectList(new[] { "Pending", "Approved", "Rejected", "Cancelled" }, model.Status);

            return View(model);
        }

        // GET: LMS/Edit/5
        [HttpGet("{id}/edit")]
        public async Task<IActionResult> Edit(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest == null) return NotFound();

            ViewBag.Employees = await BuildEmployeeUidSelectListAsync(leaveRequest.EmployeeId);
            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList(leaveRequest.LeaveType);
            ViewBag.Statuses = new SelectList(new[] { "Pending", "Approved", "Rejected", "Cancelled" }, leaveRequest.Status);

            return View(leaveRequest);
        }

        // POST: LMS/Edit/5
        [HttpPost("{id}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeId,LeaveType,FromDate,ToDate,Status,RowVersion")] LeaveRequest model)
        {
            ViewData["Module"] = "LMS";
            if (id != model.Id) return NotFound();

            if (model.EmployeeId <= 0)
                ModelState.AddModelError(nameof(LeaveRequest.EmployeeId), "Please select an employee.");

            if (model.ToDate.Date < model.FromDate.Date)
                ModelState.AddModelError(nameof(LeaveRequest.ToDate), "To date must be on or after the from date.");
            else if (model.ToDate.Year != model.FromDate.Year)
                ModelState.AddModelError(nameof(LeaveRequest.ToDate), "A leave request cannot cross 31 December. Apply for the two parts separately so each counts in its own year.");

            var leaveTypeId = await ResolveLeaveTypeIdAsync(model.LeaveType);
            if (leaveTypeId == null)
                ModelState.AddModelError(nameof(LeaveRequest.LeaveType), "Please select a valid leave type.");

            if (ModelState.IsValid && model.ToDate.Date >= model.FromDate.Date)
            {
                var current = await _context.LeaveRequests.AsNoTracking().Where(l => l.Id == model.Id).Select(l => l.Status).FirstOrDefaultAsync();
                var overlap = await FindLeaveOverlapAsync(model.EmployeeId, model.FromDate.Date, model.ToDate.Date,
                    string.IsNullOrWhiteSpace(model.Status) ? current : model.Status, model.Id);
                if (overlap != null)
                    ModelState.AddModelError(string.Empty, overlap);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == model.Id);
                    if (existing == null) return NotFound();
                    if (model.RowVersion != null)
                        _context.Entry(existing).Property(l => l.RowVersion).OriginalValue = model.RowVersion;   // S15

                    existing.EmployeeId = model.EmployeeId;
                    existing.LeaveTypeId = leaveTypeId!.Value;
                    existing.FromDate = model.FromDate.Date;
                    existing.ToDate = model.ToDate.Date;
                    existing.Status = string.IsNullOrWhiteSpace(model.Status) ? existing.Status : model.Status;
                    if (!existing.IsHalfDay)
                        existing.TotalDays = (existing.ToDate - existing.FromDate).Days + 1;
                    existing.ModifiedAt = DateTime.Now;
                    existing.ModifiedBy = User.Identity?.Name;
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Leave request updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeaveRequestExists(model.Id))
                        return NotFound();
                    ModelState.AddModelError(string.Empty, "This leave request was changed by someone else while you were editing. Reload the page and apply your changes again.");
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(string.Empty, "An identical open leave request already exists for this employee, dates and leave type.");
                }
            }

            ViewBag.Employees = await BuildEmployeeUidSelectListAsync(model.EmployeeId);
            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList(model.LeaveType);
            ViewBag.Statuses = new SelectList(new[] { "Pending", "Approved", "Rejected", "Cancelled" }, model.Status);

            return View(model);
        }

        // GET: LMS/Delete/5
        [HttpGet("{id}/delete")]
        public async Task<IActionResult> Delete(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var leaveRequest = await _context.LeaveRequests.FirstOrDefaultAsync(m => m.Id == id);
            if (leaveRequest == null) return NotFound();

            var emp = await _context.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.uid == leaveRequest.EmployeeId);
            ViewBag.EmployeeCode = emp?.EmployeeID ?? "(unknown)";
            ViewBag.EmployeeName = emp?.EmployeeName;

            return View(leaveRequest);
        }

        // POST: LMS/Delete/5
        [HttpPost("{id}/delete"), ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ViewData["Module"] = "LMS";
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest != null)
            {
                _context.LeaveRequests.Remove(leaveRequest);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Leave request deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool LeaveRequestExists(int id) =>
            _context.LeaveRequests.Any(e => e.Id == id);

        private bool LeaveQuotaExists(int uid) =>
            _context.LeaveQuotas.Any(e => e.UID == uid);

        private static void NormalizeLeaveQuota(LeaveQuota model)
        {
            model.LeaveTypeName = model.LeaveTypeName?.Trim() ?? string.Empty;
            model.Year = model.Year?.Trim() ?? string.Empty;
        }

        private async Task ValidateLeaveQuotaModelAsync(LeaveQuota model, int? excludeUid)
        {
            if (model.TotalLeaves < 0)
                ModelState.AddModelError(nameof(Models.LeaveQuota.TotalLeaves), "Total leaves cannot be negative.");
            else if (model.TotalLeaves * 2 != decimal.Truncate(model.TotalLeaves * 2))
                ModelState.AddModelError(nameof(Models.LeaveQuota.TotalLeaves), "Use whole or half days (for example 10 or 10.5).");

            if (string.IsNullOrWhiteSpace(model.LeaveTypeName) || string.IsNullOrWhiteSpace(model.Year))
                return;

            var typeId = await ResolveLeaveTypeIdAsync(model.LeaveTypeName);
            if (typeId == null)
            {
                ModelState.AddModelError(nameof(Models.LeaveQuota.LeaveTypeName), "Please select a valid leave type.");
                return;
            }
            model.LeaveTypeId = typeId.Value;

            var duplicate = await _context.LeaveQuotas.AsNoTracking().AnyAsync(lq =>
                lq.LeaveTypeId == model.LeaveTypeId
                && lq.Year == model.Year
                && (!excludeUid.HasValue || lq.UID != excludeUid.Value));

            if (duplicate)
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"A quota for \"{model.LeaveTypeName}\" in year {model.Year} already exists.");
            }
        }

        private async Task PopulateLeaveQuotaFormLookupsAsync(string? selectedLeaveType = null, string? selectedYear = null)
        {
            var types = await GetLeaveTypeNamesAsync();
            if (!string.IsNullOrWhiteSpace(selectedLeaveType)
                && !types.Exists(t => string.Equals(t, selectedLeaveType, StringComparison.OrdinalIgnoreCase)))
            {
                types.Add(selectedLeaveType);
                types = types.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
            }

            ViewBag.LeaveTypes = new SelectList(types, selectedLeaveType);

            var yearsFromDb = await _context.LeaveQuotas.AsNoTracking()
                .Where(lq => lq.Year != null && lq.Year != "")
                .Select(lq => lq.Year!)
                .Distinct()
                .ToListAsync();

            var yearChoices = yearsFromDb
                .Concat(Enumerable.Range(DateTime.Now.Year - 2, 6).Select(y => y.ToString()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(y => int.TryParse(y, out var yi) ? yi : 0)
                .ThenBy(y => y, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ViewBag.Years = new SelectList(yearChoices, selectedYear ?? DateTime.Now.Year.ToString());
        }

        private async Task<SelectList> BuildEmployeeUidSelectListAsync(int? selectedUid = null)
        {
            var q = _context.Employees.AsQueryable();
            if (selectedUid.HasValue)
                q = q.Where(e => e.EmployeeStatus == "Active" || e.uid == selectedUid.Value);
            else
                q = q.Where(e => e.EmployeeStatus == "Active");

            var items = await q
                .OrderBy(e => e.EmployeeName)
                .Select(e => new { e.uid, DisplayName = e.EmployeeID + " — " + e.EmployeeName })
                .ToListAsync();

            return new SelectList(items, "uid", "DisplayName", selectedUid);
        }

        private async Task<SelectList> BuildLeaveTypesSelectList(string? selected = null)
        {
            var types = await GetLeaveTypeNamesAsync();
            return new SelectList(types, selected);
        }
    }
}
