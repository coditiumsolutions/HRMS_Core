using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMBT.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowancesRemarksColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[Allowances]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Allowances', N'Remarks') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Allowances] ADD [Remarks] nvarchar(500) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[Allowances]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Allowances', N'Remarks') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Allowances] DROP COLUMN [Remarks];
                END
            ");
        }
    }
}
