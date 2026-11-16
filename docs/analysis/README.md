---
status: snapshot
snapshot_date: 2026-11-16
note: Static analysis at commit 2843f59, corrected with live-database checks on 2026-11-16. 05-deployment.md is outdated (see ../deployment.md); 01 and 04 are partially outdated. See each file's frontmatter.
---
# HRMS (HRMBT.Web) – Application Analysis

Static analysis of the repository at commit `2843f59` (branch `main`). Nothing was run; findings come from reading source, SQL scripts, specs and deploy files.

| File | Topic |
|------|-------|
| [01-tech-stack-and-architecture.md](01-tech-stack-and-architecture.md) | How it works: stack, structure, request pipeline |
| [02-functionality.md](02-functionality.md) | What it does: modules, business logic, payroll rules |
| [03-data-structures.md](03-data-structures.md) | Database tables, relationships, schema management |
| [04-security.md](04-security.md) | Authentication, authorization, protected routes, risks |
| [05-deployment.md](05-deployment.md) | Build/publish/IIS deployment |
| [06-gaps.md](06-gaps.md) | Gaps, bugs, risks, prioritised recommendations |
| [07-ui-elements.md](07-ui-elements.md) | Layouts, navigation, pages, front-end assets |

## One-paragraph summary
A server-rendered **ASP.NET Core MVC (.NET 8)** HR system for Bahria Town: employees, attendance (Excel upload), leave management (LMS), allowances/deductions, tax slabs, payroll/payslip generation and reports, on **SQL Server via EF Core**. Authentication is cookie-based against a custom `Users` table; there is **no role-based authorization**, and the `Setup` controller (user and config management) is **anonymous**, which is the most serious issue found.
