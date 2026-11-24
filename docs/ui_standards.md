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
| Quantity or days | `Fmt.Number(x)` | 12, 12.5, 1,200.25 | Up to two decimals, trailing zeros dropped. Leave days, quantities. |
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

- **Section header** (every `.card > .card-header`, `.filter-card`, `.grid-card`, `.section-card`, `.actions-card`, `.lms-panel`, and the Home/Setup `.setup-tile-header`): navy gradient `#00203f` to `#0b2d52`, white 0.9rem semibold text, 3px brass underline (`--hr-brass-500`), brass icon (`#f0d78c`), padding `0.4rem 1rem`. Headers that carry meaning (`bg-success`, `bg-warning`, `bg-danger`, `bg-info`) and the login card keep their own colour. Do not set a header colour in a view.
- **Search & Filters section:** use `.filter-card` with the title `Search & Filters` (capital F). It is compact by default: card body padding `0.5rem 0.85rem`, 2rem controls, 0.66rem uppercase labels, `0.5rem` gutters. Do not add `p-3 p-lg-4` to its body.
- **Page width:** a page wrapper that contains a `.filter-card` has no side padding, so filter and grid sections use the full content width (about 24px from the sidebar). For other pages use `container-fluid px-0` instead of `px-3 px-lg-4`.
- **Second top bar** (brand and user): 45px tall in every layout.
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

## Status (2026-11-23)

- Every `ToString("N2" | "N0" | "yyyy-MM-dd" | "dd MMM yyyy" | "dd-MMM-yy" | "g" | "0.##" | "F1" ...)` in `Views/` was moved to `Fmt` (about 110 places across Payroll, Payslips, Allowances, Deductions, Reports, Leave, Attendance, Employee and Setup views). Form date inputs were left ISO on purpose.
- Attendance times changed from `hh:mm:ss` to 12-hour `hh:mm tt`.
- Formats changed the same day on request: dates DD/MM/YYYY, times 12-hour, date-times time first. Controller-built messages (attendance import errors, leave overlap, setup user stamps), the Excel employee export and the document list use `Fmt` too.
- Not converted: places that print a raw number or date with no format string (for example a plain `@item.Quantity` in some lists, text built in controllers such as TempData messages, Excel exports, and the print headers in `Home`). Convert these when you touch the view.
