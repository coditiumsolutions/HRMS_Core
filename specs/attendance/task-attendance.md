# Attendance Module Tasks

## Task 1: Create Attendance Domain Models
- Create Attendance entity
- Create AttendanceUploadLog entity
- Define relationships with Employee

## Task 2: Register DbContext
- Add DbSet<Attendance>
- Add DbSet<AttendanceUploadLog>

## Task 3: Database Migration
- Create migration AttendanceModule
- Apply migration to database

## Task 4: CSV Parsing Service
- Create AttendanceCsvParser
- Parse CSV rows into DTOs
- Validate required fields

## Task 5: Attendance Service
- Validate EmployeeCode existence
- Enforce one record per employee per date
- Handle duplicate upload rules
- Persist valid attendance records
- Log invalid rows

## Task 6: Upload Summary Result
- Return total rows
- Return success count
- Return failure count
- Return error details

## Task 7: Attendance Controller
- GET Upload page
- POST Upload CSV
- Display upload results

## Task 8: Reporting Queries
- Monthly attendance per employee
- Department-wise attendance summary

## Task 9: Payroll Integration Method
- Implement GetAttendanceSummary(employeeId, month, year)

## Task 10: Testing
- Upload valid CSV
- Upload invalid CSV
- Verify database persistence
- Verify payroll consumption

## Task 11: Commit
- Commit attendance module
