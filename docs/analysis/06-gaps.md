# 6. Gaps, Risks & Recommendations

Priority: **P0** fix now · **P1** next sprint · **P2** planned · **P3** nice to have.

## Security (details in [04-security.md](04-security.md))
| P | Gap |
|---|-----|
| P0 | `SetupController` is `[AllowAnonymous]` – unauthenticated user/password/config management |
| P0 | `sa` credentials for five servers committed in `appsettings.json` (and git history) |
| P1 | No role-based authorization; every logged-in user can do everything incl. payroll and user admin |
| P1 | Verbose login errors (user enumeration, hash-format hints); legacy plain-text/SHA-256 passwords still accepted |
| P1 | HTTP only, cookie not `Secure`, no HTTPS redirection |
| P2 | No lockout/rate limiting; no global antiforgery filter; logout via GET |
| P2 | Uploaded documents live in `wwwroot` (directly servable); no content sniffing/AV |
| P2 | `Home/Test` and `Debug` expose DB metadata to any user |

## Data integrity & database
- **Mixed employee keys:** `Employee.uid` (int) for payroll vs `Employee.EmployeeID` (string) for attendance/leave/users, with no foreign keys on the string links → orphan rows possible, deleting an employee (`EmployeeController.DeleteConfirmed` does a hard `Remove`) can leave attendance/leave/allowance data dangling or fail on the payslip `Restrict` FK.
- **Department is free text** in `Employee`/`Attendance`; a `Departments` table exists but is not linked.
- **Dates as strings** (`DOB`, `ModifiedOn`, `CreatedOn`) and inconsistent date types.
- **Two leave models** (`LeaveRequests` vs `EmployeeLeaves`) with different key types; unclear which is authoritative for balances.
- **Schema drift:** model vs `db.md` differences (`CarryforwardLeaves`, `EmployeeLeaves` columns), outdated tables in `database-schema.sql`, several tables with no migration, `Deductions.PercentageValue` overloaded to hold fixed amounts, `Allowances.ModifiedBy` is `nvarchar(max)` in DB but 100/unspecified in model.
- **No concurrency control** (no row-version tokens) – two HR users can overwrite each other; payroll generation has a check-then-insert race (no unique index on `Payslips(EmployeeId, Month, Year)` evident).
- **No automatic migration**; deploy relies on manual SQL. Verified live: 6 repo migrations were applied by hand and are missing from `__EFMigrationsHistory`, and the DB records a migration that is not in the repo. Running `dotnet ef database update` would try to re-apply them.
- **Three tables have no primary key** in the live DB (`Configuration`, `EmployeeLeaves`, `LeaveQuota`); `CarryforwardLeaves` and `GazettedHolidays` do not match their EF entities (see `db.md`).
- **Referential gaps in live data:** 1 `LeaveRequests` row and 1 `Users` row point to missing employees; 54 of 58 department strings are not in `Departments`; `Project` has 8 distinct values while HR describes 6.

## Business logic
- **Tax-year bug (confirmed against live data):** `TaxRules.TaxYear` values are `2025-2026` and `2026-2027`, but payroll compares them with the payslip year `2026`. Nothing matches, so all 14 slabs are pooled and the one with the highest `MinSalary` containing the basic salary is used, regardless of fiscal year (the two years have different rates: max 20% vs 10%). A `CurrentTaxYear` key exists in `Configuration` but payroll does not use it. 827 of 848 employees have `ApplyTax = 1`, so this affects almost every payslip.
- Tax is a **flat % on basic salary** chosen by slab; Pakistan FBR salary tax is progressive (marginal slabs with fixed amounts) and based on annual taxable income – results will differ from statutory computation.
- **Leave/attendance do not influence salary** (no absent/unpaid-leave deductions, `WorkingDays`, `LeaveDays`, `LeaveBalance` unused) although leave-based adjustments were originally intended.
- Percentage allowances use basic, percentage deductions use gross; frequency fields (`Monthly`, one-time, etc.) are stored but not honoured in calculation (all active rows apply every month).
- Allowance `EffectiveDate/EndDate` are **not** applied in payroll (only `IsActive`), whereas deductions are date-filtered – inconsistent.
- Any edit to an allowance/deduction locks **all** of the employee's unlocked payslips and there is no unlock/regenerate path, so corrections require deleting/rebuilding.
- Leave: no approval workflow, no balance check when creating requests, overlapping-leave validation not evident.
- Taxes Summary report is a stub; employee `ApplyTax` is a free-text flag ("yes/y/true/1").
- Hard-coded string conventions (`"Fuel Allowance"`, `"Fixed Amount"`, month names) make behaviour fragile.

## Engineering quality
- **No automated tests** (no test project); no CI.
- **Fat controllers** (Allowances 873, Payroll 804, Deductions 820 lines) with data access and rules inline; sync EF calls in `PayrollCalculationService` inside async request paths.
- Duplicate code: Allowances/Deductions controllers and views are near copies; `Create` vs `Add`/bulk flows; both `Payroll/Details` and `Payslips/Details`, `Payroll/Create` vs `Payslips/Create`.
- Layout duplication: `_Layout`, `_MainLayout`, `_DeductionsLayout`, `_SetupLayout` and per-module sidebars; many views embed large inline `<style>` blocks and CDN links.
- Sidebar link inconsistencies (Attendance entries "By Department"/"Summary" both point to `Index`).
- Repository hygiene: `_build_verify/` (≈ 300 binaries), exported schemas, README says .NET 9 (project is .NET 8) and lists only 5 modules.
- Logging/monitoring: console only, no correlation, no health endpoint; most exceptions swallowed into `TempData` messages or `catch {}`.
- Attendance upload `UploadedBy` hard-coded `"System"` (TODO); `ModifiedBy` and audit fields inconsistently filled.
- Performance: dashboards/reports pull full tables in memory in places (`ToList()` then group); `Employee.Department.Date` comparisons (`a.AttendanceDate.Date == DateTime.Today`) may defeat indexes; EF retry strategy conflicts with user-initiated transactions if added later.
- Accessibility/i18n: English only, emoji icons in sidebar, no localisation; date/number formats depend on server culture.

## Operations
- Deploy script deletes uploaded employee documents on every release; no backup, no rollback, no blue/green.
- No environment-specific configuration or secrets handling; production DB host baked into scripts/config.
- No scheduled jobs (e.g. attendance sync, backups), no email/notifications, no reporting export beyond Excel employee list/print view.

## Recommended roadmap
1. **Week 1 (P0):** lock down `Setup`, rotate DB credentials and purge secrets from history, remove `_build_verify`.
2. **P1:** introduce roles/policies, HTTPS + secure cookies, neutral login errors, password re-hash on login, global antiforgery.
3. **P1:** move uploads outside web root and out of the deploy folder; add DB backup + non-destructive deploy (publish to new folder + swap, or `app_offline.htm`).
4. **P2:** add a unique index on `Payslips(EmployeeId, Month, Year)`, FK/relations for string employee keys, rowversion; consolidate leave models; baseline migration reflecting the real schema.
5. **P2:** extract application services from fat controllers; add unit tests for `PayrollCalculationService` (allowance/deduction/tax rules) and integration tests for login/authorization.
6. **P3:** progressive tax engine, attendance/leave-based salary, payslip unlock with audit, full audit trail, CI pipeline.
