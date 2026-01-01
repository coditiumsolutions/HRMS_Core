# Feature Specification: Attendance Management

**Feature Branch**: feat-attendance  
**Created**: 2025-12-31  
**Status**: Draft  

---

## User Scenarios

### User Story 1 – Mark Attendance (P1)

As an HR user,  
I want to record employee attendance  
So that daily presence is tracked.

---

### User Story 2 – View Attendance Summary (P1)

As an HR user,  
I want to view attendance by date and department  
So that I can monitor attendance trends.

---

## Functional Requirements

- FR-001: System MUST allow marking attendance per employee
- FR-002: Attendance MUST store date and status (Present/Absent)
- FR-003: Attendance MUST be viewable by date and department

---

## Key Entity

**Attendance**
- Id
- EmployeeId
- Date
- Status
- Department

---

## Success Criteria

- Attendance entry saved successfully
- Attendance summary loads correctly
