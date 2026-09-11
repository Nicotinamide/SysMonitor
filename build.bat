@echo off
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set WPF=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF

echo [1/3] Compiling Native C# WPF SysMonitor.exe ...
"%CSC%" /codepage:65001 /target:winexe /optimize+ /win32icon:app.ico /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xaml.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:"%WPF%\WindowsBase.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" /out:SysMonitor.exe src\*.cs

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Compilation failed with code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo [2/3] Verifying output binary ...
dir SysMonitor.exe

echo [3/3] Deploying SysMonitor.exe to User Desktop ...
taskkill /f /im SysMonitor.exe >nul 2>&1
ping 127.0.0.1 -n 2 >nul
copy /y SysMonitor.exe "%USERPROFILE%\Desktop\SysMonitor.exe"
if exist "refresh_icon_cache.exe" refresh_icon_cache.exe >nul 2>&1
powershell -NoProfile -Command "Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=([Environment]::GetFolderPath('Desktop') + '\SysMonitor.exe')}" >nul 2>&1

echo [SUCCESS] Built single-file SysMonitor.exe successfully!
