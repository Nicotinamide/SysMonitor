using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SysMonitor.Linux.UI;

namespace SysMonitor.Linux
{
    public static class LinuxProgram
    {
        // Avalonia 启动构建器（提供平台探针、矢量字体与原生桌面生命周期集成）
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<LinuxApp>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        [STAThread]
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            bool cliMode = false;
            bool statusMode = false;
            bool daemonMode = false;

            foreach (var arg in args)
            {
                if (arg == "--cli" || arg == "-c") cliMode = true;
                else if (arg == "--watch" || arg == "-w") cliMode = true;
                else if (arg == "--status" || arg == "-s") statusMode = true;
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

            // 桌面环境智能探测: 检查是否有图形显示服务 (X11 / Wayland / WSLg)
            bool hasDisplay = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) ||
                              !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

            if (!cliMode && hasDisplay)
            {
                try
                {
                    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[SysMonitor] 无法启动桌面图形视窗: " + ex.Message);
                    Console.WriteLine("[SysMonitor] 正在自动切换至终端控制台仪表盘...");
                }
            }

            // 无桌面图形环境 (SSH/Server) 或显式传参 --cli 时进入实时终端仪表盘
            RunWatchDashboard();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("SysMonitor - Cross-Platform Native Telemetry & ZeroTier Monitor (Linux Edition)");
            Console.WriteLine("Usage: sysmonitor [options]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  (无参数)        默认在具备显示服务时启动原生悬浮窗桌面看板");
            Console.WriteLine("  -c, --cli       强制以终端交互仪表盘模式运行 (TUI)");
            Console.WriteLine("  -w, --watch     实时终端仪表盘 (同 --cli)");
            Console.WriteLine("  -s, --status    单次打印硬件与 ZeroTier 遥测 JSON 快照并退出");
            Console.WriteLine("  -d, --daemon    后台静默遥测守护模式 (适合 systemd 托管)");
            Console.WriteLine("  -h, --help      显示本帮助说明");
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
