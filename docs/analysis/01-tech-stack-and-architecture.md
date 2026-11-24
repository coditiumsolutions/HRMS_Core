---
status: partially-outdated
last_verified: 2026-11-23
verified_against: main @ 7aa2685 plus the uncommitted security and user-roles changes (code), and the redesigned development database (scripts 01-04, 06, 07 applied)
outdated_since: 2026-11-23
outdated_sections: [Dependency injection (unchanged but incomplete)]
reason: Routing section updated 2026-11-24 (attribute routes, /login, /error, legacy redirects). Controller list (Debug removed, Lookups added), solution layout, request pipeline (antiforgery filter, HTTPS redirect, /uploads block, memory cache for login throttle) and Configuration were updated on 2026-11-23. The pipeline and DI sections still match Program.cs. Models and services gained entities and rules from the database redesign (see 03-data-structures.md and ../db/db_changes.md).
see_also: ../deployment.md, ../db/db_changes.md
---
# 1. Tech Stack & Architecture

## Stack
| Layer | Technology | Evidence |
|-------|-----------|----------|
| Runtime | **.NET 8** (`net8.0`) | `HRMBT.Web.csproj` |
| Web framework | ASP.NET Core MVC, Razor views (server-rendered) | `AddControllersWithViews()` |
| ORM | Entity Framework Core 8.0.11 (SqlServer) | csproj |
| Database | Microsoft SQL Server | `UseSqlServer`, retry-on-failure (5 tries / 30 s) |
| Excel I/O | EPPlus 7.5.2 (`LicenseContext.NonCommercial`) | attendance upload, employee export |
| Auth | Cookie authentication, custom `Users` table, PBKDF2 hashing | `Program.cs`, `LoginPasswordHasher` |
| Front end | Bootstrap 5 + jQuery + jquery-validation (vendored in `wwwroot/lib`), Bootstrap Icons via CDN on the home page, vanilla JS | `wwwroot/`, `Views/` |
| Hosting | IIS (ASP.NET Core Module; `web.config` generated at publish) | `scripts/deploy/DeployToIIS.bat` |
| Dev aids | Razor runtime compilation (Development only) | `Program.cs` |

No SPA, no Node build, no API layer, no test project, no CI.

## Solution layout
```
HRMS.sln
└─ src/HRMBT.Web/            single ASP.NET Core MVC project
   ├─ Program.cs             composition root + pipeline
   ├─ Controllers/ (13)      Home, Employee, EmployeeDocuments, Attendance, LMS, Payroll,
   │                         Payslips, Allowances, Deductions, Tax, Reports, Setup, Lookups
   ├─ Models/ (+ViewModels/) EF entities (incl. Project, SubDepartment, Designation, LeaveType, Shift, AttendancePunch, ...) + view models
   ├─ Data/ApplicationDbContext.cs
   ├─ Services/              AttendanceService, EmployeeDocumentService, AuditLogService,
   │   └─ Payroll/           PayrollCalculationService, PayslipLockService; LoginPasswordHasher
   ├─ Infrastructure/        AuthClaims, PayrollMonthHelper
   ├─ Migrations/            EF migrations (Dec 2025 → Sep 2026) + 2 raw .sql
   ├─ Views/                 Razor views per controller + Shared layouts/partials
   └─ wwwroot/               css, js, lib, images, uploads (employee documents)
scripts/ (deploy/, database/{schema,fixes,tools,redesign})  db.md  docs/db/
```

## Architectural style
- **Layered-light MVC**: controllers talk to `ApplicationDbContext` directly (≈7.7k lines across controllers/services). Only payroll calculation, payslip locking, audit logging, attendance import and document storage are extracted into services. There is no repository/unit-of-work/domain layer.
- The former spec-driven artifacts (`specs/`, `constitution/`, `TASK-COMPLETION-SUMMARY.md`) were removed; the code and `db.md` are the source of truth.
- **Module navigation convention**: each controller sets `ViewData["Module"]`, which `_Sidebar.cshtml` uses to choose the left-menu links.

## Dependency injection (Program.cs)
| Registration | Lifetime |
|--------------|----------|
| `ApplicationDbContext` (SQL Server, retry) | Scoped |
| `PayrollCalculationService` | Scoped |
| `IPayslipLockService` → `PayslipLockService` | Scoped |
| `AttendanceService` | Scoped |
| `IAuditLogService` → `AuditLogService` (+ `IHttpContextAccessor`) | Scoped |
| `IEmployeeDocumentService` → `EmployeeDocumentService`, options section `EmployeeDocuments` | Scoped |
| `FormOptions.MultipartBodyLengthLimit` = 100 MB | – |

## Request pipeline
`UseExceptionHandler("/error")` + `UseHsts()` + `UseHttpsRedirection()` (non-Dev) → a middleware that returns 404 for `/uploads` → `UseStaticFiles` → legacy-URL redirects (`LegacyRouteRedirects`, 301) → `UseRouting` → `UseAuthentication` → `UseAuthorization` → `MapControllers()` (attribute routes only, lowercase URLs; see `../routes.md`). The cookie handler has `LoginPath = /login` and `AccessDeniedPath = /access-denied`. `ApplicationDbContext.SaveChanges` is overridden to fill the attendance and deduction facade columns.
Absent: forwarded-headers, rate limiting, response compression, health checks, structured logging.

## Configuration
`appsettings.json` holds **no connection string** (empty `DefaultConnection`; the app throws at startup if none is set). Real values come from git-ignored `appsettings.Development.json` / `appsettings.Production.json` (templates `*.example.json`) or the `ConnectionStrings__DefaultConnection` environment variable; with `ASPNETCORE_ENVIRONMENT` unset the app runs as Production. `appsettings.json` also has `AllowedHosts: *` and the `EmployeeDocuments` options (upload root `uploads/employees`, 10 MB/file, 10 files/upload, allowed extensions pdf/doc/docx/xls/xlsx/png/jpg/jpeg). Business settings (e.g. `FuelPrice`) live in the DB table `Configuration` and are edited through Setup. Projects, departments, sub-departments, designations and leave types are their own tables, edited under Setup → Lists (`LookupsController`).

## Typical request flow (generate payslips)
1. Browser → `POST /payroll/run` (anti-forgery token).
2. Cookie auth validates → controller checks locked payslips for the month.
3. `PayrollCalculationService.GeneratePayslip(...)` loads employee, active allowances, active/effective deductions, fuel-price config and tax slabs → computes gross/tax/net → writes `Payslips` + `PayslipDetails` in one `SaveChanges`.
4. Redirect with `TempData` message; Razor renders the list.
