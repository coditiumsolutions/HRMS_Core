# HRMS Database - Pending Decisions
Version: 2.6 | Date: 2026-11-24 | Related: db_standards.md, db_changes.md (v3.4-draft), db.md (v1.9)

## How to use this file
- Only **pending** decisions are listed here. Answered ones are removed and recorded where they apply (rules in db_standards.md, schema in db_changes.md).
- Each decision has a **Suggested key** (★ = recommended) and an **Answer** block. Write the chosen letter (or "Other: ...") in `Answer`, then who and when; add free text in `Notes`.
- Part A is technical (the dev team decides). Part B is business logic (HR / management decides); each B-item says what it affects in the schema so the answer can go straight into db_changes.md.
- **Data status (2026-11-18):** the data now in `HRBTK` is **test data**. Counts and findings quoted below (employees, projects, leave balances, duplicates) describe it for design purposes only; they are not requirements, and the checks are re-run on real data before anything is applied.
- 2026-11-24: the employee Leave Balance tab is built on the LMS tables; B18 is closed (see below); leave days are kept in one decimal.
- 2026-11-24: employee history is built (`EmployeeEvents`, see B14, T1, T8, B19 below).
- Removed on 2026-11-17 as decided: employee key (`uid` stays, `EmployeeID` is the business key; see S1 and S5) and the attendance migration approach (expand then contract; see S19).

---

# Part A. Technical decisions

## T1. Leaving date on Employee
Status: Closed

**Context.** The schema has `EmployeeStatus` but no date for when someone left, so attendance and payroll cannot tell which months a former employee belongs in. Standard S6 says employees are deactivated, never deleted, so this date is the history.

**Suggested key**
- A ★ Add `Employee.DateOfLeaving date NULL`; processing skips attendance/payroll after it.
- B Use `EmployeeStatus` only (no history).

**Answer:** A
**Decided by / date:** Rayder / 17-11-2026
**Notes:** There can also be edge cases of employees leaving then returning, so let's add a returning date aswell, both nullable till they are filled.
**Update 2026-11-24:** `Employee` holds the latest leaving and return only. A resignation or termination recorded in Edit Employee sets `DateOfLeaving` and clears `DateOfReturn`; every earlier cycle stays in `EmployeeEvents`. So leaving and returning more than once is now supported.

---

## T2. `Payslips.Month` (text) alongside new `MonthNo`
Status: Closed

**Context (checked 2026-11-17 against code and live data).** A payslip period is `Payslips.Month nvarchar(20)` (English name) plus `Payslips.Year int`. Sorting is done in C# with a 12-branch `CASE` on the month name (`PayrollMonthHelper.OrderByPeriodDescending`); `Normalize()` also accepts legacy numeric months. Live data has only month names (11 month/year combinations, 99 payslips), so the numeric case does not occur today. Nothing at the database level stops `Month = 'Janury'` or a unique violation.

**Suggested key**
- A ★ **Separate `Year` (smallint or int) and `Month tinyint` columns**, `CHECK (Month BETWEEN 1 AND 12)`, unique `(EmployeeId, Year, Month)`. The name is shown by the app (`PayrollMonthHelper` already has the names). Migration: add `MonthNo`, backfill from the name, switch the app, drop the text `Month`, then rename `MonthNo` to `Month` in the contract phase. Optional: a persisted computed `PeriodStart` (`DATEFROMPARTS(Year, Month, 1)`) if range queries need it.
- B One `PeriodStart date` column (first of the month) instead of two. One column, trivially sortable and range-queryable, but it hides the month/year split you want for filtering and display.
- C Keep text `Month` plus `MonthNo` (draft).

**Answer:** A
**Decided by / date:** Rayder / 17-11-2026
**Notes:** Applies to every table that holds a payroll period; see T5.

---

## T3. Duplicate file uploads (attendance imports and employee documents)
Status: Closed

