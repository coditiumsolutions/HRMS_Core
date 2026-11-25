/*
  HRMS database redesign - phase 12: income tax slabs on the annual progressive method.
  Before: dbo.TaxRules held MONTHLY salary bands with one flat percentage applied to the whole basic salary.
  Now: a slab is an ANNUAL income band. Annual tax = FixedAmount + TaxPercentage % of (annual income - MinSalary) for the slab with the
  highest MinSalary not above the income; monthly tax = annual tax / 12 rounded to the rupee (see docs/reference/pk-income-tax.md).
  1. dbo.TaxRules.FixedAmount decimal(18,2) NOT NULL DEFAULT 0 (+ CK_TaxRules_Fixed >= 0). MinSalary/MaxSalary keep their names but now hold annual income.
  2. Seeds the Pakistan salaried slabs for tax year 2026-27 (TaxYearStart 2026) and 2025-26 (2025), replacing the old test slabs of those two years only
     (they were monthly bands with a flat rate, so they cannot be read under the new method). A year already on the new method (it has a slab
     starting at 600,000 and ending at 1,200,000) is left alone, so the script is re-runnable and HR edits are kept.
  3. Other tax years are not touched; a warning lists them, because their old monthly bands are wrong under the annual method and need replacing by HR.
  2025-26 fixed amounts are derived from the published rates by continuity (documented in docs/reference/pk-income-tax.md): verify against the Finance Act.
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

IF COL_LENGTH('dbo.TaxRules', 'FixedAmount') IS NULL
  ALTER TABLE dbo.TaxRules ADD FixedAmount decimal(18,2) NOT NULL CONSTRAINT DF_TaxRules_FixedAmount DEFAULT (0);
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TaxRules_Fixed' AND parent_object_id = OBJECT_ID('dbo.TaxRules'))
  ALTER TABLE dbo.TaxRules ADD CONSTRAINT CK_TaxRules_Fixed CHECK (FixedAmount >= 0);
GO

DECLARE @slabs TABLE (TaxYearStart smallint, MinSalary decimal(18,2), MaxSalary decimal(18,2) NULL, FixedAmount decimal(18,2), TaxPercentage decimal(5,2));
INSERT INTO @slabs VALUES
  -- tax year 2026-27 (Finance Act 2026, as published on mytaxcalculator.pk)
  (2026,       0,  600000,        0,  0),
  (2026,  600000, 1200000,        0,  1),
  (2026, 1200000, 2200000,     6000, 11),
  (2026, 2200000, 3200000,   116000, 20),
  (2026, 3200000, 4100000,   316000, 25),
  (2026, 4100000, 5600000,   541000, 29),
  (2026, 5600000, 7000000,   976000, 32),
  (2026, 7000000,    NULL,  1424000, 35),
  -- tax year 2025-26 (rates published, fixed amounts derived by continuity)
  (2025,       0,  600000,        0,  0),
  (2025,  600000, 1200000,        0,  1),
  (2025, 1200000, 2200000,     6000, 11),
  (2025, 2200000, 3200000,   116000, 23),
  (2025, 3200000, 4100000,   346000, 30),
  (2025, 4100000,    NULL,   616000, 35);

DECLARE @y smallint;
DECLARE yrs CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT TaxYearStart FROM @slabs;
OPEN yrs;
FETCH NEXT FROM yrs INTO @y;
WHILE @@FETCH_STATUS = 0
BEGIN
  IF NOT EXISTS (SELECT 1 FROM dbo.TaxRules WHERE TaxYearStart = @y AND MinSalary = 600000 AND MaxSalary = 1200000)
  BEGIN
    DELETE FROM dbo.TaxRules WHERE TaxYearStart = @y;
    INSERT INTO dbo.TaxRules (TaxYearStart, MinSalary, MaxSalary, FixedAmount, TaxPercentage, CreatedBy)
    SELECT TaxYearStart, MinSalary, MaxSalary, FixedAmount, TaxPercentage, N'script 12' FROM @slabs WHERE TaxYearStart = @y;
    PRINT 'Tax year starting ' + CAST(@y AS varchar(4)) + ': slabs replaced by the annual method.';
  END
  ELSE PRINT 'Tax year starting ' + CAST(@y AS varchar(4)) + ': already on the annual method, left alone.';
  FETCH NEXT FROM yrs INTO @y;
END
CLOSE yrs; DEALLOCATE yrs;

IF EXISTS (SELECT 1 FROM dbo.TaxRules WHERE TaxYearStart NOT IN (2025, 2026))
  PRINT 'WARNING: tax years other than 2025 and 2026 still hold old monthly bands; replace them with annual slabs in Tax rules.';
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261125000012_Redesign_Phase12_TaxSlabsAnnual', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261125000012_Redesign_Phase12_TaxSlabsAnnual');
GO
COMMIT TRANSACTION;
GO
