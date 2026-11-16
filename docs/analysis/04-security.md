---
status: partially-outdated
outdated_since: 2026-11-16
outdated_sections: [Security findings #2]
reason: Finding #2 describes credentials for 5 servers incl. the old public IP 178.105.20.255. appsettings.json now has one active DefaultConnection (production, Teezhost 16GB) and the old strings are commented out, but the sa credentials are still committed in the file and in git history, and SQL port 1433 is open to the internet.
see_also: ../deployment.md
---
# 4. Security

## Authentication
- **Scheme:** ASP.NET Core cookie authentication (`CookieAuthenticationDefaults`).
- **Login:** `POST /Home/Login` (anti-forgery validated). Looks up `Users` by case-insensitive username, verifies password via `LoginPasswordHasher.Verify`, then `SignInAsync`.
- **Cookie settings:** `HttpOnly`, `SameSite=Lax`, 8-hour expiry with sliding expiration, `IsPersistent = true`. `Secure` policy is **not** set (cookie is sent over plain HTTP).
- **Claims:** `Name`, `display_name`, `login_at`, and `Role` (from `Users.Role`, free text, optional).
- **Logout:** `GET /Home/Logout` (anonymous allowed, state-changing GET).
- **Password storage** (`LoginPasswordHasher`): PBKDF2-HMAC-SHA256, 100,000 iterations, 16-byte salt, 32-byte key, format `iterations.salt.hash`, constant-time compare. Verification **also accepts legacy SHA-256 hex and plain-text** values stored in `PasswordHash`.
- No lockout, no MFA, no password-complexity beyond ≥ 6 characters, no password reset, no forced change for legacy plain-text accounts.

## Authorization / protected routes
- `FallbackPolicy = RequireAuthenticatedUser` → **every endpoint requires login unless explicitly `[AllowAnonymous]`**. Good default.
- `[Authorize]` is also on `HomeController` and `ReportsController`.
- **There is no role/policy authorization anywhere.** `ClaimTypes.Role` is issued but no `[Authorize(Roles=…)]`, policy or `IsInRole` check exists. Any authenticated user can create/edit/delete employees, run payroll, view salaries, manage users, etc.

| Route group | Access |
|-------------|--------|
| `/Home/Login`, `/Home/Logout`, `/Home/Error`, `/Home/DbHealth` (Development only) | Anonymous |
| **`/Setup/*` (users + configuration)** | **Anonymous – `[AllowAnonymous]` on the whole `SetupController`** |
| `/Home/Index`, `/Home/Privacy`, `/Home/Test` | Any logged-in user (`Test` dumps DB table names and counts) |
| `/Debug` | Any logged-in user (same table dump as `Home/Test`) |
| Employee, EmployeeDocuments, Attendance, LMS, Payroll, Payslips, Allowances, Deductions, Tax, Reports | Any logged-in user (fallback policy) |

## Request-level protections
- **CSRF:** `[ValidateAntiForgeryToken]` on all inspected POST actions (≈ 50 occurrences across controllers). No global `AutoValidateAntiforgeryToken` filter, so a future POST without the attribute would be unprotected. Logout is a GET.
- **Open redirect:** login uses `Url.IsLocalUrl` – OK.
- **Mass assignment:** most create/edit actions use `[Bind(...)]` with explicit field lists – OK; `Payslip`/`Allowance` use view models/bind lists.
- **SQL injection:** EF/LINQ everywhere; the two raw-SQL spots (`Home/Test`, `Debug`) interpolate only a hard-coded table list.
- **File upload:** extension whitelist (config), 10 MB per file, 10 files per request, GUID stored names, `Path.GetFileName` on the original name. Weaknesses: no content-type/magic-byte check, files are stored **under `wwwroot`** (potentially directly web-served by `UseStaticFiles`, bypassing the authenticated Download action and the `IsDeleted` flag), `.doc/.xls` macro-capable formats allowed, no antivirus scan. Attendance upload accepts any posted file type into the parser; global request limit is 100 MB.
- **Output encoding:** Razor auto-encodes; no raw HTML rendering seen in review of controllers.
- **Transport:** `UseHsts` only outside Development; **no `UseHttpsRedirection`**; production URL is plain `http://…:82`.
- **Error handling:** custom `/Home/Error` in production. However the login page returns very verbose messages (see below).
- **Audit:** `AuditLogs` records IP/user but only for payslip locking.

## Security findings
| # | Severity | Finding |
|---|----------|---------|
| 1 | **Critical** | `SetupController` is `[AllowAnonymous]`: anyone who can reach the site can list users (incl. hashes), **create an account with any role, reset any user's password, delete users**, and edit `Configuration` (e.g. `FuelPrice`, which drives payroll). Full unauthenticated takeover. |
| 2 | **Critical** | **Database credentials committed in `appsettings.json`**: `sa` account with a shared password for 5 servers incl. public IP `178.105.20.255,50001`, in git history. Rotate immediately; use user-secrets/environment variables/Key Vault and a least-privilege SQL login. |
| 3 | High | No RBAC. `Users.Role` is cosmetic; payroll, salary data, user admin and deletes are open to every logged-in user. |
| 4 | High | Login errors disclose internals: "No row in Users for username …" (user enumeration), password-format hints (`DescribeStoredFormat`), SQL exception text. |
| 5 | Medium | Plain-text and unsalted SHA-256 password formats are still accepted by the code with no upgrade-on-login. Live check (2026-11-16): all 6 users already use PBKDF2, so this is a latent risk (e.g. a row inserted by SQL). |
| 6 | Medium | Cookie lacks `Secure`; site served over HTTP on a public IP; no HTTPS redirection. |
| 7 | Medium | No brute-force protection (lockout/rate limiting/CAPTCHA). |
| 8 | Medium | `Home/Test` and `Debug` expose schema/table counts to any user; `Debug` has no explicit attributes or environment guard. |
| 9 | Medium | Employee documents stored in web root → likely directly downloadable by URL without auth (verify `UseStaticFiles` serves `/uploads/...`). Move outside `wwwroot`. |
| 10 | Medium | PII/payroll (CNIC, salary, mobile) not field-level protected; audit trail missing for most data changes; `AllowedHosts: *`. |
| 11 | Low | Logout via GET (CSRF logout); `IsPersistent=true` forces a persistent cookie for everyone; 100,000 PBKDF2 iterations is below current OWASP guidance (600k for SHA-256). |
| 12 | Low | `_build_verify/` (compiled binaries incl. `appsettings.json` copy) is committed to git. |

## Recommended minimum fixes (in order)
1. Remove `[AllowAnonymous]` from `SetupController`; gate with an `Admin` policy.
2. Rotate DB credentials and remove secrets from the repo/history.
3. Add roles/policies (`Admin`, `HR`, `Payroll`, `Viewer`) and apply per controller/action.
4. Add global `AutoValidateAntiforgeryToken`, convert Logout to POST.
5. Neutral login errors, lockout/rate limiting, upgrade/force-reset legacy password formats.
6. HTTPS everywhere + `CookieSecurePolicy.Always`.
7. Move uploads outside `wwwroot` and serve only through the authorised action.

## Live data observations (2026-11-16)
- `Users`: 6 rows - roles Admin (4), HR (1) and one with an empty role; all PBKDF2. Roles are not enforced anywhere, so 'Admin' vs 'HR' makes no difference today.
- One user references an `EmployeeID` that no longer exists in `Employee`.
