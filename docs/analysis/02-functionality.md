---
status: outdated
last_verified: 2026-11-26
verified_against: main @ 7aa2685 plus the uncommitted security and user-roles changes (code), and the redesigned development database (scripts 01-04, 06, 07 applied)
outdated_since: 2026-11-23
outdated_sections: [Intended but not implemented (partly)]
changed_2026_11_23: [Setup now Admin-only, multi-role users, Employee delete = mark Inactive, Debug/Test removed]
reason: "Static analysis written 2026-11-16 and revised to 2026-11-23. Payroll screens, fuel prices, leave, work weeks, leave adjustments and the employee leave tab have all changed since."
see_also: ../../AGENTS.md, ../routes.md, ../payroll-review.html
---
# 2. What the Application Does

An internal **HR + payroll system** (Bahria Town; PKR currency; Pakistani CNIC/tax context). After login the user lands on a dashboard (employee/attendance/leave/payroll counts) and navigates to the modules below.

## Module map
| Module (route) | Purpose | Key actions |
|----------------|---------|-------------|
| **Home** `/` | Login/logout, dashboard tiles (total/active employees, today's attendance, pending leaves, payslips, tax rules) | Login (neutral errors, throttle), Logout (POST), Index, Error, Privacy |
| **Employee** `/Employee` | Employee master | List with filters (department, designation, search, status…), sort, paging; Dashboard; Create/Edit/Delete/Details; **Export to Excel**; per-employee documents |
| **EmployeeDocuments** | Employee files | Upload (multi-file), Download, soft Delete |
| **Attendance** `/Attendance` | Daily attendance | Today's list, Create (bulk per department), Off-day marking, Edit/Delete, **Excel/CSV upload** with row validation + upload log, Monthly report, By-department summary |
| **LMS** `/LMS` | Leave management | Leave requests (CRUD), Leave quota per type/year (CRUD), Leave balance by year/department |
| **Allowances** | Per-employee allowances | List/filter, single create, **bulk create**, edit/delete, fuel price view |
| **Deductions** | Per-employee deductions | List/filter, single + bulk create, edit/delete |
| **Tax** `/Tax` | Income-tax slabs (`TaxRules`) by tax year (live labels `2025-2026`, `2026-2027`; see the tax-year bug in 06-gaps.md) | CRUD, filter by year |
| **Payroll** `/Payroll` | Payroll run & status | Dashboard, Employee Detail, Generate payroll (department → select employees), Run Payroll, Generate individual, Payroll slips, Payroll status by year/month/department |
| **Payslips** | Payslip documents | List/filter (locked/unlocked), create, details, **print view**, delete, JSON helpers (`EmployeesByDepartment`, `EmployeeDetails`) |
| **Reports** | Management reports | Payroll (salaries) summary, Employee summary by department, Allowances summary, Deductions summary, Taxes summary (placeholder – no data yet) |
| **Setup** | Admin | App users (create, edit, set password, tick roles Admin/HR, disable), Configuration key/value (CRUD). Requires `Admin`; a user can hold several roles |
| **Lookups** (Setup → Lists) `/Lookups` | Reference lists | Projects, departments, sub-departments, designations, leave types: add, edit, activate/deactivate, delete (refused when in use). Role `Admin` or `HR` |

## Core business logic

### Payroll calculation (`PayrollCalculationService.GeneratePayslip`)
1. Reject if a payslip already exists for (employee, month, year).
2. `Basic` = `Employee.BasicSalary`.
3. **Allowances** (active rows that apply in the payroll month: `EffectiveDate` before the month ends, `EndDate` empty or not before the month starts; `Frequency` `Once` = effective month only, `Annual` = that calendar month each year, anything else = every month), per row:
   - percentage-based → `Basic × % / 100`
   - "Fuel Allowance": *Fixed Amount* → `Amount`; *Quantity* → `FuelPrice (Configuration table) × Quantity`; legacy type → `Amount` if > 0 else fuel price × qty
   - everything else → `Amount × Quantity`
4. `Gross = Basic + Σ allowances`.
5. **Deductions**: same date and frequency rule as allowances. *Percentage* → `% of Gross`; otherwise a fixed amount (stored in `PercentageValue` – there is no Amount column). Rounded to 2 dp.
6. **Income tax**: only if `Employee.ApplyTax` is set (a `bit` in the redesigned schema; `Yes`/`No` in code). The tax year is derived from the payslip month (July to June, e.g. March 2026 → `2025-2026`) and slabs are taken for that year only, with no fallback to other years (the old pooled-slab bug is fixed in the repository, not yet on production). The highest `MinSalary` whose range contains **Basic** wins (null/0 `MaxSalary` = unbounded). `Tax = Basic × % / 100` – a flat percentage of the matched slab, **not progressive/marginal**.
7. `Net = Gross − Σ deductions − Tax`.
8. Persist `Payslips` header + `PayslipDetails` lines (types Allowance / Deduction / Tax) and a readable `CalculationDetails` text block.

### Payslip locking
Adding/editing/deleting an allowance or deduction calls `PayslipLockService.LockEmployeePayslipsAsync`, which sets `IsLocked = true` on all of that employee's unlocked payslips (stale-payroll protection) and writes an `AuditLogs` row (`LOCK`). Locked payslips block regeneration/deletion for the month. There is **no unlock feature**.

### Attendance import
`AttendanceService` parses `.xlsx` (EPPlus; the upload page only accepts `.xlsx`): flexible header-name mapping, looks the employee up by code, validates date/time/status, upserts one row per employee per date (unique index on employee uid + date), rejects a file whose hash was already imported, and records an `AttendanceUploadLogs` entry (counts, status, error details). `UploadedBy` is still hard-coded `"System"`.

### Leave
Leave requests (employee, leave type from `LeaveTypes`, from/to, status); quotas per leave type per year (decimal days); balance view combines quotas, requests and carry-forward data. `EmployeeLeaves` has been removed, so `LeaveRequests` is the only leave model. A request is refused when it overlaps another Pending or Approved request of the same employee (exact duplicates are also refused by a unique index). Balances are not checked and there is no approval step (waiting for HR decisions B5 and B13).

### Audit
`AuditLogService` records table, operation, record id, old/new JSON, user, timestamp, module and IP. Used for payslip locking and allowance/deduction changes; other CRUD (employees, leave, attendance, users) is **not** audited.

### Employee documents
Files stored at `wwwroot/uploads/employees/{employeeUid}/{guid}{ext}`; extension and size whitelist from config; SHA-256 hash stored and used to refuse the same file twice for an employee; soft-delete flag; download through a controller action (but the file is also reachable as a static file, see 04-security.md #9).

## Intended but not implemented
Leave-adjusted salary (`WorkingDays/LeaveDays/LeaveBalance` are never populated; `LeaveTypes.IsPaid` is ignored), payslip unlock, taxes report, leave approval workflow, employee self-service, role-based permissions beyond Setup → Lists, and screens/processing for the new tables `Shifts`, `ShiftAssignments`, `RosterOverrides`, `AttendancePunches`, `HalfDayRequests`, `AttendanceCorrections`, `RecordNotes`, `LeaveEntitlementOverrides` (mapped only). See [06-gaps.md](06-gaps.md).
