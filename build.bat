@echo off
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set WPF=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF

echo ====================================================================
echo  SysMonitor Windows Builder (Native 400KB WPF + Avalonia Cross-Platform)
echo ====================================================================
echo.

echo [1/3] Compiling Native Windows WPF SysMonitor.exe (Ultra-lightweight ~480KB) ...
"%CSC%" /codepage:65001 /target:winexe /optimize+ /win32icon:app.ico /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xaml.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:"%WPF%\WindowsBase.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" /out:SysMonitor.exe src\*.cs

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Native WPF compilation failed with code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo [SUCCESS] Native 480KB SysMonitor.exe compiled:
dir SysMonitor.exe | findstr "SysMonitor.exe"

REM Optional: compile Avalonia build if passed 'all' or 'avalonia'
if /i "%1"=="all" goto BUILD_AVALONIA
if /i "%1"=="avalonia" goto BUILD_AVALONIA
goto DEPLOY_DESKTOP

:BUILD_AVALONIA
echo.
echo [2/3] Compiling Modern Cross-Platform Avalonia SysMonitor (win-x64) ...
set DOTNET=dotnet
where dotnet >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
        set DOTNET="%USERPROFILE%\.dotnet\dotnet.exe"
    ) else (
        echo [WARN] .NET 8 SDK not found, skipping Avalonia build.
        goto DEPLOY_DESKTOP
    )
)
%DOTNET% publish SysMonitor.csproj -r win-x64 -c Release -p:PublishAot=false -p:PublishSingleFile=true --self-contained true -o ./dist/win-x64
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Avalonia win-x64 built at ./dist/win-x64/sysmonitor.exe
)

:DEPLOY_DESKTOP
echo.
echo [3/3] Deploying Native 480KB SysMonitor.exe to User Desktop ...
taskkill /f /im SysMonitor.exe >nul 2>&1
taskkill /f /im sysmonitor.exe >nul 2>&1
ping 127.0.0.1 -n 2 >nul

copy /y ".\SysMonitor.exe" "%USERPROFILE%\Desktop\SysMonitor.exe" >nul
if exist "refresh_icon_cache.exe" refresh_icon_cache.exe >nul 2>&1
powershell -NoProfile -Command "Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=([Environment]::GetFolderPath('Desktop') + '\SysMonitor.exe')}" >nul 2>&1

echo [SUCCESS] Native 480KB SysMonitor.exe deployed to Desktop and started!
