using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMBT.Web.Migrations
{
    /// <inheritdoc />
    public partial class CreateEmployeeDocumentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[EmployeeDocuments]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[EmployeeDocuments] (
                        [Id] int IDENTITY(1,1) NOT NULL,
                        [EmployeeId] int NOT NULL,
                        [FileName] nvarchar(300) NOT NULL,
                        [OriginalFileName] nvarchar(300) NULL,
                        [FilePath] nvarchar(500) NOT NULL,
                        [FileExtension] nvarchar(50) NULL,
                        [FileSize] bigint NULL,
                        [UploadedOn] datetime NULL CONSTRAINT [DF_EmployeeDocuments_UploadedOn] DEFAULT (GETDATE()),
                        [IsDeleted] bit NOT NULL CONSTRAINT [DF_EmployeeDocuments_IsDeleted] DEFAULT (0),
                        CONSTRAINT [PK_EmployeeDocuments] PRIMARY KEY ([Id])
                    );

                    CREATE INDEX [IX_EmployeeDocuments_EmployeeId]
                        ON [dbo].[EmployeeDocuments] ([EmployeeId]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[EmployeeDocuments]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[EmployeeDocuments];
            ");
        }
    }
}
