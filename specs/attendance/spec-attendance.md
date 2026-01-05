# Attendance Module Specification

## Module Name
Attendance

## Purpose
The Attendance module records and manages daily employee attendance using CSV file uploads as the primary input mechanism. Attendance data serves as a foundational input for Payroll processing, Leave Management (LMS), and reporting.

## Scope
This module handles:
- CSV-based attendance upload
- Attendance validation
- Daily attendance storage
- Monthly attendance aggregation
- Downstream consumption by Payroll and LMS

## Primary Actors
- HR Administrator
- Payroll System (consumer)
- LMS System (consumer)

---

## Attendance Data Model (Logical)

Each attendance record represents **one employee on one calendar date**.

### Core Attributes
- EmployeeCode
- AttendanceDate
- AttendanceStatus
- InTime (optional)
- OutTime (optional)

---

## CSV Upload Specification

### Accepted Format
- File type: `.csv`
- Encoding: UTF-8
- One row per employee per date

### CSV Header (Exact)




---

## Required Fields
- EmployeeCode
- Date
- Status

## Optional Fields
- InTime
- OutTime

---

## Attendance Status Rules

| Status     | Meaning |
|-----------|--------|
| Present   | Full working day |
| Absent    | No attendance |
| Late      | Late arrival |
| HalfDay   | Half working day |
| Leave     | Approved leave |
| Holiday   | Company holiday |

---

## Business Rules

### 1. Employee Validation
- EmployeeCode must exist in the Employee master
- Invalid EmployeeCode rows are rejected and logged

### 2. Uniqueness Rule
- Only **one attendance record per employee per date** is allowed

### 3. Duplicate Handling Rule
- Upload mode determines behavior:
  - Reject duplicates
  - OR overwrite existing record

### 4. Time Validation
- If both InTime and OutTime are provided:
  - OutTime must be later than InTime

### 5. Working Hours Calculation
- Working hours = OutTime − InTime
- Used for reporting and future payroll rules

---

## Monthly Attendance Summary Rules

For a given employee and month:
- Total working days
- Present days
- Absent days
- Leave days
- Late days
- Half days

---

## Payroll Integration Rules
- Absent days reduce payable salary
- Attendance summary is consumed by Payroll
- Attendance module does **not** calculate salary

---

## LMS Integration Rules
- Leave attendance records are forwarded to LMS
- LMS remains the authority for leave approval

---

## Reports (Logical)
- Employee monthly attendance
- Department-wise attendance
- Attendance upload error log

---

## Non-Goals (Phase 1)
- Biometric devices
- Real-time punch-in/out
- Shift scheduling
- Overtime rules