**Context.** Re-uploading the same Excel file writes a second `AttendanceUploadLogs` row (and today would re-insert attendance). Punch dedup (S21) protects the data, but the log and `EmployeeDocuments` still accept the same file twice. See the idempotency review in db_standards.md.

**Suggested key**
- A ★ Add `FileHash char(64) NULL` (SHA-256 of the content) to `AttendanceUploadLogs`, with a unique filtered index on `FileHash` where `Status <> 'Failed'`. Re-uploading the same file is answered with "already imported on <date>" instead of a new log. `EmployeeDocuments` gets the same column with a unique index on `(EmployeeId, FileHash)` where `IsDeleted = 0`.
- B Only dedup the punches; allow duplicate logs.

**Answer:** A
**Decided by / date:** Rayder / 17-11-2026
**Notes:** ______

---

## T4. Regenerating a payslip (unlocked)
Status: Open

**Context.** `Payslips` is unique per employee per month (S17), but `PayslipDetails` has no uniqueness, so regenerating could double the line items unless the old lines are removed first.

**Suggested key**
- A ★ Regenerate = in **one transaction**, update the existing `Payslips` row, delete its `PayslipDetails`, insert fresh ones. Add a unique index on `PayslipDetails (PayslipId, ItemType, ItemName)` so a double insert fails loudly. Locked payslips are never regenerated.
- B Create a new payslip row per generation and mark the old one superseded (keeps history, needs a `SupersededBy` column).

**Answer:** ______
**Decided by / date:** ______
**Notes:** Needs B15 (can a locked payslip be unlocked?).

---

## T5. One rule for year and month columns everywhere
Status: Closed

**Context.** Years are stored in four different ways today:

| Where | Stored as | Problem |
|---|---|---|
| `Payslips.Month` + `Year` | text name + int | see T2 |
| `LeaveQuota.Year` | `varchar(50)`, live `'2026'` | compared as text with `DateTime.Now.Year.ToString()` |
| `CarryforwardLeaves.Year` | int | fine, but ambiguous (from-year or to-year) |
| `TaxRules.TaxYear` | `'2025-2026'` text | payroll compares it with `2026` and never matches (known bug) |
| `Employee.Year2022 / Year2023 / Year2023New / Year2024` | one column per year | repeating group; see B18 |
| `Configuration` key `Year` | CSV `2025, 2024, 2026, 2023` | feeds a dropdown; no FK possible |

**Suggested key**
- A ★ Calendar years and months are always `int`/`tinyint` columns named `Year` and `Month` (or `FromYear`/`ToYear`), never text. No one-column-per-year. The `Configuration` `Year` list is removed; year dropdowns are generated from data or a range.
- B Same, but keep text for display-only labels.

**Answer:** A
**Decided by / date:** Rayder / 17-11-2026
**Notes:** ______

---

## T6. Tax year representation
Status: Closed

**Context.** Tax years are July-June and stored as labels (`2025-2026`, 14 slab rows). Payroll compares the label with the payslip year (`2026`), so it never matches and all slabs are pooled. `Configuration` also holds `CurrentTaxYear = 2026-2027` and a `TaxSlabs` CSV (`2024-2025, 2025-2026, 2026-2027`).

**Suggested key**
- A ★ Store `TaxRules.TaxYearStart smallint` (2025 means 1 Jul 2025 - 30 Jun 2026); the label is derived in the app. Payroll works out the tax year from the payslip: `Month >= 7 ? Year : Year - 1`. Remove `CurrentTaxYear` and `TaxSlabs` from `Configuration` (derived). This fixes the bug at its cause and needs no manual "current year" setting each July.
- B Keep the text label; fix only the payroll lookup (`CurrentTaxYear` or derived label).

**Answer:** A
**Decided by / date:** Rayder / 17-11-2026
**Notes:** Pair with B17 (tax rules).

---

## T7. Notes and remarks on HR records
Status: Open

