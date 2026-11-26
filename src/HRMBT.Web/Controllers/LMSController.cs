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
        private readonly ApplicationDbContext _context;
        private readonly HRMBT.Web.Services.LeaveBalanceService _leaveBalances;
        private readonly HRMBT.Web.Services.IAuditLogService _audit;

        private readonly HRMBT.Web.Services.LeaveRulesService _rules;

        public LMSController(ApplicationDbContext context, HRMBT.Web.Services.LeaveBalanceService leaveBalances, HRMBT.Web.Services.IAuditLogService audit,
            HRMBT.Web.Services.LeaveRulesService rules)
        {
            _rules = rules;
            _context = context;
            _leaveBalances = leaveBalances;
            _audit = audit;
        }

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

        private Task AuditAsync(string table, string operation, object recordId, int? employeeId, object? before, object? after) =>
            _audit.WriteAsync(table, operation, recordId.ToString(), employeeId, before, after, "LMS");

        // GET: LMS — lists dbo.LeaveRequests joined with Employee for display.
        [HttpGet("")]
        public async Task<IActionResult> Index(string? search, int? employeeId, string? status, string? leaveType)
        {
            ViewData["Module"] = "LMS";
            var searchTerm = search?.Trim();
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

            if (employeeId is > 0)
            {
                // exactly this employee: a text search for the ID would also match other IDs that contain it
                query = query.Where(x => x.lr.EmployeeId == employeeId);
                ViewBag.Employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.uid == employeeId);
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
                    TotalDays = x.lr.TotalDays,
                    Status = x.lr.Status,
                    RowVersion = x.lr.RowVersion
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
                await AuditAsync("LeaveQuota", "INSERT", model.UID, null, null, new { model.LeaveTypeId, model.Year, model.TotalLeaves });
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
                    var old = await _context.LeaveQuotas.AsNoTracking().Where(q => q.UID == model.UID)
                        .Select(q => new { q.LeaveTypeId, q.Year, q.TotalLeaves }).FirstOrDefaultAsync();
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    await AuditAsync("LeaveQuota", "UPDATE", model.UID, null, old, new { model.LeaveTypeId, model.Year, model.TotalLeaves });
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
                await AuditAsync("LeaveQuota", "DELETE", id, null, new { quota.LeaveTypeId, quota.Year, quota.TotalLeaves }, null);
                TempData["SuccessMessage"] = $"Leave quota for {quota.LeaveTypeName} ({quota.Year}) deleted.";
            }

            return RedirectToAction(nameof(LeaveQuota));
        }

        /// <summary>Gives <paramref name="year"/> + 1 the same quota for every leave type that has none there yet.</summary>
        [HttpPost("quotas/copy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopyLeaveQuotas(int year)
        {
            var next = (year + 1).ToString();
            var from = year.ToString();
            var source = await _context.LeaveQuotas.AsNoTracking().Where(q => q.Year == from).ToListAsync();
            var have = (await _context.LeaveQuotas.AsNoTracking().Where(q => q.Year == next).Select(q => q.LeaveTypeId).ToListAsync()).ToHashSet();
            var add = source.Where(q => !have.Contains(q.LeaveTypeId))
                .Select(q => new LeaveQuota { LeaveTypeId = q.LeaveTypeId, Year = next, TotalLeaves = q.TotalLeaves }).ToList();
            if (add.Count > 0)
            {
                _context.LeaveQuotas.AddRange(add);
                await _context.SaveChangesAsync();
                await AuditAsync("LeaveQuota", "INSERT", $"{year}->{next}", null, null, add.Select(q => new { q.LeaveTypeId, q.Year, q.TotalLeaves }).ToList());
            }
            TempData["SuccessMessage"] = source.Count == 0 ? $"There are no quotas for {year} to copy."
                : add.Count == 0 ? $"{next} already has a quota for every leave type set in {year}."
                : $"{add.Count} quota(s) copied from {year} to {next}.";
            return RedirectToAction(nameof(LeaveQuota));
        }

        // GET: LMS/LeaveBalance: entitled (quota + carry-forward + adjustments) minus approved leave for the selected year.
        // Reads dbo.vLeaveBalances through LeaveBalanceService, the same source as the employee Leave Balance tab.
        [HttpGet("balances")]
        public async Task<IActionResult> LeaveBalance(string? year, string? department, int? employeeId)
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
                .Select(e => new { e.uid, e.EmployeeID, e.EmployeeName, Department = e.DepartmentInfo!.DepartmentName, Designation = e.DesignationInfo!.Name })
                .ToListAsync();

            var allActive = activeEmployees;
            if (!string.IsNullOrEmpty(deptFilter))
            {
                allActive = activeEmployees
                    .Where(e => e.Department != null && string.Equals(e.Department.Trim(), deptFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (employeeId is > 0)
            {
                allActive = allActive.Where(e => e.uid == employeeId).ToList();
                ViewBag.EmployeeId = employeeId;
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
                        EmployeeUid = emp.uid,
                        EmployeeCode = emp.EmployeeID,
                        Designation = emp.Designation,
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

        // GET: /leave/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            ViewData["Module"] = "LMS";
            var leaveRequest = await _context.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (leaveRequest == null) return NotFound();
            await SetRequestEmployeeAsync(leaveRequest);
            // what approving would say, so it is seen before the decision rather than after
            if (IsPending(leaveRequest) && ViewBag.Employee is Employee employee)
                ViewBag.Check = await _rules.CheckAsync(employee, leaveRequest.FromDate, leaveRequest.ToDate,
                    leaveRequest.LeaveTypeId, "Approved", leaveRequest.Id);
            if (User.IsInRole("Admin") || User.IsInRole("HR"))
                ViewBag.Notes = new RecordNotesVm
                {
                    BaseUrl = Url.Content($"~/leave/{id}/notes"),
                    ButtonClass = "btn-grid-edit",
                    Hint = "Add a note (seen by Admin and HR; does not change days or balances)",
                    Notes = await HRMBT.Web.Services.RecordNoteQueries.LoadNotesAsync(_context, n => n.LeaveRequestId == id)
                };
            return View(leaveRequest);
        }

        private static bool IsPending(LeaveRequest r) => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase);

        // ---------- approve / reject a pending request (access as for Edit, until HR answers B13) ----------

        [HttpPost("{id:int}/approve")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Approve(int id, string? rowVersion, string? returnUrl) => DecideAsync(id, "Approved", rowVersion, returnUrl);

        [HttpPost("{id:int}/reject")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Reject(int id, string? rowVersion, string? returnUrl) => DecideAsync(id, "Rejected", rowVersion, returnUrl);

        private async Task<IActionResult> DecideAsync(int id, string status, string? rowVersion, string? returnUrl)
        {
            var r = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
            if (r == null) return NotFound();
            IActionResult Back() => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(nameof(Details), new { id });

            const string changed = "This leave request was changed after the page was loaded. Check it and try again.";
            var seen = new byte[8];
            if (!Convert.TryFromBase64String(rowVersion ?? "", seen, out var length) || length != seen.Length)
            {
                TempData["ErrorMessage"] = changed;
                return Back();
            }
            if (!IsPending(r))
            {
                TempData["ErrorMessage"] = $"Leave request #{r.Id} is already {r.Status}.";
                return Back();
            }

            var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.uid == r.EmployeeId);
            if (employee == null) return NotFound();
            HRMBT.Web.Services.LeaveCheck? check = null;
            if (status == "Approved")
            {
                check = await _rules.CheckAsync(employee, r.FromDate, r.ToDate, r.LeaveTypeId, status, r.Id);
                if (!check.Ok)
                {
                    TempData["ErrorMessage"] = $"Leave request #{r.Id} cannot be approved. " + string.Join(" ", check.Blocks);
                    return Back();
                }
            }

            var before = HRMBT.Web.Services.LeaveRulesService.Snapshot(r);
            var previous = r.Status;
            _context.Entry(r).Property(l => l.RowVersion).OriginalValue = seen;   // S15
            r.Status = status;
            if (check != null && !r.IsHalfDay)
                r.TotalDays = check.Days;
            r.ModifiedAt = DateTime.Now;
            r.ModifiedBy = User.Identity?.Name;
            await _rules.StampApprovalAsync(r, previous, User.Identity?.Name);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = changed;
                return Back();
            }
            await AuditAsync("LeaveRequests", "UPDATE", r.Id, r.EmployeeId, before, HRMBT.Web.Services.LeaveRulesService.Snapshot(r));
            var warnings = check?.Warnings.Count > 0 ? " Check: " + string.Join(" ", check.Warnings) : "";
            TempData["SuccessMessage"] =
                $"{employee.EmployeeName}: leave {Fmt.DateRange(r.FromDate, r.ToDate)} {status.ToLowerInvariant()}.{warnings}";
            return Back();
        }

        // ---------- who is on leave ----------

        [HttpGet("calendar")]
        public async Task<IActionResult> Calendar(string? month, string? project, string? department,
            [FromServices] HRMBT.Web.Services.WorkScheduleService schedules)
        {
            ViewData["Module"] = "LMS";
            var first = DateTime.TryParseExact(month, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var m) && m.Year is >= 2000 and <= 2100
                ? m
                : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            var query = _context.LeaveRequests.AsNoTracking()
                .Where(l => (l.Status == "Approved" || l.Status == "Pending") && l.FromDate <= last && l.ToDate >= first)
                .Join(_context.Employees.AsNoTracking(), l => l.EmployeeId, e => e.uid, (l, e) => new { l, e });
            if (!string.IsNullOrWhiteSpace(project))
                query = query.Where(x => x.e.ProjectInfo!.Name == project);
            if (!string.IsNullOrWhiteSpace(department))
                query = query.Where(x => x.e.DepartmentInfo!.DepartmentName == department);
            var leave = await query.Select(x => new
            {
                x.l.Id, x.l.EmployeeId, x.l.FromDate, x.l.ToDate, x.l.Status, x.l.IsHalfDay,
                LeaveType = x.l.LeaveTypeInfo!.Name,
                x.e.EmployeeID, x.e.EmployeeName, Designation = x.e.DesignationInfo!.Name
            }).ToListAsync();

            var byEmployee = await schedules.LoadAsync(leave.Select(l => l.EmployeeId).Distinct().ToList(), first, last);
            var holidays = await _context.GazettedHolidays.AsNoTracking()
                .Where(h => h.ProjectId == null && !h.IsOptional && h.HolidayDate <= last && h.EndDate >= first)
                .ToListAsync();

            var vm = new LeaveCalendarVm
            {
                Month = first,
                Project = project,
                Department = department,
                Projects = await _context.Projects.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Name).Select(p => p.Name).ToListAsync(),
                Departments = (await GetDepartmentsFromConfigurationAsync()).Distinct().ToList()
            };
            for (var d = first; d <= last; d = d.AddDays(1))
                vm.Days.Add(new LeaveCalendarDayVm { Date = d, Holiday = holidays.FirstOrDefault(h => h.HolidayDate <= d && h.EndDate >= d)?.HolidayName });

            foreach (var person in leave.GroupBy(l => l.EmployeeId).OrderBy(g => g.First().EmployeeID))
            {
                var info = person.First();
                var row = new LeaveCalendarRowVm
                {
                    Uid = person.Key, EmployeeCode = info.EmployeeID, Name = info.EmployeeName, Designation = info.Designation,
                    Cells = new LeaveCalendarCellVm?[vm.Days.Count]
                };
                for (var i = 0; i < vm.Days.Count; i++)
                {
                    var day = vm.Days[i];
                    // an approved request wins over a pending one on the same date
                    var l = person.Where(x => x.FromDate <= day.Date && x.ToDate >= day.Date)
                        .OrderBy(x => x.Status == "Approved" ? 0 : 1).FirstOrDefault();
                    if (l == null) continue;
                    var cell = new LeaveCalendarCellVm
                    {
                        LeaveRequestId = l.Id, LeaveType = l.LeaveType, Approved = l.Status == "Approved", HalfDay = l.IsHalfDay,
                        WorkDay = byEmployee[person.Key].IsWorkDay(day.Date)
                    };
                    row.Cells[i] = cell;
                    if (!cell.WorkDay) continue;
                    if (cell.Approved) day.Approved++;
                    else day.Pending++;
                }
                vm.Rows.Add(row);
            }
            return View(vm);
        }

        private async Task SetRequestEmployeeAsync(LeaveRequest r)
        {
            ViewBag.Employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.uid == r.EmployeeId);
            ViewBag.Approver = r.ApprovedByUserId == null ? null
                : await _context.AppUsers.AsNoTracking().Where(u => u.Uid == r.ApprovedByUserId).Select(u => u.Username).FirstOrDefaultAsync();
        }

        /// <summary>Days a request would charge and anything wrong with it, for the form (saves nothing).</summary>
        [HttpGet("check")]
        public async Task<IActionResult> Check(int employeeId, DateTime from, DateTime to, string? leaveType, string? status, int? ignoreId)
        {
            var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.uid == employeeId);
            if (employee == null || from == default || to == default) return Json(new { found = false });
            var check = await _rules.CheckAsync(employee, from, to, await ResolveLeaveTypeIdAsync(leaveType), status ?? "Pending", ignoreId);
            return Json(new
            {
                found = true,
                workDays = check.Days,
                calendarDays = to >= from ? (to.Date - from.Date).Days + 1 : 0,
                problems = check.Blocks,
                warnings = check.Warnings
            });
        }

        // GET: /leave/new
        [HttpGet("new")]
        public async Task<IActionResult> Create(int? employeeId)
        {
            ViewData["Module"] = "LMS";
            return await RequestFormAsync(nameof(Create), new LeaveRequest
            {
                EmployeeId = employeeId ?? 0,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today,
                Status = "Pending"
            });
        }

        private async Task<IActionResult> RequestFormAsync(string view, LeaveRequest model)
        {
            ViewBag.Employees = await BuildEmployeeUidSelectListAsync(model.EmployeeId > 0 ? model.EmployeeId : null);
            ViewBag.LeaveTypes = await BuildLeaveTypesSelectList(model.LeaveType);
            ViewBag.Statuses = new SelectList(new[] { "Pending", "Approved", "Rejected", "Cancelled" }, model.Status);
            return View(view, model);
        }

        /// <summary>Runs the shared leave rules and puts refusals in ModelState. Null when the form itself is incomplete.</summary>
        private async Task<(HRMBT.Web.Services.LeaveCheck Check, int TypeId)?> ValidateRequestAsync(LeaveRequest model, int? ignoreId)
        {
            var employee = model.EmployeeId > 0
                ? await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.uid == model.EmployeeId)
                : null;
            if (employee == null)
                ModelState.AddModelError(nameof(LeaveRequest.EmployeeId), "Please select an employee.");
            var typeId = await ResolveLeaveTypeIdAsync(model.LeaveType);
            if (typeId == null)
                ModelState.AddModelError(nameof(LeaveRequest.LeaveType), "Please select a valid leave type.");
            if (employee == null || typeId == null)
                return null;

            var check = await _rules.CheckAsync(employee, model.FromDate, model.ToDate, typeId, model.Status, ignoreId);
            foreach (var problem in check.Blocks)
                ModelState.AddModelError(string.Empty, problem);
            return (check, typeId.Value);
        }

        private static string SavedMessage(string what, LeaveRequest r, HRMBT.Web.Services.LeaveCheck check) =>
            $"{what}: {Fmt.Days(r.TotalDays)} day(s) charged." + (check.Warnings.Count > 0 ? " Check: " + string.Join(" ", check.Warnings) : "");

        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EmployeeId,LeaveType,FromDate,ToDate,Status")] LeaveRequest model)
        {
            ViewData["Module"] = "LMS";
            if (string.IsNullOrWhiteSpace(model.Status))
                model.Status = "Pending";
            var validated = await ValidateRequestAsync(model, null);

            if (ModelState.IsValid && validated is { } v)
            {
                model.LeaveTypeId = v.TypeId;
                model.FromDate = model.FromDate.Date;
                model.ToDate = model.ToDate.Date;
                model.TotalDays = v.Check.Days;   // work days only (the employee's schedule)
                model.CreatedBy = User.Identity?.Name;
                await _rules.StampApprovalAsync(model, null, User.Identity?.Name);
                try
                {
                    _context.Add(model);
                    await _context.SaveChangesAsync();
                    await AuditAsync("LeaveRequests", "INSERT", model.Id, model.EmployeeId, null, HRMBT.Web.Services.LeaveRulesService.Snapshot(model));
                    TempData["SuccessMessage"] = SavedMessage("Leave request saved", model, v.Check);
                    return RedirectToAction(nameof(Index), new { employeeId = model.EmployeeId });
                }
                catch (DbUpdateException)
                {
                    _context.Entry(model).State = EntityState.Detached;
                    ModelState.AddModelError(string.Empty, "An identical open leave request already exists for this employee, dates and leave type.");
                }
            }

            return await RequestFormAsync(nameof(Create), model);
        }

        [HttpGet("{id:int}/edit")]
        public async Task<IActionResult> Edit(int id)
        {
            ViewData["Module"] = "LMS";
            var leaveRequest = await _context.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
            if (leaveRequest == null) return NotFound();
            return await RequestFormAsync(nameof(Edit), leaveRequest);
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeId,LeaveType,FromDate,ToDate,Status,RowVersion")] LeaveRequest model)
        {
            ViewData["Module"] = "LMS";
            if (id != model.Id) return NotFound();

            var existing = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == model.Id);
            if (existing == null) return NotFound();
            if (string.IsNullOrWhiteSpace(model.Status))
                model.Status = existing.Status;
            var validated = await ValidateRequestAsync(model, model.Id);
            if (existing.IsHalfDay && model.FromDate.Date != model.ToDate.Date)
                ModelState.AddModelError(string.Empty, "This is a half day, so it must start and end on the same date.");

            if (ModelState.IsValid && validated is { } v)
            {
                try
                {
                    if (model.RowVersion != null)
                        _context.Entry(existing).Property(l => l.RowVersion).OriginalValue = model.RowVersion;   // S15
                    var before = HRMBT.Web.Services.LeaveRulesService.Snapshot(existing);
                    var previousStatus = existing.Status;

                    existing.EmployeeId = model.EmployeeId;
                    existing.LeaveTypeId = v.TypeId;
                    existing.FromDate = model.FromDate.Date;
                    existing.ToDate = model.ToDate.Date;
                    existing.Status = model.Status;
                    if (!existing.IsHalfDay)
                        existing.TotalDays = v.Check.Days;
                    existing.ModifiedAt = DateTime.Now;
                    existing.ModifiedBy = User.Identity?.Name;
                    await _rules.StampApprovalAsync(existing, previousStatus, User.Identity?.Name);
                    await _context.SaveChangesAsync();
                    await AuditAsync("LeaveRequests", "UPDATE", existing.Id, existing.EmployeeId, before, HRMBT.Web.Services.LeaveRulesService.Snapshot(existing));
                    TempData["SuccessMessage"] = SavedMessage("Leave request updated", existing, v.Check);
                    return RedirectToAction(nameof(Index), new { employeeId = existing.EmployeeId });
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

            return await RequestFormAsync(nameof(Edit), model);
        }

        // Leave history is never deleted (db standards S6, S14): a request is cancelled.
        [HttpGet("{id:int}/delete")]
        public IActionResult OldDelete(int id) => RedirectToActionPermanent(nameof(Cancel), new { id });

        [HttpGet("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            ViewData["Module"] = "LMS";
            var leaveRequest = await _context.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (leaveRequest == null) return NotFound();
            await SetRequestEmployeeAsync(leaveRequest);
            return View(leaveRequest);
        }

        [HttpPost("{id:int}/cancel"), ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int id)
        {
            var r = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
            if (r == null) return NotFound();
            if (!HRMBT.Web.Services.LeaveRulesService.IsOpen(r.Status))
                TempData["ErrorMessage"] = $"This request is already {r.Status}.";
            else
            {
                var before = HRMBT.Web.Services.LeaveRulesService.Snapshot(r);
                var previous = r.Status;
                r.Status = "Cancelled";
                r.ModifiedAt = DateTime.Now;
                r.ModifiedBy = User.Identity?.Name;
                await _rules.StampApprovalAsync(r, previous, User.Identity?.Name);
                await _context.SaveChangesAsync();
                await AuditAsync("LeaveRequests", "UPDATE", r.Id, r.EmployeeId, before, HRMBT.Web.Services.LeaveRulesService.Snapshot(r));
                TempData["SuccessMessage"] = $"Leave request cancelled; its {Fmt.Days(r.TotalDays)} day(s) are no longer charged.";
            }
            return RedirectToAction(nameof(Index), new { employeeId = r.EmployeeId });
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
