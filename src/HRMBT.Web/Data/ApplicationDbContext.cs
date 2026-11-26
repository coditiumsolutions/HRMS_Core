using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using HRMBT.Web.Models;

namespace HRMBT.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<SubDepartment> SubDepartments { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Designation> Designations { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<AttendanceUploadLog> AttendanceUploadLogs { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<TaxRule> TaxRules { get; set; }
        public DbSet<FuelPrice> FuelPrices { get; set; }

        // LMS module
        public DbSet<LeaveQuota> LeaveQuotas { get; set; }
        public DbSet<GazettedHoliday> GazettedHolidays { get; set; }
        public DbSet<CarryforwardLeave> CarryforwardLeaves { get; set; }

        // Payroll module
        public DbSet<Allowance> Allowances { get; set; }
        public DbSet<Deduction> Deductions { get; set; }
        public DbSet<Payslip> Payslips { get; set; }
        public DbSet<PayslipDetail> PayslipDetails { get; set; }

        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<HrConfiguration> HrConfigurations { get; set; }
        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        // Schedule, attendance processing, notes and leave entitlements (db.md)
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<ShiftAssignment> ShiftAssignments { get; set; }
        public DbSet<RosterOverride> RosterOverrides { get; set; }
        public DbSet<AttendancePunch> AttendancePunches { get; set; }
        public DbSet<HalfDayRequest> HalfDayRequests { get; set; }
        public DbSet<AttendanceCorrection> AttendanceCorrections { get; set; }
        public DbSet<RecordNote> RecordNotes { get; set; }
        public DbSet<EmployeeEvent> EmployeeEvents { get; set; }
        public DbSet<LeaveBalanceRow> LeaveBalances { get; set; }
        public DbSet<LeaveEntitlementOverride> LeaveEntitlementOverrides { get; set; }

        // ---- legacy attendance facade: write TimeIn/TimeOut/Status into the stored columns before saving ----
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyAttendanceFacade();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyAttendanceFacade();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ApplyAttendanceFacade()
        {
            foreach (var entry in ChangeTracker.Entries<Attendance>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                    entry.Entity.ApplyFacade();
            }
            foreach (var entry in ChangeTracker.Entries<Deduction>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                    entry.Entity.ApplyFacade();
            }
        }

        // ---- value converters: the code keeps its legacy text shape, the database stores the typed column ----

        /// <summary>Payslip month: the app uses English month names, the column is a tinyint 1-12 (T2).</summary>
        private static readonly string[] MonthNames =
            { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

        private static byte MonthToNumber(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            var t = name.Trim();
            if (byte.TryParse(t, out var n) && n is >= 1 and <= 12) return n;
            for (int i = 0; i < 12; i++)
                if (MonthNames[i].Equals(t, StringComparison.OrdinalIgnoreCase)) return (byte)(i + 1);
            return 0;
        }

        private static string NumberToMonth(byte n) => n is >= 1 and <= 12 ? MonthNames[n - 1] : string.Empty;

        /// <summary>Tax year label "2025-2026" (July-June) is stored as the start year 2025 (T6).</summary>
        private static short TaxYearToStart(string? label)
        {
            if (string.IsNullOrWhiteSpace(label) || label.Trim().Length < 4) return 0;
            return short.TryParse(label.Trim().Substring(0, 4), out var y) ? y : (short)0;
        }

        private static string StartToTaxYear(short y) => y > 0 ? $"{y}-{y + 1}" : string.Empty;

        private static bool TextToBool(string? v) =>
            v != null && (v.Trim().Equals("1") || v.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)
                          || v.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) || v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));

        private static string BoolToText(bool v) => v ? "Yes" : "No";

        private static DateTime? TextToDate(string? v) =>
            DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;

        private static string? DateToText(DateTime? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static DateTime? TextToDateTime(string? v) =>
            DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        private static string? DateTimeToText(DateTime? d) => d?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);


