-- SQL Script to create AttendanceUploadLogs table
-- Run this script on your Payroll2 database if you want to enable upload logging

USE [Payroll2];
GO

IF OBJECT_ID(N'[dbo].[AttendanceUploadLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AttendanceUploadLogs] (
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
    
    PRINT 'Table AttendanceUploadLogs created successfully.';
END
ELSE
BEGIN
    PRINT 'Table AttendanceUploadLogs already exists.';
END
GO


