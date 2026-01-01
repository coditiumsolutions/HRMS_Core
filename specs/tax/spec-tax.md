# Feature Specification: Tax Management

**Feature Branch**: feat-tax  
**Created**: 2025-12-31  
**Status**: Draft  

---

## User Scenarios

### User Story 1 – Calculate Tax (P1)

As an HR user,  
I want to calculate tax based on salary  
So that payroll deductions are accurate.

---

### User Story 2 – Apply Tax Rule (P2)

As an admin,  
I want to define tax slabs  
So that tax calculations follow policy.

---

## Functional Requirements

- FR-001: System MUST calculate tax based on salary
- FR-002: Tax slabs MUST be configurable
- FR-003: Tax MUST integrate with Payroll

---

## Key Entity

**TaxRule**
- Id
- MinSalary
- MaxSalary
- TaxPercentage

---

## Success Criteria

- Tax calculated correctly
- Payroll reflects tax deduction
