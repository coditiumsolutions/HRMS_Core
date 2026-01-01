# Feature Specification: Leave Management System (LMS)

**Feature Branch**: feat-lms  
**Created**: 2025-12-31  
**Status**: Draft  

---

## User Scenarios

### User Story 1 – Apply Leave (P1)

As an employee,  
I want to apply for leave  
So that my absence is recorded.

---

### User Story 2 – View Leave Balance (P1)

As an employee,  
I want to view my leave balance  
So that I can plan my leaves.

---

### User Story 3 – Approve Leave (P2)

As an HR user,  
I want to approve or reject leave requests.

---

## Functional Requirements

- FR-001: Employees MUST apply leave
- FR-002: System MUST calculate leave balance
- FR-003: HR MUST approve/reject leaves

---

## Key Entity

**LeaveRequest**
- Id
- EmployeeId
- LeaveType
- FromDate
- ToDate
- Status

---

## Success Criteria

- Leave application saved
- Balance updated correctly
