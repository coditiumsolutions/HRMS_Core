/*
---
status: outdated
last_verified: 2026-11-26
outdated_since: 2026-11-24
reason: "Read-only queries about the old leave tables (some no longer exist) in the old database Payroll2."
superseded_by: "db.md"
---
*/
-- SQL Script to query LMS table schemas
-- Run this on Payroll2 database to get exact table structures

-- Check if tables exist and get their schemas
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('LeaveQuota', 'GazettedHoliday', 'EmployeeLeaves', 'CarryforwardLeaves', 'CarryForwardLeaves')
ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- Alternative: Get table definitions
SELECT 
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE t.name IN ('LeaveQuota', 'GazettedHoliday', 'EmployeeLeaves', 'CarryforwardLeaves', 'CarryForwardLeaves')
ORDER BY t.name, c.column_id;

