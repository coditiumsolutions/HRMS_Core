# HRMS Deployment

Last Updated: 2026-11-16

How HRMS (`HRMBT.Web`) is deployed and where it runs. Facts are marked **verified** (checked from outside on 2026-11-16) or **from repo** (taken from `scripts/deploy/DeployToIIS.bat` and not re-checked on the server). No passwords or connection strings belong in this file.

## Production server

| Item | Value | Source |
|---|---|---|
| Server name | **Teezhost 16GB** (the plan name; specs beyond the name are not documented here) | provided by owner |
| Public host name | `hr.coditium.com` | verified |
| IP address | `138.201.247.223` (DNS A record) | verified |
| Web server | Microsoft IIS 10.0 (`X-Powered-By: ASP.NET`) | verified |
| Web ports | 80 (HTTP) and 443 (HTTPS) open; 82 closed | verified |
| TLS | HTTPS served, `Strict-Transport-Security: max-age=2592000` sent. HTTP on port 80 is **not** redirected to HTTPS (it serves the login redirect over HTTP) | verified |
| Database | SQL Server 2022 (16.0.1000.6) on the same machine, TCP **1433** open to the internet, database `HRBTK` | verified |
| App behaviour | Anonymous requests to `/` get `302 -> /Home/Login?ReturnUrl=%2F` (fallback authorization is active) | verified |

The database server also hosts many unrelated databases (billing, POS, portals, etc.), so the SQL login in use has wide access. Treat that login as production-critical.

## What runs where
```
Browser --HTTPS--> IIS 10 (Teezhost 16GB) --> ASP.NET Core app HRMBT.Web (.NET 8)
                                                  |
                                                  +--> SQL Server 2022, same machine, database HRBTK
                                                  +--> files: wwwroot/uploads/employees/{employeeUid}/ (employee documents)
```

## Build and publish
Script: `scripts/deploy/DeployToIIS.bat` (run on the server, or on a machine that publishes to it).

1. `dotnet clean` / `restore` / `build -c Release`.
2. Stops IIS app pool and site named `HRMS`; kills any `HRMBT.Web.exe`.
3. **Deletes everything in the deploy folder** (`C:\HRMSDeploy` in the script), then `dotnet publish -c Release --self-contained false --runtime win-x64` into it.
4. Checks `web.config` and `HRMBT.Web.dll`, then restarts the app pool and site.

Alternative: the Visual Studio publish profile `src/HRMBT.Web/Properties/PublishProfiles/FolderProfile.pubxml` (folder publish).

### Server prerequisites (from repo)
- IIS site `HRMS`, physical path = the deploy folder; app pool `HRMS` set to **No Managed Code**, 64-bit.
- **ASP.NET Core 8 Hosting Bundle** (project targets `net8.0`).
- Firewall rules for the web ports and, if the database stays on this machine, 1433 (see risks).

## Configuration and secrets
- Connection strings are **not committed**. `appsettings.json` has an empty `DefaultConnection`; the real values are in git-ignored files created from the committed examples:
  - `src/HRMBT.Web/appsettings.Production.json` (from `appsettings.Production.example.json`) - production, database `HRBTK` on this server. Loaded when `ASPNETCORE_ENVIRONMENT=Production` (the default when the variable is unset, as on IIS).
  - `src/HRMBT.Web/appsettings.Development.json` (from `appsettings.Development.example.json`) - local development, so local runs no longer touch production.
  - Alternatively set `ConnectionStrings__DefaultConnection` as an environment variable on the app pool.
  - `.env` (git-ignored, `.env.example` committed) keeps the old/unused strings for reference only; the app does not read it. They (`localhost,50001`, old public IP `178.105.20.255`, `Payroll2`, `BT`) are outdated.
- **Publishing:** `dotnet publish` copies `appsettings.Production.json` from the project folder, so it must exist on the machine that runs `scripts/deploy/DeployToIIS.bat` (the script stops with an error if it is missing). If you deploy from a different machine, copy the file to the server's deploy folder by hand.
- The app throws a clear startup error if no connection string is configured for the current environment.
- The old passwords remain in git history; rotating the `sa` password is still required.
- `ASPNETCORE_ENVIRONMENT` on the server should be `Production`. Not verified.
- Employee document settings (`EmployeeDocuments` section: root `uploads/employees`, 10 MB per file, 10 files per upload) come from `appsettings.json`.

## Database deployment
- No automatic migrations at startup. Schema changes are applied manually (EF CLI or the SQL files in `scripts/database/`), and several were applied by hand without being recorded in `__EFMigrationsHistory`. See `db.md` (Known issues) before running `dotnet ef database update`.
- Schema reference and live verification: `db.md`. Raw dump helper: `scripts/database/tools/export-db-schema.ps1` (writes `docs/db-schema-export.txt`).
- Take a SQL Server backup of `HRBTK` before any schema change. No backup job is documented.

## Release checklist
1. Back up `HRBTK` and copy `wwwroot/uploads/employees/` somewhere safe (the script wipes the deploy folder, **including uploaded employee documents**).
2. Apply any pending SQL changes and update `db.md` (version, date, change history).
3. Run `scripts/deploy/DeployToIIS.bat`.
4. Restore the uploads folder into the new deploy folder.
5. Smoke test: open `https://hr.coditium.com`, sign in, open Employees, Payroll dashboard, one payslip print view, one document download.
6. Check the IIS stdout/event log for startup errors.

## Known issues and risks
| P | Issue |
|---|---|
| P0 | SQL Server port 1433 is reachable from the internet and the app uses the `sa` account; the same login can reach 14 other databases on the server. Restrict 1433 to the web app (firewall / localhost) and use a least-privilege login for `HRBTK`. |
| P0 | Credentials for several servers, including `sa`, are committed in `appsettings.json` and git history. Rotate and move to server-side secrets. |
| P0 | `/Setup` (user and configuration management) is anonymous; see `docs/analysis/04-security.md`. |
| P1 | HTTP (port 80) is not redirected to HTTPS and the auth cookie is not marked `Secure`; add HTTPS redirection / an IIS rewrite rule. |
| P1 | The deploy script deletes uploaded documents and has no rollback or downtime strategy. |
| P1 | `DeployToIIS.bat` now prints the real URL (`https://hr.coditium.com`) and checks, but the deploy path (`C:\HRMSDeploy`) is still hard-coded and the script still wipes the deploy folder. |
| P2 | No health-check endpoint, no structured logging, no documented backup or restore procedure. |
| P2 | Unrecorded details to confirm on the server: IIS site/app-pool names, deploy folder path, where the connection string lives, .NET hosting bundle version, backup schedule. |

## Related
- `db.md` - database schema, live verification and known database issues
- `docs/analysis/05-deployment.md` - earlier static analysis of the deploy script
- `docs/analysis/04-security.md` - security findings