**Context.** HR keeps track of edge cases in notes, for example a forgotten check-out noted on that day's attendance. Today only some tables have a free-text note and the names differ (`Comments` on Attendance, `Remarks` on Allowances, `Notes` on Payslips, `Details` on Employee). A single column per row is overwritten by the next note and records no author or time. The rule is db_standards.md S24; the drafted tables are in db_changes.md (`Notes` columns and `RecordNotes`).

**Suggested key (design)**
- A ★ A standard `Notes nvarchar(1000) NULL` column on HR-operated tables, plus an append-only `RecordNotes` table (who, when, text) linked to Attendance, Employee and LeaveRequests by real FKs (exactly one per note).
- B Only the `Notes` column.
- C Only `RecordNotes`.

**Questions to settle with the design**
1. Which records get dated notes first? Suggested: Attendance, Employee, LeaveRequests (the drafted FKs); others are added the same way when needed.
2. Can a note be edited or removed? Suggested: no editing; a wrong note is retracted (`IsDeleted`), kept in the table and hidden from the screen.
3. Should notes be categorised (e.g. `ForgotCheckout`, `LateArrival`, `Other`) so HR can filter them? Suggested: not now; a `Category` column can be added once HR lists the categories.
4. Who can read notes? Suggested: Admin and HR only, same as the rest of the app until roles are enforced (B13).

**Answer:** ______
**Decided by / date:** ______
**Notes:** ______

---

## T8. Voiding an employee event
Status: Closed (2026-11-24)

**Context.** `EmployeeEvents` is append-only (2026-11-24). A wrong entry is voided with a reason and stays visible. Recording an event also changes `Employee` (promotion sets the designation, resignation sets the leaving date and status...), so voiding raises the question of what happens to that change.

**Suggested key**
- A ★ (built) Voiding only hides the entry. The employee fields are corrected by hand on the Employee Info tab. Simple, and a void can never silently overwrite a later edit.
- B Voiding the **latest** live entry also reverts its effect (promotion/transfer restore the From columns; resignation/termination restore the previous status and dates, which needs them stored on the event). Only the latest entry could be voided.

**Answer:** B, built. **Decided by / date:** Rayder / 24-11-2026, after finding that voiding a promotion left the new designation in place.
**Notes:** Only the latest live entry can be voided; voiding it reverts its effect (promotion and transfer restore the position stored in the From columns; resignation, termination and rejoin are undone by replaying the remaining entries; a field changed by hand since is left alone and reported). Admins can permanently delete a voided entry so it no longer shows in the history; the deletion is written to `AuditLogs` with the full row. Live entries cannot be deleted.

---

# Part B. Business logic questions (HR)

## B1. Half days: separate request or part of leave?
Status: Open
**Affects:** `HalfDayRequests`, `LeaveRequests.IsHalfDay`, leave balances.

**Question.** When HR approves a half day, is it charged against the leave balance, never charged, or decided per case?

**Suggested key**
- A ★ Keep `HalfDayRequests` for attendance; it optionally links to a `LeaveRequests` row when the half day is charged to leave.
- B Always charged to leave: merge the approval/document/checkout fields into `LeaveRequests`.
- C Never charged to leave: remove `LeaveRequests.IsHalfDay`.

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______
**2026-11-24:** a half day on a leave request is stored as `IsHalfDay = 1`, `TotalDays = 0.5` (`CK_LeaveRequests_HalfDay`) and counts against the balance in `vLeaveBalances`. The leave request screens do not offer the half-day option yet, and `HalfDayRequests` (attendance) is not linked to leave until this is answered.

---

## B2. Departments: per project or shared?
Status: Closed (two follow-ups still open, below)
**Affects:** `Departments`, `Employee.DepartmentId`, sorting.

**Question.** Is "Accounts" in one project a different department from "Accounts" in another, or one shared department? Also needed: the mapping of the 58 free-text department strings.

**Suggested key**
- A ★ Per project: one row per project/department pair.
- B Shared departments plus a `ProjectDepartments` link table.
- C Other: one shared department list with no link to projects at all.

