using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Services.Payroll;
using HRMBT.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ✅ MVC
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// ✅ DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Payroll module services (REQUIRED)
builder.Services.AddScoped<PayrollCalculationService>();

// ✅ Attendance module services (REQUIRED)
builder.Services.AddScoped<AttendanceService>();

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

// MVC routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
