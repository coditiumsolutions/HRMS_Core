/* Align dbo.LeaveQuota with HRMBT.Web (UID, LeaveTypeName, TotalLeaves, Year nvarchar). Idempotent. */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[dbo].[LeaveQuota]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LeaveQuota] (
        [UID] int IDENTITY(1,1) NOT NULL,
        [LeaveTypeName] varchar(50) NOT NULL,
        [TotalLeaves] int NOT NULL,
        [Year] varchar(50) NOT NULL,
        CONSTRAINT [PK_LeaveQuota] PRIMARY KEY ([UID])
    );
END;
GO

/* Id -> UID */
IF OBJECT_ID(N'[dbo].[LeaveQuota]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.LeaveQuota', N'UID') IS NULL
   AND COL_LENGTH(N'dbo.LeaveQuota', N'Id') IS NOT NULL
BEGIN
    EXEC sp_rename N'dbo.LeaveQuota.Id', N'UID', N'COLUMN';
END;
GO

/* QuotaDays -> TotalLeaves */
IF OBJECT_ID(N'[dbo].[LeaveQuota]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.LeaveQuota', N'TotalLeaves') IS NULL
   AND COL_LENGTH(N'dbo.LeaveQuota', N'QuotaDays') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[LeaveQuota] ADD [TotalLeaves] int NULL;
END;
GO

IF OBJECT_ID(N'[dbo].[LeaveQuota]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.LeaveQuota', N'TotalLeaves') IS NOT NULL
   AND COL_LENGTH(N'dbo.LeaveQuota', N'QuotaDays') IS NOT NULL
BEGIN
    UPDATE [dbo].[LeaveQuota]
    SET [TotalLeaves] = CAST(ROUND([QuotaDays], 0) AS int)
    WHERE [TotalLeaves] IS NULL;

    ALTER TABLE [dbo].[LeaveQuota] ALTER COLUMN [TotalLeaves] int NOT NULL;
    ALTER TABLE [dbo].[LeaveQuota] DROP COLUMN [QuotaDays];
END;
GO

/* Year int -> varchar(50) */
IF OBJECT_ID(N'[dbo].[LeaveQuota]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.LeaveQuota', N'Year') IS NOT NULL
   AND EXISTS (
        SELECT 1 FROM sys.columns c
        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.LeaveQuota')
          AND c.name = N'Year'
          AND t.name IN (N'int', N'smallint', N'tinyint')
   )
BEGIN
    ALTER TABLE [dbo].[LeaveQuota] ADD [YearText] varchar(50) NULL;
END;
GO

IF OBJECT_ID(N'[dbo].[LeaveQuota]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.LeaveQuota', N'YearText') IS NOT NULL
   AND COL_LENGTH(N'dbo.LeaveQuota', N'Year') IS NOT NULL
BEGIN
    UPDATE [dbo].[LeaveQuota] SET [YearText] = CAST([Year] AS varchar(50));
    ALTER TABLE [dbo].[LeaveQuota] DROP COLUMN [Year];
    EXEC sp_rename N'dbo.LeaveQuota.YearText', N'Year', N'COLUMN';
    ALTER TABLE [dbo].[LeaveQuota] ALTER COLUMN [Year] varchar(50) NOT NULL;
END;
GO

/* Optional Description column from database-schema.sql — safe to drop for app parity */
IF OBJECT_ID(N'[dbo].[LeaveQuota]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.LeaveQuota', N'Description') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[LeaveQuota] DROP COLUMN [Description];
END;
GO
