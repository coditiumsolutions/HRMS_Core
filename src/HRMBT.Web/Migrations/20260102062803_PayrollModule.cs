using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMBT.Web.Migrations
{
    /// <inheritdoc />
    public partial class PayrollModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payrolls");

            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.RenameTable(
                name: "Employee",
                newName: "Employee",
                newSchema: "dbo");

            // Only rename if Status column exists and EmployeeStatus doesn't
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Employee') AND name = 'Status')
                AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Employee') AND name = 'EmployeeStatus')
                BEGIN
                    EXEC sp_rename 'dbo.Employee.Status', 'EmployeeStatus', 'COLUMN';
                END
            ");

            migrationBuilder.AlterColumn<int>(
                name: "Year2024",
                schema: "dbo",
                table: "Employee",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Year2023",
                schema: "dbo",
                table: "Employee",
                type: "float",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Year2022",
                schema: "dbo",
                table: "Employee",
                type: "float",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedOn",
                schema: "dbo",
                table: "Employee",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "CarryForwardLeaves1",
                schema: "dbo",
                table: "Employee",
                type: "float",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "CarryForwardLeaves",
                schema: "dbo",
                table: "Employee",
                type: "float",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BasicSalary",
                schema: "dbo",
                table: "Employee",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "ApplyTax",
                schema: "dbo",
                table: "Employee",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Allowances]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [Allowances] (
                        [Id] int NOT NULL IDENTITY,
                        [EmployeeId] int NOT NULL,
                        [AllowanceType] nvarchar(max) NOT NULL,
                        [Name] nvarchar(max) NOT NULL,
                        [Amount] decimal(18,2) NOT NULL,
                        [IsPercentage] bit NOT NULL,
                        [PercentageValue] decimal(18,2) NULL,
                        [EffectiveDate] datetime2 NOT NULL,
                        [EndDate] datetime2 NULL,
                        [IsActive] bit NOT NULL,
                        [CreatedDate] datetime2 NOT NULL,
                        [CreatedBy] nvarchar(max) NOT NULL,
                        [ModifiedDate] datetime2 NULL,
                        [ModifiedBy] nvarchar(max) NOT NULL,
                        CONSTRAINT [PK_Allowances] PRIMARY KEY ([Id])
                    );
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Deductions]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [Deductions] (
                        [Id] int NOT NULL IDENTITY,
                        [EmployeeId] int NOT NULL,
                        [DeductionType] nvarchar(max) NOT NULL,
                        [Name] nvarchar(max) NOT NULL,
                        [Amount] decimal(18,2) NULL,
                        [CalculationMethod] nvarchar(max) NOT NULL,
                        [PercentageValue] decimal(18,2) NULL,
                        [IsMandatory] bit NOT NULL,
                        [EffectiveDate] datetime2 NOT NULL,
                        [EndDate] datetime2 NULL,
                        [IsActive] bit NOT NULL,
                        [CreatedDate] datetime2 NOT NULL,
                        [CreatedBy] nvarchar(max) NOT NULL,
                        [ModifiedDate] datetime2 NULL,
                        [ModifiedBy] nvarchar(max) NOT NULL,
                        CONSTRAINT [PK_Deductions] PRIMARY KEY ([Id])
                    );
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Payslips]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [Payslips] (
                        [Id] int NOT NULL IDENTITY,
                        [EmployeeId] int NOT NULL,
                        [Month] int NOT NULL,
                        [Year] int NOT NULL,
                        [BasicSalary] decimal(18,2) NOT NULL,
                        [GrossSalary] decimal(18,2) NOT NULL,
                        [TotalDeductions] decimal(18,2) NOT NULL,
                        [NetSalary] decimal(18,2) NOT NULL,
                        [WorkingDays] int NULL,
                        [LeaveDays] decimal(18,2) NULL,
                        [LeaveBalance] nvarchar(max) NOT NULL,
                        [CalculationDetails] nvarchar(max) NOT NULL,
                        [GeneratedDate] datetime2 NOT NULL,
                        [GeneratedBy] nvarchar(max) NOT NULL,
                        [IsLocked] bit NOT NULL,
                        [LockedDate] datetime2 NULL,
                        [Notes] nvarchar(max) NOT NULL,
                        CONSTRAINT [PK_Payslips] PRIMARY KEY ([Id])
                    );
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[PayslipDetails]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [PayslipDetails] (
                        [Id] int NOT NULL IDENTITY,
                        [PayslipId] int NOT NULL,
                        [ItemType] nvarchar(max) NOT NULL,
                        [ItemName] nvarchar(max) NOT NULL,
                        [ItemCategory] nvarchar(max) NOT NULL,
                        [Amount] decimal(18,2) NOT NULL,
                        [SortOrder] int NULL,
                        CONSTRAINT [PK_PayslipDetails] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_PayslipDetails_Payslips_PayslipId] FOREIGN KEY ([PayslipId]) REFERENCES [Payslips] ([Id]) ON DELETE CASCADE
                    );
                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PayslipDetails_PayslipId' AND object_id = OBJECT_ID('PayslipDetails'))
                    BEGIN
                        CREATE INDEX [IX_PayslipDetails_PayslipId] ON [PayslipDetails] ([PayslipId]);
                    END
                END
            ");

            // Original CreateTable statements commented out - using SQL above instead
            /*
            migrationBuilder.CreateTable(
                name: "Allowances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    AllowanceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsPercentage = table.Column<bool>(type: "bit", nullable: false),
                    PercentageValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allowances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DeductionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CalculationMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PercentageValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deductions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payslips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WorkingDays = table.Column<int>(type: "int", nullable: true),
                    LeaveDays = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LeaveBalance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CalculationDetails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payslips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayslipDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayslipId = table.Column<int>(type: "int", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayslipDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayslipDetails_Payslips_PayslipId",
                        column: x => x.PayslipId,
                        principalTable: "Payslips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayslipDetails_PayslipId",
                table: "PayslipDetails",
                column: "PayslipId");
            */
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Allowances");

            migrationBuilder.DropTable(
                name: "Deductions");

            migrationBuilder.DropTable(
                name: "PayslipDetails");

            migrationBuilder.DropTable(
                name: "Payslips");

            migrationBuilder.RenameTable(
                name: "Employee",
                schema: "dbo",
                newName: "Employee");

            migrationBuilder.RenameColumn(
                name: "EmployeeStatus",
                table: "Employee",
                newName: "Status");

            migrationBuilder.AlterColumn<decimal>(
                name: "Year2024",
                table: "Employee",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Year2023",
                table: "Employee",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Year2022",
                table: "Employee",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedOn",
                table: "Employee",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CarryForwardLeaves1",
                table: "Employee",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CarryForwardLeaves",
                table: "Employee",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BasicSalary",
                table: "Employee",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "ApplyTax",
                table: "Employee",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Payrolls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payrolls", x => x.Id);
                });
        }
    }
}
