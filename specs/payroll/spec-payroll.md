# Payroll Module Spec

## Module: Payroll

### Description:
Payroll module calculates employee salaries, manages allowances, deductions, and generates payslips. It integrates with Employee module for employee details.

### Tables:
- Employee
- Allowances
- Deductions
- Payslips
- PayslipDetails

### Features:
1. Allowances Management
   - Add, Edit, Delete employee allowances
   - Percentage-based or fixed allowances
2. Deductions Management
   - Add, Edit, Delete employee deductions
   - Mandatory or optional deductions
3. Payslip Generation
   - Calculate gross salary: Basic + Allowances
   - Calculate total deductions
   - Calculate net salary = Gross - Deductions
   - Leave adjustment: reduce salary for leave days
4. Payslip Details
   - Store breakdown of all salary components
5. Lock/Unlock Payslip
   - Prevent editing once locked
6. Reports
   - Employee payslips
   - Department-wise salary summary
   - Monthly payroll summary
