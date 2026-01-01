# Feature Specification: Employee Management

**Feature Branch**: feat-employee-management  
**Created**: 2025-12-31  
**Status**: Draft  

---

## User Scenarios & Testing

### User Story 1 – View Employees (P1)

As an HR user,  
I want to view a list of employees  
So that I can see employee information in one place.

**Acceptance Criteria**
- Employee list loads successfully
- Displays Employee ID, Name, Department, Designation, Status

---

### User Story 2 – Add Employee (P1)

As an HR user,  
I want to add a new employee  
So that they can be managed in the system.

**Acceptance Criteria**
- EmployeeID must be unique
- Basic Salary defaults to 0
- Save persists data to database

---

### User Story 3 – Edit Employee (P2)

As an HR user,  
I want to edit employee details  
So that records remain accurate.

---

### User Story 4 – View Employee Details (P2)

As an HR user,  
I want to view full employee profile  
So that I can see personal and job details.

---

## Functional Requirements

- FR-001: System MUST allow CRUD operations for employees
- FR-002: EmployeeID MUST be unique
- FR-003: BasicSalary MUST default to 0
- FR-004: Employee data MUST be stored in SQL Server

---

## Key Entity

**Employee**
- uid (PK)
- EmployeeID (Unique)
- EmployeeName
- CNIC
- Department
- Designation
- DateOfJoining
- BasicSalary
- ApplyTax
- Status

---

## Success Criteria

- Employee list loads under 300ms
- No duplicate EmployeeID allowed
- CRUD works without errors
