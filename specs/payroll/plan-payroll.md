# Payroll Module Plan

## Phase 1: Database & Models
- Ensure all tables are created in SQL Server
- Create Entity Framework Core models for Allowances, Deductions, Payslips, PayslipDetails
- Configure relationships and constraints in ApplicationDbContext

## Phase 2: CRUD Operations
- Create controllers and views for Allowances
- Create controllers and views for Deductions
- Create Employee Payslip generation form

## Phase 3: Payroll Calculation
- Create a service/class to calculate gross, deductions, and net salary
- Include leave adjustments
- Store Payslip and PayslipDetails
- Implement payroll calculations strictly according to spec-payroll.md → Payroll Calculation Rules


## Phase 4: Locking & Reporting
- Add lock/unlock feature
- Add reports per employee, per department, per month

## Phase 5: Testing & Integration
- Run migrations
- Test CRUD operations
- Test payslip calculation
- Integrate with Employee module
