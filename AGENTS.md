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
- `DeployToIIS.bat` aborts if `appsettings.Production.json` is missing, because `dotnet publish` must copy it. The script also **wipes the deploy folder, including `wwwroot/uploads`**.
- Never reintroduce credentials into committed files (the old `sa` password is still in git history).
- The repo path contains `[D]`: in PowerShell use `-LiteralPath` and avoid `Resolve-Path`, which treats brackets as wildcards.

## Database

- Production DB is `HRBTK` (SQL Server 2022). `db.md` is the hand-maintained schema document (verified against the live DB); keep its version/date/Change History current whenever the schema changes. `docs/db-schema-export.txt` is a raw dump to diff against it.
- **Migrations are not trustworthy.** Six repo migrations were applied by hand and are missing from `__EFMigrationsHistory`, and one recorded migration has no file. Do not run `dotnet ef database update` against production without reading the "Known issues" in `db.md`. Schema changes have been applied via SQL in `scripts/database/`.
- Several EF entities do not match the live tables (`CarryforwardLeaves`, `GazettedHolidays`; `Configuration`, `EmployeeLeaves`, `LeaveQuota` have no PK in the DB). Check `db.md` before touching them.
- Employees are identified two ways: `Employee.uid` (int) is the FK for payroll tables; `Employee.EmployeeID` (string, unique) is the key used by Attendance, EmployeeLeaves, Users with no FK. Department/Project/Designation are free-text strings on `Employee`.

## Architecture

Controllers call `ApplicationDbContext` directly (`Data/ApplicationDbContext.cs`, fluent config in `OnModelCreating`); only these concerns are extracted into `Services/`:

- `Payroll/PayrollCalculationService` - the payroll engine. Gross = basic + active allowances (fuel allowance uses the `FuelPrice` row in the `Configuration` table); deductions are percentage-of-gross or fixed (the fixed amount is stored in `Deduction.PercentageValue`); income tax = basic x slab %, only when `Employee.ApplyTax` is yes/y/true/1. Creates `Payslips` + `PayslipDetails`.
- `Payroll/PayslipLockService` - editing an allowance/deduction locks all of that employee's unlocked payslips and writes an `AuditLogs` row. There is no unlock path.
- `AttendanceService` (EPPlus Excel/CSV import), `EmployeeDocumentService` (files under `wwwroot/uploads/employees/{uid}/`, soft delete), `AuditLogService`, `LoginPasswordHasher` (PBKDF2; still accepts legacy SHA-256/plain text).

Navigation: each controller sets `ViewData["Module"]`, and `Views/Shared/_Sidebar.cshtml` picks its menu from that key. Layouts: `_MainLayout` (default via `_ViewStart`), `_LoginLayout`, plus `_SetupLayout`/`_DeductionsLayout` for specific modules.

Config-driven dropdowns (Department, Designation, Project, LeaveTypes, AllowanceNames, ...) come from rows in the `Configuration` table, edited under Setup.

## Security facts to keep in mind

- `FallbackPolicy` requires an authenticated user everywhere; `[AllowAnonymous]` is used on login/logout/error and, currently, **the whole `SetupController`** (user and config management) - a known critical gap.
- `Users.Role` is copied into the auth cookie but nothing enforces roles yet.
- All state-changing actions use `[ValidateAntiForgeryToken]` individually (no global filter) - add it to every new POST.

## Known payroll gotchas

- `TaxRules.TaxYear` is stored as `2025-2026`/`2026-2027`, but the payroll lookup compares it to the payslip year (`2026`); nothing matches and all slabs are pooled. Fix this before relying on tax output.
- Attendance and leave do not feed payroll yet (`Payslips.WorkingDays/LeaveDays/LeaveBalance` are unused). HR requirements for the planned attendance/schedule/leave rework (hourly pay, 7h minimum day, half days with approval, overnight shifts, per-employee leave quotas, Dastarkhuwan staff without leave) are in the project discussion, not in code; `Attendance`/`EmployeeLeaves` are empty in production.

## Docs

- `docs/analysis/` - static analysis (stack, functionality, data, security, deployment, gaps, UI). `05-deployment.md` is marked outdated in its frontmatter; `docs/deployment.md` is current. Docs with `status: outdated` frontmatter should not be trusted for server/IP details.
- Commit messages in this repo use a `Prefix: summary` style (`Feat(Payroll): ...`, `Chore:`, `Docs:`).
