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
        public const string CurrentVersion = "v1.0.5";
        public const string RepoOwner = "Nicotinamide";
        public const string RepoName = "SysMonitor";

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

            // 提取匹配当前系统的下载链接
            // Windows: 绝对优先匹配独立 EXE (SysMonitor.exe)，次选 windows*.zip 避免把压缩包直接当可执行程序下载
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var exeMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*SysMonitor\\.exe)\"", RegexOptions.IgnoreCase);
                if (!exeMatch.Success)
                {
                    exeMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*\\.exe)\"", RegexOptions.IgnoreCase);
                }

                if (exeMatch.Success)
                {
                    info.DownloadUrl = exeMatch.Groups[1].Value;
                }
                else
                {
                    var zipMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*windows[^\"]*\\.zip)\"", RegexOptions.IgnoreCase);
                    if (zipMatch.Success)
                    {
                        info.DownloadUrl = zipMatch.Groups[1].Value;
                    }
                }
            }
            else
            {
                var linuxMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*linux[^\"]*\\.tar\\.gz)\"", RegexOptions.IgnoreCase);
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

                        string foundExe = Path.Combine(extractDir, "SysMonitor.exe");
                        if (!File.Exists(foundExe))
                        {
                            string[] exes = Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories);
                            if (exes.Length > 0) foundExe = exes[0];
                        }

                        if (!File.Exists(foundExe))
                        {
                            if (finishCallback != null) finishCallback(false, "压缩包中未找到 SysMonitor.exe");
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
                    string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                    int currentPid = Process.GetCurrentProcess().Id;
                    string currentDir = Path.GetDirectoryName(currentExe);
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
