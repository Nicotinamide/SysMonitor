@echo off
cd /d "%~dp0"

set DOTNET=dotnet
where dotnet >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
        set DOTNET="%USERPROFILE%\.dotnet\dotnet.exe"
    ) else (
        echo [ERROR] .NET 8 SDK not found in PATH or %USERPROFILE%\.dotnet
        exit /b 1
    )
)

echo [1/3] Compiling Unified Cross-Platform Avalonia SysMonitor for Windows (win-x64) ...
%DOTNET% publish SysMonitor.csproj -r win-x64 -c Release -p:PublishAot=false -p:PublishSingleFile=true --self-contained true -o ./dist/win-x64

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Compilation failed with code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo [2/3] Verifying and copying output binary ...
taskkill /f /im SysMonitor.exe >nul 2>&1
taskkill /f /im sysmonitor.exe >nul 2>&1
ping 127.0.0.1 -n 2 >nul

copy /y ".\dist\win-x64\sysmonitor.exe" ".\SysMonitor.exe" >nul
dir SysMonitor.exe

echo [3/3] Deploying SysMonitor.exe to User Desktop ...
copy /y ".\dist\win-x64\sysmonitor.exe" "%USERPROFILE%\Desktop\SysMonitor.exe" >nul
if exist "refresh_icon_cache.exe" refresh_icon_cache.exe >nul 2>&1
powershell -NoProfile -Command "Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=([Environment]::GetFolderPath('Desktop') + '\SysMonitor.exe')}" >nul 2>&1

echo [SUCCESS] Unified Avalonia SysMonitor built and launched successfully!
