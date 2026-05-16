using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;

namespace HRMBT.Web.Controllers
{
    public class LMSController : Controller
    {
        private const string LeaveTypesConfigKey = "LeaveTypes";
        private const string DepartmentConfigKey = "Department";

        private readonly ApplicationDbContext _context;

        public LMSController(ApplicationDbContext context)
        {
            _context = context;
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

        /// <summary>Types from dbo.Configuration (ConfigKey LeaveTypes, CSV) or LeaveQuota.</summary>
        private async Task<List<string>> GetLeaveTypeNamesAsync()
        {
            var rows = await _context.HrConfigurations.AsNoTracking()
                .Where(c => c.ConfigKey != null && c.ConfigValue != null)
                .ToListAsync();

            var fromConfig = rows
                .Where(c => ConfigKeyMatches(c.ConfigKey, LeaveTypesConfigKey))
                .SelectMany(c => SplitConfigCsv(c.ConfigValue))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (fromConfig.Count > 0)
                return fromConfig;

            var yearStr = DateTime.Now.Year.ToString();
            var typesForYear = await _context.LeaveQuotas.AsNoTracking()
                .Where(lq => lq.Year == yearStr)
                .Select(lq => lq.LeaveTypeName)
                .Where(n => n != null && n != "")
                .Distinct()
                .OrderBy(lt => lt)
                .ToListAsync();

            if (typesForYear.Count > 0)
                return typesForYear!;

            return await _context.LeaveQuotas.AsNoTracking()
                .Select(lq => lq.LeaveTypeName)
                .Where(n => n != null && n != "")
                .Distinct()
                .OrderBy(lt => lt)
                .ToListAsync()!;
        }

        /// <summary>Department names from <c>dbo.Configuration</c> where <c>ConfigKey</c> is Department (comma-separated <c>ConfigValue</c>).</summary>
        private async Task<List<string>> GetDepartmentsFromConfigurationAsync()
        {
            var rows = await _context.HrConfigurations.AsNoTracking()
                .Where(c => c.ConfigKey != null && c.ConfigValue != null)
                .ToListAsync();

            return rows
                .Where(c => ConfigKeyMatches(c.ConfigKey, DepartmentConfigKey))
                .SelectMany(c => SplitConfigCsv(c.ConfigValue))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool LeaveRequestOverlapsCalendarYear(DateTime fromDate, DateTime toDate, int year)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);
            return fromDate.Date <= yearEnd && toDate.Date >= yearStart;
        }

