# Leave Management System Tasks

## Task 1: Create LMS Domain Models
- Create LeaveQuota model
- Create GazettedHoliday model
- Create EmployeeLeave model
- Create CarryforwardLeave model
- Map models exactly to existing SQL schema

## Task 2: DbContext Registration
- Add DbSet<LeaveQuota>
- Add DbSet<GazettedHoliday>
- Add DbSet<EmployeeLeave>
- Add DbSet<CarryforwardLeave>

## Task 3: Leave Calculation Service
- Calculate leave duration
- Exclude gazetted holidays
- Apply AddDays and ExcludeDays logic

## Task 4: Leave Application Service
- Create leave request
- Validate quota availability
- Save application with status = Applied

## Task 5: Leave Approval Service
- Approve leave
- Reject leave
- Update approval fields

## Task 6: Carry Forward Service
- Calculate yearly carry forward
- Update CarryforwardLeaves table

## Task 7: LMS Controller
- Apply Leave (GET/POST)
- Approve Leave
- View Leave Balance
- View Leave History

## Task 8: Attendance Integration
- Publish approved leave dates to Attendance
- Mark attendance status as Leave

## Task 9: Payroll Integration
- Provide leave summary:
  - Paid leaves
  - Unpaid leaves

## Task 10: Views
- Leave application form
- Approval dashboard
- Leave balance view

## Task 11: Testing
- Apply valid leave
- Apply leave exceeding quota
- Approve and reject flows
- Verify attendance marking

## Task 12: Commit
- Commit LMS module
