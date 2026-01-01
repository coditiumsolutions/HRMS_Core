using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Models;

namespace HRMBT.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Payroll> Payrolls { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<TaxRule> TaxRules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Map to the Employee table (singular) in dbo schema - this table has 848 records
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("Employee", "dbo");
                
                // Explicit column type mappings to match actual database schema
                // Database has: float columns -> C# double
                entity.Property(e => e.CarryForwardLeaves).HasColumnType("float");
                entity.Property(e => e.CarryForwardLeaves1).HasColumnType("float");
                entity.Property(e => e.Year2022).HasColumnType("float");
                entity.Property(e => e.Year2023).HasColumnType("float");
                
                // Database has: decimal columns -> C# decimal
                entity.Property(e => e.Year2023New).HasColumnType("decimal(18,2)");
                entity.Property(e => e.BasicSalary).HasColumnType("decimal(18,2)");
                
                // Database has: int columns -> C# int
                entity.Property(e => e.AdjustedAjusted).HasColumnType("int");
                entity.Property(e => e.Year2024).HasColumnType("int");
            });
            
            modelBuilder.Entity<Payroll>().ToTable("Payrolls");
            modelBuilder.Entity<Attendance>().ToTable("Attendances");
            modelBuilder.Entity<LeaveRequest>().ToTable("LeaveRequests");
            modelBuilder.Entity<TaxRule>().ToTable("TaxRules");
        }
    }
}
