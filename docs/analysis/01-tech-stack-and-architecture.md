---
status: partially-outdated
outdated_since: 2026-11-16
outdated_sections: [Configuration]
reason: The Configuration section lists five connection strings. appsettings.json now has a single active DefaultConnection (production HRBTK on Teezhost 16GB); the others are commented out.
see_also: ../deployment.md
---
# 1. Tech Stack & Architecture

## Stack
| Layer | Technology | Evidence |
|-------|-----------|----------|
| Runtime | **.NET 8** (`net8.0`) | `HRMBT.Web.csproj` (README claims .NET 9 – inaccurate) |
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
   │                         Payslips, Allowances, Deductions, Tax, Reports, Setup, Debug
   ├─ Models/ (+ViewModels/) EF entities + ~17 view models
   ├─ Data/ApplicationDbContext.cs
   ├─ Services/              AttendanceService, EmployeeDocumentService, AuditLogService,
   │   └─ Payroll/           PayrollCalculationService, PayslipLockService; LoginPasswordHasher
   ├─ Infrastructure/        AuthClaims, PayrollMonthHelper
   ├─ Migrations/            EF migrations (Dec 2025 → Sep 2026) + 2 raw .sql
   ├─ Views/                 Razor views per controller + Shared layouts/partials
   └─ wwwroot/               css, js, lib, images, uploads (employee documents)
scripts/ (deploy/, database/{schema,fixes,tools})  db.md
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
`UseExceptionHandler("/Home/Error")` + `UseHsts()` (non-Dev) → `UseStaticFiles` → `UseRouting` → `UseAuthentication` → `UseAuthorization` → conventional route `{controller=Home}/{action=Login}/{id?}`.
Absent: `UseHttpsRedirection`, forwarded-headers, rate limiting, response compression, health checks, structured logging.

## Configuration
`appsettings.json` holds five connection strings (`DefaultConnection`, `…2/3/4`, `BTDatabase`), `AllowedHosts: *`, and `EmployeeDocuments` options (upload root `uploads/employees`, 10 MB/file, 10 files/upload, allowed extensions pdf/doc/docx/xls/xlsx/png/jpg/jpeg). Business settings (e.g. `FuelPrice`) live in the DB table `Configuration` and are edited through Setup.

## Typical request flow (generate payslips)
1. Browser → `POST /Payroll/GeneratePayslips` (anti-forgery token).
2. Cookie auth validates → controller checks locked payslips for the month.
3. `PayrollCalculationService.GeneratePayslip(...)` loads employee, active allowances, active/effective deductions, fuel-price config and tax slabs → computes gross/tax/net → writes `Payslips` + `PayslipDetails` in one `SaveChanges`.
4. Redirect with `TempData` message; Razor renders the list.
