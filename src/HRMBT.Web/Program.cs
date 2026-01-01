using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// ✅ REQUIRED: Add MVC services with runtime compilation
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// ✅ REQUIRED: Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// (Optional) Authorization
app.UseAuthorization();

// ✅ REQUIRED: MVC routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