        private static double LeaveRequestDaysOverlappingCalendarYear(DateTime fromDate, DateTime toDate, int year)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);
            var start = fromDate.Date > yearStart ? fromDate.Date : yearStart;
            var end = toDate.Date < yearEnd ? toDate.Date : yearEnd;
            if (end < start) return 0;
            return (end - start).TotalDays + 1;
        }

        /// <summary>Leave rows that consume quota (adjust if your workflow uses other statuses).</summary>
        private static bool LeaveRequestCountsAsAvailed(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var s = status.Trim();
            return string.Equals(s, "Approved", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Applied", StringComparison.OrdinalIgnoreCase);
        }

        // GET: LMS — lists dbo.LeaveRequests joined with Employee for display.
        public async Task<IActionResult> Index(string? employeeId, string? status, string? leaveType)
        {
            ViewData["Module"] = "LMS";
            employeeId = string.IsNullOrWhiteSpace(employeeId) ? null : employeeId.Trim();
            status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
            leaveType = string.IsNullOrWhiteSpace(leaveType) ? null : leaveType.Trim();

            var query =
                from lr in _context.LeaveRequests.AsNoTracking()
                join e in _context.Employees.AsNoTracking() on lr.EmployeeId equals e.uid into gj
                from e in gj.DefaultIfEmpty()
                select new { lr, e };

            if (!string.IsNullOrEmpty(employeeId))
            {
                query = query.Where(x =>
                    x.e != null &&
                    x.e.EmployeeID != null &&
                    x.e.EmployeeID.Contains(employeeId));
            }

            if (!string.IsNullOrEmpty(status))
                query = query.Where(x => x.lr.Status == status);

            if (!string.IsNullOrEmpty(leaveType))
                query = query.Where(x => x.lr.LeaveType == leaveType);

            var rows = await query
                .OrderByDescending(x => x.lr.FromDate)
                .ThenByDescending(x => x.lr.Id)
                .Select(x => new LmsLeaveRowVm
                {
                    Id = x.lr.Id,
                    EmployeeUid = x.lr.EmployeeId,
                    EmployeeCode = x.e != null ? x.e.EmployeeID : "?",
                    EmployeeName = x.e != null ? x.e.EmployeeName : null,
                    LeaveType = x.lr.LeaveType,
                    FromDate = x.lr.FromDate,
                    ToDate = x.lr.ToDate,
                    Status = x.lr.Status
                })
                .ToListAsync();

            ViewBag.LeaveTypes = await GetLeaveTypeNamesAsync();
            ViewBag.CurrentEmployeeId = employeeId;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentLeaveType = leaveType;

            return View(rows);
        }

        // GET: LMS/LeaveQuota — lists dbo.LeaveQuota (leave types and yearly quotas).
        public async Task<IActionResult> LeaveQuota()
        {
            ViewData["Module"] = "LMS";
            var rows = await _context.LeaveQuotas.AsNoTracking()
                .OrderByDescending(lq => lq.Year)
                .ThenBy(lq => lq.LeaveTypeName)
                .ToListAsync();
            return View(rows);
        }

        // GET: LMS/CreateLeaveQuota
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
        [HttpPost]
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
        [HttpPost]
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
        [HttpPost, ActionName("DeleteLeaveQuota")]
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

        // GET: LMS/LeaveBalance — available (LeaveQuota) minus availed (LeaveRequests) for selected year.
        public async Task<IActionResult> LeaveBalance(string? year, string? department)
        {
            ViewData["Module"] = "LMS";
            var yearStr = string.IsNullOrWhiteSpace(year) ? DateTime.Now.Year.ToString() : year!.Trim();
            var deptFilter = string.IsNullOrWhiteSpace(department) ? null : department!.Trim();
            _ = int.TryParse(yearStr, out var yearInt);

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
                .Select(e => new { e.uid, e.EmployeeID, e.EmployeeName, e.Department })
                .ToListAsync();

            var allActive = activeEmployees;
            if (!string.IsNullOrEmpty(deptFilter))
            {
                allActive = activeEmployees
                    .Where(e =>
                        e.Department != null &&
                        string.Equals(e.Department.Trim(), deptFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var allQuotas = await _context.LeaveQuotas.AsNoTracking().ToListAsync();
            var quotas = allQuotas
                .Where(lq => string.Equals(lq.Year?.Trim(), yearStr, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var quotaByType = quotas
                .GroupBy(lq => lq.LeaveTypeName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => (LeaveTypeName: g.Key, Available: g.Sum(x => x.TotalLeaves)))
                .ToList();

            string CanonicalLeaveTypeForQuota(string? requestLeaveType)
            {
                var t = (requestLeaveType ?? string.Empty).Trim();
                foreach (var q in quotaByType)
                {
                    if (string.Equals(q.LeaveTypeName, t, StringComparison.OrdinalIgnoreCase))
                        return q.LeaveTypeName;
                }

                return t;
            }

            var uidSet = allActive.Select(e => e.uid).ToHashSet();
            var uidToEmployeeCode = allActive.ToDictionary(e => e.uid, e => e.EmployeeID.Trim());

            var leaveRequests = await _context.LeaveRequests.AsNoTracking()
                .Where(lr => uidSet.Contains(lr.EmployeeId))
                .ToListAsync();

            var availedDict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var lr in leaveRequests)
            {
                if (!LeaveRequestCountsAsAvailed(lr.Status))
                    continue;

                if (yearInt > 0)
                {
                    if (!LeaveRequestOverlapsCalendarYear(lr.FromDate, lr.ToDate, yearInt))
                        continue;
                }
                else
                {
                    if (!string.Equals(lr.FromDate.Year.ToString(), yearStr, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(lr.ToDate.Year.ToString(), yearStr, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                if (!uidToEmployeeCode.TryGetValue(lr.EmployeeId, out var empCode))
                    continue;

                var type = CanonicalLeaveTypeForQuota(lr.LeaveType);
                if (string.IsNullOrEmpty(type))
                    continue;

                var days = yearInt > 0
                    ? LeaveRequestDaysOverlappingCalendarYear(lr.FromDate, lr.ToDate, yearInt)
                    : Math.Max(0, (lr.ToDate.Date - lr.FromDate.Date).TotalDays + 1);

                if (days <= 0)
                    continue;

                var key = $"{empCode}|{type}";
                availedDict.TryGetValue(key, out var prev);
                availedDict[key] = prev + days;
            }

            var rows = new List<LmsLeaveBalanceRowVm>();
            foreach (var emp in allActive)
            {
                foreach (var q in quotaByType)
                {
                    var key = $"{emp.EmployeeID.Trim()}|{q.LeaveTypeName.Trim()}";
                    availedDict.TryGetValue(key, out var availed);
                    var available = q.Available;
                    rows.Add(new LmsLeaveBalanceRowVm
                    {
                        EmployeeCode = emp.EmployeeID,
                        EmployeeName = emp.EmployeeName,
                        LeaveTypeName = q.LeaveTypeName,
                        AvailableLeaves = available,
                        AvailedLeaves = availed,
                        BalanceLeaves = available - availed
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

        private async Task<List<string>> BuildLeaveBalanceYearChoicesAsync()
        {
            var fromQuota = await _context.LeaveQuotas.AsNoTracking()
                .Select(lq => lq.Year)
                .Where(y => y != null && y != "")
                .ToListAsync();
            var fromLeaves = await _context.EmployeeLeaves.AsNoTracking()
                .Select(el => el.Year)
                .Where(y => y != null && y != "")
                .Distinct()
                .ToListAsync();

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
        public async Task<IActionResult> Create()
        {
            ViewData["Module"] = "LMS";

            ViewBag.Employees = await BuildEmployeeUidSelectListAsync();
            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList();
            ViewBag.Statuses = new SelectList(new[] { "Applied", "Approved", "Rejected", "Cancelled" }, "Applied");

            var model = new LeaveRequest
            {
                FromDate = DateTime.Today,
                ToDate = DateTime.Today,
                Status = "Applied"
            };

            return View(model);
        }

        // POST: LMS/Create — persists dbo.LeaveRequests
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EmployeeId,LeaveType,FromDate,ToDate,Status")] LeaveRequest model)
        {
            ViewData["Module"] = "LMS";

            if (model.EmployeeId <= 0)
                ModelState.AddModelError(nameof(LeaveRequest.EmployeeId), "Please select an employee.");

            if (model.ToDate.Date < model.FromDate.Date)
                ModelState.AddModelError(nameof(LeaveRequest.ToDate), "To date must be on or after the from date.");

            if (string.IsNullOrWhiteSpace(model.Status))
                model.Status = "Applied";

            if (ModelState.IsValid)
            {
                _context.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Leave request saved to LeaveRequests.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Employees = await BuildEmployeeUidSelectListAsync(model.EmployeeId);
            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList(model.LeaveType);
            ViewBag.Statuses = new SelectList(new[] { "Applied", "Approved", "Rejected", "Cancelled" }, model.Status);

            return View(model);
        }

        // GET: LMS/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            ViewData["Module"] = "LMS";
            if (id == null) return NotFound();

            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest == null) return NotFound();

            ViewBag.Employees = await BuildEmployeeUidSelectListAsync(leaveRequest.EmployeeId);
            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList(leaveRequest.LeaveType);
            ViewBag.Statuses = new SelectList(new[] { "Applied", "Approved", "Rejected", "Cancelled" }, leaveRequest.Status);

            return View(leaveRequest);
        }

        // POST: LMS/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeId,LeaveType,FromDate,ToDate,Status")] LeaveRequest model)
        {
            ViewData["Module"] = "LMS";
            if (id != model.Id) return NotFound();

            if (model.EmployeeId <= 0)
                ModelState.AddModelError(nameof(LeaveRequest.EmployeeId), "Please select an employee.");

            if (model.ToDate.Date < model.FromDate.Date)
                ModelState.AddModelError(nameof(LeaveRequest.ToDate), "To date must be on or after the from date.");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Leave request updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeaveRequestExists(model.Id))
                        return NotFound();
                    throw;
                }
            }

            ViewBag.Employees = await BuildEmployeeUidSelectListAsync(model.EmployeeId);
            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList(model.LeaveType);
            ViewBag.Statuses = new SelectList(new[] { "Applied", "Approved", "Rejected", "Cancelled" }, model.Status);

            return View(model);
        }

        // GET: LMS/Delete/5
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
        [HttpPost, ActionName("Delete")]
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

            if (string.IsNullOrWhiteSpace(model.LeaveTypeName) || string.IsNullOrWhiteSpace(model.Year))
                return;

            var duplicate = await _context.LeaveQuotas.AsNoTracking().AnyAsync(lq =>
                lq.LeaveTypeName == model.LeaveTypeName
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
