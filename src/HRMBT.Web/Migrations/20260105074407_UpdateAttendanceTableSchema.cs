using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMBT.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAttendanceTableSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if table exists, if not create it
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Attendance]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Attendance] (
                        [AttendanceID] int IDENTITY(1,1) NOT NULL,
                        [EmployeeID] nvarchar(50) NOT NULL,
                        [EmployeeName] nvarchar(100) NOT NULL,
                        [DepartmentName] nvarchar(50) NOT NULL,
                        [AttendanceDate] date NOT NULL,
                        [TimeIn] time NULL,
                        [TimeOut] time NULL,
                        [Status] nvarchar(10) NULL,
                        [Comments] nvarchar(255) NULL,
                        CONSTRAINT [PK_Attendance] PRIMARY KEY ([AttendanceID])
                    );
                    
                    CREATE UNIQUE INDEX [IX_Attendance_EmployeeID_Date] 
                    ON [Attendance] ([EmployeeID], [AttendanceDate]);
                END
                ELSE
                BEGIN
                    -- Table exists, ensure columns match
                    -- Add AttendanceID if missing (shouldn't happen, but safe)
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'AttendanceID')
                    BEGIN
                        ALTER TABLE [Attendance] ADD [AttendanceID] int IDENTITY(1,1) NOT NULL;
                        ALTER TABLE [Attendance] ADD CONSTRAINT [PK_Attendance] PRIMARY KEY ([AttendanceID]);
                    END
                    
                    -- Add EmployeeID if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'EmployeeID')
                    BEGIN
                        ALTER TABLE [Attendance] ADD [EmployeeID] nvarchar(50) NOT NULL DEFAULT '';
                    END
                    
                    -- Add EmployeeName if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'EmployeeName')
                    BEGIN
                        ALTER TABLE [Attendance] ADD [EmployeeName] nvarchar(100) NOT NULL DEFAULT '';
                    END
                    
                    -- Add DepartmentName if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'DepartmentName')
                    BEGIN
                        ALTER TABLE [Attendance] ADD [DepartmentName] nvarchar(50) NOT NULL DEFAULT '';
                    END
                    
                    -- Add AttendanceDate if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'AttendanceDate')
                    BEGIN
                        ALTER TABLE [Attendance] ADD [AttendanceDate] date NOT NULL DEFAULT GETDATE();
                    END
                    
                    -- Add TimeIn if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'TimeIn')
                    BEGIN
                        ALTER TABLE [Attendance] ADD [TimeIn] time NULL;
                    END
                    
                    -- Add TimeOut if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'TimeOut')
                    BEGIN
                        ALTER TABLE [Attendance] ADD [TimeOut] time NULL;
                    END
                    
                    -- Add Status if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'Status')
                    BEGIN
                        ALTER TABLE [Attendance] ADD [Status] nvarchar(10) NULL;
                    END
                    
                    -- Add Comments if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'Comments')
                    BEGIN
                        ALTER TABLE [Attendance] ADD [Comments] nvarchar(255) NULL;
                    END
                    
                    -- Create unique index if it doesn't exist
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attendance_EmployeeID_Date' AND object_id = OBJECT_ID('Attendance'))
                    BEGIN
                        CREATE UNIQUE INDEX [IX_Attendance_EmployeeID_Date] 
                        ON [Attendance] ([EmployeeID], [AttendanceDate]);
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop index if exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attendance_EmployeeID_Date' AND object_id = OBJECT_ID('Attendance'))
                BEGIN
                    DROP INDEX [IX_Attendance_EmployeeID_Date] ON [Attendance];
                END
            ");
        }
    }
}
