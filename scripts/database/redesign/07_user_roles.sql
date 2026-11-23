/*
  HRMS database redesign - phase 7: several roles per user.
  Replaces the single Users.Role column with dbo.UserRoles (UserId, Role), so one login can be HR and Admin at once.
  Needs 03 first (Users.Role is constrained to Admin/HR there). Independent of 06; run it in the same window as the others,
  together with the new app build (the old build reads Users.Role).
  Re-runnable. Run with sqlcmd -b -I (see 01_lookups_and_keys.sql). Back up first (S19).
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

IF OBJECT_ID('dbo.UserRoles', 'U') IS NULL
BEGIN
  CREATE TABLE dbo.UserRoles (
    UserId int          NOT NULL,
    [Role] nvarchar(30) NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, [Role]),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (uid) ON DELETE CASCADE,
    CONSTRAINT CK_UserRoles_Role CHECK ([Role] IN ('Admin', 'HR'))
  );
END
GO

/* Copy the single role into the new table, check nothing was lost, then drop the old column. */
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'Role')
BEGIN
  EXEC sp_executesql N'
    INSERT INTO dbo.UserRoles (UserId, [Role])
    SELECT u.uid, u.[Role] FROM dbo.Users u
    WHERE u.[Role] IS NOT NULL AND LTRIM(RTRIM(u.[Role])) <> ''''
      AND NOT EXISTS (SELECT 1 FROM dbo.UserRoles r WHERE r.UserId = u.uid AND r.[Role] = u.[Role]);';

  DECLARE @expected int, @actual int;
  EXEC sp_executesql N'SELECT @e = COUNT(*) FROM dbo.Users WHERE [Role] IS NOT NULL AND LTRIM(RTRIM([Role])) <> ''''', N'@e int OUTPUT', @e = @expected OUTPUT;
  SELECT @actual = COUNT(*) FROM dbo.UserRoles;
  IF @actual < @expected THROW 50070, 'Users.Role values were not all copied to UserRoles.', 1;

  IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Users_Role' AND parent_object_id = OBJECT_ID('dbo.Users'))
    ALTER TABLE dbo.Users DROP CONSTRAINT CK_Users_Role;
  ALTER TABLE dbo.Users DROP COLUMN [Role];
END
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261123000007_Redesign_Phase7_UserRoles', '8.0.0'
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261123000007_Redesign_Phase7_UserRoles');
GO
COMMIT TRANSACTION;
GO
