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

            bool watchMode = false;
            bool statusMode = false;
            bool daemonMode = false;

            foreach (var arg in args)
            {
                if (arg == --watch || arg == -w) watchMode = true;
                else if (arg == --status || arg == -s) statusMode = true;
                else if (arg == --daemon || arg == -d) daemonMode = true;
                else if (arg == --help || arg == -h)
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
            Console.WriteLine(SysMonitor - Cross-Platform Native Telemetry & ZeroTier Monitor (Linux Edition));
            Console.WriteLine(Usage: sysmonitor [options]);
            Console.WriteLine(");
 Console.WriteLine(Options:);
 Console.WriteLine(  -w, --watch     Run live interactive terminal dashboard (default));
 Console.WriteLine(  -s, --status    Print one-shot JSON snapshot of hardware & ZeroTier status);
 Console.WriteLine(  -d, --daemon    Run as background telemetry daemon (suitable for systemd));
 Console.WriteLine(  -h, --help      Show this help message);
 Console.WriteLine();
 Console.WriteLine(GitHub: https://github.com/Nicotinamide/SysMonitor);
 }

 private static void PrintStatusOnce()
 {
 var cpu = LinuxMonitors.GetCpuUsagePercent();
 var mem = LinuxMonitors.GetMemoryStatus();
 var net = LinuxMonitors.GetNetworkRates();
 var bat = LinuxMonitors.GetBatteryStatus();
 var zt = LinuxMonitors.GetZeroTierStatus();

 string json = string.Format(
 {{"cpu":{0},"ram_used_gb":{1},"ram_total_gb":{2},"ram_pct":{3},"net_down":{4},"net_up":{5},"battery":{6},"charging":{7},"watts":{8},"zt_running":{9},"zt_node":"{10}","moon_ok":{11},"moon_ms":{12}}},
 cpu.UsagePercent, mem.UsedGb, mem.TotalGb, mem.Percent,
 net.RxSpeedSec, net.TxSpeedSec,
 bat.Percent, bat.IsCharging ? true : false, bat.RateWatts,
 zt.IsRunning ? true : false, zt.NodeId ?? ,
 zt.MoonConnected ? true : false, zt.MinMoonLatencyMs);

 Console.WriteLine(json);
 }

 private static void RunDaemon()
 {
 Console.WriteLine([INFO] SysMonitor Linux Daemon started. Press Ctrl+C to stop.);
 while (true)
 {
 try
 {
 LinuxMonitors.GetCpuUsagePercent();
 LinuxMonitors.GetNetworkRates();
 LinuxMonitors.GetZeroTierStatus();
 }
 catch { }
 Thread.Sleep(2000);
 }
 }

 private static void RunWatchDashboard()
 {
 Console.CursorVisible = false;
 Console.CancelKeyPress += delegate { Console.CursorVisible = true; };

 LinuxMonitors.GetCpuUsagePercent();
 LinuxMonitors.GetNetworkRates();
 Thread.Sleep(500);

 while (true)
 {
 var cpu = LinuxMonitors.GetCpuUsagePercent();
 var mem = LinuxMonitors.GetMemoryStatus();
 var net = LinuxMonitors.GetNetworkRates();
 var bat = LinuxMonitors.GetBatteryStatus();
 var zt = LinuxMonitors.GetZeroTierStatus();

 Console.Clear();
 Console.WriteLine(\u001b[1;36m===========================================================\u001b[0m);
 Console.WriteLine(\u001b[1;37m   SysMonitor - Linux Native Telemetry Dashboard           \u001b[0m);
 Console.WriteLine(\u001b[1;36m===========================================================\u001b[0m);
 Console.WriteLine();

 string cpuColor = cpu.UsagePercent > 80 ? \u001b[1;31m : (cpu.UsagePercent > 50 ? \u001b[1;33m : \u001b[1;32m);
 Console.WriteLine( [CPU] 核心使用率: {0}{1:F1}%\u001b[0m (活跃核心: {2}), cpuColor, cpu.UsagePercent, cpu.ActiveCores);
 
 string memColor = mem.Percent > 85 ? \u001b[1;31m : \u001b[1;32m;
 Console.WriteLine( [RAM] 物理内存:   {0}{1:F1} GB / {2:F1} GB ({3}%)\u001b[0m, memColor, mem.UsedGb, mem.TotalGb, mem.Percent);
 Console.WriteLine();

 Console.WriteLine( [NET] 实时吞吐:   \u001b[1;34m⬇ {0}\u001b[0m   \u001b[1;35m⬆ {1}\u001b[0m, net.RxSpeedFmt, net.TxSpeedFmt);
 Console.WriteLine();

 string pwrState = bat.IsCharging ? ⚡ 正在充电 : (bat.IsPluggedIn ? 🔌 已接通电源 : 🔋 电池放电中);
 Console.WriteLine( [PWR] 电源状态:   {0} ({1}%) - {2:F1} W, pwrState, bat.Percent, bat.RateWatts);
 Console.WriteLine();

 string ztStatus = zt.IsRunning ? \u001b[1;32m● 在线运行\u001b[0m : \u001b[1;31m○ 未运行\u001b[0m;
 Console.WriteLine( [ZT]  ZeroTier:   {0} (Node: \u001b[1;37m{1}\u001b[0m), ztStatus, zt.NodeId ?? N/A);
 
 string moonStatus = zt.MoonConnected 
 ? string.Format(\u001b[1;32m● 直连良好 ({0} ms)\u001b[0m, zt.MinMoonLatencyMs)
 : \u001b[1;33m○ 探测中或中继\u001b[0m;
 Console.WriteLine( [ZT]  Moon 轨道:  {0}, moonStatus);

 Console.WriteLine();
 Console.WriteLine(\u001b[2;37m[提示] 按 Ctrl+C 退出 | 刷新间隔: 1.0s\u001b[0m);

 Thread.Sleep(1000);
 }
 }
 }
}
