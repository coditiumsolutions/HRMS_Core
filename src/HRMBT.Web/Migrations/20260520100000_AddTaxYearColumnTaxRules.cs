using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMBT.Web.Migrations
{
    /// <summary>Add dbo.TaxRules.TaxYear (aligned with db.txt); idempotent when column already exists.</summary>
    public partial class AddTaxYearColumnTaxRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[TaxRules]', N'U') IS NULL RETURN;

                IF COL_LENGTH(N'dbo.TaxRules', N'TaxYear') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[TaxRules] ADD [TaxYear] nvarchar(20) NULL;
                    UPDATE [dbo].[TaxRules]
                    SET [TaxYear] = CAST(YEAR(GETDATE()) AS nvarchar(4))
                    WHERE [TaxYear] IS NULL OR LTRIM(RTRIM([TaxYear])) = N'';
                    ALTER TABLE [dbo].[TaxRules] ALTER COLUMN [TaxYear] nvarchar(20) NOT NULL;
                END
                ELSE
                BEGIN
                    UPDATE [dbo].[TaxRules]
                    SET [TaxYear] = CAST(YEAR(GETDATE()) AS nvarchar(4))
                    WHERE [TaxYear] IS NULL OR LTRIM(RTRIM([TaxYear])) = N'';

                    IF EXISTS (
                        SELECT 1
                        FROM sys.columns c
                        WHERE c.object_id = OBJECT_ID(N'dbo.TaxRules')
                          AND c.name = N'TaxYear'
                          AND c.is_nullable = 1)
                    BEGIN
                        ALTER TABLE [dbo].[TaxRules] ALTER COLUMN [TaxYear] nvarchar(20) NOT NULL;
                    END
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'dbo.TaxRules', N'TaxYear') IS NOT NULL
                    ALTER TABLE [dbo].[TaxRules] DROP COLUMN [TaxYear];
            ");
        }
    }
}
