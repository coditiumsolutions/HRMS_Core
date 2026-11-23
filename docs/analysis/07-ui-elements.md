---
status: partially-outdated
last_verified: 2026-11-23
verified_against: main @ ddc0ef8 plus the uncommitted UI work (Fmt, hrms-theme.css, hrms-grid.js, Employee Details restructure), checked in the running app with headless-Chrome screenshots, and the redesigned development database
outdated_since: 2026-11-23
outdated_sections: [Pages by module (partly)]
reason: Updated on 2026-11-23 for the Lookups views, the shared org-fields partial, the role tick boxes, the Logout button, the display standard and the site-wide restyle (hrms-theme.css, hrms-grid.js). Navigation and per-page sections were not re-verified line by line.
see_also: 06-gaps.md
---
# 7. UI Elements

Server-rendered Razor (`.cshtml`) – 102 view files (counted 2026-11-23), Bootstrap 5 styling, a small amount of vanilla JS/jQuery. No SPA components.

## Layouts & shell
| Layout | Used for |
|--------|----------|
| `_ViewStart.cshtml` → `_MainLayout` | Default for all authenticated pages |
| `_MainLayout.cshtml` | Two-tier header: white top bar (brand "HR Management System", signed-in user display name + login time, logout) and dark-navy module bar (`#0b1c3d`, gold `#D4AF37` bottom border); left sidebar; content area; renders `Styles`/`Scripts` sections; loads Bootstrap, `site.css`, scoped CSS bundle |
| `_Layout.cshtml` | Older/alternative shell (placeholder sidebar links "Link 1…") |
| `_LoginLayout.cshtml` | Centred login card |
| `_SetupLayout.cshtml`, `_DeductionsLayout.cshtml` | Module-specific variants (Setup and Deductions `_ViewStart`s select them) |

**Palette:** navy `#0b1c3d`/`#00203f`, teal `#336B87`/`#2a5a73`, gold `#D4AF37`/`#f0d78c`, light grey backgrounds.

## Navigation
- **Top module bar:** Home, Employees, Payroll, LMS, and a *Modules* dropdown (Allowances, Deductions, Attendance, Tax, Reports, Setup).
- **Left sidebar (`_Sidebar`)** is context-sensitive via `ViewData["Module"]`, with emoji icons:
  - Home: all modules
  - Employees: Dashboard, Add Record, All Records
  - Payroll: Dashboard, Employee Detail, Generate Payroll, Run Payroll, Payroll Slips, Payroll Status (also a dedicated `_PayrollSidebar`)
  - Attendance: Today, By Department, Summary, Off-Day (plus `_AttendanceSidebar`)
  - LMS: Add Leave, View Leaves, Leaves Quota, Leave Balance, Leave Reports
  - Tax: Tax slabs, New slab · Reports: Payroll / Employee / Allowances / Deductions / Taxes summaries
  - Allowances: list, Add single, Add multi · Deductions: all, bulk add, single (`_DeductionsSidebar`)

## Pages by module (≈ views)
| Module | Views |
|--------|-------|
| Home | `Login`, `Index` (dashboard of "setup tiles" with icon header, KPI counts, gradient tile headers with gold underline), `Privacy`, `Error` |
| Employee | `Index` (filterable, sortable, paged data grid with status badges, export button, selectable rows), `Dashboard`, `Create`/`Edit` (card-grouped sections), `Details` (profile incl. salary), `Delete`, partial `_EmployeeDocuments` (upload/list/download/delete) |
| Attendance | `Index`, `Create`, `Edit`, `Delete`, `Details`, `OffDay`, `Upload`, `UploadResult`, `MonthlyReport`, `AttendanceByDepartment` |
| LMS | `Index`, `Create`, `Edit`, `Delete`, `Details`, `LeaveQuota` (+ create/edit/delete, `_LeaveQuotaForm`), `LeaveBalance` |
| Payroll | `Dashboard`, `Index`, `EmployeeDetail`, `Generate`, `GeneratePayslips`, `GenerateIndividual`, `PayrollStatus`, `Search`, `Create`, `Details`, `Edit`, `Delete` |
| Payslips | `Index`, `Create`, `Details`, `Print` (print-optimised with Print button), `Delete` |
| Allowances / Deductions | `Index`, `Add` (bulk), `Create`, `Edit`, `Details`, `Delete`, `_FuelAllowanceFormScript` |
| Tax | `Index`, `Create`, `Edit`, `Details`, `Delete` |
| Reports | `SalariesSummary`, `EmployeeSummary`, `AllowancesSummary`, `DeductionsSummary`, `TaxesSummary` |
| Setup | `Index` (tiles incl. Lists), `Users`, `CreateUser`, `EditUser`, `DeleteUser`, `Config`, `CreateConfig`, `EditConfig`, `DeleteConfig` |
| Lookups (Setup → Lists) | `Index` (one list per kind), `Edit` (create/edit form) |
| Setup users | `CreateUser`/`EditUser` have an Admin and an HR tick box (several roles per user); the Users list shows all roles |
| Logout | a POST form button in the three layouts (was a link) |
| Employee partial | `_EmployeeOrgFields` (project/department/sub-department/designation selects shared by Create and Edit) |
| Home | also `AccessDenied` (403 page) |

