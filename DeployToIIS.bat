@echo off
REM Deploy HRMS (HRMBT.Web) to IIS
REM Run this script from the HRMS repo root (same folder as this .bat), or adjust paths below.

echo ========================================
echo HRMS Application Deployment Script
echo ========================================
echo.

set PROJECT_ROOT=%~dp0
set PROJECT_PATH=%PROJECT_ROOT%src\HRMBT.Web\
set DEPLOY_PATH=C:\HRMSDeploy

echo Project Path: %PROJECT_PATH%
echo Deploy Path: %DEPLOY_PATH%
echo.

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
echo Public URL (HTTP^): http://178.105.20.255:82/
echo.
echo IIS binding (you added the site^): http, port 82, host name blank ^(answers for IP^).
echo Optional: add hostname zkbeclipse.pk on port 82 if you want that URL too.
echo.
echo One-time / server checks:
echo   - Site name: HRMS  ^| Physical path: %DEPLOY_PATH% ^(must match your folder^)
echo   - App pool: HRMS (No Managed Code, 64-bit^)
echo   - Firewall: allow TCP 82 inbound to this server
echo   - ASP.NET Core 8 Hosting Bundle installed
echo.
echo Deploy script finished.
