# 2. What the Application Does

An internal **HR + payroll system** (Bahria Town; PKR currency; Pakistani CNIC/tax context). After login the user lands on a dashboard (employee/attendance/leave/payroll counts) and navigates to the modules below.

## Module map
| Module (route) | Purpose | Key actions |
|----------------|---------|-------------|
| **Home** `/` | Login/logout, dashboard tiles (total/active employees, today's attendance, pending leaves, payslips, tax rules) | Login, Logout, Index, Error, Privacy, `Test` (DB table dump) |
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
| **Setup** | Admin | App users (CRUD, set password), Configuration key/value (CRUD) |

## Core business logic

### Payroll calculation (`PayrollCalculationService.GeneratePayslip`)
1. Reject if a payslip already exists for (employee, month, year).
2. `Basic` = `Employee.BasicSalary`.
3. **Allowances** (active rows only), per row:
   - percentage-based → `Basic × % / 100`
   - "Fuel Allowance": *Fixed Amount* → `Amount`; *Quantity* → `FuelPrice (Configuration table) × Quantity`; legacy type → `Amount` if > 0 else fuel price × qty
   - everything else → `Amount × Quantity`
4. `Gross = Basic + Σ allowances`.
5. **Deductions**: active and effective in the payroll month (`EffectiveDate ≤ month end` and `EndDate` null or ≥ month start). *Percentage* → `% of Gross`; otherwise a fixed amount (stored in `PercentageValue` – there is no Amount column). Rounded to 2 dp.
6. **Income tax**: only if `Employee.ApplyTax` ∈ {yes, y, true, 1}. Slab chosen from `TaxRules` for the payslip year (fallback: blank-year rules, then all rules); the highest `MinSalary` whose range contains **Basic** wins (null/0 `MaxSalary` = unbounded). `Tax = Basic × % / 100` – a flat percentage of the matched slab, **not progressive/marginal**.
7. `Net = Gross − Σ deductions − Tax`.
8. Persist `Payslips` header + `PayslipDetails` lines (types Allowance / Deduction / Tax) and a readable `CalculationDetails` text block.

### Payslip locking
Adding/editing/deleting an allowance or deduction calls `PayslipLockService.LockEmployeePayslipsAsync`, which sets `IsLocked = true` on all of that employee's unlocked payslips (stale-payroll protection) and writes an `AuditLogs` row (`LOCK`). Locked payslips block regeneration/deletion for the month. There is **no unlock feature**.

### Attendance import
`AttendanceService` parses `.xlsx` (EPPlus) and CSV: flexible header-name mapping, validates employee/department/date/time/status, enforces one row per employee per date (unique index), and records an `AttendanceUploadLogs` entry (total/success/failure + error details).

### Leave
Leave requests (employee, type, from/to, status); quotas per leave type per year; balance view combines quotas with `EmployeeLeaves` and carry-forward data. Two parallel leave data models exist (`LeaveRequests` vs `EmployeeLeaves`).

### Audit
`AuditLogService` records table, operation, record id, old/new JSON, user, timestamp, module and IP. Currently used essentially only for payslip locking; ordinary CRUD is **not** audited.

### Employee documents
Files stored at `wwwroot/uploads/employees/{employeeUid}/{guid}{ext}`; extension and size whitelist from config; soft-delete flag; download through a controller action.

## Intended but not implemented
Leave-adjusted salary (originally intended; `WorkingDays/LeaveDays/LeaveBalance` are never populated), payslip unlock, taxes report, leave approval workflow, employee self-service, role-based permissions. See [06-gaps.md](06-gaps.md).
