using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SysMonitor.Linux
{
    /// <summary>
    /// Linux 原生硬件与网络遥测采集引擎
    /// 直接读取 Linux 内核 /proc 与 /sys 虚拟文件系统，纳秒级读取，零命令调用开销
    /// </summary>
    public static class LinuxMonitors
    {
        // ==========================================
        // 1. 电池供电监测 (/sys/class/power_supply)
        // ==========================================
        public static BatterySnapshot GetBatteryStatus()
        {
            var snapshot = new BatterySnapshot
            {
                Percent = 100,
                IsCharging = true,
                IsPluggedIn = true,
                RateWatts = 0
            };

            try
            {
                const string powerSupplyPath = "/sys/class/power_supply";
                if (!Directory.Exists(powerSupplyPath))
                    return snapshot;

                // 优先查找 BAT 开头的电池目录 (BAT0, BAT1 等)
                string[] batDirs = Directory.GetDirectories(powerSupplyPath, "BAT*");
                if (batDirs.Length == 0)
                {
                    // 部分设备可能命名为 battery 或 bat_
                    batDirs = Directory.GetDirectories(powerSupplyPath, "*bat*");
                }

                if (batDirs.Length > 0)
                {
                    string bat = batDirs[0];
                    string capFile = Path.Combine(bat, "capacity");
                    string statusFile = Path.Combine(bat, "status");

                    if (File.Exists(capFile))
                    {
                        int.TryParse(File.ReadAllText(capFile).Trim(), out int cap);
                        snapshot.Percent = Math.Max(0, Math.Min(100, cap));
                    }

                    if (File.Exists(statusFile))
                    {
                        string status = File.ReadAllText(statusFile).Trim().ToLowerInvariant();
                        snapshot.IsCharging = status.Contains("charging");
                        snapshot.IsPluggedIn = status.Contains("charging") || status.Contains("full") || status.Contains("not charging");
                    }

                    // 估算充放电功率 (Watts)
                    // 检查 power_now (微瓦) 或 current_now * voltage_now
                    string powerNowFile = Path.Combine(bat, "power_now");
                    if (File.Exists(powerNowFile))
                    {
                        if (long.TryParse(File.ReadAllText(powerNowFile).Trim(), out long uWatts))
                        {
                            snapshot.RateWatts = Math.Round(uWatts / 1000000.0, 1);
                        }
                    }
                    else
                    {
                        string currFile = Path.Combine(bat, "current_now");
                        string voltFile = Path.Combine(bat, "voltage_now");
                        if (File.Exists(currFile) && File.Exists(voltFile))
                        {
                            if (long.TryParse(File.ReadAllText(currFile).Trim(), out long uAmps) &&
                                long.TryParse(File.ReadAllText(voltFile).Trim(), out long uVolts))
                            {
                                snapshot.RateWatts = Math.Round((uAmps * uVolts) / 1000000000000.0, 1);
                            }
                        }
                    }
                }
            }
            catch
            {
                // 静默容错，保留默认 AC 供电参数
            }

            return snapshot;
        }

        // ==========================================
        // 2. CPU 负载采样 (/proc/stat)
        // ==========================================
        private static long _prevCpuIdle = 0;
        private static long _prevCpuTotal = 0;

        public static double GetCpuUsage()
        {
            try
            {
                if (!File.Exists("/proc/stat")) return 0.0;

                string firstLine = File.ReadLines("/proc/stat").FirstOrDefault();
                if (string.IsNullOrEmpty(firstLine) || !firstLine.StartsWith("cpu")) return 0.0;

                // 格式: cpu user nice system idle iowait irq softirq steal guest guest_nice
                string[] parts = firstLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) return 0.0;

                long user = long.Parse(parts[1]);
                long nice = long.Parse(parts[2]);
                long system = long.Parse(parts[3]);
                long idle = long.Parse(parts[4]);
                long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;
                long irq = parts.Length > 6 ? long.Parse(parts[6]) : 0;
                long softirq = parts.Length > 7 ? long.Parse(parts[7]) : 0;
                long steal = parts.Length > 8 ? long.Parse(parts[8]) : 0;

                long currentIdle = idle + iowait;
                long currentTotal = user + nice + system + idle + iowait + irq + softirq + steal;

                long deltaIdle = currentIdle - _prevCpuIdle;
                long deltaTotal = currentTotal - _prevCpuTotal;

                _prevCpuIdle = currentIdle;
                _prevCpuTotal = currentTotal;

                if (deltaTotal <= 0) return 0.0;
                double usage = (1.0 - (double)deltaIdle / deltaTotal) * 100.0;
                return Math.Max(0.0, Math.Min(100.0, Math.Round(usage, 1)));
            }
            catch
            {
                return 0.0;
            }
        }

        // ==========================================
        // 3. 内存与交换空间采样 (/proc/meminfo)
        // ==========================================
        public static MemorySnapshot GetMemoryStatus()
        {
            var snapshot = new MemorySnapshot();
            try
            {
                if (!File.Exists("/proc/meminfo")) return snapshot;

                long totalKb = 0;
                long availKb = 0;
                long freeKb = 0;
                long buffersKb = 0;
                long cachedKb = 0;

                foreach (string line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:")) totalKb = ParseMemKb(line);
                    else if (line.StartsWith("MemAvailable:")) availKb = ParseMemKb(line);
                    else if (line.StartsWith("MemFree:")) freeKb = ParseMemKb(line);
                    else if (line.StartsWith("Buffers:")) buffersKb = ParseMemKb(line);
                    else if (line.StartsWith("Cached:")) cachedKb = ParseMemKb(line);
                }

                if (availKb == 0)
                {
                    // 内核较老未提供 MemAvailable 时采用传统估算
                    availKb = freeKb + buffersKb + cachedKb;
                }

                long usedKb = Math.Max(0, totalKb - availKb);

                snapshot.TotalMB = (int)(totalKb / 1024);
                snapshot.UsedMB = (int)(usedKb / 1024);
                snapshot.UsagePercent = totalKb > 0 ? (int)Math.Round((double)usedKb * 100.0 / totalKb) : 0;
            }
            catch
            {
            }
            return snapshot;
        }

        private static long ParseMemKb(string line)
        {
            var match = Regex.Match(line, @"\d+");
            return match.Success ? long.Parse(match.Value) : 0;
        }

        // ==========================================
        // 4. 网络实时吞吐流量 (/proc/net/dev)
        // ==========================================
        private static long _prevRxBytes = 0;
        private static long _prevTxBytes = 0;
        private static DateTime _prevNetTime = DateTime.UtcNow;

        public static NetworkRateSnapshot GetNetworkRates()
        {
            var snapshot = new NetworkRateSnapshot();
            try
            {
                if (!File.Exists("/proc/net/dev")) return snapshot;

                long totalRx = 0;
                long totalTx = 0;

                // /proc/net/dev 格式：
                // Inter-|   Receive                                                |  Transmit
                //  face |bytes    packets errs drop fifo frame compressed multicast|bytes ...
                foreach (string line in File.ReadLines("/proc/net/dev"))
                {
                    int colonIdx = line.IndexOf(':');
                    if (colonIdx < 0) continue;

                    string iface = line.Substring(0, colonIdx).Trim();
                    // 忽略回环网卡
                    if (iface == "lo") continue;

                    string statsPart = line.Substring(colonIdx + 1);
                    string[] tokens = statsPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length >= 9)
                    {
                        if (long.TryParse(tokens[0], out long rx)) totalRx += rx;
                        if (long.TryParse(tokens[8], out long tx)) totalTx += tx;
                    }
                }

                DateTime now = DateTime.UtcNow;
                double seconds = (now - _prevNetTime).TotalSeconds;
                if (seconds > 0 && _prevRxBytes > 0)
                {
                    snapshot.DownloadKBps = Math.Max(0, (totalRx - _prevRxBytes) / 1024.0 / seconds);
                    snapshot.UploadKBps = Math.Max(0, (totalTx - _prevTxBytes) / 1024.0 / seconds);
                }

                _prevRxBytes = totalRx;
                _prevTxBytes = totalTx;
                _prevNetTime = now;
            }
            catch
            {
            }
            return snapshot;
        }

        // ==========================================
        // 5. ZeroTier 本地探测与 Moon 状态 (基于标准接口)
        // ==========================================
        public static async Task<ZeroTierLocalSnapshot> GetZeroTierLocalStatusAsync()
        {
            var snapshot = new ZeroTierLocalSnapshot();
            try
            {
                string secret = GetAuthSecret();
                if (string.IsNullOrEmpty(secret))
                {
                    // 尝试以命令行兜底
                    return await GetZeroTierStatusFromCliAsync();
                }

                using (var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true })
                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromMilliseconds(1500);
                    client.DefaultRequestHeaders.Add("X-ZT1-Auth", secret);

                    // 1. 状态请求
                    var statusResp = await client.GetStringAsync("http://127.0.0.1:9993/status");
                    if (!string.IsNullOrEmpty(statusResp))
                    {
                        snapshot.IsRunning = true;
                        // 从 JSON 提取 address / version
                        var addrMatch = Regex.Match(statusResp, "\"address\"\\s*:\\s*\"([^\"]+)\"");
                        if (addrMatch.Success) snapshot.NodeId = addrMatch.Groups[1].Value;
                    }

                    // 2. Peers 拓扑
                    var peerResp = await client.GetStringAsync("http://127.0.0.1:9993/peer");
                    if (!string.IsNullOrEmpty(peerResp))
                    {
                        // 寻找 role == "MOON" 的节点
                        var moonMatches = Regex.Matches(peerResp, "\"role\"\\s*:\\s*\"MOON\"[^}]*\"latency\"\\s*:\\s*(-?\\d+)");
                        snapshot.MoonCount = moonMatches.Count;
                        if (moonMatches.Count > 0)
                        {
                            int minLat = 9999;
                            foreach (Match m in moonMatches)
                            {
                                if (int.TryParse(m.Groups[1].Value, out int lat) && lat >= 0)
                                {
                                    if (lat < minLat) minLat = lat;
                                }
                            }
                            snapshot.MinMoonLatency = minLat < 9999 ? minLat : -1;
                        }
                    }
                }
            }
            catch
            {
                // 静默
            }
            return snapshot;
        }

        private static string GetAuthSecret()
        {
            string[] searchPaths = new[]
            {
                "/var/lib/zerotier-one/authtoken.secret",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zeroTierOneAuthToken")
            };

            foreach (string p in searchPaths)
            {
                try
                {
                    if (File.Exists(p))
                    {
                        return File.ReadAllText(p).Trim();
                    }
                }
                catch { }
            }
            return null;
        }

        private static async Task<ZeroTierLocalSnapshot> GetZeroTierStatusFromCliAsync()
        {
            var snapshot = new ZeroTierLocalSnapshot();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "zerotier-cli",
                    Arguments = "-j status",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        string outStr = await proc.StandardOutput.ReadToEndAsync();
                        proc.WaitForExit(1000);
                        var addrMatch = Regex.Match(outStr, "\"address\"\\s*:\\s*\"([^\"]+)\"");
                        if (addrMatch.Success)
                        {
                            snapshot.IsRunning = true;
                            snapshot.NodeId = addrMatch.Groups[1].Value;
                        }
                    }
                }
            }
            catch { }
            return snapshot;
        }
    }

    public class BatterySnapshot
    {
        public int Percent { get; set; }
        public bool IsCharging { get; set; }
        public bool IsPluggedIn { get; set; }
        public double RateWatts { get; set; }
    }

    public class MemorySnapshot
    {
        public int TotalMB { get; set; }
        public int UsedMB { get; set; }
        public int UsagePercent { get; set; }
    }

    public class NetworkRateSnapshot
    {
        public double DownloadKBps { get; set; }
        public double UploadKBps { get; set; }
    }

    public class ZeroTierLocalSnapshot
    {
        public bool IsRunning { get; set; }
        public string NodeId { get; set; }
        public int MoonCount { get; set; }
        public int MinMoonLatency { get; set; } = -1;
    }
}