## Display standard (added 2026-11-23)
Numbers, ranges, dates and times in views go through `Infrastructure/Fmt`: dates `23/11/2026` (DD/MM/YYYY; long form `Monday, 23 November 2026`), times 12-hour `02:05 PM`, date-times time first `02:05 PM 23/11/2026`, amounts `12,345.50`. See `../ui_standards.md`.

## Look and feel (added 2026-11-23)
One design layer, `wwwroot/css/hrms-theme.css`, is linked after the inline layout styles in `_MainLayout`, `_SetupLayout`, `_DeductionsLayout` and `_LoginLayout`. Direction: warm paper canvas, navy ink, brass accent, hairline borders, Fraunces headings with Source Sans 3 body (Google Fonts; falls back to Georgia/system fonts offline). Base text size is 18.75px (19.4px on wide screens), 25% above the Bootstrap default of the first version, and all rem-based sizes scale with it. The login page has a show/hide password eye button (inline SVG, no icon font needed). It re-points Bootstrap variables and restyles the top bars, sidebar, cards, tables, forms, buttons, badges, alerts and pagination, and adds a small-screen layout (sidebar stacks), print rules (chrome hidden) and reduced-motion handling. `wwwroot/js/hrms-grid.js` aligns table columns by content. The sidebar has one width on every page (`--hr-sidebar-w`); before, the three layouts used 16.7%, col-2 and col-md-3/col-lg-2. Page CSS still loads after the theme and wins where it overrides. Per-view inline styles were not removed; hard-coded Bootstrap blue and blue row stripes were swapped for theme tokens.

## Employee details page (changed 2026-11-23)
Tabs: Employee Info, Salary Info, Leave Balance, Employee History, Attachments. Personal Info and Official Info are one "Employee Info" section; "Additional Info" (details, modified by/on, contact note) is a collapsed block at the bottom of Employee Info. Older saved tab keys (`personal`, `official`, `additional`) open Employee Info.

## Recurring UI components
- **Data grids:** Bootstrap tables with filter bars (department, search, status, month/year), column sort arrows (`bi-arrow-down-up`/`bi-sort-up`), `PaginatedList<T>` pager that preserves filters, row selection with "select all" (`hrms-grid-selection.js`, `_HrmsGridSelectionScript`).
- **Forms:** two-column "label/value" tables (`pd-label`), card-grouped sections, `asp-validation-for` messages with jQuery-validation, TempData success/error alerts, validation-summary banners (login).
- **Status badges:** coloured pills for Active/Inactive/Pending/Leave, Locked/Unlocked payslips, attendance P/A/L/H.
- **Action buttons:** unified icon buttons for View/Edit/Delete (Bootstrap Icons), confirmation pages for deletes (GET confirm → POST).
- **Cascading selects & AJAX:** payslip create uses `EmployeesByDepartment` and `EmployeeDetails` JSON endpoints; Allowance/Deduction forms use `FindEmployee` lookup; `_PayrollMonthSelect` partial for month/year pickers.
- **Dashboards:** KPI tiles (employees, attendance, leaves, payslips), department attendance summary, payroll dashboard by month.
- **Printing/export:** payslip print page via `window.print()`, Excel export of employee list (EPPlus), Excel/CSV upload form with result page.
- **Documents panel:** multi-file upload with allowed-type hint, list with download/delete.

## Front-end assets
| Asset | Notes |
|-------|-------|
| `wwwroot/lib` | Bootstrap, jQuery, jquery-validation(+unobtrusive) – vendored |
| `css/` | `site.css` (118 lines), `employee-create.css` (755), `employee-list.css` (488), `payslips.css` (602), `home-dashboard.css` (252) |
| `js/` | `site.js` (stub), `employee-create.js`, `employee-details.js`, `hrms-grid-selection.js` |
| `images/` | `employee-avatar-placeholder.svg`, `favicon.ico` |
| CDN | Bootstrap Icons 1.11.3 (jsDelivr) referenced from many views – needs internet access on the client (intranet risk) |

## UI/UX observations
- Consistent navy/teal/gold identity and responsive Bootstrap grid, but the sidebar is a fixed `col-2` and `vh-100`, so small screens are cramped.
- Heavy per-view inline `<style>` and duplicated CSS; mixture of emoji and Bootstrap-icon iconography.
- Some sidebar entries are duplicates pointing to the same action; `_Layout.cshtml` still has placeholder links.
- No role-based menu hiding (all users see Setup, Payroll, etc.), no breadcrumbs, no dark mode, limited accessibility labelling, English-only.
- Not built yet: screens for shifts, rosters, attendance punches, half-day requests, attendance corrections and record notes (tables exist).
