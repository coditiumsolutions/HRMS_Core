/*
  HRMS database redesign - phase 14: work weeks (decided 2026-11-26).
  The company work week is Monday to Saturday. An employee with a different week gets a dbo.ShiftAssignments row (weekday mask,
  effective dates); single days that differ (rosters) are dbo.RosterOverrides rows. Shift TIMES are not defined yet, so:
  1. dbo.ShiftAssignments.ShiftId becomes NULL-able (NULL = working days are known, shift times are not).
  2. CK_RosterOverrides_Shift: an 'ExtraDuty' day may have no shift (a day worked that is normally off); 'Off' still has none,
     'ShiftChange' and 'Swap' still need one.
  Only relaxes constraints: earlier builds keep working. Run BEFORE deploying the build that needs it.
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

IF COLUMNPROPERTY(OBJECT_ID('dbo.ShiftAssignments'), 'ShiftId', 'AllowsNull') = 0
BEGIN
  IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ShiftAssignments_Shifts')
    ALTER TABLE dbo.ShiftAssignments DROP CONSTRAINT FK_ShiftAssignments_Shifts;
  IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ShiftAssignments_ShiftId' AND object_id = OBJECT_ID('dbo.ShiftAssignments'))
    DROP INDEX IX_ShiftAssignments_ShiftId ON dbo.ShiftAssignments;
  ALTER TABLE dbo.ShiftAssignments ALTER COLUMN ShiftId int NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ShiftAssignments_ShiftId' AND object_id = OBJECT_ID('dbo.ShiftAssignments'))
  CREATE INDEX IX_ShiftAssignments_ShiftId ON dbo.ShiftAssignments (ShiftId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ShiftAssignments_Shifts')
  ALTER TABLE dbo.ShiftAssignments WITH CHECK ADD CONSTRAINT FK_ShiftAssignments_Shifts FOREIGN KEY (ShiftId) REFERENCES dbo.Shifts (Id);
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_RosterOverrides_Shift' AND parent_object_id = OBJECT_ID('dbo.RosterOverrides')
           AND definition NOT LIKE '%ShiftChange%')
  ALTER TABLE dbo.RosterOverrides DROP CONSTRAINT CK_RosterOverrides_Shift;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_RosterOverrides_Shift' AND parent_object_id = OBJECT_ID('dbo.RosterOverrides'))
  ALTER TABLE dbo.RosterOverrides WITH CHECK ADD CONSTRAINT CK_RosterOverrides_Shift CHECK (
    (OverrideType = 'Off' AND ShiftId IS NULL)
    OR OverrideType = 'ExtraDuty'
    OR (OverrideType IN ('ShiftChange', 'Swap') AND ShiftId IS NOT NULL));
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261126000014_Redesign_Phase14_WorkWeeks', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261126000014_Redesign_Phase14_WorkWeeks');
GO
COMMIT TRANSACTION;
GO
