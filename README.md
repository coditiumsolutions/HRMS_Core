## HRMS - ASP.NET Core MVC

This project is implemented as a server-rendered ASP.NET Core MVC application with Razor views.

## Tech Stack

- .NET 9 (`net9.0`)
- ASP.NET Core MVC (`AddControllersWithViews`)
- Razor views (`.cshtml`)
- Entity Framework Core (SQL Server)
- Bootstrap and jQuery (static assets from `wwwroot/lib`)

## Architecture Notes

- No React application is used in this repository.
- No Node.js frontend build pipeline is required for build or runtime.
- Routing is controller/view based via `MapControllerRoute` in `Program.cs`.

## Run Locally

From the repository root:

```powershell
dotnet build "src/HRMBT.Web/HRMBT.Web.csproj"
dotnet run --project "src/HRMBT.Web/HRMBT.Web.csproj"
```

Default development URL (from launch settings):

- `http://localhost:5128`

## Core Modules (MVC Routes)

- Home: `/`
- Employees: `/Employee`
- Attendance: `/Attendance`
- Payroll: `/Payroll`
- LMS: `/LMS`

