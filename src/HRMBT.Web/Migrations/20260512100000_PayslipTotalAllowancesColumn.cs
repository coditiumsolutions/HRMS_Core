using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMBT.Web.Migrations
{
    /// <inheritdoc />
    public partial class PayslipTotalAllowancesColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[Payslips]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Payslips', N'TotalAllowances') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Payslips] ADD [TotalAllowances] decimal(18,2) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[Payslips]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Payslips', N'TotalAllowances') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Payslips] DROP COLUMN [TotalAllowances];
                END
            ");
        }
    }
}
