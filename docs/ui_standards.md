---
status: current
last_verified: 2026-11-23
applies_to: src/HRMBT.Web/Views (Razor), wwwroot/css/hrms-theme.css, wwwroot/js/hrms-grid.js
implementation: src/HRMBT.Web/Infrastructure/Fmt.cs, wwwroot/css/hrms-theme.css, wwwroot/js/hrms-grid.js
---
# UI display standards

How values are shown on screen. One helper, `Fmt` (`Infrastructure/Fmt.cs`), decides all of it, so a page looks the same on any server and every screen agrees. It always uses the invariant culture (comma thousands, dot decimals, English day and month names), never the server's regional settings. Null or blank values show as an em dash (—).

In a view write `@Fmt.Amount(Model.NetSalary)`, not `@Model.NetSalary.ToString("N2")`.

## The formats

| Kind | Helper | Example | Notes |
|---|---|---|---|
| Whole number | `Fmt.Int(x)` | 12,345 | Counts, KPI tiles, rounded totals. |
| Amount (money value, two decimals) | `Fmt.Amount(x)` | 12,345.50 | Salaries, allowances, deductions, tax. |
| Amount with currency | `Fmt.Money(x)` | PKR 12,345.50 | Use when the currency is not already in the column header. |
| Quantity | `Fmt.Number(x)` | 12, 12.5, 1,200.25 | Up to two decimals, trailing zeros dropped. Quantities. |
| Leave days | `Fmt.Days(x)` | 10.0, 0.5, -1.5 | Always one decimal (leave is tracked in half days). |
| Percentage (given as 12.5) | `Fmt.Percent(x)` | 12.5% | No space before the sign. |
| Number range | `Fmt.Range(from, to)` | 10 – 20 | En dash with spaces. Open end: `50,000 and above`; open start: `up to 20`. `money: true` for two decimals. |
| Short date | `Fmt.ShortDate(x)` | 23/11/2026 | Always DD/MM/YYYY, never YYYY-MM-DD or YYYY/MM/DD. The default for every date. |
| Long date | `Fmt.LongDate(x)` | Monday, 23 November 2026 | Day first, month spelled out. Headings and printed documents only. |
| Time | `Fmt.Time(x)` | 02:05 PM | 12-hour with AM/PM, no seconds. 24-hour is never used. |
| Time with seconds | `Fmt.TimeWithSeconds(x)` | 02:05:09 PM | Only where seconds matter (audit stamps). |
| Date and time | `Fmt.DateAndTime(x)` | 02:05 PM 23/11/2026 | **Time first, then date.** Created/modified/generated stamps. |
| Date and time with seconds | `Fmt.DateAndTimeWithSeconds(x)` | 02:05:09 PM 23/11/2026 | Same order. |
| Long date and time | `Fmt.LongDateAndTime(x)` | 02:05 PM Monday, 23 November 2026 | Same order. |
| Date range | `Fmt.DateRange(from, to)` | 01/01/2026 – 05/01/2026 | One day shows once. Open end: `from 01/01/2026 onwards`. |

`Fmt.ShortDate(string)` and `Fmt.DateAndTime(string)` exist for the legacy text columns `Employee.DOB` and `Employee.ModifiedOn`: they format the value when it parses as a date and show it as stored when it does not.

## Order rule

A value with both a time and a date is written **time first, then date** (`02:05 PM 23/11/2026`), at every precision (with seconds, with a long date). Never date first, never 24-hour.

## What stays ISO (do not use `Fmt` here)

- `value="..."` of `<input type="date">` and `<input type="time">`: browsers require `yyyy-MM-dd` / `HH:mm`. The browser shows the box in the user's own regional format (DD/MM/YYYY on Windows set to Pakistan or UK); the app cannot change that.
- Values stored as text in the database (`Employee.DOB`, `Employee.ModifiedOn` keep `yyyy-MM-dd HH:mm`); views convert them with `Fmt.ShortDate(string)` / `Fmt.DateAndTime(string)`.
- Anything read back by code: JSON, query strings, hidden fields, data attributes, file names, log lines, Excel export cells that should stay numeric or real dates.

## Rules for new views

1. Do not call `ToString("...")` with a format on a number or date in a view; use `Fmt`.
2. Add a new format to `Fmt` (and to the table above) rather than inventing one in a view.
3. Right-align numeric columns (`text-end`), keep dates `text-nowrap`.
4. Empty values: show `—`. `Fmt` already does this for nulls, so do not add `?? "—"` after a `Fmt` call.

## Text and grid standard (hrms-theme.css, hrms-grid.js)

