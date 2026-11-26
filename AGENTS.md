# AGENTS.md

This file provides guidance to every coding agent when working with code in this repository. CLAUDE.md points here; every coding agent reads this file first.

## Project

HRMS for Bahria Town: a single server-rendered ASP.NET Core MVC app (`src/HRMBT.Web`, .NET 8, Razor views, EF Core + SQL Server, cookie auth). No SPA, no API layer, no Node build, **no test project and no CI**. Production runs on IIS at https://hr.coditium.com (server "Teezhost 16GB"); see `docs/deployment.md`.

## Commands

```powershell
dotnet build src/HRMBT.Web/HRMBT.Web.csproj
dotnet run --project src/HRMBT.Web/HRMBT.Web.csproj      # http://localhost:5128, Development environment
scripts\deploy\DeployToIIS.bat                            # build + publish to C:\HRMSDeploy + restart IIS site "HRMS"
powershell -File scripts\database\tools\export-db-schema.ps1   # dump live schema to docs/db-schema-export.txt
```

There are no tests or linters configured. `dotnet ef` is available via the `Microsoft.EntityFrameworkCore.Tools` package, but see the migration caveat below before using it.

## Configuration (important)

- `appsettings.json` has **no connection string**. The app throws at startup if `ConnectionStrings:DefaultConnection` is empty.
- Real values live in git-ignored `src/HRMBT.Web/appsettings.Development.json` / `appsettings.Production.json`; create them from the committed `*.example.json`. The env var `ConnectionStrings__DefaultConnection` overrides both. `.env` (git-ignored) only stores old reference strings and is not read by the app.
- `DeployToIIS.bat` aborts if `appsettings.Production.json` is missing, because `dotnet publish` must copy it. The script wipes the deploy folder but copies `wwwroot/uploads` to `C:\HRMSBackups` first and restores it after publishing; prefer `EmployeeDocuments:StorageBasePath` (outside the site).
- Never reintroduce credentials into committed files (the old `sa` password is still in git history).
- The repo path contains `[D]`: in PowerShell use `-LiteralPath` and avoid `Resolve-Path`, which treats brackets as wildcards.

## Database

- Production DB is `HRBTK` (SQL Server 2022 Express) and has the **redesigned schema since 2026-11-24** (34 tables in `dbo`, scripts in `scripts/database/redesign/`). `db.md` is the schema document and the **only** place schema changes are tracked; keep its version/date/Change History current, and mark anything not yet run on production as pending. `docs/db/db_changes.md` is retired: do not update it. (`docs/db-schema-export.txt` is an outdated dump from before the redesign.)
- **Migrations are not trustworthy.** Six older repo migrations were applied by hand and are missing from `__EFMigrationsHistory`, and one recorded migration has no file. Do not run `dotnet ef database update` against production without reading the "Known issues" in `db.md`. Schema changes are applied via re-runnable SQL in `scripts/database/redesign/` (back up first, `sqlcmd -b -I`).
- Entities follow the new schema (`Models/`, `Data/ApplicationDbContext.cs`); `db.md` explains each column.
- Employees are identified two ways: `Employee.uid` (int) is the FK used by every table (payroll, leave, attendance, users); `Employee.EmployeeID` (string, unique) is the business key shown to users. Project, department, sub-department and designation are foreign keys to managed lists.

## Architecture

Controllers call `ApplicationDbContext` directly (`Data/ApplicationDbContext.cs`, fluent config in `OnModelCreating`); only these concerns are extracted into `Services/`:

