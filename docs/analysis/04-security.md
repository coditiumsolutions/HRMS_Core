---
status: partially-outdated
last_verified: 2026-11-26
verified_against: main @ 7aa2685 plus the uncommitted security and user-roles changes (code), and the redesigned development database (scripts 01-04, 06, 07 applied)
supersedes_snapshot: 2026-11-16 (commit 2843f59)
see_also: ../deployment.md, ../db/decisions.md
note_urls: URLs in this file use the attribute routes of 2026-11-24 (old /Home/Login style paths redirect); production still uses the old paths until the next deployment. See ../routes.md.
note: Each finding carries a status as of the verified date. Production still runs the older build; "Fixed" items exist only in this repository (uncommitted at the verified date) until the next deployment.
summary: {fixed: 12, partly: 3, deferred: 1}   # 16 tasks
deployed_to_production: false
tasks:
  - {id: 1,  title: "SetupController anonymous -> Admin only",              status: fixed}
  - {id: 2,  title: "sa password in git history: rotate, least-privilege login, close port 1433", status: deferred, owner: project owner}
  - {id: 3,  title: "Role checks beyond Setup/Lists/employee delete (payroll, salary, reports)", status: partly, blocked_by: "decision on what HR may do"}
  - {id: 4,  title: "Neutral login errors",                                 status: fixed}
  - {id: 5,  title: "Re-hash legacy passwords on login",                    status: fixed, follow_up: "remove plain-text/SHA-256 support after every user has logged in once"}
  - {id: 6,  title: "Secure cookie + HTTPS redirect",                       status: fixed, follow_up: "verify after deployment (only tested over http locally)"}
  - {id: 7,  title: "Login throttle",                                       status: fixed}
  - {id: 8,  title: "Remove Debug and Home/Test",                           status: fixed}
  - {id: 9,  title: "Uploads: block /uploads; move outside wwwroot",        status: partly, follow_up: "set EmployeeDocuments:StorageBasePath and copy existing files"}
  - {id: 10, title: "PII protection, audit coverage, AllowedHosts",         status: partly}
  - {id: 11, title: "Logout POST, PBKDF2 600k",                             status: fixed}
  - {id: 12, title: "_build_verify removed from git",                       status: fixed}
  - {id: 13, title: "Employee soft delete (Admin only)",                    status: fixed}
  - {id: 14, title: "Attendance UploadedBy = signed-in user",               status: fixed}
  - {id: 15, title: "Global antiforgery filter",                            status: fixed}
  - {id: 16, title: "Several roles per user (UserRoles, script 07)",        status: fixed, note: "added 2026-11-23; farhan has Admin+HR on the dev copy"}
reason: "Static analysis written 2026-11-16 and revised to 2026-11-23. The findings are still a useful list, but their not-yet-deployed statements, the task list and the role table were not rechecked. Known change: Setup allows Admin or HR, with user management Admin only."
---
# 4. Security

## Authentication
- **Scheme:** ASP.NET Core cookie authentication (`CookieAuthenticationDefaults`).
- **Login:** `POST /login` (anti-forgery validated). Looks up `Users` by case-insensitive username, verifies password via `LoginPasswordHasher.Verify`, then `SignInAsync`.
- **Cookie settings:** `HttpOnly` (verified in the `Set-Cookie` header), `SameSite=Lax`, `Secure` outside Development (`SameAsRequest` locally), 8-hour expiry with sliding expiration, `IsPersistent = true`. The antiforgery cookie is also HttpOnly by default.
- **Claims:** `Name`, `display_name`, `login_at`, and one `Role` claim per row in `UserRoles` (a user can hold `Admin`, `HR`, both or none; the database CHECK allows only those two). Login also requires `Users.IsActive` (users are disabled, not deleted).
- **Logout:** `POST /Home/Logout` with an antiforgery token (changed 2026-11-23; was a state-changing GET).
- **Password storage** (`LoginPasswordHasher`): PBKDF2-HMAC-SHA256, 600,000 iterations, 16-byte salt, 32-byte key, format `iterations.salt.hash`, constant-time compare. Verification **still accepts legacy SHA-256 hex and plain-text** values, but re-hashes them (and older, lower-iteration hashes) to the current format on the next successful login.
- Lockout: 5 failed attempts per client address + username lock that pair for 15 minutes (in-memory). Still no MFA, no password complexity beyond ≥ 6 characters, no password reset.