**HR's response (received 2026-11-18).** Projects and departments are linked, but not tightly:
- Galaxy Construction has a Maintenance department that the other projects do not have.
- Every project has Admin and Security departments. When HR wants to see them, HR first sorts by Admin, which should show all the admin staff regardless of project, and then by project, which shows the admin staff of that project.
- There is only one HR department for all the projects, and it comes under Bahria Town Karachi (BTK).

**Answer:** Other (C): one shared `Departments` list, independent of `Projects`. An employee has `ProjectId` and `DepartmentId` as two separate fields; the database does not tie one to the other, and there is no `ProjectDepartments` link table. Lists can sort department first, then project, or the reverse.
**Decided by / date:** Rayder / 18-11-2026, on HR's response above
**Notes:** Applied in db_changes.md v3.2 (`Departments.SortOrder`, `Employee.DepartmentId` -> `Departments.Id`, new index `IX_Employee_Department_Project`; the link table and the composite key are removed). HR staff are employees of the project Bahria Town Karachi, with a department of Human Resource. Two follow-ups, the second now decided:
1. **Mapping of the 58 free-text department strings** to the shared list. These strings come from the test data, so this is a go-live task with HR's real list, not a blocker now; development seeds `Departments` from the test values.
2. **Sub-departments (decided 2026-11-18, project owner).** Some names in the test data are a base department plus a function: `Admin - Mess`, `Admin - Transportation`, `Admin - Sweeper`; `Operations - Record`, `Operations - Transfer`, `Operations - Dispatch` and so on. Decision: `Employee.DepartmentId` stays the **main department** (and `Admin` is the Administration department, so the department is named `Administration`). A new **optional** `Employee.SubDepartmentId` carries the function. It is information and detail only: no processing, payroll, leave or approval rule reads it, and a department that has no sub-departments leaves it empty. This replaces the held `ParentDepartmentId` idea (a hierarchy inside `Departments`), which is dropped.
   - **Design:** sub-departments are a small table (`SubDepartments`, each one belongs to one department; S11), not free text on `Employee`, so the same function is not spelled two ways (`Mess` against `Admin - Mess` in the test data is exactly that drift). A free-text `SubDepartment` column was the simpler alternative.
   - **Migration rule:** split each test-data department name at the first ` - `: the left side is the department, the right side the sub-department. Only two bases carry a ` - ` in the test data: Administration (Mess, Sweeper, Transportation) and Operations (Audit & Verification, Data Center Q.C, Dispatch, Lahore / Rawalpindi, Nawabshah, Record, Transfer). That turns 58 test names into 48 departments plus 10 sub-departments; the rest (`Sales`, `Accounts & Finance`...) stay plain departments.
   - **Still to confirm with HR (go-live):** whether `Mess` (BT Tower) and `Transportation` (Icon), which have no `Admin - ` prefix, are the Administration functions of the same name (suggested: yes); and whether `Administrator` (BT Tower, Icon) is the Administration department (suggested: yes, but HR confirms, it may be a role).

---

## B3. Projects: a dynamic list, not a fixed number
Status: Closed (the initial list is still needed at go-live)
**Affects:** `Projects`, `Employee.ProjectId`, all employee lists, Setup screen.

**Question (original).** The test data has 8 distinct project names while HR mentioned 6. Which are merged, and in what order are they shown?

**Answer from the project owner (2026-11-18).** The number of projects is not fixed and must not be built in. HR adds, renames and removes projects as they see fit, so 6 or 8 does not matter, and the system is dynamic. The data currently in the database is **test data** and is not a source of truth for the project list.

**Suggested key**
- A ★ `Projects` is an HR-managed table. No project name or count is hard-coded in the app or the schema. HR can add, rename, reorder (`SortOrder`) and deactivate projects from Setup. A project that has employees (or any history) is never hard-deleted: it is deactivated (`IsActive = 0`), which hides it from new-employee dropdowns and keeps old records valid (S6, S14). A project with no employees at all may be deleted.
- B A fixed project list with changes made by the developers (rejected: this is what the Configuration CSV already was, and it drifted from the data).

