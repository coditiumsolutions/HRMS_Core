using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMBT.Web.Migrations
{
    /// <inheritdoc />
    public partial class FixAttendanceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use SQL to check if table exists and handle accordingly
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Attendances]', N'U') IS NOT NULL
                BEGIN
                    -- Table exists, modify it
                    IF COL_LENGTH('Attendances', 'Department') IS NOT NULL
                    BEGIN
                        DECLARE @var0 sysname;
                        SELECT @var0 = [d].[name]
                        FROM [sys].[default_constraints] [d]
                        INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                        WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Attendances]') AND [c].[name] = N'Department');
                        IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Attendances] DROP CONSTRAINT [' + @var0 + '];');
                        ALTER TABLE [Attendances] DROP COLUMN [Department];
                    END
                    
                    -- Rename columns if they exist
                    IF COL_LENGTH('Attendances', 'Status') IS NOT NULL
                    BEGIN
                        EXEC sp_rename 'Attendances.Status', 'AttendanceStatus', 'COLUMN';
                    END
                    
                    IF COL_LENGTH('Attendances', 'Date') IS NOT NULL
                    BEGIN
                        EXEC sp_rename 'Attendances.Date', 'AttendanceDate', 'COLUMN';
                    END
                    
                    -- Alter EmployeeId to nullable if it exists
                    IF COL_LENGTH('Attendances', 'EmployeeId') IS NOT NULL
                    BEGIN
                        ALTER TABLE [Attendances] ALTER COLUMN [EmployeeId] int NULL;
                    END
                    
                    -- Add new columns if they don't exist
                    IF COL_LENGTH('Attendances', 'EmployeeCode') IS NULL
                    BEGIN
                        ALTER TABLE [Attendances] ADD [EmployeeCode] nvarchar(50) NOT NULL DEFAULT '';
                    END
                    
                    IF COL_LENGTH('Attendances', 'InTime') IS NULL
                    BEGIN
                        ALTER TABLE [Attendances] ADD [InTime] time NULL;
                    END
                    
                    IF COL_LENGTH('Attendances', 'OutTime') IS NULL
                    BEGIN
                        ALTER TABLE [Attendances] ADD [OutTime] time NULL;
                    END
                END
                ELSE
                BEGIN
                    -- Table doesn't exist, create it
                    CREATE TABLE [Attendances] (
                        [Id] int NOT NULL IDENTITY,
                        [EmployeeId] int NULL,
                        [AttendanceDate] datetime2 NOT NULL,
                        [AttendanceStatus] nvarchar(20) NOT NULL,
                        [EmployeeCode] nvarchar(50) NOT NULL,
                        [InTime] time NULL,
                        [OutTime] time NULL,
                        CONSTRAINT [PK_Attendances] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Attendances_Employee_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employee] ([uid]) ON DELETE SET NULL
                    );
                    
                    CREATE UNIQUE INDEX [IX_Attendance_EmployeeCode_Date] ON [Attendances] ([EmployeeCode], [AttendanceDate]);
                    CREATE INDEX [IX_Attendances_EmployeeId] ON [Attendances] ([EmployeeId]);
                END
            ");

            // Create AttendanceUploadLogs table if it doesn't exist
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[AttendanceUploadLogs]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AttendanceUploadLogs] (
                        [Id] int NOT NULL IDENTITY,
                        [UploadDate] datetime2 NOT NULL,
                        [UploadedBy] nvarchar(100) NULL,
                        [FileName] nvarchar(255) NULL,
                        [TotalRows] int NOT NULL,
                        [SuccessCount] int NOT NULL,
                        [FailureCount] int NOT NULL,
                        [ErrorDetails] nvarchar(max) NULL,
                        CONSTRAINT [PK_AttendanceUploadLogs] PRIMARY KEY ([Id])
                    );
                END
            ");

            // Add foreign key if table exists and FK doesn't exist
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Attendances]', N'U') IS NOT NULL
                AND OBJECT_ID(N'FK_Attendances_Employee_EmployeeId', N'F') IS NULL
                BEGIN
                    ALTER TABLE [Attendances] 
                    ADD CONSTRAINT [FK_Attendances_Employee_EmployeeId] 
                    FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employee] ([uid]) ON DELETE SET NULL;
                END
            ");

            // Create indexes if they don't exist
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Attendances]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attendance_EmployeeCode_Date' AND object_id = OBJECT_ID('Attendances'))
                    BEGIN
                        CREATE UNIQUE INDEX [IX_Attendance_EmployeeCode_Date] ON [Attendances] ([EmployeeCode], [AttendanceDate]);
                    END
                    
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attendances_EmployeeId' AND object_id = OBJECT_ID('Attendances'))
                    BEGIN
                        CREATE INDEX [IX_Attendances_EmployeeId] ON [Attendances] ([EmployeeId]);
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Employee_EmployeeId",
                table: "Attendances");

            migrationBuilder.DropTable(
                name: "AttendanceUploadLogs");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_EmployeeCode_Date",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_EmployeeId",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "EmployeeCode",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "InTime",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "OutTime",
                table: "Attendances");

            migrationBuilder.RenameColumn(
                name: "AttendanceStatus",
                table: "Attendances",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "AttendanceDate",
                table: "Attendances",
                newName: "Date");

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "Attendances",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Attendances",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
