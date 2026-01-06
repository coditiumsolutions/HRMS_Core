# Leave Management System Plan

## Phase 1: Foundation
- Map existing SQL tables to EF Core models
- Establish relationships with Employee

## Phase 2: Leave Quota & Holidays
- Manage LeaveQuota records
- View GazettedHolidays

## Phase 3: Leave Application
- Create leave application flow
- Calculate leave duration

## Phase 4: Approval Workflow
- Supervisor approval
- HR approval
- Status transitions

## Phase 5: Carry Forward Processing
- Year-end leave carry forward logic
- Balance updates

## Phase 6: Integration
- Publish approved leaves to Attendance
- Publish leave summary to Payroll

## Phase 7: Reporting
- Leave balance report
- Employee leave history

## Phase 8: Testing
- Valid leave application
- Approval flow
- Payroll and attendance integration
