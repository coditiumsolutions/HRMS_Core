/*
---
status: outdated
last_verified: 2026-11-26
outdated_since: 2026-11-24
reason: "One-off fix for the old Payslips columns. Do not run it against a database with the redesigned schema (production since 2026-11-24): it creates or alters old-shape tables and columns."
superseded_by: "scripts/database/redesign/ (run in order) and db.md"
---
*/
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
