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
            // The "Employees" table (plural) only has 1 record and should be ignored
            modelBuilder.Entity<Employee>().ToTable("Employee", "dbo");
            // EmployeeStatus property maps to EmployeeStatus column in database (no mapping needed)
            modelBuilder.Entity<Payroll>().ToTable("Payrolls");
            modelBuilder.Entity<Attendance>().ToTable("Attendances");
            modelBuilder.Entity<LeaveRequest>().ToTable("LeaveRequests");
            modelBuilder.Entity<TaxRule>().ToTable("TaxRules");
        }
    }
}
