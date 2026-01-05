# Payroll Module Tasks

## Task 1: Create EF Models
- Create `Allowance.cs`, `Deduction.cs`, `Payslip.cs`, `PayslipDetail.cs` under Models folder
- Add DbSet<> in ApplicationDbContext

## Task 2: Run Migrations
- `dotnet ef migrations add PayrollModule`
- `dotnet ef database update`

## Task 3: Create Controllers
- PayrollController (for generating payslips)
- AllowancesController
- DeductionsController

## Task 4: Create Views
- Allowances CRUD
- Deductions CRUD
- Payroll generation and payslip view

## Task 5: Implement Payroll Service

- Create PayrollCalculationService
- Implement the following methods based on spec-payroll.md:
  - CalculateGrossSalary
  - CalculateTotalDeductions
  - CalculateTax
  - CalculateNetSalary
- Ensure calculations follow Payroll Calculation Rules


## Task 6: Locking Payslips
- Add IsLocked flag
- Lock/Unlock functionality in UI

## Task 7: Reports
- Monthly summary report
- Department-wise report

## Task 8: Testing
- Verify CRUD operations
- Verify calculations
- Verify reports