- `Payroll/PayrollCalculationService` - the payroll engine. Gross = basic + active allowances (fuel by quantity uses the `FuelPrices` row in force on the 1st of the payslip month, Setup -> Fuel prices); deductions are percentage-of-gross or fixed (the fixed amount is stored in `Deduction.PercentageValue`); income tax = annual progressive slab method on basic x 12 (`Payroll/IncomeTaxCalculator`; fixed amount + rate on the part over the slab start, monthly = annual / 12 rounded to the rupee; `docs/reference/pk-income-tax.md`), only when `Employee.ApplyTax` is yes/y/true/1. Creates `Payslips` + `PayslipDetails` (a second row of the same allowance or deduction becomes a line named `... (2)`, because line names are unique per payslip); `RegeneratePayslipAsync` replaces the figures of an unlocked payslip in one transaction.
- Payroll screens: one run page `/payroll/run`, one list `/payslips`, one detail `/payslips/{id}` (decision T10); do not add parallel ones.
- `Payroll/PayslipLockService` - editing an allowance/deduction locks all of that employee's unlocked payslips and writes an `AuditLogs` row. There is no unlock path.
- `WorkScheduleService` - the one place that says which days an employee works: Monday-Saturday by default, a per-employee work week from a date (`ShiftAssignments`, weekday bit mask Mon=1..Sun=64, `ShiftId` NULL until shift times exist), single days in `RosterOverrides`; company holidays (`GazettedHolidays`, `/holidays`) are not work days unless one person is marked as working. Changing any of the three recounts the open leave it touches. Attendance should use it too.
- `LeaveRulesService` - the rules every way of entering leave applies (request screens and Attendance -> Off Days): refusals (not managed, outside employment, overlap, no work day, crosses 31 December), balance warning, approver stamp. Leave `TotalDays` = work days in the range. Leave requests are cancelled, never deleted. Approve/Reject (`/leave/{id}/approve`, `/reject`) run the same rules. Dated notes on a leave request or an employee are `RecordNotes` (`RecordNotesControllers.cs`, partial `_RecordNotes`; Admin/HR; editable, removal hides; changes go to `AuditLogs`, decision T7). Another record type is one more small controller there.
- `AttendanceService` (EPPlus Excel/CSV import), `EmployeeDocumentService` (files under `wwwroot/uploads/employees/{uid}/`, soft delete), `AuditLogService`, `LoginPasswordHasher` (PBKDF2; still accepts legacy SHA-256/plain text).

Routing: attribute routes only, lowercase kebab-case plural URLs (`/employees/143/edit`, `/leave/quotas`, `/payroll/run`); employees in URLs are `uid`; old `/Controller/Action/id` URLs 301-redirect. Every new action needs an `[HttpGet("...")]`/`[HttpPost("...")]` template, and links/forms need `asp-route-id` (a hidden field does not build the URL). Map and rules: `docs/routes.md`.

Navigation: each controller sets `ViewData["Module"]`, and `Views/Shared/_Sidebar.cshtml` picks its menu from that key. Layouts: `_MainLayout` (default via `_ViewStart`), `_LoginLayout`, plus `_SetupLayout`/`_DeductionsLayout` for specific modules.

Projects, departments, sub-departments, designations and leave types are managed lists in their own tables (Setup -> Lists); the remaining dropdowns (for example AllowanceNames, AllowanceTypes) come from rows in the `Configuration` table, edited under Setup.

## Database redesign (development)

- The redesigned schema (described in `db.md`; standards in `docs/db/db_standards.md`, open questions in `docs/db/decisions.md`) is applied to the dev copy and, since 2026-11-24, to production `HRBTK` (historical record: `docs/db/prod_rollout.md`). Scripts: `scripts/database/redesign/` (01-04 expand, 06 contract, 07 user roles, 08 employee history, 09 leave days one decimal, 10 legacy leave archive, 11 leave balances view v2, 12 tax slabs on the annual method (run on production 2026-11-25); run in order, back up first, `sqlcmd -b -I`). New schema changes are written as new re-runnable scripts, tested on the dev copy first.
- The app code targets the redesigned schema. Legacy shapes are kept in code through EF value converters and facade properties (payslip month name, tax year label, `Employee.Department`/`Project`/`Designation` names, attendance `TimeIn`/`TimeOut`/`Status`, deduction `PercentageValue`); in LINQ queries use the real columns/navigations (`e.DepartmentInfo!.DepartmentName`, `a.AttendanceDate`, `d.Percentage`/`d.Amount`).
- Projects, departments, sub-departments, designations and leave types are managed under Setup -> Lists (`LookupsController`, Admin or HR role). Nothing about them is hard-coded.
- Always run the app with `ASPNETCORE_ENVIRONMENT=Development` when testing; without it the app reads `appsettings.Production.json` and connects to the production server.

## Security facts to keep in mind

