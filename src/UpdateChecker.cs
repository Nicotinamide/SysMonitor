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
        public const string CurrentVersion = "v1.0.2";
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
                    callback(info);
                }
            });
        }

        private static UpdateInfo CheckForUpdatesInternal()
        {
            UpdateInfo info = new UpdateInfo();
            try
            {
                // 1. 先尝试查询 GitHub Releases API
                string releaseApi = string.Format("https://api.github.com/repos/{0}/{1}/releases/latest", RepoOwner, RepoName);
                int statusCode;
                string json = FetchGitHubJson(releaseApi, out statusCode);

                if (statusCode == 200 && !string.IsNullOrEmpty(json))
                {
                    ParseReleaseJson(json, info);
                    return info;
                }

                // 2. 若暂无 Release (404)，则查询 main 分支的最新 Commit
                string commitApi = string.Format("https://api.github.com/repos/{0}/{1}/commits/main", RepoOwner, RepoName);
                string commitJson = FetchGitHubJson(commitApi, out statusCode);

                if (statusCode == 200 && !string.IsNullOrEmpty(commitJson))
                {
                    ParseCommitJson(commitJson, info);
                    return info;
                }

                info.Success = false;
                info.ErrorMessage = "GitHub API response: " + statusCode;
            }
            catch (Exception ex)
            {
                info.Success = false;
                info.ErrorMessage = ex.Message;
            }
            return info;
        }

        private static string FetchGitHubJson(string url, out int statusCode)
        {
            statusCode = 0;
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "SysMonitor-Client/" + CurrentVersion;
                req.Accept = "application/vnd.github.v3+json";
                req.Timeout = 5000;
                req.ReadWriteTimeout = 5000;

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    statusCode = (int)resp.StatusCode;
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (WebException wex)
            {
                HttpWebResponse errResp = wex.Response as HttpWebResponse;
                if (errResp != null)
                {
                    statusCode = (int)errResp.StatusCode;
                }
                return null;
            }
            catch
            {
                return null;
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

            // 提取匹配当前系统的下载链接 (Windows: .exe / .zip; Linux: .tar.gz)
            string searchPattern = Environment.OSVersion.Platform == PlatformID.Win32NT
                ? "\"browser_download_url\"\\s*:\\s*\"([^\"]*(\\.exe|windows[^\"]*\\.zip))\""
                : "\"browser_download_url\"\\s*:\\s*\"([^\"]*linux[^\"]*\\.tar\\.gz)\"";

            var dlMatch = Regex.Match(json, searchPattern, RegexOptions.IgnoreCase);
            if (dlMatch.Success)
            {
                info.DownloadUrl = dlMatch.Groups[1].Value;
            }

            // 判断是否有新版本 (例如 v1.0.1 vs v1.0.0)
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

                    string tempPath = Path.Combine(Path.GetTempPath(), "SysMonitor_Latest.exe");
                    if (File.Exists(tempPath)) File.Delete(tempPath);

                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "SysMonitor-Updater");
                        client.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
                        {
                            if (progressCallback != null) progressCallback(e.ProgressPercentage);
                        };

                        client.DownloadFile(new Uri(downloadUrl), tempPath);
                    }

                    // 替换并重启
                    string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                    int currentPid = Process.GetCurrentProcess().Id;
                    string batchScript = Path.Combine(Path.GetTempPath(), "sysmonitor_updater.bat");

                    string batContent = string.Format(
@"@echo off
rem 1. 强制终止旧进程以释放文件锁定
taskkill /F /PID {2} >nul 2>&1
timeout /t 1 /nobreak >nul

rem 2. 重试循环覆盖文件（防止系统缓存或杀软瞬时占用）
set RETRIES=0
:RETRY_LOOP
copy /y ""{0}"" ""{1}"" >nul 2>&1
if %ERRORLEVEL% EQU 0 goto SUCCESS

set /a RETRIES+=1
if %RETRIES% LEQ 15 (
    timeout /t 1 /nobreak >nul
    goto RETRY_LOOP
)

:SUCCESS
del ""{0}"" >nul 2>&1
start """" ""{1}""
del ""%~f0""
", tempPath, currentExe, currentPid);

                    File.WriteAllText(batchScript, batContent, Encoding.Default);

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
                    if (finishCallback != null) finishCallback(false, ex.Message);
                }
            });
        }
    }
}
