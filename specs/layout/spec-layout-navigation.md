# Feature Specification: Layout & Contextual Navigation

**Feature Name**: Global Layout and Contextual Navigation  
**Status**: Draft  
**Applies To**: All modules (Employees, Payroll, Attendance, LMS, Tax)

---

## Problem Statement

The HR Management System requires a consistent user interface that:
- Separates global navigation from module-specific navigation
- Prevents users from seeing irrelevant links
- Scales cleanly as new modules are added

---

## Goals

- Provide two stacked top navigation bars
- Display module-specific links in the sidebar
- Ensure a consistent 20% / 80% layout across all pages
- Allow future modules without layout refactoring

---

## Layout Structure

1. **Top Navigation Bar 1**
   - Background color: White
   - Purpose: Branding, user info, logout (future)

2. **Top Navigation Bar 2**
   - Background color: Dark Blue
   - Contains module links:
     - Home
     - Employees
     - Payroll
     - Attendance
     - LMS
     - Tax

3. **Main Content Area**
   - Sidebar: 20% width
   - Page Content: 80% width

---

## Sidebar Behavior

- Sidebar content MUST change based on selected module
- Only links related to the active module are visible
- Sidebar MUST NOT show links from other modules

---

## Module → Sidebar Mapping

### Employees
- Add Employees
- View Employees
- Active Employees
- Employees Reports

### Payroll
- Run Payroll
- Payroll History
- Payslips
- Payroll Reports

### Attendance
- Attendance Today
- Attendance By Department
- Attendance Summary

### LMS
- Add Leave
- View Leaves
- Leave Balance
- Leave Reports

### Tax
- Tax Slabs
- Employee Tax
- Tax Reports

---

## Functional Requirements

- FR-001: System MUST display two stacked top navigation bars
- FR-002: Sidebar MUST update without full page reload
- FR-003: Sidebar MUST reflect only the selected module
- FR-004: Layout MUST be reusable across all modules
- FR-005: Sidebar width MUST be 20% and content width 80%

---

## Success Criteria

- Sidebar updates immediately when module changes
- No unrelated links appear in sidebar
- Layout works consistently on all module pages