**Answer:** A (dynamic, HR-managed)
**Decided by / date:** Rayder / 18-11-2026
**Notes:**
- Applied in db_changes.md v3.3: the `Projects` section says the list is fully HR-managed and the 6 or 8 question is removed.
- **Setup screen:** the app needs add, rename, reorder and deactivate for projects, the same way Departments and Designations are managed. The old project dropdown in `Configuration` (a comma-separated string) is retired in the last phase (S11).
- **Starting list:** for development the table is seeded from the distinct values in the test data. At go-live HR provides the real list; nothing is built around today's names.
- **What the test data showed (kept as evidence for the table design, not as requirements):** the Setup dropdown and the employee records had drifted apart. Some project names in employee records were not in the dropdown at all, and some were spelled differently (for example "Bahria Town Icon Project" in the list against "Maintenance & Services - Icon" on employees), plus a test row "Sample Entry Project". A table with a foreign key removes this class of problem, because a project can only be chosen from the list and a rename is one row.
- **Still true for any project list:** `Projects.HasLeaveManagement` (0 for projects such as Dastarkhuwans where leave is not managed) and `SortOrder` are per project and edited by HR, not fixed in code.

---

## B4. Designation ranking
Status: Open
**Affects:** `Designations.Rank`, list sorting.

**Suggested key**
- A ★ HR provides an ordered list of designations from most to least senior.
- B Alphabetical for now.

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B5. Leave year and quotas
Status: Open
**Affects:** `LeaveQuota`, `LeaveEntitlementOverrides`, `CarryforwardLeaves`, `vLeaveBalances`.

**Questions.**
1. Is the leave year calendar (Jan-Dec), fiscal (Jul-Jun) or per project?
2. Is the full quota granted at the start of the year, or earned monthly?
3. Do new joiners get a pro-rated quota or a waiting period (probation)?
4. Is there a carry-forward cap?
5. Do weekly offs and holidays inside a leave period count as leave days?
6. The leave types and the standard days for each (live `LeaveQuota` has 3 rows).

**Suggested key (question 1)**
- A ★ Calendar year. B Fiscal year. C Per project.

**Answer:** ______  **Decided by / date:** ______  **Notes (2-6):** ______
**Built 2026-11-24 without waiting for the answers:** carry-forward eligibility and the cap are data on `LeaveTypes` (`AllowsCarryForward`, default 1 except Unpaid and Compensation; `MaxCarryForwardDays`, NULL = no cap) and the year-end rollover reads them, so HR's answer to question 4 is a Setup change, not a code change.

---

## B6. Hourly pay rule
Status: Open
**Affects:** `Employee.PayBasis/HourlyRate`, `Attendance.PayableMinutes`, `Payslips.HourlyAmount`.

**Questions.** Days under 7 hours (420 minutes) are paid hourly. How is the rate worked out, and is time rounded? Does the 1-minute grace count as worked time? Who is on hourly pay permanently?

**Suggested key (rate)**
- A ★ Per-employee `HourlyRate` if set, otherwise BasicSalary / working days in the month / 7.
- B Always BasicSalary / 30 / 7.
- C Store a rate on every employee.

**Suggested key (rounding)**
- A ★ Exact minutes. B Round down to 15 minutes. C Round down to 30 minutes.

**Answer (rate / rounding):** ______ / ______  **Decided by / date:** ______  **Notes:** ______

---

## B7. Half-day rule
Status: Open
**Affects:** `Shifts.HalfDayMinMinutes/HalfDayCutoffTime`, `HalfDayRequests`.

**Questions.** Exact rule for a half day (minimum hours; earliest leave time 12:00 or 13:00; first half and second half both allowed?). Is a document mandatory? Without approval, is the short day paid hourly or marked absent? When may a half-day employee skip check-out?

