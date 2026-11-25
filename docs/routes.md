---
status: current
last_verified: 2026-11-24
applies_to: src/HRMBT.Web/Controllers, src/HRMBT.Web/Program.cs, src/HRMBT.Web/Infrastructure/LegacyRouteRedirects.cs
verified_against: main @ 218de0e plus the uncommitted routing change, checked by crawling the running app (about 250 pages, forms and JSON endpoints) and by requesting old URLs
note: Production still runs the older build with the old URLs (/Home/Login, /Employee/Details/5). The new URLs exist only in this repository until the next deployment.
---
# Routes and URLs

## Standard

- **Attribute routes only.** Every action has an `[HttpGet("...")]` / `[HttpPost("...")]` template and every controller (except Home) has a `[Route("prefix")]`. There is no conventional `{controller}/{action}/{id?}` route any more, so an action without a template is unreachable. Add the template when you add an action.
- **Lowercase, kebab-case, plural nouns:** `/employees`, `/tax-rules`, `/leave/quotas`, `/payroll/run`. `RouteOptions.LowercaseUrls` is on, so generated links stay lowercase.
- **Same verbs everywhere:** list = the prefix itself, `new` (form), `{id}` (details), `{id}/edit`, `{id}/delete` (GET asks, POST does it). Other pages are nouns (`balances`, `quotas`, `salaries`), not verbs with the controller name repeated.
- **Employees in a URL are always `uid`** (the int primary key), for example `/employees/143`. `EmployeeID` (the string code such as `1007`) is only used as a search filter in a query string.
- **Controller and action C# names did not change.** `asp-controller` / `asp-action` / `Url.Action` / `RedirectToAction` still use them; only the URL changed. That keeps view folders, `_ViewStart` layout selection and the sidebar active-link logic working.
- **Link and form rule:** a template with `{id}` (or `{kind}`) needs the value in the link or form. Pass `asp-route-id`; a hidden `<input name="id">` alone does not build the URL (the form gets `action=""`). A delete or edit page may rely on the id already in its own URL.
- **JavaScript** never builds URLs by hand. Grids get templates such as `data-edit-base="/employees/__id__/edit"` from `Url.Action(..., new { id = "__id__" })`; `hrms-grid-selection.js` replaces `__id__`. Do not put an `int` constraint on `{id}` (the `__id__` placeholder would fail link generation).
- `/` is the dashboard; anonymous users get `302 /login?ReturnUrl=%2F`. Cookie `LoginPath` is `/login`, `AccessDeniedPath` is `/access-denied`, the exception handler is `/error` (any verb).

## Old URLs keep working

`LegacyRouteRedirects` (301) maps the old `/Controller/Action/id` form to the new URL of the same action, keeping the query string: `/Employee/Edit/143` → `/employees/143/edit`, `/Lookups/Index?kind=projects` → `/setup/lists/projects`, `/LMS/LeaveQuota` → `/leave/quotas`, `/Home/Login` → `/login`, `/Home/Index` → `/`. The table is built from the actions themselves at the first request, so it needs no maintenance. GET and HEAD only; an old POST gets 404.

## URL map

| Area | URLs |
|---|---|
| Home | `/` dashboard, `/login` (GET, POST), `/logout` (POST), `/privacy`, `/access-denied`, `/error`, `/db-health` (Development only) |
| Employees | `/employees` (list, filters in query), `/employees/dashboard`, `/employees/export` (Excel), `/employees/new`, `/employees/{id}`, `/employees/{id}/edit`, `/employees/{id}/delete` |
| Employee documents | `POST /employee-documents/upload`, `GET /employee-documents/{id}` (download), `POST /employee-documents/{id}/delete` |
| Allowances | `/allowances`, `/allowances/new`, `/allowances/bulk` (GET page, POST create many), `/allowances/{id}`, `/allowances/{id}/edit`, `/allowances/{id}/delete`, JSON `/allowances/employee-lookup?q=`, `/allowances/fuel-price` |
| Deductions | `/deductions`, `/deductions/new`, `/deductions/bulk`, `/deductions/{id}`, `/deductions/{id}/edit`, `/deductions/{id}/delete`, JSON `/deductions/employee-lookup?q=` |
| Attendance | `/attendance`, `/attendance/new`, `/attendance/off-days`, `/attendance/upload`, `/attendance/monthly-report`, `/attendance/by-department`, `/attendance/{id}`, `/attendance/{id}/edit`, `/attendance/{id}/delete` |
| Leave | `/leave` (requests), `/leave/new`, `/leave/{id}`, `/leave/{id}/edit`, `/leave/{id}/delete`, `/leave/balances`, `/leave/quotas`, `/leave/quotas/new`, `/leave/quotas/{id}/edit`, `/leave/quotas/{id}/delete` |
| Payroll | `/payroll` (list), `/payroll/dashboard`, `/payroll/generate` (pick employees; POST `/payroll/generate/selected`), `/payroll/run` (run for everyone), `/payroll/run/single`, `/payroll/new`, `/payroll/search`, `/payroll/employees`, `/payroll/status`, `/payroll/{id}` |
| Payslips | `/payslips`, `/payslips/new`, `/payslips/generate` (redirects to `new`), `/payslips/{id}`, `/payslips/{id}/print`, `/payslips/{id}/delete`, JSON `/payslips/lookup/employees-by-department?department=`, `/payslips/lookup/employees/{uid}` |
| Reports | `/reports`, `/reports/salaries`, `/reports/employees`, `/reports/allowances`, `/reports/deductions`, `/reports/taxes` |
| Tax | `/tax-rules` (one card per tax year; `?taxYear=2025-2026` redirects), `/tax-rules/years/{startYear}` (slabs of one year), `/tax-rules/years/new` (a whole tax year with all its slabs), `/tax-rules/new` (`?year=` pre-selects the year), `/tax-rules/{id}`, `/tax-rules/{id}/edit`, `/tax-rules/{id}/delete` |
| Setup (Admin) | `/setup`, `/setup/users`, `/setup/users/new`, `/setup/users/{id}/edit`, `/setup/users/{id}/delete`, `/setup/config`, `/setup/config/new`, `/setup/config/{id}/edit`, `/setup/config/{id}/delete` |
| Setup lists (Admin, HR) | `/setup/lists/{kind}` where kind is `projects`, `departments`, `subdepartments`, `designations`, `leavetypes`; `/setup/lists/{kind}/new`, `/setup/lists/{kind}/{id}/edit`, POST `/setup/lists/{kind}/{id}/toggle` and `/setup/lists/{kind}/{id}/delete` |

## Open points

- Old URLs are redirected, but anything outside the app that holds an absolute old URL (printed documents, monitoring) should be updated. The `/Home/DbHealth` check, if a monitor uses it, is now `/db-health`.
- Payroll and Payslips overlap in purpose (`/payroll/new` and `/payslips/new` both create a payslip); merging them is a behaviour decision, not a routing one.
