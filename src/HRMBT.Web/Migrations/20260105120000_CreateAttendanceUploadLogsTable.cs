using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMBT.Web.Migrations
{
    /// <inheritdoc />
    public partial class CreateAttendanceUploadLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[AttendanceUploadLogs]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AttendanceUploadLogs] (
                        [Id] int IDENTITY(1,1) NOT NULL,
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
                ELSE
                BEGIN
                    -- Table exists, ensure columns match
                    -- Add Id if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'Id')
                    BEGIN
                        ALTER TABLE [AttendanceUploadLogs] ADD [Id] int IDENTITY(1,1) NOT NULL;
                        ALTER TABLE [AttendanceUploadLogs] ADD CONSTRAINT [PK_AttendanceUploadLogs] PRIMARY KEY ([Id]);
                    END
                    
                    -- Add UploadDate if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'UploadDate')
                    BEGIN
                        ALTER TABLE [AttendanceUploadLogs] ADD [UploadDate] datetime2 NOT NULL DEFAULT GETDATE();
                    END
                    
                    -- Add UploadedBy if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'UploadedBy')
                    BEGIN
                        ALTER TABLE [AttendanceUploadLogs] ADD [UploadedBy] nvarchar(100) NULL;
                    END
                    
                    -- Add FileName if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'FileName')
                    BEGIN
                        ALTER TABLE [AttendanceUploadLogs] ADD [FileName] nvarchar(255) NULL;
                    END
                    
                    -- Add TotalRows if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'TotalRows')
                    BEGIN
                        ALTER TABLE [AttendanceUploadLogs] ADD [TotalRows] int NOT NULL DEFAULT 0;
                    END
                    
                    -- Add SuccessCount if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'SuccessCount')
                    BEGIN
                        ALTER TABLE [AttendanceUploadLogs] ADD [SuccessCount] int NOT NULL DEFAULT 0;
                    END
                    
                    -- Add FailureCount if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'FailureCount')
                    BEGIN
                        ALTER TABLE [AttendanceUploadLogs] ADD [FailureCount] int NOT NULL DEFAULT 0;
                    END
                    
                    -- Add ErrorDetails if missing
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'ErrorDetails')
                    BEGIN
                        ALTER TABLE [AttendanceUploadLogs] ADD [ErrorDetails] nvarchar(max) NULL;
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[AttendanceUploadLogs]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [AttendanceUploadLogs];
                END
            ");
        }
    }
}


