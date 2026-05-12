using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMBT.Web.Migrations
{
    /// <summary>Align dbo.Deductions with db.txt (DeductionName, Frequency; drop legacy Amount/Name if present).</summary>
    public partial class AlignDeductionsSchemaDbTxt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[Deductions]', N'U') IS NULL RETURN;

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
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[Deductions]', N'U') IS NULL RETURN;

                IF COL_LENGTH(N'dbo.Deductions', N'Amount') IS NULL
                    ALTER TABLE [dbo].[Deductions] ADD [Amount] decimal(18,2) NULL;

                IF COL_LENGTH(N'dbo.Deductions', N'Frequency') IS NOT NULL
                    ALTER TABLE [dbo].[Deductions] DROP COLUMN [Frequency];

                IF COL_LENGTH(N'dbo.Deductions', N'Name') IS NULL AND COL_LENGTH(N'dbo.Deductions', N'DeductionName') IS NOT NULL
                    EXEC sp_rename N'dbo.Deductions.DeductionName', N'Name', N'COLUMN';
            ");
        }
    }
}
