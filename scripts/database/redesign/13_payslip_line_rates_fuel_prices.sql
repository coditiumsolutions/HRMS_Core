/*
  HRMS database redesign - phase 13: unit rates on payslip lines and fuel price history (decision T9).
  1. dbo.PayslipDetails: Quantity decimal(9,2) NULL, UnitAmount decimal(18,2) NULL, Note nvarchar(200) NULL, so a line can show
     "100 x 280.00" or "5% of gross". Existing lines keep NULL: how they were worked out was not stored.
  2. dbo.FuelPrices: one price per effective-from date. Payroll uses the price in force on the 1st of the payslip month.
     Seeded once from the dbo.Configuration 'FuelPrice' value, effective 2000-01-01 (the earliest year payroll accepts), so every month
     resolves to the same price as before. The Configuration row is left in place (older builds still read it); this build does not.
  Additive only: the previous build keeps working after this script. Run it BEFORE deploying the build that needs it.
  Run with sqlcmd -b -I (see 01_lookups_and_keys.sql). Back up first (S19). Needs 04 and 06.
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_WARNINGS ON;
SET ANSI_PADDING ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

IF COL_LENGTH('dbo.PayslipDetails', 'Quantity') IS NULL ALTER TABLE dbo.PayslipDetails ADD Quantity decimal(9,2) NULL;
IF COL_LENGTH('dbo.PayslipDetails', 'UnitAmount') IS NULL ALTER TABLE dbo.PayslipDetails ADD UnitAmount decimal(18,2) NULL;
IF COL_LENGTH('dbo.PayslipDetails', 'Note') IS NULL ALTER TABLE dbo.PayslipDetails ADD Note nvarchar(200) NULL;
GO

IF OBJECT_ID('dbo.FuelPrices', 'U') IS NULL
CREATE TABLE dbo.FuelPrices (
  Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuelPrices PRIMARY KEY,
  EffectiveFrom date          NOT NULL,
  UnitPrice     decimal(18,2) NOT NULL,
  CreatedAt     datetime2(0)  NOT NULL CONSTRAINT DF_FuelPrices_CreatedAt DEFAULT sysdatetime(),
  CreatedBy     nvarchar(100) NULL,
  CONSTRAINT CK_FuelPrices_UnitPrice CHECK (UnitPrice > 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_FuelPrices_EffectiveFrom' AND object_id = OBJECT_ID('dbo.FuelPrices'))
  CREATE UNIQUE INDEX UX_FuelPrices_EffectiveFrom ON dbo.FuelPrices (EffectiveFrom);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.FuelPrices)
BEGIN
  DECLARE @raw nvarchar(200) =
    (SELECT TOP (1) LTRIM(RTRIM(ConfigValue)) FROM dbo.Configuration WHERE LTRIM(RTRIM(ConfigKey)) = 'FuelPrice' ORDER BY Id);
  -- the app read the first comma separated value
  IF CHARINDEX(',', @raw) > 0 SET @raw = RTRIM(LEFT(@raw, CHARINDEX(',', @raw) - 1));
  DECLARE @price decimal(18,2) = TRY_CONVERT(decimal(18,2), @raw);
  IF @price > 0
  BEGIN
    INSERT INTO dbo.FuelPrices (EffectiveFrom, UnitPrice, CreatedBy) VALUES ('20000101', @price, N'script 13');
    PRINT 'Fuel price copied from Configuration: ' + CONVERT(varchar(30), @price) + ' effective 2000-01-01.';
  END
  ELSE PRINT 'WARNING: no usable FuelPrice in Configuration; add one under Setup -> Fuel prices before running payroll.';
END
ELSE PRINT 'dbo.FuelPrices already has rows, left alone.';
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261126000013_Redesign_Phase13_LineRatesFuelPrices', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261126000013_Redesign_Phase13_LineRatesFuelPrices');
GO
COMMIT TRANSACTION;
GO