## Authorization / protected routes
- `FallbackPolicy = RequireAuthenticatedUser` → **every endpoint requires login unless explicitly `[AllowAnonymous]`**. Good default.
- `[Authorize]` is also on `HomeController` and `ReportsController`.
- **Role authorization exists in one place only.** `LookupsController` (Setup → Lists) has `[Authorize(Roles = "Admin,HR")]`; a signed-in user without the role is sent to `/Home/AccessDenied` (cookie `AccessDeniedPath`, 403). Nothing else checks roles (no policy, no `IsInRole`), so any authenticated user can still create/edit/delete employees, run payroll, view salaries, and so on. Only the roles `Admin` and `HR` exist; the planned `Payroll`/`Viewer` roles do not.

| Route group | Access |
|-------------|--------|
| `/login`, `/logout` (POST), `/error`, `/db-health` (Development only) | Anonymous |
| `/setup/*` (users + configuration) | `Admin` (was anonymous before 2026-11-23) |
| `/` (dashboard), `/privacy` | Any logged-in user |
| `/Home/AccessDenied` | Target of the cookie handler (403) |
| `/Lookups/*` (projects, departments, sub-departments, designations, leave types) | `Admin` or `HR` |
| Employee, EmployeeDocuments, Attendance, LMS, Payroll, Payslips, Allowances, Deductions, Tax, Reports | Any logged-in user (fallback policy) |

## Request-level protections
- **CSRF:** every `[HttpPost]` action in the controllers has `[ValidateAntiForgeryToken]` (rechecked 2026-11-23, none missing). There is still no global `AutoValidateAntiforgeryToken` filter, so a future POST without the attribute would be unprotected. Logout is a GET.
- **Open redirect:** login uses `Url.IsLocalUrl` – OK.
- **Mass assignment:** most create/edit actions use `[Bind(...)]` with explicit field lists – OK; `Payslip`/`Allowance` use view models/bind lists.
- **SQL injection:** EF/LINQ everywhere; the two raw-SQL spots (`Home/Test`, `Debug`) were removed on 2026-11-23.
- **File upload:** extension whitelist (config), 10 MB per file, 10 files per request, GUID stored names, `Path.GetFileName` on the original name, and (new) a SHA-256 duplicate check per employee. Weaknesses (unchanged): no content-type/magic-byte check, files are stored **under `wwwroot`** (potentially directly web-served by `UseStaticFiles`, bypassing the authenticated Download action and the `IsDeleted` flag), `.doc/.xls` macro-capable formats allowed, no antivirus scan. Attendance upload accepts any posted file type into the parser; global request limit is 100 MB.
- **Documents:** requests under `/uploads` return 404 (middleware before `UseStaticFiles`).
- **Output encoding:** Razor auto-encodes; no raw HTML rendering seen in review of controllers.
- **Transport:** `UseHsts` and `UseHttpsRedirection` outside Development; production URL is `https://hr.coditium.com`.
- **Error handling:** custom `/error` in production; login errors are neutral.
- **Audit:** `AuditLogs` records IP/user but only for payslip locking and allowance/deduction changes (`AllowancesController`, `DeductionsController`). Employee, leave, attendance and user changes are not audited. New: `ModifiedBy`/`ModifiedOn` on the main edit screens and `RowVersion` (optimistic concurrency) on `Employee`, `Attendance`, `LeaveRequests`, `Payslips`.

## Security findings
Status as of 2026-11-23, after the security fixes in the repository: **Open**, **Partly**, **Fixed**, **Deferred** (owner decision). "Fixed" means fixed in the code in this repository; production gets it with the next deployment.

