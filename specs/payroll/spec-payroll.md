# Payroll Module Specification

## Module: Payroll

### Description
The Payroll module is responsible for calculating employee salaries, managing allowances and deductions, and generating payslips.  
It integrates with the Employee module to retrieve employee-related data.

---

## Database Tables
- Employee
- Allowances
- Deductions
- Payslips
- PayslipDetails

---

## Functional Features

### 1. Allowances Management
- Add, edit, and delete employee allowances
- Supports both fixed and percentage-based allowances
- Only active allowances are considered during payroll calculation

### 2. Deductions Management
- Add, edit, and delete employee deductions
- Supports fixed and percentage-based deductions
- Supports mandatory and optional deductions

### 3. Payslip Generation
- Calculate gross salary (Basic Salary + Allowances)
- Calculate total deductions
- Calculate net salary
- Apply leave-based salary adjustments

### 4. Payslip Details
- Store a detailed breakdown of all salary components
- Maintain calculation transparency for audit purposes

### 5. Payslip Locking
- Lock payslips after generation to prevent modification
- Allow unlock only by authorized users (future enhancement)

### 6. Reports
- Employee-wise payslip report
- Department-wise salary summary
- Monthly payroll summary

---

## Payroll Calculation Rules

### 1. Gross Salary
- Gross Salary = Basic Salary + Total Allowances
- Only active allowances are included
- Percentage-based allowances are calculated on Basic Salary

### 2. Deductions
- Deductions may be fixed or percentage-based
- Percentage-based deductions are calculated on Gross Salary

### 3. Tax Calculation
- Tax is applied only if `Employee.ApplyTax = "Yes"`
- Tax Rules (Learning Version):
  - Gross ≤ 50,000 → No tax
  - 50,001 – 100,000 → 5%
  - Above 100,000 → 10%

### 4. Net Salary
- Net Salary = Gross Salary – Total Deductions – Tax