        private static int TextToInt(string? v) => int.TryParse(v?.Trim(), out var n) ? n : 0;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- lookups ----
            modelBuilder.Entity<Project>(entity =>
            {
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(x => x.ModifiedAt).HasColumnType("datetime2(0)");
                entity.ToTable("Projects", "dbo");
                entity.Property(p => p.Name).HasMaxLength(100).IsRequired();
                entity.Property(p => p.Code).HasMaxLength(20).IsUnicode(false);
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(x => x.ModifiedAt).HasColumnType("datetime2(0)");
                entity.ToTable("Departments", "dbo");
                entity.HasKey(d => d.DepartmentID);
                entity.Property(d => d.DepartmentID).HasColumnName("Id");
                entity.Property(d => d.DepartmentName).HasColumnName("DepartmentName").HasColumnType("nvarchar(100)");
            });

            modelBuilder.Entity<SubDepartment>(entity =>
            {
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(x => x.ModifiedAt).HasColumnType("datetime2(0)");
                entity.ToTable("SubDepartments", "dbo");
                entity.Property(s => s.Name).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<Designation>(entity =>
            {
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(x => x.ModifiedAt).HasColumnType("datetime2(0)");
                entity.ToTable("Designations", "dbo");
                entity.Property(d => d.Name).HasMaxLength(100).IsRequired();
                entity.Property(d => d.Rank).HasColumnName("Rank");
            });

            modelBuilder.Entity<LeaveType>(entity =>
            {
                entity.Property(x => x.MaxCarryForwardDays).HasColumnType("decimal(5,1)");
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(x => x.ModifiedAt).HasColumnType("datetime2(0)");
                entity.ToTable("LeaveTypes", "dbo");
                entity.Property(l => l.Code).HasMaxLength(10).IsUnicode(false).IsRequired();
                entity.Property(l => l.Name).HasMaxLength(50).IsRequired();
            });

            // ---- Employee ----
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("Employee", "dbo");

                entity.Property(e => e.EmployeeID).HasMaxLength(50).IsUnicode(false);
                entity.Property(e => e.BasicSalary).HasColumnType("decimal(18,2)");
                entity.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PayBasis).HasMaxLength(10).IsUnicode(false);
                entity.Property(e => e.EmployeeStatus).HasMaxLength(20).IsUnicode(false);
                entity.Property(e => e.CNIC).HasMaxLength(15).IsUnicode(false);
                entity.Property(e => e.MobileNo).HasMaxLength(20).IsUnicode(false);
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(e => e.RowVersion).IsRowVersion();

                // legacy text shapes over typed columns
                entity.Property(e => e.DOB).HasColumnType("date")
                    .HasConversion(v => TextToDate(v), v => DateToText(v));
                entity.Property(e => e.ModifiedOn).HasColumnType("datetime2(0)")
                    .HasConversion(v => TextToDateTime(v), v => DateTimeToText(v));
                entity.Property(e => e.ApplyTax).HasColumnType("bit")
                    .HasConversion(v => TextToBool(v), v => BoolToText(v));

                entity.HasOne(e => e.ProjectInfo).WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.DepartmentInfo).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.SubDepartmentInfo).WithMany().HasForeignKey(e => e.SubDepartmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.DesignationInfo).WithMany().HasForeignKey(e => e.DesignationId).OnDelete(DeleteBehavior.Restrict);

                // the org names are shown everywhere an employee is shown, so always load them with the employee
                entity.Navigation(e => e.ProjectInfo).AutoInclude();
                entity.Navigation(e => e.DepartmentInfo).AutoInclude();
                entity.Navigation(e => e.SubDepartmentInfo).AutoInclude();
                entity.Navigation(e => e.DesignationInfo).AutoInclude();
            });

            // ---- Attendance ----
            modelBuilder.Entity<Attendance>(entity =>
            {
                entity.ToTable("Attendance", "dbo");
                entity.HasKey(a => a.AttendanceID);
                entity.Property(a => a.AttendanceID).HasColumnName("Id");
                entity.Property(a => a.EmployeeUid).HasColumnName("EmployeeId");
                entity.Property(a => a.AttendanceDate).HasColumnName("ShiftDate").HasColumnType("date").IsRequired();
                entity.Property(a => a.CheckInAt).HasColumnType("datetime2(0)");
                entity.Property(a => a.CheckOutAt).HasColumnType("datetime2(0)");
                entity.Property(a => a.DayStatus).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Absent");
                entity.Property(a => a.LateMinutes).HasDefaultValue(0);
                entity.Property(a => a.IsLate).ValueGeneratedOnAddOrUpdate();
                entity.Property(a => a.Comments).HasColumnName("Notes").HasMaxLength(1000);
                entity.Property(a => a.RowVersion).IsRowVersion();

                entity.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeUid).OnDelete(DeleteBehavior.Restrict);
                entity.Navigation(a => a.Employee).AutoInclude();

                entity.HasIndex(a => new { a.EmployeeUid, a.AttendanceDate })
                    .IsUnique()
                    .HasDatabaseName("UX_Attendance_Employee_ShiftDate");
            });

            modelBuilder.Entity<AttendanceUploadLog>(entity =>
            {
                entity.ToTable("AttendanceUploadLogs");
                entity.Property(l => l.UploadDate).HasColumnType("datetime2(0)");
                entity.Property(l => l.Status).HasMaxLength(20).IsUnicode(false);
                entity.Property(l => l.FileHash).HasColumnType("char(64)");
            });

            // ---- Leave ----
            modelBuilder.Entity<LeaveRequest>(entity =>
            {
                entity.ToTable("LeaveRequests");
                entity.Property(l => l.FromDate).HasColumnType("date");
                entity.Property(l => l.ToDate).HasColumnType("date");
                entity.Property(l => l.Status).HasMaxLength(10).IsUnicode(false);
                entity.Property(l => l.TotalDays).HasColumnType("decimal(5,1)");
                entity.Property(l => l.AppliedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(l => l.ApprovedAt).HasColumnType("datetime2(0)");
                entity.Property(l => l.ModifiedAt).HasColumnType("datetime2(0)");
                entity.Property(l => l.RowVersion).IsRowVersion();
                entity.HasOne(l => l.LeaveTypeInfo).WithMany().HasForeignKey(l => l.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
                entity.Navigation(l => l.LeaveTypeInfo).AutoInclude();
            });

            modelBuilder.Entity<LeaveQuota>(entity =>
            {
                entity.ToTable("LeaveQuota");
                entity.HasKey(e => e.UID);
                entity.Property(e => e.UID).HasColumnName("Id");
                entity.Property(e => e.TotalLeaves).HasColumnName("TotalDays").HasColumnType("decimal(5,1)");
                entity.Property(e => e.Year).HasColumnName("Year").HasColumnType("int")
                    .HasConversion(v => TextToInt(v), v => v.ToString(CultureInfo.InvariantCulture));
                entity.HasOne(e => e.LeaveTypeInfo).WithMany().HasForeignKey(e => e.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
                entity.Navigation(e => e.LeaveTypeInfo).AutoInclude();
            });

            modelBuilder.Entity<GazettedHoliday>(entity =>
            {
                entity.ToTable("GazettedHolidays");
                entity.Property(h => h.HolidayDate).HasColumnType("date");
                entity.Property(h => h.EndDate).HasColumnType("date");
                entity.Property(h => h.NoticeDate).HasColumnType("date");
                entity.Property(h => h.CreatedAt).HasColumnType("datetime2(0)");
                entity.Property(h => h.ModifiedAt).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<CarryforwardLeave>(entity =>
            {
                entity.ToTable("CarryforwardLeaves");
                entity.Property(c => c.CarryForwardDays).HasColumnType("decimal(5,1)");
            });

            // ---- Tax ----
            modelBuilder.Entity<TaxRule>(entity =>
            {
                entity.ToTable("TaxRules");
                entity.Property(t => t.MinSalary).HasColumnType("decimal(18,2)");
                entity.Property(t => t.MaxSalary).HasColumnType("decimal(18,2)");
                entity.Property(t => t.FixedAmount).HasColumnType("decimal(18,2)");
                entity.Property(t => t.TaxPercentage).HasColumnType("decimal(5,2)");
                entity.Property(t => t.TaxYear).HasColumnName("TaxYearStart").HasColumnType("smallint")
                    .HasConversion(v => TaxYearToStart(v), v => StartToTaxYear(v));
            });

            // ---- Payroll ----
            modelBuilder.Entity<Allowance>(entity =>
            {
                entity.ToTable("Allowances");
                entity.Property(a => a.Amount).HasColumnType("decimal(18,2)");
                entity.Property(a => a.PercentageValue).HasColumnType("decimal(5,2)");
                entity.Property(a => a.Frequency).HasMaxLength(30).IsRequired();
                entity.Property(a => a.Remarks).HasColumnName("Notes").HasMaxLength(1000);
                entity.Property(a => a.EffectiveDate).HasColumnType("date");
                entity.Property(a => a.EndDate).HasColumnType("date");
                entity.Property(a => a.CreatedDate).HasColumnType("datetime2(0)");
                entity.Property(a => a.ModifiedDate).HasColumnType("datetime2(0)");
                entity.Property(a => a.ModifiedBy).HasMaxLength(100);
            });
            modelBuilder.Entity<Deduction>(entity =>
            {
                entity.ToTable("Deductions");
                entity.Property(d => d.DeductionType).HasMaxLength(50).IsRequired();
                entity.Property(d => d.DeductionName).HasMaxLength(100).IsRequired();
                entity.Property(d => d.Frequency).HasMaxLength(50).IsRequired();
                entity.Property(d => d.CalculationMethod).HasMaxLength(20).IsUnicode(false).IsRequired();
                entity.Property(d => d.Percentage).HasColumnName("PercentageValue").HasColumnType("decimal(5,2)");
                entity.Property(d => d.Amount).HasColumnType("decimal(18,2)");
                entity.Property(d => d.EffectiveDate).HasColumnType("date");
                entity.Property(d => d.EndDate).HasColumnType("date");
                entity.Property(d => d.CreatedDate).HasColumnType("datetime2(0)");
                entity.Property(d => d.ModifiedDate).HasColumnType("datetime2(0)");
                entity.Property(d => d.CreatedBy).HasMaxLength(100).IsRequired();
                entity.Property(d => d.ModifiedBy).HasMaxLength(100);
            });
            modelBuilder.Entity<FuelPrice>(entity =>
            {
                entity.ToTable("FuelPrices");
                entity.Property(f => f.EffectiveFrom).HasColumnType("date");
                entity.Property(f => f.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(f => f.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("sysdatetime()");
            });
            modelBuilder.Entity<PayslipDetail>(entity =>
            {
                entity.Property(d => d.Amount).HasColumnType("decimal(18,2)");
                entity.ToTable("PayslipDetails");
                entity.Property(d => d.ItemType).HasMaxLength(20).IsUnicode(false);
                entity.Property(d => d.Quantity).HasColumnType("decimal(9,2)");
                entity.Property(d => d.UnitAmount).HasColumnType("decimal(18,2)");
                entity.Property(d => d.Note).HasMaxLength(200);
                entity.HasOne(d => d.Payslip)
                    .WithMany(p => p.PayslipDetails)
                    .HasForeignKey(d => d.PayslipId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Payslip>(entity =>
            {
                entity.ToTable("Payslips");
                // the app works with month names; the column is the month number (T2)
                entity.Property(p => p.Month).HasColumnType("tinyint")
                    .HasConversion(v => MonthToNumber(v), v => NumberToMonth(v));
                entity.Property(p => p.BasicSalary).HasColumnType("decimal(18,2)");
                entity.Property(p => p.TotalAllowances).HasColumnType("decimal(18,2)");
                entity.Property(p => p.LeaveDays).HasColumnType("decimal(5,2)");
                entity.Property(p => p.GrossSalary).HasColumnType("decimal(18,2)");
                entity.Property(p => p.TaxPercentage).HasColumnType("decimal(5,2)");
                entity.Property(p => p.TaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(p => p.TotalDeductions).HasColumnType("decimal(18,2)");
                entity.Property(p => p.NetSalary).HasColumnType("decimal(18,2)");
                entity.Property(p => p.GeneratedDate).HasColumnType("datetime2(0)");
                entity.Property(p => p.LockedDate).HasColumnType("datetime2(0)");
                entity.Property(p => p.RowVersion).IsRowVersion();
                entity.HasOne(p => p.Employee)
                    .WithMany()
                    .HasForeignKey(p => p.EmployeeId)
                    .HasPrincipalKey(e => e.uid)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Users / configuration / documents / audit ----
            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Uid);
                entity.Property(e => e.Uid).HasColumnName("uid");
                entity.Property(e => e.EmployeeUid).HasColumnName("EmployeeId");
                entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
                entity.Property(e => e.CreatedOn).HasColumnType("datetime2(0)");
                entity.Property(e => e.ModifiedAt).HasColumnType("datetime2(0)");
                entity.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeUid).OnDelete(DeleteBehavior.Restrict);
                entity.Navigation(e => e.Employee).AutoInclude();
                entity.HasMany(e => e.Roles).WithOne().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.Navigation(e => e.Roles).AutoInclude();
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("UserRoles");
                entity.HasKey(r => new { r.UserId, r.Role });
                entity.Property(r => r.Role).HasMaxLength(30);
            });

            modelBuilder.Entity<HrConfiguration>(entity =>
            {
                entity.ToTable("Configuration");
                entity.HasKey(e => e.UID);
                entity.Property(e => e.UID).HasColumnName("Id");
                entity.Property(e => e.ConfigKey).HasMaxLength(50);
                entity.Property(e => e.ConfigValue).HasColumnType("nvarchar(max)");
            });

            modelBuilder.Entity<EmployeeDocument>(entity =>
            {
                entity.ToTable("EmployeeDocuments", "dbo");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.FileName).HasMaxLength(300).IsRequired();
                entity.Property(e => e.OriginalFileName).HasMaxLength(300);
                entity.Property(e => e.FilePath).HasMaxLength(500).IsRequired();
                entity.Property(e => e.FileExtension).HasMaxLength(50);
                entity.Property(e => e.FileSize).HasColumnType("bigint");
                entity.Property(e => e.UploadedOn).HasColumnType("datetime2(0)");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.DocumentType).HasMaxLength(30).IsUnicode(false);
                entity.Property(e => e.FileHash).HasColumnType("char(64)");
                entity.HasOne(e => e.Employee)
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .HasPrincipalKey(e => e.uid)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLogs", "dbo");
                entity.HasKey(e => e.LogId);
                entity.Property(e => e.LogId).HasColumnName("Id").ValueGeneratedOnAdd();
                entity.Property(e => e.TableName).HasMaxLength(128).IsRequired();
                entity.Property(e => e.Operation).HasMaxLength(20).IsRequired();
                entity.Property(e => e.RecordId).HasMaxLength(100);
                entity.Property(e => e.OldData).HasColumnType("nvarchar(max)");
                entity.Property(e => e.NewData).HasColumnType("nvarchar(max)");
                entity.Property(e => e.ChangedBy).HasMaxLength(128);
                entity.Property(e => e.ChangedAt).HasColumnType("datetime2(0)");
                entity.Property(e => e.ModuleName).HasMaxLength(100);
                entity.Property(e => e.IPAddress).HasMaxLength(50);
            });
            // ---- schedule, attendance processing, notes, entitlements ----
            modelBuilder.Entity<Shift>(entity =>
            {
                entity.ToTable("Shifts", "dbo");
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.StartTime).HasColumnType("time(0)");
                entity.Property(x => x.EndTime).HasColumnType("time(0)");
                entity.Property(x => x.IsOvernight).ValueGeneratedOnAddOrUpdate();
                entity.Property(x => x.HalfDayCutoffTime).HasColumnType("time(0)");
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(x => x.ModifiedAt).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<ShiftAssignment>(entity =>
            {
                entity.ToTable("ShiftAssignments", "dbo");
                entity.Property(x => x.EffectiveFromDate).HasColumnType("date");
                entity.Property(x => x.EffectiveToDate).HasColumnType("date");
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(x => x.ModifiedAt).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<RosterOverride>(entity =>
            {
                entity.ToTable("RosterOverrides", "dbo");
                entity.Property(x => x.OverrideDate).HasColumnType("date");
                entity.Property(x => x.OverrideType).HasMaxLength(15).IsUnicode(false);
                entity.Property(x => x.ApprovedAt).HasColumnType("datetime2(0)");
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(x => x.ModifiedAt).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<AttendancePunch>(entity =>
            {
                entity.ToTable("AttendancePunches", "dbo");
                entity.Property(x => x.RawEmployeeCode).HasMaxLength(50).IsUnicode(false);
                entity.Property(x => x.PunchAt).HasColumnType("datetime2(0)");
                entity.Property(x => x.Direction).HasMaxLength(7).IsUnicode(false);
                entity.Property(x => x.Source).HasMaxLength(10).IsUnicode(false);
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<HalfDayRequest>(entity =>
            {
                entity.ToTable("HalfDayRequests", "dbo");
                entity.Property(x => x.HalfDayDate).HasColumnType("date");
                entity.Property(x => x.HalfType).HasMaxLength(10).IsUnicode(false);
                entity.Property(x => x.ExpectedTime).HasColumnType("time(0)");
                entity.Property(x => x.Status).HasMaxLength(10).IsUnicode(false);
                entity.Property(x => x.ApprovedAt).HasColumnType("datetime2(0)");
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.Property(x => x.ModifiedAt).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<AttendanceCorrection>(entity =>
            {
                entity.ToTable("AttendanceCorrections", "dbo");
                entity.Property(x => x.CorrectionType).HasMaxLength(20).IsUnicode(false);
                entity.Property(x => x.OldDayStatus).HasMaxLength(20).IsUnicode(false);
                entity.Property(x => x.NewDayStatus).HasMaxLength(20).IsUnicode(false);
                entity.Property(x => x.CorrectedCheckInAt).HasColumnType("datetime2(0)");
                entity.Property(x => x.CorrectedCheckOutAt).HasColumnType("datetime2(0)");
                entity.Property(x => x.ReportedByEmployeeAt).HasColumnType("datetime2(0)");
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<RecordNote>(entity =>
            {
                entity.ToTable("RecordNotes", "dbo");
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<LeaveBalanceRow>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vLeaveBalances", "dbo");
                foreach (var col in new[] { "BaseDays", "Adjustment", "CarryForward", "Entitled", "Taken", "Pending", "Remaining" })
                    entity.Property<decimal>(col).HasColumnType("decimal(10,1)");
            });

            modelBuilder.Entity<EmployeeEvent>(entity =>
            {
                entity.ToTable("EmployeeEvents", "dbo");
                entity.Property(x => x.EventType).HasMaxLength(20).IsUnicode(false);
                entity.Property(x => x.EventDate).HasColumnType("date");
                entity.Property(x => x.VoidedAt).HasColumnType("datetime2(0)");
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
                entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Project>().WithMany().HasForeignKey(x => x.FromProjectId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Project>().WithMany().HasForeignKey(x => x.ToProjectId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Department>().WithMany().HasForeignKey(x => x.FromDepartmentId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Department>().WithMany().HasForeignKey(x => x.ToDepartmentId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Designation>().WithMany().HasForeignKey(x => x.FromDesignationId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Designation>().WithMany().HasForeignKey(x => x.ToDesignationId).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<LeaveEntitlementOverride>(entity =>
            {
                entity.ToTable("LeaveEntitlementOverrides", "dbo");
                entity.Property(x => x.AdjustmentDays).HasColumnType("decimal(5,1)");
                entity.Property(x => x.OverrideTotalDays).HasColumnType("decimal(5,1)");
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").ValueGeneratedOnAdd();
            });
        }
    }
}
