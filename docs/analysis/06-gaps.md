---
status: outdated
superseded_by: ../db/decisions.md
last_verified: 2026-11-26
verified_against: main @ 7aa2685 plus the uncommitted security and user-roles changes (code), and the redesigned development database (scripts 01-04, 06, 07 applied)
supersedes_snapshot: 2026-11-16 (commit 2843f59)
see_also: ../db/decisions.md, ../payroll-review.html, 04-security.md
note: Items are marked Open, Partly, Deferred, Fixed, or Done in dev (done in the repository and the local development database, not yet on production). Production still runs the old build and the old schema until the rollout in ../db/prod_rollout.md.
deployed_to_production: false
next_step: "Commit and push the security + user-roles work, then deploy it together with the database rollout (script 01-04, 06, 07)."
tasks:
  security:
    - {title: "Setup behind Admin",                         status: fixed_not_deployed}
    - {title: "Rotate sa password / least-privilege login / close 1433", status: deferred, owner: project owner}
    - {title: "Several roles per user",                     status: fixed_not_deployed}
    - {title: "Roles on payroll, salary, reports",          status: partly, blocked_by: "what HR may do (decision)"}
    - {title: "Neutral login, throttle, re-hash, Secure cookie, logout POST, global antiforgery", status: fixed_not_deployed}
    - {title: "Uploads outside wwwroot (StorageBasePath)",  status: partly}
  database:
    - {title: "Redesign applied to local dev DB (01-04, 06, 07)", status: done_in_dev}
    - {title: "Apply redesign to production (prod_rollout.md)",   status: open, blocked_by: "HR decisions (B2, B3, B4...), maintenance window, approval"}
    - {title: "db.md migration-pending note",              status: done}
    - {title: "db.md rewrite to v2.0",                      status: open, after: "production rollout"}
    - {title: "RowVersion on allowances/deductions/quotas/tax rules", status: open}
  business_logic:
    - {title: "Tax-year bug",                               status: done_in_dev}
    - {title: "Progressive tax (T4)",                       status: open}
    - {title: "Allowance effective/end dates in payroll",     status: fixed_not_committed}
    - {title: "Allowance/deduction frequency Once and Annual", status: fixed_not_committed}
    - {title: "Frequency Weekly and Bi-Weekly",                status: waiting_for_hr, decision: "what a weekly/bi-weekly amount means in a monthly payslip"}
    - {title: "Unpaid-leave deduction in payroll",              status: waiting_for_hr, decision: B11}
    - {title: "Leave balance check",                           status: waiting_for_hr, decision: B5}
    - {title: "Leave approval workflow (who approves)",        status: waiting_for_hr, decision: B13}
    - {title: "Leave overlap check",                           status: fixed_not_committed}
    - {title: "Screens/processing for shifts, punches, half days, corrections, roster, notes", status: open}
  engineering:
    - {title: "Tests and CI",                              status: open}
    - {title: "Split fat controllers, remove duplicate code", status: open}
reason: "Static analysis written 2026-11-16 and revised to 2026-11-23. The production rollout it lists as open is done, and many gaps in payroll and leave have since been closed. Do not rely on its task list."
---
# 6. Gaps, Risks & Recommendations

Priority: **P0** fix now · **P1** next sprint · **P2** planned · **P3** nice to have.

## What is left, in order
1. **Commit and deploy the security fixes and the multi-role support.** (The work is done and tested on the local copy but not yet committed.) `SetupController` (Admin only), neutral login, `Secure` cookie and the other items in 04-security.md are in the repository but production still runs the old build, where Setup is anonymous.
2. **Deferred (owner):** rotate the old `sa` password (in git history), least-privilege app login, close port 1433.
3. **P1: role checks beyond Setup and Lists** (payroll, salary views, reports); needs the HR/Admin decision.
4. **P1: production rollout** of the database redesign and the new build in one maintenance window (`../db/prod_rollout.md`), after HR answers the open decisions.
5. **P2: finish the new attendance/schedule features** (tables and entities exist, no screens or processing).
6. **P2: payroll rules** that need HR answers (paid/unpaid leave, allowance dates, frequency, progressive tax).
7. **P3: tests and CI.**

## Security (details and status per finding in [04-security.md](04-security.md))
| P | Gap | Status |
|---|-----|--------|
| P0 | `SetupController` was `[AllowAnonymous]` | **Fixed in the repository** (Admin only); production exposed until deployed |
| P0 | `sa` credentials in git history | **Deferred** to the project owner (rotate, least-privilege login, close port 1433) |
| P1 | Users can hold only one role | **Fixed in the repository**: several roles per user (`UserRoles`, script 07; for example HR plus Admin) |
| P1 | Role-based authorization | **Partly**: Setup (Admin), Lookups (Admin, HR), employee delete (Admin); payroll/salary/reports still open to all logged-in users |
| P1 | Verbose login errors; legacy passwords accepted | **Fixed**: neutral errors, re-hash on login |
| P1 | Cookie not `Secure`, no HTTPS redirection | **Fixed** (verify after deployment) |
| P2 | No lockout; no global antiforgery; logout via GET | **Fixed**: throttle, global filter, POST logout |
| P2 | Uploaded documents directly servable | **Partly**: `/uploads` blocked; set `StorageBasePath` to move files out of `wwwroot` |
| P2 | `Home/Test` and `Debug` | **Fixed**: removed |
| P2 | Employee hard delete | **Fixed**: marks Inactive (Admin only) |

