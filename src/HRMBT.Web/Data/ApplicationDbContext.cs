using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Models;

namespace HRMBT.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // ❌ REMOVE THIS
        // public DbSet<Payroll> Payrolls { get; set; }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<TaxRule> TaxRules { get; set; }

        // ✅ PAYROLL MODULE (CORRECT)
        public DbSet<Allowance> Allowances { get; set; }
        public DbSet<Deduction> Deductions { get; set; }
        public DbSet<Payslip> Payslips { get; set; }
        public DbSet<PayslipDetail> PayslipDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("Employee", "dbo");

                entity.Property(e => e.CarryForwardLeaves).HasColumnType("float");
                entity.Property(e => e.CarryForwardLeaves1).HasColumnType("float");
                entity.Property(e => e.Year2022).HasColumnType("float");
                entity.Property(e => e.Year2023).HasColumnType("float");

                entity.Property(e => e.Year2023New).HasColumnType("decimal(18,2)");
                entity.Property(e => e.BasicSalary).HasColumnType("decimal(18,2)");

                entity.Property(e => e.AdjustedAjusted).HasColumnType("int");
                entity.Property(e => e.Year2024).HasColumnType("int");
            });

            modelBuilder.Entity<Attendance>().ToTable("Attendances");
            modelBuilder.Entity<LeaveRequest>().ToTable("LeaveRequests");
            modelBuilder.Entity<TaxRule>().ToTable("TaxRules");

            // ✅ PAYROLL TABLES
            modelBuilder.Entity<Allowance>().ToTable("Allowances");
            modelBuilder.Entity<Deduction>().ToTable("Deductions");
            modelBuilder.Entity<Payslip>().ToTable("Payslips");
            modelBuilder.Entity<PayslipDetail>().ToTable("PayslipDetails");
        }
    }
}
