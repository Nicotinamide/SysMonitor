using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SysMonitor.Linux
{
    /// <summary>
    /// Linux 原生硬件与网络遥测采集引擎
    /// 直接读取 Linux 内核 /proc 与 /sys 虚拟文件系统，纳秒级读取，零命令调用开销；同时跨平台支持 Windows 原生遥测
    /// </summary>
    public static class LinuxMonitors
    {
        #region Windows Native Interop
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_BATTERY_STATE
        {
            [MarshalAs(UnmanagedType.I1)] public bool AcOnLine;
            [MarshalAs(UnmanagedType.I1)] public bool BatteryPresent;
            [MarshalAs(UnmanagedType.I1)] public bool Charging;
            [MarshalAs(UnmanagedType.I1)] public bool Discharging;
            public byte Spare1, Spare2, Spare3, Spare4;
            public uint MaxCapacity;
            public uint RemainingCapacity;
            public int Rate;
            public uint EstimatedTime;
            public uint DefaultAlert1;
            public uint DefaultAlert2;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("powrprof.dll")]
        public static extern uint CallNtPowerInformation(int InformationLevel, IntPtr lpInputBuffer, uint nInputBufferSize, out SYSTEM_BATTERY_STATE lpOutputBuffer, uint nOutputBufferSize);

        [DllImport("kernel32.dll")]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS sps);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

        private static long _prevWinCpuIdle = 0;
        private static long _prevWinCpuKernel = 0;
        private static long _prevWinCpuUser = 0;
        #endregion

        // ==========================================
        // 1. 电池供电监测 (/sys/class/power_supply & Win32)
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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    GetSystemPowerStatus(out SYSTEM_POWER_STATUS sps);
                    SYSTEM_BATTERY_STATE sbs = default;
                    try { CallNtPowerInformation(5, IntPtr.Zero, 0, out sbs, (uint)Marshal.SizeOf(typeof(SYSTEM_BATTERY_STATE))); } catch { }

                    bool hasBattery = sbs.BatteryPresent && (sps.BatteryFlag != 128) && (sps.BatteryLifePercent != 255);
                    if (!hasBattery)
                    {
                        snapshot.Percent = 100;
                        snapshot.IsCharging = false;
                        snapshot.IsPluggedIn = true;
                        snapshot.RateWatts = 0;
                    }
                    else
                    {
                        snapshot.Percent = sps.BatteryLifePercent <= 100 ? sps.BatteryLifePercent : 100;
                        snapshot.IsPluggedIn = (sps.ACLineStatus == 1 || sbs.AcOnLine);
                        snapshot.IsCharging = sbs.Charging || ((sps.BatteryFlag & 8) != 0) || sbs.Rate > 0;
                        snapshot.RateWatts = Math.Round(Math.Abs(sbs.Rate) / 1000.0, 1);
                    }
                }
                catch { }
                return snapshot;
            }

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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    GetSystemTimes(out long currIdle, out long currKernel, out long currUser);
                    long sysDiff = (currKernel - _prevWinCpuKernel) + (currUser - _prevWinCpuUser);
                    long idleDiff = currIdle - _prevWinCpuIdle;
                    long busy = sysDiff - idleDiff;

                    double usage = 0.0;
                    if (sysDiff > 0 && _prevWinCpuKernel > 0)
                    {
                        usage = Math.Max(0.0, Math.Min(100.0, Math.Round((busy * 100.0) / sysDiff, 1)));
                    }

                    _prevWinCpuIdle = currIdle;
                    _prevWinCpuKernel = currKernel;
                    _prevWinCpuUser = currUser;
                    return usage;
                }
                catch { return 0.0; }
            }

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
        // 3. 内存与交换空间采样 (/proc/meminfo & Win32)
        // ==========================================
        public static MemorySnapshot GetMemoryStatus()
        {
            var snapshot = new MemorySnapshot();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var mem = new MEMORYSTATUSEX();
                    GlobalMemoryStatusEx(mem);
                    snapshot.TotalMB = (int)(mem.ullTotalPhys / (1024 * 1024));
                    snapshot.UsedMB = (int)((mem.ullTotalPhys - mem.ullAvailPhys) / (1024 * 1024));
                    snapshot.UsagePercent = (int)mem.dwMemoryLoad;
                }
                catch { }
                return snapshot;
            }

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
        // 4. 网络实时吞吐流量 (/proc/net/dev & Win32)
        // ==========================================
        private static long _prevRxBytes = 0;
        private static long _prevTxBytes = 0;
        private static DateTime _prevNetTime = DateTime.UtcNow;

        public static NetworkRateSnapshot GetNetworkRates()
        {
            var snapshot = new NetworkRateSnapshot();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    long totalRx = 0, totalTx = 0;
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                        string desc = ni.Description.ToLower();
                        string name = ni.Name.ToLower();
                        if (desc.Contains("zerotier") || desc.Contains("virtual") || desc.Contains("vpn") || desc.Contains("pseudo") ||
                            desc.Contains("mihomo") || desc.Contains("clash") || desc.Contains("tun") || desc.Contains("tap") || desc.Contains("wsl") ||
                            name.Contains("mihomo") || name.Contains("clash") || name.Contains("zerotier") || name.Contains("vethernet"))
                            continue;
                        var s = ni.GetIPStatistics();
                        totalRx += s.BytesReceived;
                        totalTx += s.BytesSent;
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
                catch { }
                return snapshot;
            }

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

                    int port = GetZeroTierPort();

                    // 1. 状态请求
                    var statusResp = await client.GetStringAsync($"http://127.0.0.1:{port}/status");
                    if (!string.IsNullOrEmpty(statusResp))
                    {
                        snapshot.IsRunning = true;
                        // 从 JSON 提取 address / version
                        var addrMatch = Regex.Match(statusResp, "\"address\"\\s*:\\s*\"([^\"]+)\"");
                        if (addrMatch.Success) snapshot.NodeId = addrMatch.Groups[1].Value;
                    }

                    // 2. Peers 拓扑
                    var peerResp = await client.GetStringAsync($"http://127.0.0.1:{port}/peer");
                    if (!string.IsNullOrEmpty(peerResp))
                    {
                        var peerMatches = Regex.Matches(peerResp, "\"address\"\\s*:\\s*\"([^\"]+)\"");
                        snapshot.PeerCount = peerMatches.Count;

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

        private static int GetZeroTierPort()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string[] portFiles = new[]
            {
                "/var/lib/zerotier-one/zerotier-one.port",
                @"C:\ProgramData\ZeroTier\One\zerotier-one.port",
                Path.Combine(commonAppData, "ZeroTier", "One", "zerotier-one.port"),
                Path.Combine(localAppData, "ZeroTier", "zerotier-one.port"),
                Path.Combine(localAppData, "ZeroTier", "One", "zerotier-one.port")
            };
            foreach (var pf in portFiles)
            {
                if (File.Exists(pf))
                {
                    try
                    {
                        string txt = File.ReadAllText(pf).Trim();
                        if (int.TryParse(txt, out int port) && port > 0) return port;
                    }
                    catch { }
                }
            }
            return 9993;
        }

        private static string GetAuthSecret()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            string[] searchPaths = new[]
            {
                "/var/lib/zerotier-one/authtoken.secret",
                Path.Combine(userProfile, ".zeroTierOneAuthToken"),
                @"C:\ProgramData\ZeroTier\One\authtoken.secret",
                Path.Combine(commonAppData, "ZeroTier", "One", "authtoken.secret"),
                Path.Combine(localAppData, "ZeroTier", "authtoken.secret"),
                Path.Combine(localAppData, "ZeroTier", "One", "authtoken.secret")
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
        public double UsedGB => Math.Round(UsedMB / 1024.0, 1);
        public double TotalGB => Math.Round(TotalMB / 1024.0, 1);
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
        public int PeerCount { get; set; }
        public int MoonCount { get; set; }
        public int MinMoonLatency { get; set; } = -1;
    }
}