- **Alignment:** `hrms-grid.js` right-aligns any table column whose cells are all numbers (amounts, counts, percentages, days), keeps date/time columns on one line, and aligns the header with its column. Columns holding inputs, badges or buttons are left alone. Opt a table or column out with `data-align="none"`; a hand-set `text-start`/`text-center` wins.
- **Numbers:** tabular figures everywhere, so digits line up down a column.
- **Type:** Source Sans 3 for text, Fraunces for page headings. Base size 18.75px (19.4px at 1400px and wider); use rem so sizes scale from `html` in `hrms-theme.css`, never fixed px.
- **Headers:** small uppercase labels on a warm tint; secondary lines 0.78rem under the main line.
- **Cells:** long words wrap, dates and numbers never wrap; row padding 0.6rem (0.4rem for `table-sm`); hover is a light teal.
- **Label/value grids** (details pages, forms): a quiet muted label column (16% wide, min 7.5rem) beside a strong value column.
- **Action columns:** `col-actions`, right-aligned, never wrapped.
- **Sidebar width:** one value, `--hr-sidebar-w` (12.5rem) in `hrms-theme.css`, used by the Main, Setup and Deductions layouts. Do not set a width or `col-*` class on a sidebar in a layout or view. Phones stack the sidebar above the content. Only the Main layout has the collapse button.
- **Spacing scale:** `--hr-s1`..`--hr-s6` (0.25rem to 2rem); page padding `--hr-s5`/`--hr-s6`, card gaps `--hr-s5`.
- **Colour tokens** (`--hr-navy-*`, `--hr-teal-*`, `--hr-brass-*`, `--hr-ink*`, `--hr-line*`) replace hard-coded Bootstrap blue in views and page CSS; use them for new work.

## Page and section standard (2026-11-24, Home and Employees are the templates)

Everything below lives in `hrms-theme.css`; page CSS and views should not restate it.

- **Dropdowns (two tiers):** a short list stays a native `<select class="form-select">`; on Chrome/Edge 135+ and Safari with a mouse its popup is themed in `hrms-theme.css` (`appearance: base-select`, opens upward when there is no room below), Firefox and touch devices keep the OS list. A select with 15 or more options (departments, designations, projects, employees) becomes a type-to-filter box automatically (`wwwroot/js/hrms-select.js`, Tom Select in `wwwroot/lib/tom-select`, list appended to `body` so cards and sticky bars never clip it). The original `<select>` stays in the page, so forms, validation and `onchange` keep working. `data-native` opts a select out, `data-searchable` forces it on; after a script only sets `select.value`, call `hrmsSelectRefresh(select)` (rewriting the options needs nothing). Do not add another dropdown library.
- **Section header** (every `.card > .card-header`, `.filter-card`, `.grid-card`, `.section-card`, `.actions-card`, `.lms-panel`, and the Home/Setup `.setup-tile-header`): navy gradient `#00203f` to `#0b2d52`, white 0.9rem semibold text, 3px brass underline (`--hr-brass-500`), brass icon (`#f0d78c`), padding `0.4rem 1rem`. Headers that carry meaning (`bg-success`, `bg-warning`, `bg-danger`, `bg-info`) and the login card keep their own colour. Do not set a header colour in a view.
- **Search & Filters section:** use `.filter-card` with the title `Search & Filters` (capital F). It is compact by default: card body padding `0.5rem 0.85rem`, 2rem controls, 0.66rem uppercase labels, `0.5rem` gutters. Do not add `p-3 p-lg-4` to its body.
- **Page width:** a page wrapper that contains a `.filter-card` has no side padding, so filter and grid sections use the full content width (about 24px from the sidebar). For other pages use `container-fluid px-0` instead of `px-3 px-lg-4`.
- **Second top bar** (brand and user): 45px tall in every layout.
- **Text and forms inside a section card (2026-11-25):** `.grid-card .card-body` has **no padding** so a table can run edge to edge. Anything else in a `.grid-card` (form fields, an intro line, buttons, a note) goes in `<div class="card-body grid-form">` (padding `1rem 1.25rem`), or text in `.grid-note`. A bare `card-body` holding fields puts the text against the card edge (seen on New tax year). A card that holds both a form and a table (slab entry rows) uses `grid-form`, with the table in `table-wrap`. Inputs are the normal `form-control` (2.2rem), not `form-control-sm`; small helper text uses `form-text` or `text-muted small`. Buttons that are not destructive (Back, Cancel, helper actions) are outlined (`btn-outline-secondary`); the red `btn-grid-delete` style is for delete only.
- **Tax rules pages (2026-11-25):** `/tax-rules` shows one `setup-tile` card per tax year (the whole card is the link; a brass "Current" tag marks the current year); the year page is a `grid-card` table with the standard Actions column; entry pages carry the `_TaxSourceNote` partial (link to the source site and "contact the software department"). Details and Delete pages of a non-employee record use the same label | value pattern as the dossier (`tax-detail-table`: four columns, tinted small-caps labels, semibold values) inside a `grid-card`, with a pale red consequence note (`tax-danger-note`) and Back (outlined) plus the red action on the right for confirmations; no internal record ids. Create and Edit use the same two-card layout (a section per group of fields). Page CSS is `wwwroot/css/tax-rules.css`, scoped by `.tax-page`.
- **Overview cards (Home, Setup):** the whole card is the link (`<a class="setup-tile">`), no "View" button. Compact body (`0.6rem 0.85rem`), brass left edge, number in dark brass `#9a7b10`. Related counts go in one card (Home: Employees shows Total and Active).
- **Grid** (Employees is the template): header row 0.5rem vertical padding, 0.78rem uppercase titles; record rows 60px (65px when a cell has a second line); status pill 0.66rem with a 0.38rem dot.
- **Row actions:** an Actions column (`col-actions`, `data-align="none"` on the `th`) with an Edit icon (`bi-pencil-square`) and a Delete icon (`bi-trash3`; Employees shows it to Admin only because only Admin may delete employees), both plain links (`row-action row-action-edit|delete`) with `title` and `aria-label`. Delete opens the existing confirmation page. Rows keep the click highlight (`.is-selected`) and double-click to open; there is no radio button and no Edit/Delete button in the grid header.
- **`hrms-grid-selection.js`:** works without radios. A grid that still has radios and header buttons (`#btnEditSelected`, `#btnDeleteSelected`) works as before; convert those pages to the Employees pattern when you touch them.
- **Sidebar collapse button:** `_SidebarToggle.cshtml`, included once in every layout with a side navbar (Main, Setup, Deductions); the choice is remembered in `localStorage` (`hrms.sidebar.collapsed`).
- **Show-password eye** (`Home/Login`): SVG icons toggled with the `hidden` attribute (SVG elements have no `.hidden` property).

