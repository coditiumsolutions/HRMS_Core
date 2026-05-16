IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

/* HRMS: idempotent EF migration chain + catalog DDL aligned with repo db.txt (2026-05-12). */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231061446_CreatePayrollModule'
)
BEGIN
    CREATE TABLE [Payrolls] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [BasicSalary] decimal(18,2) NOT NULL,
        [Month] int NOT NULL,
        [Year] int NOT NULL,
        CONSTRAINT [PK_Payrolls] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231061446_CreatePayrollModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251231061446_CreatePayrollModule', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231070610_CreateEmployeeTable'
)
BEGIN
    CREATE TABLE [Employees] (
        [uid] int NOT NULL IDENTITY,
        [EmployeeID] nvarchar(50) NULL,
        [EmployeeName] nvarchar(100) NOT NULL,
        [CNIC] nvarchar(15) NULL,
        [Department] nvarchar(100) NULL,
        [Designation] nvarchar(100) NULL,
        [DateOfJoining] datetime2 NULL,
        [BasicSalary] decimal(18,2) NOT NULL,
        [ApplyTax] bit NOT NULL,
        [Status] nvarchar(50) NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([uid])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231070610_CreateEmployeeTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251231070610_CreateEmployeeTable', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231072842_CreateAttendance'
)
BEGIN
    CREATE TABLE [Attendances] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [Date] datetime2 NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Department] nvarchar(100) NULL,
        CONSTRAINT [PK_Attendances] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231072842_CreateAttendance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251231072842_CreateAttendance', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231074336_CreateLMS'
)
BEGIN
    CREATE TABLE [LeaveRequests] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [LeaveType] nvarchar(50) NOT NULL,
        [FromDate] datetime2 NOT NULL,
        [ToDate] datetime2 NOT NULL,
        [Status] nvarchar(50) NULL,
        CONSTRAINT [PK_LeaveRequests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231074336_CreateLMS'
)
BEGIN
    CREATE TABLE [TaxRules] (
        [Id] int NOT NULL IDENTITY,
        [MinSalary] decimal(18,2) NOT NULL,
        [MaxSalary] decimal(18,2) NULL,
        [TaxPercentage] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_TaxRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231074336_CreateLMS'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251231074336_CreateLMS', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231074354_CreateTax'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251231074354_CreateTax', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [AdjustedAjusted] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [CarryForwardLeaves] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [CarryForwardLeaves1] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [DOB] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [Details] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [FatherName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [MobileNo] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [ModifiedBy] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [ModifiedOn] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [Project] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [Year2022] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [Year2023] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [Year2023New] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    ALTER TABLE [Employees] ADD [Year2024] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260101104505_AddMissingFieldsToEmployee'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260101104505_AddMissingFieldsToEmployee', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[Payrolls]', N'U') IS NOT NULL
        DROP TABLE [Payrolls];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[Employee]', N'U') IS NULL AND OBJECT_ID(N'[dbo].[Employees]', N'U') IS NOT NULL
        EXEC sp_rename N'dbo.Employees', N'Employee';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    IF OBJECT_ID(N'Employee', N'U') IS NOT NULL
       AND SCHEMA_NAME(SCHEMA_ID(OBJECT_ID(N'Employee'))) <> N'dbo'
        ALTER SCHEMA [dbo] TRANSFER [Employee];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN

                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Employee') AND name = 'Status')
                    AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Employee') AND name = 'EmployeeStatus')
                    BEGIN
                        EXEC sp_rename 'dbo.Employee.Status', 'EmployeeStatus', 'COLUMN';
                    END
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Employee]') AND [c].[name] = N'Year2024');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Employee] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [dbo].[Employee] ALTER COLUMN [Year2024] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Employee]') AND [c].[name] = N'Year2023');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Employee] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [dbo].[Employee] ALTER COLUMN [Year2023] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Employee]') AND [c].[name] = N'Year2022');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Employee] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [dbo].[Employee] ALTER COLUMN [Year2022] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Employee]') AND [c].[name] = N'ModifiedOn');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Employee] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [dbo].[Employee] ALTER COLUMN [ModifiedOn] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Employee]') AND [c].[name] = N'CarryForwardLeaves1');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Employee] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [dbo].[Employee] ALTER COLUMN [CarryForwardLeaves1] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Employee]') AND [c].[name] = N'CarryForwardLeaves');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Employee] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [dbo].[Employee] ALTER COLUMN [CarryForwardLeaves] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Employee]') AND [c].[name] = N'BasicSalary');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Employee] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [dbo].[Employee] ALTER COLUMN [BasicSalary] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Employee]') AND [c].[name] = N'ApplyTax');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Employee] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [dbo].[Employee] ALTER COLUMN [ApplyTax] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN

                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Allowances]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [Allowances] (
                            [Id] int NOT NULL IDENTITY,
                            [EmployeeId] int NOT NULL,
                            [AllowanceType] nvarchar(max) NOT NULL,
                            [Name] nvarchar(max) NOT NULL,
                            [Amount] decimal(18,2) NOT NULL,
                            [IsPercentage] bit NOT NULL,
                            [PercentageValue] decimal(18,2) NULL,
                            [EffectiveDate] datetime2 NOT NULL,
                            [EndDate] datetime2 NULL,
                            [IsActive] bit NOT NULL,
                            [CreatedDate] datetime2 NOT NULL,
                            [CreatedBy] nvarchar(max) NOT NULL,
                            [ModifiedDate] datetime2 NULL,
                            [ModifiedBy] nvarchar(max) NOT NULL,
                            CONSTRAINT [PK_Allowances] PRIMARY KEY ([Id])
                        );
                    END
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN

                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Deductions]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [Deductions] (
                            [Id] int NOT NULL IDENTITY,
                            [EmployeeId] int NOT NULL,
                            [DeductionType] nvarchar(max) NOT NULL,
                            [Name] nvarchar(max) NOT NULL,
                            [Amount] decimal(18,2) NULL,
                            [CalculationMethod] nvarchar(max) NOT NULL,
                            [PercentageValue] decimal(18,2) NULL,
                            [IsMandatory] bit NOT NULL,
                            [EffectiveDate] datetime2 NOT NULL,
                            [EndDate] datetime2 NULL,
                            [IsActive] bit NOT NULL,
                            [CreatedDate] datetime2 NOT NULL,
                            [CreatedBy] nvarchar(max) NOT NULL,
                            [ModifiedDate] datetime2 NULL,
                            [ModifiedBy] nvarchar(max) NOT NULL,
                            CONSTRAINT [PK_Deductions] PRIMARY KEY ([Id])
                        );
                    END
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN

                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Payslips]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [Payslips] (
                            [Id] int NOT NULL IDENTITY,
                            [EmployeeId] int NOT NULL,
                            [Month] int NOT NULL,
                            [Year] int NOT NULL,
                            [BasicSalary] decimal(18,2) NOT NULL,
                            [GrossSalary] decimal(18,2) NOT NULL,
                            [TotalDeductions] decimal(18,2) NOT NULL,
                            [NetSalary] decimal(18,2) NOT NULL,
                            [WorkingDays] int NULL,
                            [LeaveDays] decimal(18,2) NULL,
                            [LeaveBalance] nvarchar(max) NOT NULL,
                            [CalculationDetails] nvarchar(max) NOT NULL,
                            [GeneratedDate] datetime2 NOT NULL,
                            [GeneratedBy] nvarchar(max) NOT NULL,
                            [IsLocked] bit NOT NULL,
                            [LockedDate] datetime2 NULL,
                            [Notes] nvarchar(max) NOT NULL,
                            CONSTRAINT [PK_Payslips] PRIMARY KEY ([Id])
                        );
                    END
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN

                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[PayslipDetails]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [PayslipDetails] (
                            [Id] int NOT NULL IDENTITY,
                            [PayslipId] int NOT NULL,
                            [ItemType] nvarchar(max) NOT NULL,
                            [ItemName] nvarchar(max) NOT NULL,
                            [ItemCategory] nvarchar(max) NOT NULL,
                            [Amount] decimal(18,2) NOT NULL,
                            [SortOrder] int NULL,
                            CONSTRAINT [PK_PayslipDetails] PRIMARY KEY ([Id]),
                            CONSTRAINT [FK_PayslipDetails_Payslips_PayslipId] FOREIGN KEY ([PayslipId]) REFERENCES [Payslips] ([Id]) ON DELETE CASCADE
                        );
                        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PayslipDetails_PayslipId' AND object_id = OBJECT_ID('PayslipDetails'))
                        BEGIN
                            CREATE INDEX [IX_PayslipDetails_PayslipId] ON [PayslipDetails] ([PayslipId]);
                        END
                    END
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102062803_PayrollModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260102062803_PayrollModule', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102131023_PayrollModuleUpdate'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Deductions]') AND [c].[name] = N'ModifiedBy');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Deductions] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [Deductions] ALTER COLUMN [ModifiedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102131023_PayrollModuleUpdate'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Allowances]') AND [c].[name] = N'ModifiedBy');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Allowances] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [Allowances] ALTER COLUMN [ModifiedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102131023_PayrollModuleUpdate'
)
BEGIN
    CREATE INDEX [IX_Payslips_EmployeeId] ON [Payslips] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102131023_PayrollModuleUpdate'
)
BEGIN
    CREATE INDEX [IX_Deductions_EmployeeId] ON [Deductions] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102131023_PayrollModuleUpdate'
)
BEGIN
    CREATE INDEX [IX_Allowances_EmployeeId] ON [Allowances] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102131023_PayrollModuleUpdate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260102131023_PayrollModuleUpdate', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105063701_FixAttendanceTable'
)
BEGIN

                    IF OBJECT_ID(N'[Attendances]', N'U') IS NOT NULL
                    BEGIN
                        -- Table exists, modify it
                        IF COL_LENGTH('Attendances', 'Department') IS NOT NULL
                        BEGIN
                            DECLARE @var10 sysname;
                            SELECT @var10 = [d].[name]
                            FROM [sys].[default_constraints] [d]
                            INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                            WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Attendances]') AND [c].[name] = N'Department');
                            IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Attendances] DROP CONSTRAINT [' + @var10 + '];');
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
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105063701_FixAttendanceTable'
)
BEGIN

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
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105063701_FixAttendanceTable'
)
BEGIN

                    IF OBJECT_ID(N'[Attendances]', N'U') IS NOT NULL
                    AND OBJECT_ID(N'FK_Attendances_Employee_EmployeeId', N'F') IS NULL
                    BEGIN
                        ALTER TABLE [Attendances] 
                        ADD CONSTRAINT [FK_Attendances_Employee_EmployeeId] 
                        FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employee] ([uid]) ON DELETE SET NULL;
                    END
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105063701_FixAttendanceTable'
)
BEGIN

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
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105063701_FixAttendanceTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260105063701_FixAttendanceTable', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105064812_RenameAttendancesToAttendance'
)
BEGIN
    ALTER TABLE [Attendances] DROP CONSTRAINT [FK_Attendances_Employee_EmployeeId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105064812_RenameAttendancesToAttendance'
)
BEGIN
    ALTER TABLE [Attendances] DROP CONSTRAINT [PK_Attendances];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105064812_RenameAttendancesToAttendance'
)
BEGIN
    EXEC sp_rename N'[Attendances]', N'Attendance', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105064812_RenameAttendancesToAttendance'
)
BEGIN
    EXEC sp_rename N'[Attendance].[IX_Attendances_EmployeeId]', N'IX_Attendance_EmployeeId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105064812_RenameAttendancesToAttendance'
)
BEGIN
    ALTER TABLE [Attendance] ADD CONSTRAINT [PK_Attendance] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105064812_RenameAttendancesToAttendance'
)
BEGIN
    ALTER TABLE [Attendance] ADD CONSTRAINT [FK_Attendance_Employee_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employee] ([uid]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105064812_RenameAttendancesToAttendance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260105064812_RenameAttendancesToAttendance', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105074407_UpdateAttendanceTableSchema'
)
BEGIN

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
                        -- Add/rename AttendanceID: legacy rename-from-Attendances uses identity column [Id]; do not add a second IDENTITY
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'AttendanceID')
                        BEGIN
                            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendance') AND name = 'Id' AND is_identity = 1)
                            BEGIN
                                DECLARE @pkAttendance74407 sysname;
                                SELECT @pkAttendance74407 = [kc].[name]
                                FROM [sys].[key_constraints] [kc]
                                WHERE [kc].[parent_object_id] = OBJECT_ID(N'Attendance') AND [kc].[type] = 'PK';
                                IF @pkAttendance74407 IS NOT NULL
                                    EXEC(N'ALTER TABLE [Attendance] DROP CONSTRAINT [' + @pkAttendance74407 + '];');
                                EXEC sp_rename N'Attendance.Id', N'AttendanceID', N'COLUMN';
                                IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'Attendance') AND type = 'PK')
                                    ALTER TABLE [Attendance] ADD CONSTRAINT [PK_Attendance] PRIMARY KEY ([AttendanceID]);
                            END
                            ELSE
                            BEGIN
                                ALTER TABLE [Attendance] ADD [AttendanceID] int IDENTITY(1,1) NOT NULL;
                                IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'Attendance') AND type = 'PK')
                                    ALTER TABLE [Attendance] ADD CONSTRAINT [PK_Attendance] PRIMARY KEY ([AttendanceID]);
                            END
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
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105074407_UpdateAttendanceTableSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260105074407_UpdateAttendanceTableSchema', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105120000_CreateAttendanceUploadLogsTable'
)
BEGIN
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
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'Id')
        BEGIN
            ALTER TABLE [AttendanceUploadLogs] ADD [Id] int IDENTITY(1,1) NOT NULL;
            ALTER TABLE [AttendanceUploadLogs] ADD CONSTRAINT [PK_AttendanceUploadLogs] PRIMARY KEY ([Id]);
        END
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'UploadDate')
            ALTER TABLE [AttendanceUploadLogs] ADD [UploadDate] datetime2 NOT NULL CONSTRAINT [DF_AttendanceUploadLogs_UploadDate] DEFAULT GETDATE();
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'UploadedBy')
            ALTER TABLE [AttendanceUploadLogs] ADD [UploadedBy] nvarchar(100) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'FileName')
            ALTER TABLE [AttendanceUploadLogs] ADD [FileName] nvarchar(255) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'TotalRows')
            ALTER TABLE [AttendanceUploadLogs] ADD [TotalRows] int NOT NULL CONSTRAINT [DF_AttendanceUploadLogs_TotalRows] DEFAULT 0;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'SuccessCount')
            ALTER TABLE [AttendanceUploadLogs] ADD [SuccessCount] int NOT NULL CONSTRAINT [DF_AttendanceUploadLogs_SuccessCount] DEFAULT 0;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'FailureCount')
            ALTER TABLE [AttendanceUploadLogs] ADD [FailureCount] int NOT NULL CONSTRAINT [DF_AttendanceUploadLogs_FailureCount] DEFAULT 0;
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceUploadLogs') AND name = 'ErrorDetails')
            ALTER TABLE [AttendanceUploadLogs] ADD [ErrorDetails] nvarchar(max) NULL;
    END
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260105120000_CreateAttendanceUploadLogsTable', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512100000_PayslipTotalAllowancesColumn'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[Payslips]', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.Payslips', N'TotalAllowances') IS NULL
        ALTER TABLE [dbo].[Payslips] ADD [TotalAllowances] decimal(18,2) NULL;
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512100000_PayslipTotalAllowancesColumn', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516120000_PayslipTaxColumns'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[Payslips]', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Payslips', N'TaxPercentage') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Payslips] ADD [TaxPercentage] decimal(18,2) NOT NULL CONSTRAINT [DF_Payslips_TaxPct20260516] DEFAULT (0);
        ALTER TABLE [dbo].[Payslips] DROP CONSTRAINT [DF_Payslips_TaxPct20260516];
    END;
    IF OBJECT_ID(N'[dbo].[Payslips]', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Payslips', N'TaxAmount') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Payslips] ADD [TaxAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Payslips_TaxAmt20260516] DEFAULT (0);
        ALTER TABLE [dbo].[Payslips] DROP CONSTRAINT [DF_Payslips_TaxAmt20260516];
    END;
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516120000_PayslipTaxColumns', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515120000_AlignDeductionsSchemaDbTxt'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[Deductions]', N'U') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'dbo.Deductions', N'DeductionName') IS NULL AND COL_LENGTH(N'dbo.Deductions', N'Name') IS NOT NULL
            EXEC sp_rename N'dbo.Deductions.Name', N'DeductionName', N'COLUMN';
        IF COL_LENGTH(N'dbo.Deductions', N'DeductionName') IS NULL
        BEGIN
            ALTER TABLE [dbo].[Deductions] ADD [DeductionName] nvarchar(100) NOT NULL CONSTRAINT [DF_Deductions_DeductionName] DEFAULT (N'');
            ALTER TABLE [dbo].[Deductions] DROP CONSTRAINT [DF_Deductions_DeductionName];
        END
        IF COL_LENGTH(N'dbo.Deductions', N'Frequency') IS NULL
        BEGIN
            ALTER TABLE [dbo].[Deductions] ADD [Frequency] nvarchar(50) NOT NULL CONSTRAINT [DF_Deductions_Frequency] DEFAULT (N'Monthly');
            ALTER TABLE [dbo].[Deductions] DROP CONSTRAINT [DF_Deductions_Frequency];
        END
        IF COL_LENGTH(N'dbo.Deductions', N'Amount') IS NOT NULL
            ALTER TABLE [dbo].[Deductions] DROP COLUMN [Amount];
    END
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515120000_AlignDeductionsSchemaDbTxt', N'9.0.0');
END;

COMMIT;
GO

/* ----------------------------------------------------------------------------
   Objects used by HRMS / EF but not created by the migration chain above.
   Shapes match ApplicationDbContext + models (EmployeeLeaves per dbo.EmployeeLeaves;
   LeaveQuota, GazettedHoliday, CarryforwardLeaves per EF). Production may still
   expose legacy column names (see repo db.txt); align those databases manually.
   Each block is idempotent (skips if the table already exists).
   ---------------------------------------------------------------------------- */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[dbo].[Departments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Departments] (
        [DepartmentID] int NOT NULL IDENTITY(1,1),
        [DepartmentName] nvarchar(100) NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([DepartmentID])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[EmployeeLeaves]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[EmployeeLeaves] (
        [uid] int NOT NULL IDENTITY(1,1),
        [EmployeeID] varchar(50) NOT NULL,
        [LeaveTypeName] varchar(250) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [TotalDays] float NULL,
        [AddDays] int NULL,
        [ExcludeDays] int NULL,
        [Short_Adj] varchar(max) NULL,
        [DepSupervisorComments] varchar(max) NULL,
        [Year] varchar(50) NULL,
        [Status] nvarchar(50) NULL,
        [ApprovedBy] varchar(50) NULL,
        [ApprovedOn] date NULL,
        [AppliedDate] date NULL,
        CONSTRAINT [PK_EmployeeLeaves] PRIMARY KEY ([uid])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[LeaveQuota]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LeaveQuota] (
        [UID] int NOT NULL IDENTITY(1,1),
        [LeaveTypeName] varchar(50) NOT NULL,
        [TotalLeaves] int NOT NULL,
        [Year] varchar(50) NOT NULL,
        CONSTRAINT [PK_LeaveQuota] PRIMARY KEY ([UID])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[GazettedHoliday]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GazettedHoliday] (
        [Id] int NOT NULL IDENTITY(1,1),
        [HolidayDate] date NOT NULL,
        [HolidayName] nvarchar(100) NOT NULL,
        [Description] nvarchar(255) NULL,
        CONSTRAINT [PK_GazettedHoliday] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[CarryforwardLeaves]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CarryforwardLeaves] (
        [Id] int NOT NULL IDENTITY(1,1),
        [EmployeeID] nvarchar(50) NOT NULL,
        [LeaveTypeName] nvarchar(50) NOT NULL,
        [FromYear] int NOT NULL,
        [ToYear] int NOT NULL,
        [CarryForwardDays] decimal(18,2) NOT NULL,
        [Description] nvarchar(255) NULL,
        CONSTRAINT [PK_CarryforwardLeaves] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Configuration]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Configuration] (
        [UID] int NOT NULL IDENTITY(1,1),
        [ConfigKey] nvarchar(50) NULL,
        [ConfigValue] nvarchar(max) NULL,
        CONSTRAINT [PK_Configuration] PRIMARY KEY ([UID])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users] (
        [uid] int NOT NULL IDENTITY(1,1),
        [EmployeeId] varchar(50) NULL,
        [Username] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(255) NOT NULL,
        [Role] nvarchar(max) NULL,
        [CreatedBy] varchar(max) NULL,
        [CreatedOn] varchar(max) NULL,
        [History] varchar(max) NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([uid])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Employee]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Employee', N'GenStatus') IS NULL
    ALTER TABLE [dbo].[Employee] ADD [GenStatus] nvarchar(50) NULL;
GO

