# Leave Management System (LMS) Specification

## Module Name
Leave Management System (LMS)

## Purpose
The Leave Management System manages employee leave entitlements, applications, approvals, and balances.  
It ensures accurate leave tracking and provides validated leave data to Attendance and Payroll modules.

## Scope
This module handles:
- Leave quota definition (yearly)
- Gazetted holidays
- Leave applications
- Leave approval workflow
- Carry-forward leave tracking
- Leave balance calculation

## Primary Actors
- Employee
- Department Supervisor
- HR Administrator
- Payroll System (consumer)
- Attendance System (consumer)

---

## Core Entities (Logical)

- LeaveQuota
- GazettedHoliday
- EmployeeLeaves
- CarryForwardLeaves

---

## Leave Types
Leave types are defined in **LeaveQuota** table and are year-specific.

Examples:
- Casual
- Sick
- Annual
- Unpaid

---

## Business Rules

### 1. Leave Quota Rules
- Leave quota is defined per **LeaveTypeName per Year**
- Quota applies to all employees unless overridden

### 2. Gazetted Holidays Rules
- Gazetted holidays are **non-working days**
- Holidays are excluded from leave day calculation

### 3. Leave Application Rules
- Leave must have:
  - EmployeeID
  - LeaveTypeName
  - StartDate
  - EndDate
- Leave duration includes:
  - StartDate to EndDate
  - Excludes gazetted holidays
  - Excludes weekends (configurable – Phase 2)

### 4. Leave Days Calculation
- TotalDays = Date range − excluded days + AddDays − ExcludeDays
- Short leave or manual adjustment stored in `Short_Adj`

### 5. Leave Status Workflow
Valid statuses:
- Applied
- Approved
- Rejected
- Cancelled

### 6. Approval Rules
- Leave must be approved by supervisor or HR
- Approval records:
  - ApprovedBy
  - ApprovedOn

### 7. Carry Forward Rules
- Unused eligible leaves are carried forward yearly
- Carry forward is stored in CarryforwardLeaves
- Leave balance = TotalLeaves − LeavesUsed + CarryForward

### 8. Attendance Integration
- Approved leave days are sent to Attendance module
- Attendance marks these days as `Leave`

### 9. Payroll Integration
- Paid leaves do not reduce salary
- Unpaid leaves reduce payable salary
- Payroll consumes final approved leave summary

---

## Reports (Logical)
- Employee leave balance
- Leave application history
- Yearly leave utilization
- Carry-forward summary

---

## Non-Goals (Phase 1)
- Half-day leaves
- Shift-based leave rules
- Leave encashment
- Mobile approvals
