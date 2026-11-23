---
status: index
last_verified: 2026-11-23
verified_against: main @ 7aa2685 plus the uncommitted security and user-roles changes (code), and the redesigned development database (scripts 01-04, 06, 07 applied)
snapshot_date: 2026-11-16
work_state: "security fixes and multi-role users done and tested on the local copy, not committed or deployed; sa password rotation deferred to the owner; production rollout pending"
note: Written at commit 2843f59 and revised on 2026-11-23 after the security fixes, the multi-role change and the db.md migration note. 04 and 06 are current as of 2026-11-23 and carry machine-readable task lists (`tasks:` in their frontmatter); 01, 02, 03 and 07 are partially outdated (see each file's frontmatter); 05 is outdated (see ../deployment.md).
---
# HRMS (HRMBT.Web) – Application Analysis

Static analysis first written at commit `2843f59` (2026-11-16) and rechecked against `main` @ `7aa2685` and the redesigned development database on 2026-11-23. Findings come from reading source, SQL scripts and deploy files, plus checks of the live database on 2026-11-16.

| Status | File | Topic |
|---|---|---|
| partly outdated | [01-tech-stack-and-architecture.md](01-tech-stack-and-architecture.md) | How it works: stack, structure, request pipeline |
| partly outdated | [02-functionality.md](02-functionality.md) | What it does: modules, business logic, payroll rules |
| partly outdated | [03-data-structures.md](03-data-structures.md) | Database tables, relationships, schema management (production schema; banner lists the redesign) |
| **current** | [04-security.md](04-security.md) | Authentication, authorization, protected routes, risks, with status per finding |
| outdated | [05-deployment.md](05-deployment.md) | Build/publish/IIS deployment (use ../deployment.md) |
| **current** | [06-gaps.md](06-gaps.md) | Gaps, bugs, risks, what is left, prioritised |
| partly outdated | [07-ui-elements.md](07-ui-elements.md) | Layouts, navigation, pages, front-end assets |

## One-paragraph summary
A server-rendered **ASP.NET Core MVC (.NET 8)** HR system for Bahria Town: employees, attendance (Excel upload), leave management (LMS), allowances/deductions, tax slabs, payroll/payslip generation and reports, on **SQL Server via EF Core**. Authentication is cookie-based against a custom `Users` table. Users can hold several roles (`Admin`, `HR`, or both). Setup now requires `Admin` and Lists require `Admin` or `HR`; payroll, salary and report screens are still open to every logged-in user. These security fixes are in the repository but **not yet deployed**, so production's Setup is still anonymous. Rotating the old `sa` password is deferred to the owner. The database redesign (typed columns, foreign keys, managed lists) is done in the repository and the local database but **not yet applied to production**.