**Suggested key**
- A ★ Minimum 4 worked hours; leave no earlier than 12:00; both halves allowed; document required; unapproved short day is `Hourly`.
- B Cutoff 13:00; document optional.
- C Other (specify).

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B8. Schedule edge cases
Status: Open
**Affects:** `Shifts`, `ShiftAssignments`, `RosterOverrides`.

**Question.** HR said schedule management is detailed with many edge cases but has not listed them. Which of these apply, and what else: rotating shifts, per-person weekly offs, split shifts, Ramadan timings, swaps, extra duty on an off day, overnight shifts crossing month-end, a holiday on a weekly off?

**Suggested key**
- A ★ HR lists the cases before the schedule tables are built; the model is checked against the list.
- B Build the generic base and add cases as they appear.

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B9. Value lists and roles
Status: Open
**Affects:** `Employee.EmployeeStatus`, `EmployeeDocuments.DocumentType`, `UserRoles.Role` CHECK constraint.

- Employee statuses. Suggested: Active, Inactive, Resigned, Terminated. (2026-11-24: Employee History sets `Resigned` and `Terminated` on a resignation or termination and `Active` on a rejoin.)
- Document types. Suggested: CNIC, HalfDayProof, LeaveProof, MedicalCertificate, Other.
- Roles beyond Admin and HR. Suggested: Supervisor (approves leave and half days).

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B10. Mid-month joiners and leavers
Status: Open
**Affects:** payroll calculation, `Employee.DateOfJoining/DateOfLeaving`, `Payslips`.

**Question.** What is paid in the month someone joins or leaves?

**Suggested key**
- A ★ Pro-rated by calendar days (BasicSalary x days employed / days in month).
- B Pro-rated by scheduled working days.
- C Full month if present on any day.

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B11. Absence, lateness and unpaid leave deductions
Status: Open
**Affects:** `Attendance.DayStatus`, `LeaveTypes.IsPaid`, payroll deductions.

**Questions.** Is an absent day deducted, and at what rate? Do unpaid leave types (`IsPaid = 0`) deduct the same way? Is there any penalty for repeated lateness beyond the 1-minute grace (e.g. 3 lates = 1 day)?

**Suggested key**
- A ★ Absent and unpaid-leave days deduct BasicSalary / 30 per day; lateness is recorded, not deducted.
- B Same, plus a late-count rule (specify).
- C No attendance-based deductions; hourly employees only.

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B12. Work on off days and holidays, and overtime
Status: Open
**Affects:** `Attendance.DayStatus`, `RosterOverrides.ExtraDuty`, payroll.

**Question.** When someone works a weekly off or a gazetted holiday, or works past the end of the shift, is that paid extra, given as a compensatory day off, or ignored?

**Suggested key**
- A ★ Not paid extra by the system for now; recorded as `ExtraDuty` so HR can decide later.
- B Paid at a multiple of the hourly rate (specify).
- C Compensatory off, tracked as a leave credit.

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B13. Who approves what
Status: Open
**Affects:** `UserRoles` (was `Users.Role` until 2026-11-23), `*.ApprovedBy`, authorization (Setup requires Admin and Lists require Admin or HR; other areas are not yet role-checked). A user can hold several roles, so an HR user can also be given Admin in Setup → Users.

**Questions.** Who approves leave, half days, attendance corrections and shift overrides? Is there a supervisor per department or project, or does HR approve everything? Can someone approve their own request?

**Suggested key**
- A ★ HR approves everything for now; roles Admin and HR only; self-approval blocked.
- B Supervisor approves for their department, HR for the rest (adds a supervisor link on `Departments` or `Employee`).

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B14. Transfers between projects, departments and designations
Status: Partly answered (2026-11-24); one question left for HR
**Affects:** `Employee`, `EmployeeEvents`; past reports.

**Answered 2026-11-24 (Rayder).** HR wants a history of key events (joining, promotion, transfer, resignation, termination, rejoin). Built as the `EmployeeEvents` timeline: each promotion or transfer stores the project, department and designation before and after, and is recorded from Edit Employee, which also updates the employee. This replaces the effective-dated `EmployeeAssignments` table suggested in A below (the events carry the same facts, and a position on a past date can be worked out from them).