## Data integrity & database
| Gap (2026-11-16) | Status |
|---|---|
| Mixed employee keys (`uid` vs string `EmployeeID` with no FK) | **Done in dev**: Attendance, leave, quota, carry-forward and `Users` link to `Employee.uid` by FK; `EmployeeID` stays the unique business code. App maps `Attendance.EmployeeID` as a read-only facade. |
| Department/Project/Designation are free text | **Done in dev**: `Projects`, `Departments`, `SubDepartments`, `Designations`, `LeaveTypes` tables with FKs, managed under Setup → Lists. HR still has to confirm the real project list and the department mapping (decisions B2, B3). |
| Dates stored as strings (`DOB`, `ModifiedOn`, `Users.CreatedOn`) | **Done in dev** (`date`/`datetime2`, converters keep the old code shapes) |
| Two leave models (`LeaveRequests` vs `EmployeeLeaves`) | **Done in dev**: `EmployeeLeaves` removed (it was empty), `LeaveRequests` is the single model |
| Schema drift (`CarryforwardLeaves`, `GazettedHolidays` entities vs tables) | **Done in dev**: entities and tables aligned; verify against `db.md` once production is migrated |
| `Deductions.PercentageValue` overloaded for fixed amounts | **Done in dev**: separate `Percentage` and `Amount` columns; `PercentageValue` kept as an unmapped facade in code |
| No concurrency control | **Partly**: `RowVersion` on `Employee`, `Attendance`, `LeaveRequests`, `Payslips` and `HalfDayRequests` (stale saves are refused on the Employee edit); allowances, deductions, quotas and tax rules have no token yet |
| Payroll generation race / duplicate payslips | **Done in dev**: unique index on `Payslips(EmployeeId, Year, MonthNo)`; month stored as a number with a converter to the month name |
| No primary key on `Configuration`, `EmployeeLeaves`, `LeaveQuota` | **Done in dev** (keys added by script 01/03; `EmployeeLeaves` dropped) |
| Referential gaps in live data (1 leave row and 1 user pointing at missing employees; 54 of 58 department strings not in `Departments`) | **Done in dev**: orphan leave rows moved to `mig.LeaveRequests_Orphans`, user kept with `EmployeeId = NULL`, departments split into department + sub-department via `mig.EmployeeOrgMap`. HR sign-off pending. |
| No automatic migration; EF history untrustworthy | **Open**: changes are applied by the SQL scripts in `scripts/database/redesign/`; do not run `dotnet ef database update` against production |
| Production still on the old schema; `db.md` describes production | **Partly**: `db.md` (v1.10) now carries a "migration pending" note pointing to `db_changes.md` and `prod_rollout.md`. Rewrite it as v2.0 only after the production rollout. |

## Business logic
| Gap | Status |
|---|---|
| **Tax-year bug** (slabs matched against `2026` instead of `2025-2026`, all slabs pooled) | **Done in dev**: tax year is derived from the payslip month (July-June) and stored as `TaxYearStart`; no fallback to pooled slabs. Not on production yet, so production payslips are still computed the old way. |
| Tax is a flat % on basic, not the progressive FBR computation | **Open** (decision T4) |
| Leave/attendance do not influence salary | **Waiting for HR** (B11 absence and unpaid-leave rate, B6 hourly pay, B10 mid-month joiners): `WorkingDays/LeaveDays/LeaveBalance` unused. `LeaveTypes.IsPaid` exists but payroll ignores it. Deliberately not built until HR answers. |
| Allowance `EffectiveDate/EndDate` not applied in payroll (only `IsActive`), while deductions are date-filtered | **Fixed in the repository (2026-11-23)**: allowances and deductions use the same rule: started before the month ends, not ended before it starts (`PayrollCalculationService.AppliesInPeriod`). Also used by the gross preview on the payroll list (current month). Tested with March and December 2026 payslips. |
| Allowance/deduction `Frequency` stored but not honoured | **Partly**: `Once` applies only in the month of its effective date and `Annual` only in that calendar month each year (fixed in the repository). `Weekly` and `Bi-Weekly` are still applied once a month because HR has not said how a weekly amount maps to a monthly payslip. The live data only has `Monthly` (allowances) and `Monthly`/`Once` (deductions). |
| Percentage allowances use basic, percentage deductions use gross | Unchanged (as designed; confirm with HR) |
| Editing an allowance/deduction locks all payslips of the employee and there is no unlock | **Open** |
| Leave: no approval workflow, no balance check, overlap only blocks exact duplicates | **Partly**: overlap is **fixed in the repository**: a new or edited Pending/Approved request is refused when it overlaps another Pending/Approved request of the same employee (Rejected and Cancelled never conflict; existing overlapping rows, 21 and 22 in the production copy, are not changed). **Waiting for HR:** the balance check (B5: leave year, quota rules, whether weekly offs count) and the approval workflow (B13: who approves; `ApprovedByUserId`/`ApprovedAt` exist in the schema but no screen sets them). |
| Taxes Summary report is a stub | **Open** |
| `ApplyTax` free-text flag | **Done in dev**: `bit` column, converter keeps `Yes`/`No` in code |
| Hard-coded strings (`"Fuel Allowance"`, `"Fixed Amount"`, month names) | **Open** (project, department, designation and leave-type lists are no longer hard-coded) |
| New attendance/schedule requirements (hourly pay, 7 h minimum day, half days with approval, overnight shifts, per-employee quotas, Dastarkhuwan staff without leave) | **Partly**: tables and entities exist (`Shifts`, `ShiftAssignments`, `RosterOverrides`, `AttendancePunches`, `HalfDayRequests`, `AttendanceCorrections`, `RecordNotes`, `LeaveEntitlementOverrides`); no pages and no processing yet |

