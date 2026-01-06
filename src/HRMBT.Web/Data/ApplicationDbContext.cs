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
        public DbSet<AttendanceUploadLog> AttendanceUploadLogs { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<TaxRule> TaxRules { get; set; }

        // ✅ LMS MODULE
        public DbSet<LeaveQuota> LeaveQuotas { get; set; }
        public DbSet<GazettedHoliday> GazettedHolidays { get; set; }
        public DbSet<EmployeeLeave> EmployeeLeaves { get; set; }
        public DbSet<CarryforwardLeave> CarryforwardLeaves { get; set; }

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

            // Attendance entity configuration
            modelBuilder.Entity<Attendance>(entity =>
            {
                entity.ToTable("Attendance"); // Use singular table name

                // Map primary key
                entity.HasKey(a => a.AttendanceID);
                entity.Property(a => a.AttendanceID).HasColumnName("AttendanceID");

                // Map properties to database columns
                entity.Property(a => a.EmployeeID).HasColumnName("EmployeeID").IsRequired();
                entity.Property(a => a.EmployeeName).HasColumnName("EmployeeName").IsRequired();
                entity.Property(a => a.DepartmentName).HasColumnName("DepartmentName").IsRequired();
                entity.Property(a => a.AttendanceDate).HasColumnName("AttendanceDate").HasColumnType("date").IsRequired();
                entity.Property(a => a.TimeIn).HasColumnName("TimeIn").HasColumnType("time");
                entity.Property(a => a.TimeOut).HasColumnName("TimeOut").HasColumnType("time");
                entity.Property(a => a.Status).HasColumnName("Status");
                entity.Property(a => a.Comments).HasColumnName("Comments");

                // Enforce uniqueness: one record per employee per date
                entity.HasIndex(a => new { a.EmployeeID, a.AttendanceDate })
                    .IsUnique()
                    .HasDatabaseName("IX_Attendance_EmployeeID_Date");
            });

            // AttendanceUploadLog entity configuration
            modelBuilder.Entity<AttendanceUploadLog>().ToTable("AttendanceUploadLogs");

            modelBuilder.Entity<LeaveRequest>().ToTable("LeaveRequests");
            modelBuilder.Entity<TaxRule>().ToTable("TaxRules");

            // ✅ LMS TABLES
            modelBuilder.Entity<LeaveQuota>().ToTable("LeaveQuota");
            modelBuilder.Entity<GazettedHoliday>().ToTable("GazettedHoliday");
            
            // EmployeeLeave entity configuration
            modelBuilder.Entity<EmployeeLeave>(entity =>
            {
                entity.ToTable("EmployeeLeaves");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("uid");
            });
            
            modelBuilder.Entity<CarryforwardLeave>().ToTable("CarryforwardLeaves");

            // ✅ PAYROLL TABLES
            modelBuilder.Entity<Allowance>().ToTable("Allowances");
            modelBuilder.Entity<Deduction>().ToTable("Deductions");
            modelBuilder.Entity<Payslip>().ToTable("Payslips");
            modelBuilder.Entity<PayslipDetail>().ToTable("PayslipDetails");
        }
    }
}