**Still open for HR.** Must attendance and payslip **reports** show where the person was at the time (needs a snapshot or a lookup through the events), or is the current position enough? Today reports use the current position.

**Question.** When someone moves project or department, do old attendance and payslip reports have to show where they were at the time, or is the current position enough?

**Suggested key**
- A ★ History needed: keep an `EmployeeAssignments (EmployeeId, ProjectId, DepartmentId, DesignationId, EffectiveFrom, EffectiveTo)` table; `Employee` shows the current row.
- B Current position only; payroll and attendance rows keep a snapshot of the department/project text at the time.
- C Current position only, no snapshot.

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B15. Unlocking a payslip
Status: Open
**Affects:** `Payslips.IsLocked`, `AuditLogs`, T4.

**Question.** Today a locked payslip can never be unlocked. Should HR be able to unlock it (reason required, logged) and regenerate, or is a correction made in the next month?

**Suggested key**
- A ★ Admin can unlock with a reason; recorded in `AuditLogs`; payslip regenerates.
- B Never unlock; corrections go into the next month as adjustments.

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B16. Forgotten check-outs
Status: Open
**Affects:** `AttendanceCorrections`, `Attendance.DayStatus = 'MissingCheckout'`, payroll.

**Questions.** How long does an employee have to report a forgotten check-out? Is there a limit per month? If it is still missing when payroll runs, is the day paid, hourly, or does payroll wait?

**Suggested key**
- A ★ Report within the same month; unresolved days are shown to HR before payroll can be generated.
- B Payroll treats a missing check-out as a full shift if there was a check-in.
- C Treated as absent until corrected.

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B18. Legacy per-year leave figures on Employee
Status: Closed (2026-11-24)
**Decided by Rayder, 2026-11-24.** The seven columns (`CarryForwardLeaves`, `CarryForwardLeaves1`, `Year2022`, `Year2023`, `Year2023New`, `Year2024`, `AdjustedAjusted`) are copied once into the read-only `EmployeeLegacyLeaveBalances` and dropped from `Employee` (script 10; the archive is option A below). The data is test data, so no figure was checked for correctness. In it, Year2023 equalled CarryForwardLeaves minus Year2022 and Year2024 equalled CarryForwardLeaves1 minus Year2023New on every row: they were results typed in as numbers, not inputs. The employee Leave Balance tab now shows the live balance from the LMS tables (quota, carry-forward, adjustments, approved leave); the Employees grid no longer filters by Year 2024. Leave days are tracked in one decimal (`decimal(5,1)`, half days = 0.5).

**Original question (kept for the record).** What does each column mean, which are still used, and which figures must be kept as history? Options were A archive then drop, B drop, C keep as they are.

---

## B19. Which key events does HR track?
Status: Open
**Affects:** `EmployeeEvents.EventType` (`CK_EmployeeEvents_Type`), Employee History form.

**Question.** The history records Joined, Promotion, Transfer, Resignation, Termination, Rejoin and Other (2026-11-24). Does HR need more types as separate entries: suspension, probation confirmed, contract renewed or ended, salary revision, warning? Does a **salary revision** belong in this history (it would store amounts, so it would not be shown on grids, only on the employee's own page)?

**Suggested key**
- A ★ Keep the seven; HR uses `Other` with remarks until a pattern shows which types are needed, then types are added to the CHECK.
- B HR lists the types now.

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______

---

## B17. Tax
Status: Open
**Affects:** `TaxRules`, `Employee.ApplyTax`, payroll.

**Questions.** Is tax charged on basic salary only (today) or gross? Is the tax year July-June? Who is exempt (`ApplyTax = 0`), and should that be a flag per employee or a rule?

**Suggested key**
- A ★ Keep basic-only, July-June tax year, per-employee flag; fix the payroll lookup to use the tax-year label.
- B Tax on gross (specify).

**Answer:** ______  **Decided by / date:** ______  **Notes:** ______