| # | Severity | Status | Finding |
|---|----------|--------|---------|
| 1 | **Critical** | **Fixed** (not yet deployed) | `SetupController` was `[AllowAnonymous]`. It now has `[Authorize(Roles = "Admin")]`: anonymous users are sent to login, `HR` users get 403. **Production is exposed until this build is deployed.** |
| 2 | **Critical** | **Deferred** | Connection strings are out of `appsettings.json`. The old `sa` password is still in git history and must be treated as leaked: rotating it, a least-privilege SQL login for the app and closing port 1433 are **deferred to the project owner**. |
| 3 | High | **Partly** | A user can now hold several roles (`UserRoles`; for example HR plus Admin), edited in Setup → Users. Role checks: `Setup` (Admin), `Lookups` (Admin, HR), employee delete (Admin). Payroll, salaries, allowances, deductions, tax and reports are still open to every logged-in user; what `HR` may do needs a decision. |
| 4 | High | **Fixed** | Login now answers "Invalid username or password." for an unknown user, a wrong password, an empty hash and a disabled user, and "Sign-in is temporarily unavailable" for database errors (details only in the server log). Hash-format hints removed. |
| 5 | Medium | **Fixed** | A successful login re-hashes plain-text, SHA-256 and low-iteration values to PBKDF2. Legacy formats are still accepted for that first login, so the support can be removed once every row has been upgraded. |
| 6 | Medium | **Fixed** (not yet deployed) | Auth cookie is `Secure` outside Development, `HttpOnly`, `SameSite=Lax`; `UseHttpsRedirection` and HSTS outside Development. The `Secure` setting could only be tested on plain http locally (Development uses `SameAsRequest`), so check it after deployment. |
| 7 | Medium | **Fixed** (basic) | Login throttle: 5 failures per client address + username locks that pair for 15 minutes (in-memory, resets on app restart, per server). No CAPTCHA or MFA. |
| 8 | Medium | **Fixed** | `DebugController` and `Home/Test` removed. `Home/DbHealth` remains but returns 404 outside Development. |
| 9 | Medium | **Partly** | Anything under `/uploads` now returns 404 from the web server, so documents are only reachable through the authorised `Download` action. Files still live in `wwwroot/uploads` until `EmployeeDocuments:StorageBasePath` is set to a folder outside the site (supported now; see below). |
| 10 | Medium | **Partly** | CNIC, salary and mobile numbers are not protected at field level, `AllowedHosts` is `*`, and the audit trail covers only payroll locks and allowance/deduction edits. `ModifiedBy`/`ModifiedOn`, soft delete, `RowVersion` and `RequestId` guards exist. |
| 11 | Low | **Fixed** | Logout is POST with an antiforgery token (layouts use a form); PBKDF2 raised to 600,000 iterations (existing hashes keep working and are upgraded at login). `IsPersistent = true` is unchanged (8-hour sliding cookie). |
| 12 | Low | **Fixed** | `_build_verify/` is no longer tracked in git. |
| 13 | Medium | **Fixed** | Employee "delete" now sets `EmployeeStatus = Inactive` (and `ModifiedBy`/`ModifiedOn`) instead of removing the row; Admin only. |
| 14 | Low | **Fixed** | Attendance uploads record the signed-in user in `UploadedBy`. |
| 15 | Low | **Fixed** | A global `AutoValidateAntiforgeryToken` filter now covers every POST/PUT/DELETE, in addition to the per-action attributes. |
| 16 | Medium | **Fixed** (not yet deployed) | A user could hold only one role, so an HR user could not also have Admin rights. Roles now live in `UserRoles` (script `07_user_roles.sql`); Setup → Users has a tick box per role, and the last active Admin cannot be demoted or disabled. |

**Uploads outside the site (finding 9).** Set `EmployeeDocuments:StorageBasePath` (absolute path, or relative to the content root) in `appsettings.Production.json`, copy the existing `wwwroot/uploads` folder into it (the stored paths stay `uploads/employees/{uid}/{file}`), then deploy. `DeployToIIS.bat` wipes only the deploy folder, so a path outside `C:\HRMSDeploy` survives releases.

## Recommended fixes (state on 2026-11-23)
1. **Done** (repository): `SetupController` requires `Admin`.
2. **Deferred to the owner:** rotate the `sa` password (in git history), least-privilege app login, close port 1433.
3. **Partly done:** extend role checks to payroll, salary views, Reports (needs the HR/Admin decision).
4. **Done:** global antiforgery filter; Logout is POST.
5. **Done:** neutral login errors, throttle, re-hash legacy passwords. Later: remove plain-text/SHA-256 support once all users have logged in once, add MFA if wanted.
6. **Done** (verify after deployment): `Secure` cookie, HTTPS redirect. If IIS sits behind a proxy that terminates TLS, add forwarded-headers handling.
7. **Partly done:** `/uploads` blocked; move the files with `StorageBasePath`.
8. **Done:** `Debug` and `Home/Test` removed.

## Live data observations (production, 2026-11-16; not re-checked)
- `Users`: 6 rows - roles Admin (4), HR (1) and one with an empty role; all PBKDF2. Roles are not enforced anywhere, so 'Admin' vs 'HR' makes no difference today.
- One user references an `EmployeeID` that no longer exists in `Employee`.

## Local copy (2026-11-23)
The development database is a copy of production with the redesigned schema applied (`../db/prod_rollout.md`). There the roles are in `UserRoles` (limited by a CHECK to `Admin`/`HR`; the old `Users.Role` column is gone), and `Users.EmployeeId` is a nullable FK to `Employee.uid`. Production does not have these changes yet.
