/*
  HRMS database redesign - phase 15: holiday notices (2026-11-26).
  HR issues a notice for each approved holiday. One dbo.GazettedHolidays row is one notice and may cover several days (Eid):
    HolidayDate  = first day (unchanged), EndDate = last day (new, backfilled with HolidayDate),
    NoticeNumber / NoticeDate (new), Description widened to hold the notice text.
  Only adds and widens. Run BEFORE deploying the build that needs it: payroll is not affected, but leave entry,
  the month view and work weeks read EndDate.
  Run with sqlcmd -b -I (see 01_lookups_and_keys.sql). Back up first (S19). Needs 04.
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

IF COL_LENGTH('dbo.GazettedHolidays', 'EndDate') IS NULL      ALTER TABLE dbo.GazettedHolidays ADD EndDate date NULL;
IF COL_LENGTH('dbo.GazettedHolidays', 'NoticeNumber') IS NULL ALTER TABLE dbo.GazettedHolidays ADD NoticeNumber nvarchar(50) NULL;
IF COL_LENGTH('dbo.GazettedHolidays', 'NoticeDate') IS NULL   ALTER TABLE dbo.GazettedHolidays ADD NoticeDate date NULL;
GO
UPDATE dbo.GazettedHolidays SET EndDate = HolidayDate WHERE EndDate IS NULL;
IF COLUMNPROPERTY(OBJECT_ID('dbo.GazettedHolidays'), 'EndDate', 'AllowsNull') = 1
  ALTER TABLE dbo.GazettedHolidays ALTER COLUMN EndDate date NOT NULL;
IF COL_LENGTH('dbo.GazettedHolidays', 'Description') < 2000   -- bytes: below nvarchar(1000)
  ALTER TABLE dbo.GazettedHolidays ALTER COLUMN Description nvarchar(1000) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_GazettedHolidays_Dates' AND parent_object_id = OBJECT_ID('dbo.GazettedHolidays'))
  ALTER TABLE dbo.GazettedHolidays WITH CHECK ADD CONSTRAINT CK_GazettedHolidays_Dates CHECK (EndDate >= HolidayDate);
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261126000015_Redesign_Phase15_HolidayNotices', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261126000015_Redesign_Phase15_HolidayNotices');
GO
COMMIT TRANSACTION;
GO
