## HRMS - ASP.NET Core MVC

This project is implemented as a server-rendered ASP.NET Core MVC application with Razor views.

## Tech Stack

- .NET 8 (`net8.0`)
- ASP.NET Core MVC (`AddControllersWithViews`)
- Razor views (`.cshtml`)
- Entity Framework Core (SQL Server)
- Bootstrap and jQuery (static assets from `wwwroot/lib`)

## Architecture Notes

- No React application is used in this repository.
- No Node.js frontend build pipeline is required for build or runtime.
- Routing is controller/view based via `MapControllerRoute` in `Program.cs`.

## Configuration (connection strings are not committed)

`src/HRMBT.Web/appsettings.json` holds only non-secret defaults. Connection strings come from per-environment files that are git-ignored:

| File | Used when | Create from |
|---|---|---|
| `appsettings.Development.json` | `ASPNETCORE_ENVIRONMENT=Development` (`dotnet run`, local) | `appsettings.Development.example.json` |
| `appsettings.Production.json` | `ASPNETCORE_ENVIRONMENT=Production` (IIS server; also the default when unset) | `appsettings.Production.example.json` |

The `ConnectionStrings__DefaultConnection` environment variable overrides both. The app refuses to start without a connection string. `.env` (git-ignored, see `.env.example`) only keeps reference connection strings; the app does not read it.

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