## Engineering quality
- **No automated tests** (no test project) and no CI: **Open**.
- **Fat controllers** (Allowances, Payroll, Deductions each 700-870 lines) and sync EF calls in `PayrollCalculationService`: **Open**.
- Duplicate code (Allowances vs Deductions, Payroll vs Payslips Create/Details), layout duplication, inline `<style>` blocks, sidebar link inconsistencies: **Open**.
- Repository hygiene: `_build_verify/` removed and the README now says .NET 8: **Done**. The README lists five modules, and `src/HRMBT.Web/bin` and `obj` build output appear in file searches (ignored by git).
- Logging/monitoring: console only, no health endpoint, many exceptions swallowed into `TempData` or `catch {}`: **Open**.
- `AttendanceService.UploadedBy`: **Done** (uses the signed-in user). `ModifiedBy`/`ModifiedOn` are now filled on the main edit screens.
- Performance: some dashboards/reports load whole tables into memory: **Open**.
- Accessibility/i18n: English only, no role-based menu hiding: **Open**.
- Warnings: the build is at 0 warnings and 0 errors (2026-11-23).

## Operations
- `DeployToIIS.bat` wiped the deploy folder including uploaded employee documents on every release: **Fixed in the script** (backup to `C:\HRMSBackups`, restore after publish; abort if the backup fails). Still open: no database or app rollback; set `StorageBasePath` so documents live outside the site.
- Secrets: **Done in the files** (per-environment git-ignored settings), **open in git history** (see Security).
- Environment safety: with `ASPNETCORE_ENVIRONMENT` unset the app runs as Production and uses the production connection string. Launch profiles (default = Development, `Production` on port 5129) and README warnings are in place; the Production profile has not been started to verify it.
- No scheduled jobs (attendance sync, backups), no email/notifications, no report export beyond the Excel employee list: **Open**.

## Open decisions that block work (see `../db/decisions.md`)
B5 (leave year and quotas, balance check), B11 (absence/unpaid-leave deduction), B13 (who approves), B6/B10 (hourly pay, mid-month joiners), B1/B7/B8/B16 (half days, schedules, forgotten check-outs), and a missing definition of Weekly/Bi-Weekly frequency. Also:
T4 (progressive tax), T7, B1, B4-B18, the real project list (B3), HR mapping of `Administrator`/`Mess`/`Transportation` departments, and what `HR` may do versus `Admin`.

## Recommended roadmap
1. **Done in the repository:** `Setup` behind `Admin`; `Debug` and `Home/Test` removed; neutral login errors, throttle, re-hash; `Secure` cookie and HTTPS redirect; logout POST; employee soft delete; `/uploads` blocked. **Deferred (owner):** `sa` password rotation.
2. **Next (P1):** roles on payroll/salary/Reports; move uploads outside `wwwroot` (`StorageBasePath`); deploy.
3. **Rollout (P1):** HR sign-off on `mig.EmployeeOrgMap` and the project list, then follow `../db/prod_rollout.md`; update `db.md` to v2.0 afterwards.
4. **P2 (waiting for HR answers):** screens and processing for punches, shifts, half days, corrections, roster, notes (B1, B7, B8, B16); payroll use of the paid/unpaid flag (B11, B6, B10); leave balance check (B5) and approval workflow (B13); weekly/bi-weekly frequency meaning.
5. **P2:** extract services from fat controllers; unit tests for `PayrollCalculationService` (tax year, slabs, allowances, deductions) and integration tests for login and authorization; CI.
6. **P3:** progressive tax engine, payslip unlock with audit, full audit trail.
