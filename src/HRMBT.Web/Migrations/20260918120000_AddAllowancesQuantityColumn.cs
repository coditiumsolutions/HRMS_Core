using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMBT.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowancesQuantityColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[Allowances]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Allowances', N'Quantity') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Allowances] ADD [Quantity] int NOT NULL
                        CONSTRAINT [DF_Allowances_Quantity] DEFAULT (1);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[Allowances]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Allowances', N'Quantity') IS NOT NULL
                BEGIN
                    DECLARE @df sysname;
                    SELECT @df = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Allowances')
                      AND c.name = N'Quantity';
                    IF @df IS NOT NULL
                        EXEC(N'ALTER TABLE [dbo].[Allowances] DROP CONSTRAINT [' + @df + N']');
                    ALTER TABLE [dbo].[Allowances] DROP COLUMN [Quantity];
                END
            ");
        }
    }
}
