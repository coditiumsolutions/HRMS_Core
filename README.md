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
- Routing is attribute-based (lowercase, kebab-case URLs); the URL map is in `docs/routes.md`.

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

### Run against the local database

1. Put the local SQL Server connection string in `src/HRMBT.Web/appsettings.Development.json` (git-ignored; start from `appsettings.Development.example.json`):

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=<server>\\SQLEXPRESS;Initial Catalog=HRBTK;User ID=<user>;Password=<password>;TrustServerCertificate=True;MultipleActiveResultSets=True"
     }
   }
   ```

2. Run the app in the Development environment (this is what makes it read the file above):

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   dotnet run --project "src/HRMBT.Web/HRMBT.Web.csproj" --urls http://localhost:5128
   ```

### Launch profiles (Development / Production)

The profiles are in `src/HRMBT.Web/Properties/launchSettings.json`:

| Command | Environment | Database | URL |
|---|---|---|---|
| `dotnet run --project "src/HRMBT.Web/HRMBT.Web.csproj"` (default profile) | Development | local, from `appsettings.Development.json` | `http://localhost:5128` |
| `dotnet run --launch-profile Production --project "src/HRMBT.Web/HRMBT.Web.csproj"` | Production | **production server**, from `appsettings.Production.json` | `http://localhost:5129` |

The Production profile runs the app on your machine against the **live production database with real data**. Use it only to check something against production, avoid saving anything, and prefer a read-only SQL login. It needs `appsettings.Production.json` (git-ignored). Normal work uses the default profile.

If you pass `--no-launch-profile`, set `ASPNETCORE_ENVIRONMENT` yourself: when it is unset the app defaults to Production and connects to the **production** server.

The local `HRBTK` database needs the redesigned schema: run the scripts in `scripts/database/redesign/` in order. `db.md` describes the schema and says which scripts are not yet on production.

## Core Modules (MVC Routes)

- Home: `/`
- Employees: `/employees`
- Attendance: `/attendance`
- Payroll: `/payroll/run`, `/payslips`
- Leave: `/leave`, `/work-weeks`

