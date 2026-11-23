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

## Status (2026-11-23)

- Every `ToString("N2" | "N0" | "yyyy-MM-dd" | "dd MMM yyyy" | "dd-MMM-yy" | "g" | "0.##" | "F1" ...)` in `Views/` was moved to `Fmt` (about 110 places across Payroll, Payslips, Allowances, Deductions, Reports, Leave, Attendance, Employee and Setup views). Form date inputs were left ISO on purpose.
- Attendance times changed from `hh:mm:ss` to 12-hour `hh:mm tt`.
- Formats changed the same day on request: dates DD/MM/YYYY, times 12-hour, date-times time first. Controller-built messages (attendance import errors, leave overlap, setup user stamps), the Excel employee export and the document list use `Fmt` too.
- Not converted: places that print a raw number or date with no format string (for example a plain `@item.Quantity` in some lists, text built in controllers such as TempData messages, Excel exports, and the print headers in `Home`). Convert these when you touch the view.
