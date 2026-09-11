using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysMonitor.Linux
{
    public static class LinuxProgram
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            bool statusMode = false;
            bool daemonMode = false;

            foreach (var arg in args)
            {
                if (arg == "--status" || arg == "-s") statusMode = true;
                else if (arg == "--daemon" || arg == "-d") daemonMode = true;
                else if (arg == "--help" || arg == "-h")
                {
                    PrintHelp();
                    return;
                }
            }

            if (statusMode)
            {
                PrintStatusOnce();
                return;
            }

            if (daemonMode)
            {
                RunDaemon();
                return;
            }

            // 默认或 --watch 模式进入实时终端监视看板
            RunWatchDashboard();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("SysMonitor - Cross-Platform Native Telemetry & ZeroTier Monitor (Linux Edition)");
            Console.WriteLine("Usage: sysmonitor [options]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  -w, --watch     Run live interactive terminal dashboard (default)");
            Console.WriteLine("  -s, --status    Print one-shot JSON snapshot of hardware & ZeroTier status");
            Console.WriteLine("  -d, --daemon    Run as background telemetry daemon (suitable for systemd)");
            Console.WriteLine("  -h, --help      Show this help message");
            Console.WriteLine("");
            Console.WriteLine("GitHub: https://github.com/Nicotinamide/SysMonitor");
        }

        private static void PrintStatusOnce()
        {
            double cpu = LinuxMonitors.GetCpuUsage();
            MemorySnapshot mem = LinuxMonitors.GetMemoryStatus();
            NetworkRateSnapshot net = LinuxMonitors.GetNetworkRates();
            BatterySnapshot bat = LinuxMonitors.GetBatteryStatus();
            ZeroTierLocalSnapshot zt = LinuxMonitors.GetZeroTierLocalStatusAsync().GetAwaiter().GetResult();

            string json = string.Format(
                "{{\"cpu\":{0:F1},\"ram_used_mb\":{1},\"ram_total_mb\":{2},\"ram_pct\":{3},\"net_down_kb\":{4:F1},\"net_up_kb\":{5:F1},\"battery\":{6},\"charging\":{7},\"watts\":{8:F1},\"zt_running\":{9},\"zt_node\":\"{10}\",\"moon_count\":{11},\"moon_ms\":{12}}}",
                cpu, mem.UsedMB, mem.TotalMB, mem.UsagePercent,
                net.DownloadKBps, net.UploadKBps,
                bat.Percent, bat.IsCharging ? "true" : "false", bat.RateWatts,
                zt.IsRunning ? "true" : "false", zt.NodeId ?? "",
                zt.MoonCount, zt.MinMoonLatency);

            Console.WriteLine(json);
        }

        private static void RunDaemon()
        {
            Console.WriteLine("[INFO] SysMonitor Linux Daemon started. Press Ctrl+C to stop.");
            while (true)
            {
                try
                {
                    LinuxMonitors.GetCpuUsage();
                    LinuxMonitors.GetNetworkRates();
                    LinuxMonitors.GetZeroTierLocalStatusAsync().GetAwaiter().GetResult();
                }
                catch { }
                Thread.Sleep(2000);
            }
        }

        private static void RunWatchDashboard()
        {
            Console.CursorVisible = false;
            Console.CancelKeyPress += delegate { Console.CursorVisible = true; };

            // 初次采样初始化差值
            LinuxMonitors.GetCpuUsage();
            LinuxMonitors.GetNetworkRates();
            Thread.Sleep(500);

            while (true)
            {
                double cpu = LinuxMonitors.GetCpuUsage();
                MemorySnapshot mem = LinuxMonitors.GetMemoryStatus();
                NetworkRateSnapshot net = LinuxMonitors.GetNetworkRates();
                BatterySnapshot bat = LinuxMonitors.GetBatteryStatus();
                ZeroTierLocalSnapshot zt = LinuxMonitors.GetZeroTierLocalStatusAsync().GetAwaiter().GetResult();

                Console.Clear();
                Console.WriteLine("\u001b[1;36m===========================================================\u001b[0m");
                Console.WriteLine("\u001b[1;37m   SysMonitor - Linux Native Telemetry Dashboard           \u001b[0m");
                Console.WriteLine("\u001b[1;36m===========================================================\u001b[0m");
                Console.WriteLine();

                // 算力负载
                string cpuColor = cpu > 80 ? "\u001b[1;31m" : (cpu > 50 ? "\u001b[1;33m" : "\u001b[1;32m");
                Console.WriteLine(" [CPU] 核心使用率: {0}{1:F1}%\u001b[0m", cpuColor, cpu);
                
                string memColor = mem.UsagePercent > 85 ? "\u001b[1;31m" : "\u001b[1;32m";
                double usedGb = mem.UsedMB / 1024.0;
                double totalGb = mem.TotalMB / 1024.0;
                Console.WriteLine(" [RAM] 物理内存:   {0}{1:F1} GB / {2:F1} GB ({3}%)\u001b[0m", memColor, usedGb, totalGb, mem.UsagePercent);
                Console.WriteLine();

                // 网络吞吐
                string downFmt = net.DownloadKBps > 1024 ? string.Format("{0:F1} MB/s", net.DownloadKBps / 1024.0) : string.Format("{0:F0} KB/s", net.DownloadKBps);
                string upFmt = net.UploadKBps > 1024 ? string.Format("{0:F1} MB/s", net.UploadKBps / 1024.0) : string.Format("{0:F0} KB/s", net.UploadKBps);
                Console.WriteLine(" [NET] 实时吞吐:   \u001b[1;34m⬇ {0}\u001b[0m   \u001b[1;35m⬆ {1}\u001b[0m", downFmt, upFmt);
                Console.WriteLine();

                // 供电状态
                string pwrState = bat.IsCharging ? "⚡ 正在充电" : (bat.IsPluggedIn ? "🔌 已接通电源" : "🔋 电池放电中");
                Console.WriteLine(" [PWR] 电源状态:   {0} ({1}%) - {2:F1} W", pwrState, bat.Percent, bat.RateWatts);
                Console.WriteLine();

                // 局域网络与 Moon
                string ztStatus = zt.IsRunning ? "\u001b[1;32m● 在线运行\u001b[0m" : "\u001b[1;31m○ 未运行\u001b[0m";
                Console.WriteLine(" [ZT]  ZeroTier:   {0} (Node: \u001b[1;37m{1}\u001b[0m)", ztStatus, zt.NodeId ?? "N/A");
                
                string moonStatus = zt.MoonCount > 0
                    ? string.Format("\u001b[1;32m● 直连良好 ({0} ms)\u001b[0m", zt.MinMoonLatency >= 0 ? zt.MinMoonLatency.ToString() : "N/A")
                    : "\u001b[1;33m○ 探测中或中继\u001b[0m";
                Console.WriteLine(" [ZT]  Moon 轨道:  {0}", moonStatus);

                Console.WriteLine();
                Console.WriteLine("\u001b[2;37m[提示] 按 Ctrl+C 退出 | 刷新间隔: 1.0s\u001b[0m");

                Thread.Sleep(1000);
            }
        }
    }
}
