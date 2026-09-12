using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SysMonitor
{
    public class UpdateInfo
    {
        public bool Success { get; set; }
        public bool HasUpdate { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleasePageUrl { get; set; }
        public string LatestCommitSha { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsCommitBased { get; set; }

        public UpdateInfo()
        {
            CurrentVersion = UpdateChecker.CurrentVersion;
            LatestVersion = "";
            ReleaseNotes = "";
            DownloadUrl = "";
            ReleasePageUrl = "https://github.com/" + UpdateChecker.RepoOwner + "/" + UpdateChecker.RepoName + "/releases";
            LatestCommitSha = "";
            ErrorMessage = "";
        }
    }

    public static class UpdateChecker
    {
        public const string CurrentVersion = "v1.0.16";
        public const string RepoOwner = "Nicotinamide";
        public const string RepoName = "SysMonitor";

        private static bool _isAvaloniaExplicit = false;
        private static bool _isAvalonia = false;

        public static bool IsAvalonia
        {
            get
            {
                if (!_isAvaloniaExplicit)
                {
                    try
                    {
                        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                        {
                            return true;
                        }
                        return Environment.Version.Major >= 5 || Type.GetType("Avalonia.Application, Avalonia") != null;
                    }
                    catch
                    {
                        return false;
                    }
                }
                return _isAvalonia;
            }
            set
            {
                _isAvalonia = value;
                _isAvaloniaExplicit = true;
            }
        }

        public static string EditionName
        {
            get
            {
                if (IsAvalonia)
                {
                    return Environment.OSVersion.Platform == PlatformID.Win32NT ? "Avalonia (Win)" : "Avalonia (Linux)";
                }
                return "WPF Native (480KB)";
            }
        }

        static UpdateChecker()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 /* Tls12 */ | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch { }
        }

        private static void Log(string msg)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysMonitor");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string logFile = Path.Combine(dir, "debug.log");
                File.AppendAllText(logFile, string.Format("[{0:HH:mm:ss.fff}] [Updater] {1}\r\n", DateTime.Now, msg));
            }
            catch { }
        }

        /// <summary>
        /// 异步检查 GitHub 上的最新版本状态（优先检查 Releases，未发 Release 时查询 main 分支最新 Commit）
        /// </summary>
        public static void CheckForUpdatesAsync(Action<UpdateInfo> callback)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                UpdateInfo info = CheckForUpdatesInternal();
                if (callback != null)
                {
                    try { callback(info); } catch { }
                }
            });
        }

        private static UpdateInfo CheckForUpdatesInternal()
        {
            UpdateInfo info = new UpdateInfo();
            try
            {
                // 1. 优先尝试从 GitHub Releases 获取正式发版
                string relUrl = string.Format("https://api.github.com/repos/{0}/{1}/releases", RepoOwner, RepoName);
                string relJson = HttpGet(relUrl);

                if (!string.IsNullOrEmpty(relJson) && relJson.TrimStart().StartsWith("["))
                {
                    // 检查是否包含任何发版
                    if (relJson.Contains("\"tag_name\""))
                    {
                        ParseReleaseJson(relJson, info);
                        return info;
                    }
                }

                // 2. 如果尚无正式 Release，则获取 main 分支最新的 commit 信息
                string commitUrl = string.Format("https://api.github.com/repos/{0}/{1}/commits/main", RepoOwner, RepoName);
                string commitJson = HttpGet(commitUrl);

                if (!string.IsNullOrEmpty(commitJson) && commitJson.Contains("\"sha\""))
                {
                    ParseCommitJson(commitJson, info);
                    return info;
                }

                info.Success = false;
                info.ErrorMessage = "未找到可用版本信息";
            }
            catch (Exception ex)
            {
                info.Success = false;
                info.ErrorMessage = ex.Message;
            }
            return info;
        }

        private static string HttpGet(string url)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "SysMonitor-App");
                client.Headers.Add("Accept", "application/vnd.github.v3+json");
                return client.DownloadString(url);
            }
        }

        private static void ParseReleaseJson(string json, UpdateInfo info)
        {
            info.Success = true;
            info.IsCommitBased = false;

            // 提取 tag_name
            var tagMatch = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (tagMatch.Success) info.LatestVersion = tagMatch.Groups[1].Value.Trim();

            // 提取 html_url
            var urlMatch = Regex.Match(json, "\"html_url\"\\s*:\\s*\"([^\"]+)\"");
            if (urlMatch.Success) info.ReleasePageUrl = urlMatch.Groups[1].Value.Trim();

            // 提取更新日志 body
            var bodyMatch = Regex.Match(json, "\"body\"\\s*:\\s*\"([^\"]*)\"");
            if (bodyMatch.Success)
            {
                string rawBody = bodyMatch.Groups[1].Value;
                info.ReleaseNotes = Regex.Unescape(rawBody);
            }

            // 提取匹配当前系统与版本架构的下载链接 (严格物理隔离 Native WPF 与 Avalonia 跨平台版)
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (IsAvalonia)
                {
                    // Windows 下运行 Avalonia 现代跨平台版：精准匹配 Avalonia 资产
                    Match avaExeMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*avalonia[^\"]*\\.exe)\"", RegexOptions.IgnoreCase);
                    Match avaZipMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*avalonia[^\"]*\\.zip)\"", RegexOptions.IgnoreCase);

                    if (avaExeMatch.Success)
                    {
                        info.DownloadUrl = avaExeMatch.Groups[1].Value;
                    }
                    else if (avaZipMatch.Success)
                    {
                        info.DownloadUrl = avaZipMatch.Groups[1].Value;
                    }
                }
                else
                {
                    // Windows 下运行原生极简 WPF 版 (480KB)：精准匹配原生独立 EXE (SysMonitor.exe) 或 wpf.zip
                    Match wpfExeMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*SysMonitor\\.exe)\"", RegexOptions.IgnoreCase);
                    Match wpfZipMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*wpf[^\"]*\\.zip)\"", RegexOptions.IgnoreCase);

                    if (wpfExeMatch.Success)
                    {
                        info.DownloadUrl = wpfExeMatch.Groups[1].Value;
                    }
                    else if (wpfZipMatch.Success)
                    {
                        info.DownloadUrl = wpfZipMatch.Groups[1].Value;
                    }
                    else
                    {
                        // 兜底备选普通 exe（排除 avalonia 命名）
                        Match anyExeMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*(?<!avalonia)\\.exe)\"", RegexOptions.IgnoreCase);
                        if (anyExeMatch.Success)
                        {
                            info.DownloadUrl = anyExeMatch.Groups[1].Value;
                        }
                    }
                }
            }
            else
            {
                // Linux: 始终为 Avalonia，检测 CPU 架构以精准匹配 arm64 或 x64
                bool isArm64 = false;
                try
                {
                    if (File.Exists("/proc/cpuinfo"))
                    {
                        string cpuinfo = File.ReadAllText("/proc/cpuinfo");
                        if (cpuinfo.IndexOf("aarch64", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            cpuinfo.IndexOf("ARM", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isArm64 = true;
                        }
                    }
                }
                catch { }

                string pattern = isArm64
                    ? "\"browser_download_url\"\\s*:\\s*\"([^\"]*linux[^\"]*arm64[^\"]*\\.tar\\.gz)\""
                    : "\"browser_download_url\"\\s*:\\s*\"([^\"]*linux[^\"]*x64[^\"]*\\.tar\\.gz)\"";

                Match linuxMatch = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
                if (!linuxMatch.Success)
                {
                    linuxMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*linux[^\"]*\\.tar\\.gz)\"", RegexOptions.IgnoreCase);
                }
                if (linuxMatch.Success)
                {
                    info.DownloadUrl = linuxMatch.Groups[1].Value;
                }
            }

            // 判断是否有新版本 (例如 v1.0.3 vs v1.0.2)
            if (!string.IsNullOrEmpty(info.LatestVersion))
            {
                info.HasUpdate = CompareVersions(info.LatestVersion, CurrentVersion) > 0;
            }
        }

        private static void ParseCommitJson(string json, UpdateInfo info)
        {
            info.Success = true;
            info.IsCommitBased = true;

            // 提取 commit sha
            var shaMatch = Regex.Match(json, "\"sha\"\\s*:\\s*\"([^\"]+)\"");
            if (shaMatch.Success)
            {
                string fullSha = shaMatch.Groups[1].Value;
                info.LatestCommitSha = fullSha.Length > 7 ? fullSha.Substring(0, 7) : fullSha;
                info.LatestVersion = "main:" + info.LatestCommitSha;
            }

            // 提取 commit message
            var msgMatch = Regex.Match(json, "\"message\"\\s*:\\s*\"([^\"]+)\"");
            if (msgMatch.Success)
            {
                info.ReleaseNotes = Regex.Unescape(msgMatch.Groups[1].Value);
            }

            info.ReleasePageUrl = string.Format("https://github.com/{0}/{1}/tree/main", RepoOwner, RepoName);
            info.HasUpdate = false; // Commit 阶段不强制标记升级，展示最新提交状态
        }

        private static int CompareVersions(string vA, string vB)
        {
            try
            {
                string cleanA = Regex.Replace(vA, @"[^\d\.]", "");
                string cleanB = Regex.Replace(vB, @"[^\d\.]", "");
                Version verA = new Version(cleanA);
                Version verB = new Version(cleanB);
                return verA.CompareTo(verB);
            }
            catch
            {
                return string.Compare(vA, vB, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 执行拉取更新（下载并热替换重启）
        /// </summary>
        public static void DownloadAndApplyUpdateAsync(string downloadUrl, Action<int> progressCallback, Action<bool, string> finishCallback)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        if (finishCallback != null) finishCallback(false, "下载链接为空");
                        return;
                    }

                    Log("Starting update download from: " + downloadUrl);

                    string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                    int currentPid = Process.GetCurrentProcess().Id;
                    string currentDir = Path.GetDirectoryName(currentExe);

                    // ==========================================
                    // 1. Linux 平台下载、解压与热替换更新流程
                    // ==========================================
                    if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    {
                        string tempTarPath = Path.Combine(Path.GetTempPath(), "SysMonitor_Update_" + Guid.NewGuid().ToString("N") + ".tar.gz");
                        string extractDir = Path.Combine(Path.GetTempPath(), "SysMonitor_Extract_" + Guid.NewGuid().ToString("N"));

                        using (WebClient client = new WebClient())
                        {
                            client.Headers.Add("User-Agent", "SysMonitor-Updater");
                            client.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
                            {
                                if (progressCallback != null) progressCallback(e.ProgressPercentage);
                            };
                            client.DownloadFile(new Uri(downloadUrl), tempTarPath);
                        }

                        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                        Directory.CreateDirectory(extractDir);

                        // 解压 tar.gz
                        ProcessStartInfo tarPsi = new ProcessStartInfo
                        {
                            FileName = "tar",
                            Arguments = string.Format("-xzf \"{0}\" -C \"{1}\"", tempTarPath, extractDir),
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using (Process p = Process.Start(tarPsi))
                        {
                            if (p != null) p.WaitForExit(30000);
                        }

                        string foundBin = Path.Combine(extractDir, "sysmonitor");
                        if (!File.Exists(foundBin))
                        {
                            string[] bins = Directory.GetFiles(extractDir, "sysmonitor", SearchOption.AllDirectories);
                            if (bins.Length > 0) foundBin = bins[0];
                        }

                        if (!File.Exists(foundBin))
                        {
                            if (finishCallback != null) finishCallback(false, "压缩包中未找到 sysmonitor 执行程序");
                            return;
                        }

                        // 授予新可执行文件执行权限
                        try
                        {
                            ProcessStartInfo chmodPsi = new ProcessStartInfo("chmod", string.Format("+x \"{0}\"", foundBin))
                            {
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };
                            using (Process p = Process.Start(chmodPsi))
                            {
                                if (p != null) p.WaitForExit(2000);
                            }
                        }
                        catch { }

                        // 检查当前程序目录写权限
                        bool hasWritePermission = false;
                        try
                        {
                            string testFile = Path.Combine(currentDir, ".perm_test_" + Guid.NewGuid().ToString("N"));
                            File.WriteAllText(testFile, "test");
                            File.Delete(testFile);
                            hasWritePermission = true;
                        }
                        catch
                        {
                            hasWritePermission = false;
                        }

                        if (!hasWritePermission)
                        {
                            string errMsg = string.Format("当前安装目录 ({0}) 需要管理员权限。\n更新文件已下载至: {1}\n请使用 sudo 复制替换，或通过包管理器 (pacman/yay) 升级。", currentDir, foundBin);
                            Log(errMsg);
                            if (finishCallback != null) finishCallback(false, errMsg);
                            return;
                        }

                        // Linux 运行中可执行文件写保护 (ETXTBSY) 绕过：先重命名旧文件再覆盖
                        string oldBackup = currentExe + ".old";
                        try
                        {
                            if (File.Exists(oldBackup)) File.Delete(oldBackup);
                            File.Move(currentExe, oldBackup);
                        }
                        catch
                        {
                            try { File.Delete(currentExe); } catch { }
                        }

                        File.Copy(foundBin, currentExe, true);

                        // 确保目标程序具有执行权限
                        try
                        {
                            ProcessStartInfo chmodPsi = new ProcessStartInfo("chmod", string.Format("+x \"{0}\"", currentExe))
                            {
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };
                            using (Process p = Process.Start(chmodPsi))
                            {
                                if (p != null) p.WaitForExit(2000);
                            }
                        }
                        catch { }

                        try { File.Delete(tempTarPath); } catch { }
                        try { Directory.Delete(extractDir, true); } catch { }

                        Log("Linux update applied successfully. Restarting: " + currentExe);
                        if (finishCallback != null) finishCallback(true, "下载完成，正在重启更新...");

                        // 创建重启启动脚本
                        string restartSh = Path.Combine(Path.GetTempPath(), "sysmonitor_restart.sh");
                        string shContent = string.Format("#!/bin/sh\nsleep 0.6\n\"{0}\" &\nrm -f \"$0\"\n", currentExe);
                        File.WriteAllText(restartSh, shContent);
                        try
                        {
                            using (Process p = Process.Start("chmod", string.Format("+x \"{0}\"", restartSh)))
                            {
                                if (p != null) p.WaitForExit(2000);
                            }
                        }
                        catch { }

                        ProcessStartInfo rPsi = new ProcessStartInfo("/bin/sh", string.Format("\"{0}\"", restartSh))
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(rPsi);

                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            Thread.Sleep(300);
                            Environment.Exit(0);
                        });
                        return;
                    }

                    // ==========================================
                    // 2. Windows 平台下载与热替换更新流程
                    // ==========================================
                    string tempExePath = Path.Combine(Path.GetTempPath(), "SysMonitor_Latest.exe");
                    if (File.Exists(tempExePath))
                    {
                        try { File.Delete(tempExePath); } catch { }
                    }

                    bool isZip = downloadUrl.IndexOf(".zip", StringComparison.OrdinalIgnoreCase) >= 0;
                    string downloadTarget = isZip 
                        ? Path.Combine(Path.GetTempPath(), "SysMonitor_Update_" + Guid.NewGuid().ToString("N") + ".zip") 
                        : tempExePath;

                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "SysMonitor-Updater");
                        client.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
                        {
                            if (progressCallback != null) progressCallback(e.ProgressPercentage);
                        };

                        client.DownloadFile(new Uri(downloadUrl), downloadTarget);
                    }

                    if (isZip)
                    {
                        Log("Extracting zip archive: " + downloadTarget);
                        string extractDir = Path.Combine(Path.GetTempPath(), "SysMonitor_Extract_" + Guid.NewGuid().ToString("N"));
                        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                        Directory.CreateDirectory(extractDir);

                        ProcessStartInfo unzipPsi = new ProcessStartInfo
                        {
                            FileName = "powershell",
                            Arguments = string.Format("-NoProfile -Command \"Expand-Archive -Path '{0}' -DestinationPath '{1}' -Force\"", downloadTarget, extractDir),
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using (Process p = Process.Start(unzipPsi))
                        {
                            if (p != null) p.WaitForExit(30000);
                        }

                        string foundExe = null;
                        if (IsAvalonia)
                        {
                            string[] avaExes = Directory.GetFiles(extractDir, "*avalonia*.exe", SearchOption.AllDirectories);
                            if (avaExes.Length > 0) foundExe = avaExes[0];
                        }
                        else
                        {
                            string wpfExe = Path.Combine(extractDir, "SysMonitor.exe");
                            if (File.Exists(wpfExe)) foundExe = wpfExe;
                        }

                        if (string.IsNullOrEmpty(foundExe) || !File.Exists(foundExe))
                        {
                            string[] exes = Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories);
                            if (exes.Length > 0) foundExe = exes[0];
                        }

                        if (string.IsNullOrEmpty(foundExe) || !File.Exists(foundExe))
                        {
                            if (finishCallback != null) finishCallback(false, "压缩包中未找到匹配的可执行程序");
                            return;
                        }

                        File.Copy(foundExe, tempExePath, true);
                        try { File.Delete(downloadTarget); } catch { }
                        try { Directory.Delete(extractDir, true); } catch { }
                    }

                    // 验证下载文件有效性 (正常单文件 EXE 约 400KB~2MB)
                    FileInfo fi = new FileInfo(tempExePath);
                    if (!fi.Exists || fi.Length < 50000)
                    {
                        Log("Downloaded file invalid or too small: " + (fi.Exists ? fi.Length.ToString() : "not found"));
                        if (finishCallback != null) finishCallback(false, "下载文件不完整或损坏");
                        return;
                    }

                    Log("Update file ready (" + fi.Length + " bytes). Preparing updater batch...");

                    // 替换并重启
                    string batchScript = Path.Combine(Path.GetTempPath(), "sysmonitor_updater.bat");

                    string batContent = string.Format(
@"@echo off
rem 1. 强制终止旧进程以释放文件锁定 (CreateNoWindow已静默，绝不使用 >nul 避免触发Windows安全中心拦截)
taskkill /F /PID {2}
timeout /t 1 /nobreak

rem 2. 重试循环覆盖文件（防止系统缓存或杀软瞬时占用）
set RETRIES=0
:RETRY_LOOP
copy /y ""{0}"" ""{1}""
if %ERRORLEVEL% EQU 0 goto SUCCESS

set /a RETRIES+=1
if %RETRIES% LEQ 20 (
    timeout /t 1 /nobreak
    goto RETRY_LOOP
)
exit /b 1

:SUCCESS
del ""{0}""
start """" /d ""{3}"" ""{1}""
del ""%~f0""
", tempExePath, currentExe, currentPid, currentDir);

                    File.WriteAllText(batchScript, batContent, Encoding.Default);

                    Log("Starting updater batch script: " + batchScript);

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = batchScript,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi);

                    if (finishCallback != null) finishCallback(true, "下载完成，正在重启更新...");

                    // 主动退出当前进程，释放对 EXE 文件的独占锁定
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        Thread.Sleep(300);
                        Environment.Exit(0);
                    });
                }
                catch (Exception ex)
                {
                    Log("Update error: " + ex.ToString());
                    if (finishCallback != null) finishCallback(false, ex.Message);
                }
            });
        }
    }
}
