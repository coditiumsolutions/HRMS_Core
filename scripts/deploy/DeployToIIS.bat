@echo off
REM Deploy HRMS (HRMBT.Web) to IIS
REM Lives in scripts\deploy; PROJECT_ROOT resolves to the repo root (two levels up).

echo ========================================
echo HRMS Application Deployment Script
echo ========================================
echo.

set PROJECT_ROOT=%~dp0..\..\
set PROJECT_PATH=%PROJECT_ROOT%src\HRMBT.Web\
set DEPLOY_PATH=C:\HRMSDeploy

echo Project Path: %PROJECT_PATH%
echo Deploy Path: %DEPLOY_PATH%
echo.

REM The production connection string is not in git. It must exist next to the project so publish copies it.
if not exist "%PROJECT_PATH%appsettings.Production.json" (
    echo [ERROR] %PROJECT_PATH%appsettings.Production.json not found.
    echo         Create it from appsettings.Production.example.json ^(contains the production connection string^).
    exit /b 1
)

REM Step 1: Clean previous build
echo Step 1: Cleaning previous build...
dotnet clean "%PROJECT_PATH%HRMBT.Web.csproj" --configuration Release
if %ERRORLEVEL% NEQ 0 (
    echo Clean failed!
    exit /b 1
)

REM Step 2: Restore packages
echo Step 2: Restoring NuGet packages...
dotnet restore "%PROJECT_PATH%HRMBT.Web.csproj"
if %ERRORLEVEL% NEQ 0 (
    echo Restore failed!
    exit /b 1
)

REM Step 3: Build in Release mode
echo Step 3: Building project (Release mode)...
dotnet build "%PROJECT_PATH%HRMBT.Web.csproj" --configuration Release --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    exit /b 1
)

REM Step 3.5: Stop HRMS app pool / site to avoid file locks
echo Step 3.5: Stopping HRMS app (IIS)...
powershell -NoProfile -Command "$ErrorActionPreference='SilentlyContinue'; Import-Module WebAdministration; if (Test-Path 'IIS:\AppPools\HRMS') { Stop-WebAppPool -Name 'HRMS' }; if (Test-Path 'IIS:\Sites\HRMS') { Stop-Website -Name 'HRMS' }"
taskkill /F /IM HRMBT.Web.exe >nul 2>&1

REM Step 3.7: Back up employee documents before the deploy folder is wiped (they live in wwwroot\uploads unless EmployeeDocuments:StorageBasePath points elsewhere)
echo Step 3.7: Backing up employee documents...
set UPLOADS_SRC=%DEPLOY_PATH%\wwwroot\uploads
set UPLOADS_BACKUP=
if exist "%UPLOADS_SRC%\employees" (
    for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set STAMP=%%i
    set UPLOADS_BACKUP=C:\HRMSBackups\uploads_%STAMP%
    robocopy "%UPLOADS_SRC%" "%UPLOADS_BACKUP%" /E /COPY:DAT /R:2 /W:2 /NFL /NDL /NJH /NJS /NP >nul
    if %ERRORLEVEL% GEQ 8 (
        echo [ERROR] Could not back up %UPLOADS_SRC% to %UPLOADS_BACKUP%. Nothing was deleted.
        exit /b 1
    )
    echo [OK] Documents copied to %UPLOADS_BACKUP%
) else (
    echo No wwwroot\uploads\employees folder found ^(nothing to back up; fine if StorageBasePath is used^)
)

REM Step 4: Publish to deployment folder
echo Step 4: Publishing to %DEPLOY_PATH%...
if exist "%DEPLOY_PATH%" (
    echo Cleaning deployment folder...
    powershell -NoProfile -Command "if (Test-Path '%DEPLOY_PATH%') { Remove-Item -Path '%DEPLOY_PATH%' -Recurse -Force -ErrorAction SilentlyContinue }"
    timeout /t 1 /nobreak >nul
)
if not exist "%DEPLOY_PATH%" (
    mkdir "%DEPLOY_PATH%"
)

dotnet publish "%PROJECT_PATH%HRMBT.Web.csproj" --configuration Release --output "%DEPLOY_PATH%" --self-contained false --runtime win-x64

if %ERRORLEVEL% NEQ 0 (
    echo Publish failed!
    exit /b 1
)

REM Step 4.5: Put the employee documents back
if defined UPLOADS_BACKUP (
    echo Step 4.5: Restoring employee documents...
    robocopy "%UPLOADS_BACKUP%" "%DEPLOY_PATH%\wwwroot\uploads" /E /COPY:DAT /R:2 /W:2 /NFL /NDL /NJH /NJS /NP >nul
    if %ERRORLEVEL% GEQ 8 (
        echo [ERROR] Documents could not be restored. They are safe in %UPLOADS_BACKUP%; copy them to %DEPLOY_PATH%\wwwroot\uploads by hand.
        exit /b 1
    )
    echo [OK] Documents restored ^(backup kept in %UPLOADS_BACKUP%^)
)

REM Step 5: Verify web.config (ASP.NET Core Module)
echo Step 5: Verifying web.config...
if exist "%DEPLOY_PATH%\web.config" (
    echo [OK] web.config found
) else (
    echo [WARNING] web.config not found - publish may be incomplete
)

REM Step 6: Verify deployment
echo Step 6: Verifying deployment...
if exist "%DEPLOY_PATH%\HRMBT.Web.dll" (
    echo [OK] HRMBT.Web.dll found
) else (
    echo [ERROR] HRMBT.Web.dll not found!
    exit /b 1
)

REM Step 7: Start IIS site / app pool again
echo Step 7: Starting IIS site / app pool...
powershell -NoProfile -Command "$ErrorActionPreference='SilentlyContinue'; Import-Module WebAdministration; if (Test-Path 'IIS:\AppPools\HRMS') { Start-WebAppPool -Name 'HRMS' }; if (Test-Path 'IIS:\Sites\HRMS') { Start-Website -Name 'HRMS' }"

echo.
echo ========================================
echo Deployment completed successfully!
echo ========================================
echo.
echo Deployment Location: %DEPLOY_PATH%
echo Public URL: https://hr.coditium.com/ ^(Teezhost 16GB, IIS 10^)
echo Database: HRBTK on the same server ^(DefaultConnection in appsettings.json^)
echo.
echo One-time / server checks:
echo   - Site name: HRMS  ^| Physical path: %DEPLOY_PATH% ^(must match your folder^)
echo   - IIS bindings: https on 443 with the hr.coditium.com certificate, http on 80
echo   - App pool: HRMS (No Managed Code, 64-bit^)
echo   - Firewall: allow TCP 80 and 443 inbound
echo   - ASP.NET Core 8 Hosting Bundle installed
echo   - Employee documents: copied to C:\HRMSBackups before the wipe and restored after publish. Better: set EmployeeDocuments:StorageBasePath ^(appsettings.Production.json^) to a folder outside %DEPLOY_PATH%
echo.
echo Deploy script finished.