- `FallbackPolicy` requires an authenticated user everywhere; `[AllowAnonymous]` is only on login/logout/error/DbHealth. `SetupController` requires `Admin` or `HR` (user management `Admin` only); a user can hold several roles (table `UserRoles`, script `07_user_roles.sql`; one role claim per row; manage them with the tick boxes in Setup → Users; the last active Admin cannot be demoted or disabled); `LookupsController` requires `Admin,HR`; employee delete (marks Inactive) requires `Admin`. Other controllers still allow any logged-in user (payroll/salary/reports need a decision on what `HR` may do).
- A global `AutoValidateAntiforgeryToken` filter covers all POSTs; keep `[ValidateAntiForgeryToken]` on actions anyway. Logout is POST (forms in the layouts).
- Login errors are neutral, failed logins are throttled (5 per address+username, 15 min), legacy password formats are re-hashed to PBKDF2 (600k) on login. Auth cookie is HttpOnly, SameSite=Lax, Secure outside Development.
- `/uploads` returns 404 from the web server; documents are served only by `EmployeeDocumentsController.Download`. Set `EmployeeDocuments:StorageBasePath` to store them outside `wwwroot`.
- Deferred to the owner: rotating the old `sa` password (still in git history), least-privilege app login, closing SQL port 1433.

## Known payroll gotchas

- **Run `scripts/database/redesign/15_holiday_notices.sql` on a database before deploying this build to it** (done on the dev copy and on production, 2026-11-26). Without it, leave entry, the month view, work weeks and `/holidays` fail.
- **Run `scripts/database/redesign/14_work_weeks.sql` on a database before deploying this build to it** (done on the dev copy and on production, 2026-11-26).
- **Run `scripts/database/redesign/13_payslip_line_rates_fuel_prices.sql` on a database before deploying this build to it** (done on the dev copy and on production, 2026-11-26). Without it, payslip pages and payroll fail. A build from before that date still reads the `Configuration` fuel price: if it was changed there before the new build went live, add it under Setup -> Fuel prices.
- Income tax: `TaxRules` slabs hold ANNUAL income (`MinSalary`/`MaxSalary`) with `FixedAmount` + `TaxPercentage` on the part over `MinSalary`; the tax year is July-June (`TaxYearStart`, decision T6) derived from the payslip (`Month >= 7 ? Year : Year - 1`). Monthly tax = annual tax / 12 rounded to the rupee, on basic x 12 only (allowances are not taxed; the base is open, B17). Script 12 must be run on a database before this build is deployed to it. Source and slab figures: `docs/reference/pk-income-tax.md`.
- Attendance and leave do not feed payroll yet (`Payslips.WorkingDays/LeaveDays/LeaveBalance` are unused). HR requirements for the planned attendance/schedule/leave rework (hourly pay, 7h minimum day, half days with approval, overnight shifts, per-employee leave quotas, Dastarkhuwan staff without leave) are in the project discussion, not in code; `Attendance`/`EmployeeLeaves` are empty in production.

## UI display

- Show numbers, dates and times in views through `Infrastructure/Fmt` (`Fmt.Amount`, `Fmt.Int`, `Fmt.Number`, `Fmt.Percent`, `Fmt.Days` (leave days, one decimal), `Fmt.Range`, `Fmt.ShortDate`, `Fmt.LongDate`, `Fmt.Time`, `Fmt.DateAndTime`, `Fmt.DateRange`), never `ToString("N2")`/`ToString("yyyy-MM-dd")` in a view. `<input type="date">` values stay ISO. Formats: dates DD/MM/YYYY, times 12-hour, date-times time first (`02:05 PM 23/11/2026`). Colours/spacing come from `wwwroot/css/hrms-theme.css` tokens; tables are aligned by `wwwroot/js/hrms-grid.js`. Standard and examples: `docs/ui_standards.md`, including the section-header, Search & Filters and grid standard (Home and Employees are the templates; pages about one employee use the dossier standard (`employee-dossier.css`, `_DossierHero`); grids show Emp ID plus name over designation, and never any salary figure; do not restate header colours or filter padding in views; form buttons (Save/Cancel/Run) use the shared `.action-bar`, never a fixed or hand-styled button row).

## Docs

- Doc frontmatter says how far to trust a file: `current`, `partially-outdated` (see the listed sections), `outdated`, `historical` or `retired` (never update the last two). `docs/analysis/` is a November snapshot, mostly outdated; `docs/deployment.md` is the deployment reference.
- Commit messages in this repo use a `Prefix: summary` style (`Feat(Payroll): ...`, `Chore:`, `Docs:`).
