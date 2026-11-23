using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Services.Payroll;
using HRMBT.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC (runtime Razor compilation only in Development — avoids IIS publish issues)
// Every unsafe request (POST/PUT/DELETE) must carry an antiforgery token, even if a new action forgets the attribute.
var mvcBuilder = builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddMemoryCache();
if (builder.Environment.IsDevelopment())
    mvcBuilder.AddRazorRuntimeCompilation();

// ✅ DbContext
// Connection strings are NOT committed: they come from appsettings.{Environment}.json (git-ignored; see the
// *.example.json files) or from the ConnectionStrings__DefaultConnection environment variable.
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException(
        $"ConnectionStrings:DefaultConnection is not configured for environment '{builder.Environment.EnvironmentName}'. " +
        $"Create src/HRMBT.Web/appsettings.{builder.Environment.EnvironmentName}.json from its .example.json file, " +
        "or set the ConnectionStrings__DefaultConnection environment variable.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        defaultConnection,
        sql =>
        {
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

// ✅ Payroll module services (REQUIRED)
builder.Services.AddScoped<PayrollCalculationService>();
builder.Services.AddScoped<IPayslipLockService, PayslipLockService>();

// ✅ Attendance module services (REQUIRED)
builder.Services.AddScoped<AttendanceService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.Configure<EmployeeDocumentOptions>(
    builder.Configuration.GetSection(EmployeeDocumentOptions.SectionName));
builder.Services.AddScoped<IEmployeeDocumentService, EmployeeDocumentService>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Login";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Secure cookie everywhere except local Development (plain http://localhost).
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Employee documents must only be served through EmployeeDocumentsController.Download (login + IsDeleted check).
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// MVC routing — open app at login; authenticated users browse from Home/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.Run();