- **Employee identity in grids:** always two columns. `Emp ID` (the `Employee.EmployeeID` string, never the `uid`) in a narrow muted `col-emp-id` cell, then `Employee` with the name in bold (`emp-name`) and the designation underneath (`emp-meta`, `—` when empty). Department may carry the sub-department as a second line; add Project and Status like the Employees grid. Lists built from a view model (for example `LmsLeaveRowVm`) carry `EmployeeCode`, `EmployeeName` and `EmployeeDesignation` for this. Internal record ids are not shown.
- **Employee pick grids** (select employees for a bulk action: Attendance new, Off-days): a leading `col-pick` checkbox column, then the same Emp ID / Employee / Department / Project / Status columns. No mobile number, joining date or salary.
- **No salary on a grid (rule, 2026-11-24):** never show salary information (basic, gross, net, allowance or deduction amounts, tax amounts, percentages that reveal pay) as a grid column. To see money the user opens the employee's or record's details page, and the view that shows it is the place to check roles. Removed from the Allowances, Deductions, Payroll, Payroll status, Payroll search, Payroll employee-selection, Payslips and Attendance pick grids. Not covered, needs a decision: the Reports pages (Salaries, Allowances and Deductions summaries) whose purpose is the amounts, the "Net (filtered period)" chip on the Payslips page, and the tax slab limits on the Tax grid (those are rules, not one person's pay).
All grid pages use the Actions column now: Employees, Allowances, Deductions, Attendance, Leave, Leave quotas, Payroll (View, plus Delete while the payslip is unlocked), Tax, Setup Config and Setup Users. The styles (`col-actions`, `row-action*`) are in `hrms-theme.css`, so a new grid only needs the markup: an Actions `th` (`class="col-actions text-center" data-align="none"`) and a `td.col-actions` with the icon links. Not covered: Payslips and Lookups, which have their own row buttons; convert them when you touch them.
- **No colour in page CSS:** section headers, their icons and their spacing come from the theme. Do not add `.xxx .card-header { background... }` rules to a view or a page stylesheet (the old teal ones were removed on 2026-11-24).

## Employee dossier standard (2026-11-24): every page that shows or edits one employee

Pages: Employees Details, Edit, Create and the Delete (mark inactive) confirmation. The look lives in `wwwroot/css/employee-dossier.css`, scoped by the wrapper class `dossier-page` (put `class="employee-create-page dossier-page py-3"` on the page wrapper and `container-fluid px-0` inside it; link `employee-create.css` first, then `employee-dossier.css`). Any new page about a single employee (for example a payslip or leave summary for one person) should use it.

- **Standard sections (Details, Edit and Create all use exactly these five tabs, in this order, with the same names and `data-tab-key`s):** `Employee Info` (`employee`), `Salary Info` (`salary`), `Leave Balance` (`leave`), `Employee History` (`history`), `Attachments` (`attachments`). Employee Info is one combined table: identity and contact (ID, name, father, CNIC, date of birth, mobile), then the organisation fields (project, department, sub-department, designation, dates, status; on Edit/Create through `_EmployeeOrgFields` as a second `<tbody>` of the same table), then a collapsed "Additional Info" block (`emp-additional`, id `additionalInfoCollapse`: details, modified by/on, notes). The Employee History tab is the `EmployeeEvents` timeline (`_EmployeeHistory` partial): read-only on Details, with the "Record an event" form on Edit; never write history rows by hand-built markup. The Leave Balance tab is the `_EmployeeLeaveBalance` partial fed by `LeaveBalanceService` (read-only, same source as LMS -> Leave Balance): first row the leaves remaining per leave type for the current year, second row the carry-forward into that year, then a "Historical balances" sub-heading with one row per earlier year and a column per leave type. Never put editable leave figures on the employee forms; carry-forward and adjustments come from the LMS. Do not add separate Personal, Official or Additional tabs; a new group of employee data goes into one of these five or a new tab that is added to all three pages.
- **Hero band** (`Views/Employee/_DossierHero.cshtml`, never hand-written in a view): navy gradient with brass underline, circular avatar with a brass ring, name in Fraunces, a brass `ID 1007` chip, `designation  ·  department  ·  project`, status pill and joining date on the right. The avatar is the uploaded profile photo, else the initials. Options through `ViewData`: `PhotoId` (the profile photo document id, from `EmployeeDocumentsViewModel.PhotoDocumentId`), `HeroTag` (small brass pill: "Editing", "Mark inactive"), `HeroNew` (Create: "New employee" with a user-plus icon), `HeroDanger` (red underline for destructive confirmations).
- **Profile photo:** a document of type `ProfilePhoto` (`EmployeeDocuments.DocumentType`). One live photo per employee; uploading replaces it. Managed on the Attachments tab through `_EmployeePhotoCard.cshtml` (`/employee-documents/photo`, `/employee-documents/photo/remove`); it is kept out of the documents list. PNG or JPG only.
- **Tabs:** dark text, brass icons, brass underline on the active tab; a tab that holds one section shows only its one-line intro (no repeated heading); a tab with several sections (Salary) keeps small-caps section headings with a brass left bar.
- **Label | value table** (Details, Delete; class `dos-table`) and the same table with inputs (Edit, Create; `emp-form-table`): four columns (label, value, label, value), tinted label cells with 0.68rem small-caps labels, values 0.98rem semibold, hairline dividers, brass hover tint, empty values shown as a muted `—`, rows about 52px. Do not use a horizontal grid of cards for these fields.
- **List tables inside a tab** (allowances, deductions, history, documents): the standard grid look (teal header with brass underline, 0.5rem rows).
- **Confirmation pages** (Delete): hero with `HeroDanger`, a pale red note with the consequence in plain words, the key fields in the label | value table, then Back (outlined) and the red action on the right.
- **Placeholders** are muted grey, inputs are 2.2rem high with a brass focus ring.
- **Uploads** on Details and Edit: documents and photo upload from the Attachments tab; files that are missing on the server show a "File missing" badge instead of a dead download link.

## Status (2026-11-23)

- Every `ToString("N2" | "N0" | "yyyy-MM-dd" | "dd MMM yyyy" | "dd-MMM-yy" | "g" | "0.##" | "F1" ...)` in `Views/` was moved to `Fmt` (about 110 places across Payroll, Payslips, Allowances, Deductions, Reports, Leave, Attendance, Employee and Setup views). Form date inputs were left ISO on purpose.
- Attendance times changed from `hh:mm:ss` to 12-hour `hh:mm tt`.
- Formats changed the same day on request: dates DD/MM/YYYY, times 12-hour, date-times time first. Controller-built messages (attendance import errors, leave overlap, setup user stamps), the Excel employee export and the document list use `Fmt` too.
- Not converted: places that print a raw number or date with no format string (for example a plain `@item.Quantity` in some lists, text built in controllers such as TempData messages, Excel exports, and the print headers in `Home`). Convert these when you touch the view.
