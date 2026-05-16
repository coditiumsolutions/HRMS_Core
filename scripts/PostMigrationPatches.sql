/* Post-migration patches aligned with live HRMS (db.txt). Idempotent. Run after database-schema.sql */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* Payslips.[Month]: int -> nvarchar(20) with month names */
IF OBJECT_ID(N'[dbo].[Payslips]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Payslips', N'Month') IS NOT NULL
       AND EXISTS (
            SELECT 1 FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID(N'dbo.Payslips')
              AND c.name = N'Month'
              AND t.name IN (N'int', N'smallint', N'tinyint')
       )
    BEGIN
        ALTER TABLE [dbo].[Payslips] ADD [MonthName] nvarchar(20) NULL;
    END
END;
GO

IF OBJECT_ID(N'[dbo].[Payslips]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Payslips', N'MonthName') IS NOT NULL
   AND COL_LENGTH(N'dbo.Payslips', N'Month') IS NOT NULL
   AND EXISTS (
        SELECT 1 FROM sys.columns c
        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Payslips')
          AND c.name = N'Month'
          AND t.name IN (N'int', N'smallint', N'tinyint')
   )
BEGIN
    UPDATE [dbo].[Payslips] SET [MonthName] = CASE CAST([Month] AS int)
        WHEN 1 THEN N'January' WHEN 2 THEN N'February' WHEN 3 THEN N'March'
        WHEN 4 THEN N'April' WHEN 5 THEN N'May' WHEN 6 THEN N'June'
        WHEN 7 THEN N'July' WHEN 8 THEN N'August' WHEN 9 THEN N'September'
        WHEN 10 THEN N'October' WHEN 11 THEN N'November' WHEN 12 THEN N'December'
        ELSE N'January' END;
    ALTER TABLE [dbo].[Payslips] DROP COLUMN [Month];
    EXEC sp_rename N'dbo.Payslips.MonthName', N'Month', N'COLUMN';
    ALTER TABLE [dbo].[Payslips] ALTER COLUMN [Month] nvarchar(20) NOT NULL;
END;
GO

IF OBJECT_ID(N'[dbo].[Payslips]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Payslips', N'Month') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID(N'dbo.Payslips')
              AND c.name = N'Month'
              AND t.name IN (N'int', N'smallint', N'tinyint')
       )
    BEGIN
        ALTER TABLE [dbo].[Payslips] ALTER COLUMN [Month] nvarchar(20) NOT NULL;
    END
    ELSE IF COL_LENGTH(N'dbo.Payslips', N'Month') IS NOT NULL
        ALTER TABLE [dbo].[Payslips] ALTER COLUMN [Month] nvarchar(20) NOT NULL;
END;
GO

/* Employee document attachments */
IF OBJECT_ID(N'[dbo].[EmployeeDocuments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[EmployeeDocuments] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [EmployeeId] int NOT NULL,
        [FileName] nvarchar(300) NOT NULL,
        [OriginalFileName] nvarchar(300) NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [FileExtension] nvarchar(50) NULL,
        [FileSize] bigint NULL,
        [UploadedOn] datetime NULL CONSTRAINT [DF_EmployeeDocuments_UploadedOn] DEFAULT (GETDATE()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_EmployeeDocuments_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_EmployeeDocuments] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_EmployeeDocuments_EmployeeId] ON [dbo].[EmployeeDocuments] ([EmployeeId]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515140000_CreateEmployeeDocumentsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515140000_CreateEmployeeDocumentsTable', N'9.0.0');
END;
GO
