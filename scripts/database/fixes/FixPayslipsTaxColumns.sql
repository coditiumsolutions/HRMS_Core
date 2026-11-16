-- Run against your HRMS database (e.g. SSMS or sqlcmd) if Payroll/Payslips fails with invalid column TaxPercentage/TaxAmount.
-- Idempotent: safe to run more than once.

IF OBJECT_ID(N'dbo.Payslips', N'U') IS NULL
BEGIN
    RAISERROR('dbo.Payslips does not exist.', 16, 1);
    RETURN;
END;

IF COL_LENGTH(N'dbo.Payslips', N'TaxPercentage') IS NULL
BEGIN
    ALTER TABLE [dbo].[Payslips] ADD [TaxPercentage] decimal(18,2) NOT NULL CONSTRAINT [DF_Payslips_TaxPctFix] DEFAULT (0);
    ALTER TABLE [dbo].[Payslips] DROP CONSTRAINT [DF_Payslips_TaxPctFix];
END;

IF COL_LENGTH(N'dbo.Payslips', N'TaxAmount') IS NULL
BEGIN
    ALTER TABLE [dbo].[Payslips] ADD [TaxAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Payslips_TaxAmtFix] DEFAULT (0);
    ALTER TABLE [dbo].[Payslips] DROP CONSTRAINT [DF_Payslips_TaxAmtFix];
END;

IF COL_LENGTH(N'dbo.Payslips', N'TotalAllowances') IS NULL
    ALTER TABLE [dbo].[Payslips] ADD [TotalAllowances] decimal(18,2) NULL;

PRINT 'Payslips tax/total columns OK.';
