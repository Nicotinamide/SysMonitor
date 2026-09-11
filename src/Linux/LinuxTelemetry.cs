using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SysMonitor.Linux
{
    // ==========================================
    // Telemetry Data Models for Linux (1:1 with Windows)
    // ==========================================
    public class LinuxSystemLoadData
    {
        public double CpuPercent { get; set; }
        public double RamUsedGb { get; set; }
        public double RamTotalGb { get; set; }
        public int RamPercent { get; set; }
    }

    public enum LinuxPowerStateKind
    {
        Unknown,
        DesktopAc,
        ChargedFull,
        ChargingFast,
        AcDirect,
        DischargingNormal,
        DischargingLow
    }

    public class LinuxPowerData
    {
        public int BatteryPercent { get; set; } = 100;
        public double Watts { get; set; } = 0.0;
        public double CpuWatts { get; set; } = 0.0;
        public bool HasBattery { get; set; } = true;
        public bool IsCharging { get; set; } = false;
        public bool IsDischarging { get; set; } = false;
        public bool IsAcOnline { get; set; } = true;
        public LinuxPowerStateKind StateKind { get; set; } = LinuxPowerStateKind.ChargedFull;
        public string StatusText { get; set; } = "";
        public string EstimatedTimeStr { get; set; } = "--";
        public double BatteryWh { get; set; } = 0.0;
        public double DischargingHours { get; set; } = 0.0;

        public string GetStatusText(TranslationSet i18n)
        {
            switch (StateKind)
            {
                case LinuxPowerStateKind.DesktopAc:
                    return i18n.PowerDesktop;
                case LinuxPowerStateKind.ChargedFull:
                    return i18n.PowerFull;
                case LinuxPowerStateKind.ChargingFast:
                    return string.Format(i18n.Lang == AppLanguage.Zh ? "⚡ 充电中 +{0:0.0}W" : "⚡ Charging +{0:0.0}W", Math.Abs(Watts));
                case LinuxPowerStateKind.AcDirect:
                    return i18n.PowerAcDirect;
                case LinuxPowerStateKind.DischargingNormal:
                    return string.Format(i18n.Lang == AppLanguage.Zh ? "电池供电 -{0:0.0}W" : "Battery -{0:0.0}W", Math.Abs(Watts));
                case LinuxPowerStateKind.DischargingLow:
                    return string.Format(i18n.Lang == AppLanguage.Zh ? "低电量 -{0:0.0}W" : "Low Battery -{0:0.0}W", Math.Abs(Watts));
                default:
                    return !string.IsNullOrEmpty(StatusText) ? StatusText : "--";
            }
        }

        public string GetCompactStatusText(TranslationSet i18n)
        {
            switch (StateKind)
            {
                case LinuxPowerStateKind.DesktopAc:
                    return i18n.Lang == AppLanguage.Zh ? "市电" : "AC";
                case LinuxPowerStateKind.ChargedFull:
                    return i18n.Lang == AppLanguage.Zh ? "满电" : "FULL";
                case LinuxPowerStateKind.ChargingFast:
                    return string.Format("+{0:0.0}W", Math.Abs(Watts));
                case LinuxPowerStateKind.AcDirect:
                    return i18n.Lang == AppLanguage.Zh ? "市电" : "AC";
                case LinuxPowerStateKind.DischargingNormal:
                case LinuxPowerStateKind.DischargingLow:
                    return string.Format("-{0:0.0}W", Math.Abs(Watts));
                default:
                    return !string.IsNullOrEmpty(StatusText) ? StatusText : "--";
            }
        }

        public string GetEstimatedTimeText(TranslationSet i18n)
        {
            switch (StateKind)
            {
                case LinuxPowerStateKind.DesktopAc:
                case LinuxPowerStateKind.ChargedFull:
                case LinuxPowerStateKind.AcDirect:
                    return i18n.PowerNoBatteryDrain;
                case LinuxPowerStateKind.ChargingFast:
                    return i18n.PowerCharging;
                case LinuxPowerStateKind.DischargingNormal:
                case LinuxPowerStateKind.DischargingLow:
                    if (DischargingHours > 0.05 && DischargingHours < 100)
                    {
                        int totalMin = (int)(DischargingHours * 60);
                        int h = totalMin / 60;
                        int m = totalMin % 60;
                        return i18n.Lang == AppLanguage.Zh ? string.Format("{0}小时{1}分", h, m) : string.Format("{0}h {1}m", h, m);
                    }
                    return "--";
                default:
                    return "--";
            }
        }
    }

    public class LinuxNetworkData
    {
        public string DownSpeedStr { get; set; } = "↓ 0.0K";
        public string UpSpeedStr { get; set; } = "↑ 0.0K";
        public double DownBytesPerSec { get; set; }
        public double UpBytesPerSec { get; set; }
        public double SessionRecvMb { get; set; }
        public double SessionSentMb { get; set; }
        public string PublicIp { get; set; } = "获取中...";
        public string CountryCode { get; set; } = "";
        public string Country { get; set; } = "";
        public string City { get; set; } = "";
        public string Isp { get; set; } = "";
        public string ActiveInterface { get; set; } = "eth0";
        public string LocalIp { get; set; } = "127.0.0.1";
        public string LinkSpeedStr { get; set; } = "--";
    }

    public class LinuxMoonNode
    {
        public string Address { get; set; } = "";
        public int Latency { get; set; } = -1;
        public bool IsDirect { get; set; } = false;
        public bool IsOffline { get; set; } = false;
        public string PhysicalAddress { get; set; } = "";
        public string Role { get; set; } = "MOON";
    }

    public class LinuxZeroTierData
    {
        public bool IsInstalled { get; set; } = true;
        public bool IsRunning { get; set; } = false;
        public string LocalNodeId { get; set; } = "";
        public List<LinuxMoonNode> Moons { get; set; } = new List<LinuxMoonNode>();
        public int TotalMoons => Moons.Count;
        public int DirectMoons => Moons.Count(m => m.IsDirect && !m.IsOffline);
        public bool HasDroppedMoons => Moons.Any(m => m.IsOffline);
        public int MinLatency
        {
            get
            {
                var direct = Moons.Where(m => m.IsDirect && m.Latency >= 0).ToList();
                return direct.Count > 0 ? direct.Min(m => m.Latency) : -1;
            }
        }

        public string GetSummaryText(TranslationSet i18n)
        {
            if (!IsRunning) return i18n.ZtOffline;
            if (HasDroppedMoons) return string.Format(i18n.MoonDroppedSummary, Moons.Count(m => m.IsOffline));
            if (TotalMoons > 0)
            {
                if (DirectMoons > 0)
                {
                    return MinLatency >= 0 ? string.Format("{0}ms", MinLatency) : i18n.Direct;
                }
                return i18n.Relay;
            }
            return i18n.ZtOnline;
        }
    }

    public class LinuxMemberNode
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Ip { get; set; } = "";
        public List<string> AllIps { get; set; } = new List<string>();
        public bool Authorized { get; set; } = true;
        public bool IsOnline { get; set; } = false;
        public int Latency { get; set; } = -1;
        public string PhysicalAddress { get; set; } = "";
        public string Role { get; set; } = "LEAF";
        public string PinyinInitials { get; set; } = "";
    }

    public class LinuxMemberConfig
    {
        public string ControllerUrl { get; set; } = "";
        public string NetworkId { get; set; } = "";
        public string ApiToken { get; set; } = "";
    }

    // ==========================================
    // Linux Native Telemetry Engine
    // ==========================================
    public class LinuxTelemetryEngine
    {
        public event Action<LinuxSystemLoadData> SystemLoadUpdated;
        public event Action<LinuxPowerData> PowerUpdated;
        public event Action<LinuxNetworkData> NetworkUpdated;
        public event Action<LinuxZeroTierData> ZeroTierUpdated;
        public event Action<string, int> MoonDirectAlert;
        public event Action<string> MoonRelayAlert;

        private Dictionary<string, bool> _moonLinkStates = new Dictionary<string, bool>();
        private Dictionary<string, int> _moonRelayCounters = new Dictionary<string, int>();
        private bool _moonInitialScanDone = false;

        private long _prevCpuIdle = 0;
        private long _prevCpuTotal = 0;

        private long _prevRxBytes = 0;
        private long _prevTxBytes = 0;
        private DateTime _prevNetTime = DateTime.UtcNow;
        private double _sessionRecvBytes = 0;
        private double _sessionSentBytes = 0;

        private string _cachedPublicIp = "";
        private string _cachedCountryCode = "";
        private string _cachedCountryZh = "";
        private string _cachedCityZh = "";
        private string _cachedCountryEn = "";
        private string _cachedCityEn = "";
        private string _cachedIsp = "";
        private DateTime _lastGeoIpFetch = DateTime.MinValue;

        private Timer _timer;
        private bool _isUpdating = false;

        public LinuxTelemetryEngine()
        {
            _timer = new Timer(OnTimerTick, null, 0, 1000);
            LinuxSettings.SettingsChanged += () =>
            {
                bool needFetch = (LinuxSettings.Language == AppLanguage.En && (string.IsNullOrEmpty(_cachedCityEn) || string.IsNullOrEmpty(_cachedCountryEn))) ||
                                 (LinuxSettings.Language == AppLanguage.Zh && (string.IsNullOrEmpty(_cachedCityZh) || string.IsNullOrEmpty(_cachedCountryZh)));
                if (needFetch && !string.IsNullOrEmpty(_cachedPublicIp))
                {
                    TriggerGeoIpRefresh();
                }
            };
            Task.Run(() => FetchGeoIpAsync());
        }

        public void TriggerGeoIpRefresh()
        {
            _lastGeoIpFetch = DateTime.MinValue;
            Task.Run(() => FetchGeoIpAsync());
        }

        private void OnTimerTick(object state)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                UpdateCpuAndRam();
                UpdatePower();
                UpdateNetwork();
                UpdateZeroTier();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateCpuAndRam()
        {
            try
            {
                // 1. CPU Usage from /proc/stat
                double cpuPercent = 0.0;
                if (File.Exists("/proc/stat"))
                {
                    string firstLine = File.ReadLines("/proc/stat").FirstOrDefault();
                    if (!string.IsNullOrEmpty(firstLine) && firstLine.StartsWith("cpu"))
                    {
                        string[] parts = firstLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                        {
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

                            if (deltaTotal > 0)
                            {
                                cpuPercent = Math.Max(0.0, Math.Min(100.0, Math.Round((1.0 - (double)deltaIdle / deltaTotal) * 100.0, 1)));
                            }
                        }
                    }
                }

                // 2. RAM from /proc/meminfo
                double totalGb = 0.0;
                double usedGb = 0.0;
                int ramPct = 0;
                if (File.Exists("/proc/meminfo"))
                {
                    long totalKb = 0, availKb = 0, freeKb = 0, buffersKb = 0, cachedKb = 0;
                    foreach (string line in File.ReadLines("/proc/meminfo"))
                    {
                        if (line.StartsWith("MemTotal:")) totalKb = ParseKb(line);
                        else if (line.StartsWith("MemAvailable:")) availKb = ParseKb(line);
                        else if (line.StartsWith("MemFree:")) freeKb = ParseKb(line);
                        else if (line.StartsWith("Buffers:")) buffersKb = ParseKb(line);
                        else if (line.StartsWith("Cached:")) cachedKb = ParseKb(line);
                    }
                    if (availKb == 0) availKb = freeKb + buffersKb + cachedKb;
                    long usedKb = Math.Max(0, totalKb - availKb);

                    totalGb = Math.Round(totalKb / 1024.0 / 1024.0, 1);
                    usedGb = Math.Round(usedKb / 1024.0 / 1024.0, 1);
                    ramPct = totalKb > 0 ? (int)Math.Round(usedKb * 100.0 / totalKb) : 0;
                }

                SystemLoadUpdated?.Invoke(new LinuxSystemLoadData
                {
                    CpuPercent = cpuPercent,
                    RamUsedGb = usedGb,
                    RamTotalGb = totalGb,
                    RamPercent = ramPct
                });
            }
            catch { }
        }

        private void UpdatePower()
        {
            try
            {
                var p = new LinuxPowerData();
                const string powerSupplyPath = "/sys/class/power_supply";
                bool foundBattery = false;

                if (Directory.Exists(powerSupplyPath))
                {
                    string[] batDirs = Directory.GetDirectories(powerSupplyPath, "BAT*");
                    if (batDirs.Length == 0) batDirs = Directory.GetDirectories(powerSupplyPath, "*bat*");

                    if (batDirs.Length > 0)
                    {
                        foundBattery = true;
                        string bat = batDirs[0];
                        string capFile = Path.Combine(bat, "capacity");
                        string statusFile = Path.Combine(bat, "status");

                        if (File.Exists(capFile) && int.TryParse(File.ReadAllText(capFile).Trim(), out int cap))
                        {
                            p.BatteryPercent = Math.Max(0, Math.Min(100, cap));
                        }

                        bool charging = false;
                        bool full = false;
                        if (File.Exists(statusFile))
                        {
                            string st = File.ReadAllText(statusFile).Trim().ToLowerInvariant();
                            charging = st.Contains("charging");
                            full = st.Contains("full") || p.BatteryPercent >= 98;
                            p.IsCharging = charging;
                            p.IsAcOnline = charging || full || st.Contains("not charging");
                            p.IsDischarging = st.Contains("discharging");
                        }

                        // Power / Watts
                        double rateWatts = 0.0;
                        string powerNow = Path.Combine(bat, "power_now");
                        if (File.Exists(powerNow) && long.TryParse(File.ReadAllText(powerNow).Trim(), out long uWatts))
                        {
                            rateWatts = Math.Round(uWatts / 1000000.0, 1);
                        }
                        else
                        {
                            string curr = Path.Combine(bat, "current_now");
                            string volt = Path.Combine(bat, "voltage_now");
                            if (File.Exists(curr) && File.Exists(volt) &&
                                long.TryParse(File.ReadAllText(curr).Trim(), out long uA) &&
                                long.TryParse(File.ReadAllText(volt).Trim(), out long uV))
                            {
                                rateWatts = Math.Round((uA * uV) / 1000000000000.0, 1);
                            }
                        }

                        // Energy now for Wh
                        string energyNow = Path.Combine(bat, "energy_now");
                        if (File.Exists(energyNow) && long.TryParse(File.ReadAllText(energyNow).Trim(), out long uWh))
                        {
                            p.BatteryWh = Math.Round(uWh / 1000000.0, 1);
                        }

                        p.Watts = rateWatts;
                        // Estimate PC/CPU power
                        p.CpuWatts = EstimateSystemPower(rateWatts, p.IsAcOnline);

                        if (full)
                        {
                            p.StateKind = LinuxPowerStateKind.ChargedFull;
                        }
                        else if (charging)
                        {
                            p.StateKind = LinuxPowerStateKind.ChargingFast;
                        }
                        else if (p.IsAcOnline)
                        {
                            p.StateKind = LinuxPowerStateKind.AcDirect;
                        }
                        else
                        {
                            p.StateKind = p.BatteryPercent <= 20 ? LinuxPowerStateKind.DischargingLow : LinuxPowerStateKind.DischargingNormal;
                            if (rateWatts > 0 && p.BatteryWh > 0)
                            {
                                p.DischargingHours = p.BatteryWh / rateWatts;
                            }
                        }

                        p.StatusText = p.GetStatusText(I18n.Current);
                        p.EstimatedTimeStr = p.GetEstimatedTimeText(I18n.Current);
                    }
                }

                if (!foundBattery)
                {
                    p.HasBattery = false;
                    p.IsAcOnline = true;
                    p.StateKind = LinuxPowerStateKind.DesktopAc;
                    p.BatteryPercent = 100;
                    p.CpuWatts = EstimateSystemPower(0, true);
                    p.StatusText = p.GetStatusText(I18n.Current);
                    p.EstimatedTimeStr = p.GetEstimatedTimeText(I18n.Current);
                }

                PowerUpdated?.Invoke(p);
            }
            catch { }
        }

        private double EstimateSystemPower(double batWatts, bool isAc)
        {
            try
            {
                const string raplPath = "/sys/class/powercap/intel-rapl/intel-rapl:0";
                if (Directory.Exists(raplPath))
                {
                    string energyFile = Path.Combine(raplPath, "energy_uj");
                    if (File.Exists(energyFile))
                    {
                        return Math.Round(15.0 + (_prevCpuTotal > 0 ? 25.0 : 0.0), 1);
                    }
                }
            }
            catch { }
            return isAc ? Math.Max(25.0, Math.Round(batWatts + 18.5, 1)) : Math.Max(10.0, batWatts);
        }

        private void UpdateNetwork()
        {
            try
            {
                var net = new LinuxNetworkData();

                long totalRx = 0;
                long totalTx = 0;

                if (File.Exists("/proc/net/dev"))
                {
                    foreach (string line in File.ReadLines("/proc/net/dev"))
                    {
                        int colonIdx = line.IndexOf(':');
                        if (colonIdx < 0) continue;

                        string iface = line.Substring(0, colonIdx).Trim();
                        if (iface == "lo" || iface.StartsWith("docker") || iface.StartsWith("br-") || iface.StartsWith("zt")) continue;

                        string statsPart = line.Substring(colonIdx + 1);
                        string[] tokens = statsPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length >= 9)
                        {
                            if (long.TryParse(tokens[0], out long rx)) totalRx += rx;
                            if (long.TryParse(tokens[8], out long tx)) totalTx += tx;
                        }
                    }
                }

                DateTime now = DateTime.UtcNow;
                double seconds = (now - _prevNetTime).TotalSeconds;
                if (seconds <= 0) seconds = 1.0;

                long deltaRecv = totalRx - _prevRxBytes;
                long deltaSent = totalTx - _prevTxBytes;

                if (_prevRxBytes > 0 && deltaRecv > 0) _sessionRecvBytes += deltaRecv;
                if (_prevTxBytes > 0 && deltaSent > 0) _sessionSentBytes += deltaSent;

                double downRate = (_prevRxBytes > 0 && deltaRecv >= 0) ? (deltaRecv / seconds) : 0;
                double upRate = (_prevTxBytes > 0 && deltaSent >= 0) ? (deltaSent / seconds) : 0;

                _prevRxBytes = totalRx;
                _prevTxBytes = totalTx;
                _prevNetTime = now;

                net.DownBytesPerSec = downRate;
                net.UpBytesPerSec = upRate;
                net.DownSpeedStr = "↓ " + FormatCompactSpeed(downRate);
                net.UpSpeedStr = "↑ " + FormatCompactSpeed(upRate);
                net.SessionRecvMb = Math.Round(_sessionRecvBytes / 1024.0 / 1024.0, 1);
                net.SessionSentMb = Math.Round(_sessionSentBytes / 1024.0 / 1024.0, 1);

                // Active interface & local IP
                string activeIface = GetDefaultInterface();
                net.ActiveInterface = !string.IsNullOrEmpty(activeIface) ? activeIface : "eth0";
                net.LocalIp = GetInterfaceIpv4(net.ActiveInterface);
                net.LinkSpeedStr = GetInterfaceSpeed(net.ActiveInterface);

                // GeoIP
                net.PublicIp = !string.IsNullOrEmpty(_cachedPublicIp) ? _cachedPublicIp : I18n.Current.NetFetching;
                net.CountryCode = _cachedCountryCode;
                bool isEn = LinuxSettings.Language == AppLanguage.En;
                net.Country = isEn ? (!string.IsNullOrEmpty(_cachedCountryEn) ? _cachedCountryEn : _cachedCountryZh)
                                   : (!string.IsNullOrEmpty(_cachedCountryZh) ? _cachedCountryZh : _cachedCountryEn);
                net.City = isEn ? (!string.IsNullOrEmpty(_cachedCityEn) ? _cachedCityEn : _cachedCityZh)
                                : (!string.IsNullOrEmpty(_cachedCityZh) ? _cachedCityZh : _cachedCityEn);
                net.Isp = _cachedIsp;

                NetworkUpdated?.Invoke(net);
            }
            catch { }
        }

        private string GetDefaultInterface()
        {
            try
            {
                if (File.Exists("/proc/net/route"))
                {
                    foreach (string line in File.ReadLines("/proc/net/route").Skip(1))
                    {
                        string[] parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && parts[1] == "00000000")
                        {
                            return parts[0];
                        }
                    }
                }
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        string n = ni.Name.ToLower();
                        if (!n.StartsWith("zt") && !n.StartsWith("docker") && !n.StartsWith("veth")) return ni.Name;
                    }
                }
            }
            catch { }
            return "eth0";
        }

        private string GetInterfaceIpv4(string ifaceName)
        {
            try
            {
                var ni = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(i => i.Name == ifaceName);
                if (ni != null)
                {
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip.Address))
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private string GetInterfaceSpeed(string ifaceName)
        {
            try
            {
                string speedFile = Path.Combine("/sys/class/net", ifaceName, "speed");
                if (File.Exists(speedFile) && int.TryParse(File.ReadAllText(speedFile).Trim(), out int speed) && speed > 0)
                {
                    if (speed >= 1000) return string.Format("{0:0.#} Gbps", speed / 1000.0);
                    return string.Format("{0} Mbps", speed);
                }
            }
            catch { }
            return "--";
        }

        private async Task FetchGeoIpAsync()
        {
            bool isEn = LinuxSettings.Language == AppLanguage.En;
            bool hasCurrentLang = isEn ? (!string.IsNullOrEmpty(_cachedCountryEn) && !string.IsNullOrEmpty(_cachedCityEn))
                                       : (!string.IsNullOrEmpty(_cachedCountryZh) && !string.IsNullOrEmpty(_cachedCityZh));

            if ((DateTime.UtcNow - _lastGeoIpFetch).TotalMinutes < 15 && !string.IsNullOrEmpty(_cachedPublicIp) && hasCurrentLang)
                return;

            try
            {
                _lastGeoIpFetch = DateTime.UtcNow;
                string queryLang = isEn ? "en" : "zh-CN";
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    string json = await client.GetStringAsync($"http://ip-api.com/json/?lang={queryLang}");
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("status", out var st) && st.GetString() == "success")
                        {
                            if (root.TryGetProperty("query", out var q)) _cachedPublicIp = q.GetString();
                            if (root.TryGetProperty("countryCode", out var cc)) _cachedCountryCode = cc.GetString();
                            string country = root.TryGetProperty("country", out var c) ? c.GetString() : "";
                            string city = root.TryGetProperty("city", out var ci) ? ci.GetString() : "";
                            if (isEn)
                            {
                                _cachedCountryEn = country;
                                _cachedCityEn = city;
                            }
                            else
                            {
                                _cachedCountryZh = country;
                                _cachedCityZh = city;
                            }
                            if (root.TryGetProperty("isp", out var isp)) _cachedIsp = isp.GetString();
                        }
                    }
                }
            }
            catch { }
        }

        private void UpdateZeroTier()
        {
            Task.Run(async () =>
            {
                try
                {
                    var zt = new LinuxZeroTierData();
                    string secret = GetZeroTierSecret();

                    if (!string.IsNullOrEmpty(secret))
                    {
                        using (var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true })
                        using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(1800) })
                        {
                            client.DefaultRequestHeaders.Add("X-ZT1-Auth", secret);

                            // Status
                            var respStatus = await client.GetStringAsync("http://127.0.0.1:9993/status");
                            using (var doc = JsonDocument.Parse(respStatus))
                            {
                                zt.IsRunning = true;
                                if (doc.RootElement.TryGetProperty("address", out var addr))
                                    zt.LocalNodeId = addr.GetString();
                            }

                            // Peers (Moons)
                            var respPeers = await client.GetStringAsync("http://127.0.0.1:9993/peer");
                            using (var doc = JsonDocument.Parse(respPeers))
                            {
                                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var p in doc.RootElement.EnumerateArray())
                                    {
                                        string role = p.TryGetProperty("role", out var r) ? r.GetString() : "";
                                        if (role == "MOON")
                                        {
                                            var moon = new LinuxMoonNode
                                            {
                                                Address = p.TryGetProperty("address", out var a) ? a.GetString() : "",
                                                Latency = p.TryGetProperty("latency", out var lat) ? lat.GetInt32() : -1,
                                                Role = role
                                            };
                                            if (p.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.Array)
                                            {
                                                foreach (var path in paths.EnumerateArray())
                                                {
                                                    if (path.TryGetProperty("address", out var paddr))
                                                    {
                                                        moon.PhysicalAddress = paddr.GetString();
                                                        break;
                                                    }
                                                }
                                            }
                                            moon.IsDirect = moon.Latency >= 0 && !string.IsNullOrEmpty(moon.PhysicalAddress);
                                            moon.IsOffline = moon.Latency < 0 && string.IsNullOrEmpty(moon.PhysicalAddress);
                                            zt.Moons.Add(moon);

                                            // Direct <-> Relay link state transition tracking
                                            bool isDirect = moon.IsDirect;
                                            if (!_moonLinkStates.ContainsKey(moon.Address))
                                            {
                                                _moonLinkStates[moon.Address] = isDirect;
                                                _moonRelayCounters[moon.Address] = 0;
                                            }
                                            else
                                            {
                                                bool lastDirect = _moonLinkStates[moon.Address];
                                                if (isDirect)
                                                {
                                                    _moonRelayCounters[moon.Address] = 0;
                                                    if (!lastDirect)
                                                    {
                                                        _moonLinkStates[moon.Address] = true;
                                                        if (_moonInitialScanDone)
                                                        {
                                                            MoonDirectAlert?.Invoke(moon.Address, moon.Latency);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if (lastDirect)
                                                    {
                                                        if (!_moonRelayCounters.ContainsKey(moon.Address)) _moonRelayCounters[moon.Address] = 0;
                                                        _moonRelayCounters[moon.Address]++;
                                                        if (_moonRelayCounters[moon.Address] >= 2)
                                                        {
                                                            _moonLinkStates[moon.Address] = false;
                                                            if (_moonInitialScanDone)
                                                            {
                                                                MoonRelayAlert?.Invoke(moon.Address);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    _moonInitialScanDone = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Fallback to zerotier-cli
                        var cliZt = await QueryZeroTierCliAsync();
                        if (cliZt != null) zt = cliZt;
                    }

                    ZeroTierUpdated?.Invoke(zt);
                }
                catch
                {
                    ZeroTierUpdated?.Invoke(new LinuxZeroTierData { IsRunning = false });
                }
            });
        }

        private string GetZeroTierSecret()
        {
            string[] paths = new[]
            {
                "/var/lib/zerotier-one/authtoken.secret",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zeroTierOneAuthToken")
            };
            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    try { return File.ReadAllText(p).Trim(); } catch { }
                }
            }
            return null;
        }

        private async Task<LinuxZeroTierData> QueryZeroTierCliAsync()
        {
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
                        using (var doc = JsonDocument.Parse(outStr))
                        {
                            var zt = new LinuxZeroTierData { IsRunning = true };
                            if (doc.RootElement.TryGetProperty("address", out var addr))
                                zt.LocalNodeId = addr.GetString();
                            return zt;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static long ParseKb(string line)
        {
            var m = Regex.Match(line, @"\d+");
            return m.Success ? long.Parse(m.Value) : 0;
        }

        public static string CountryCodeToEmoji(string cc)
        {
            if (string.IsNullOrEmpty(cc) || cc.Length != 2) return "🌐";
            cc = cc.ToUpperInvariant();
            int first = 0x1F1E6 + (cc[0] - 'A');
            int second = 0x1F1E6 + (cc[1] - 'A');
            return char.ConvertFromUtf32(first) + char.ConvertFromUtf32(second);
        }

        public static string FormatCompactSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024) return string.Format("{0:0}B", bytesPerSec);
            if (bytesPerSec < 1024 * 1024) return string.Format("{0:0.0}K", bytesPerSec / 1024.0);
            return string.Format("{0:0.0}M", bytesPerSec / (1024.0 * 1024.0));
        }
    }

    // ==========================================
    // Linux ZeroTier Member Directory Engine
    // ==========================================
    public class LinuxMemberDirectoryEngine
    {
        public event Action<List<LinuxMemberNode>> MembersUpdated;
        public event Action<string> StatusChanged;
        public event Action ConfigChanged;

        private LinuxMemberConfig _config = new LinuxMemberConfig();
        private List<LinuxMemberNode> _members = new List<LinuxMemberNode>();
        private readonly object _lock = new object();
        private Timer _timer;
        private bool _isFetching = false;
        private string _lastStatusText = "";

        public LinuxMemberConfig CurrentConfig
        {
            get
            {
                lock (_lock)
                {
                    return new LinuxMemberConfig
                    {
                        ControllerUrl = _config.ControllerUrl,
                        NetworkId = _config.NetworkId,
                        ApiToken = _config.ApiToken
                    };
                }
            }
        }

        public bool HasToken
        {
            get
            {
                lock (_lock)
                {
                    return !string.IsNullOrWhiteSpace(_config.ApiToken) && !string.IsNullOrWhiteSpace(_config.NetworkId);
                }
            }
        }

        public LinuxMemberDirectoryEngine()
        {
            LoadConfig();
            _timer = new Timer(state => TriggerRefresh(), null, 2000, 30000);
        }

        public string GetCurrentStatusText() => _lastStatusText;

        public void LoadConfig()
        {
            try
            {
                string json = LinuxSecretStorage.LoadSecureConfig();
                if (!string.IsNullOrEmpty(json))
                {
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        lock (_lock)
                        {
                            if (root.TryGetProperty("controllerUrl", out var u)) _config.ControllerUrl = u.GetString() ?? "";
                            if (root.TryGetProperty("networkId", out var n)) _config.NetworkId = n.GetString() ?? "";
                            if (root.TryGetProperty("apiToken", out var t)) _config.ApiToken = t.GetString() ?? "";
                        }
                    }
                }
            }
            catch { }
        }

        public void SaveEncryptedConfig(string url, string nwid, string token)
        {
            lock (_lock)
            {
                _config.ControllerUrl = url?.Trim() ?? "";
                _config.NetworkId = nwid?.Trim() ?? "";
                _config.ApiToken = token?.Trim() ?? "";

                try
                {
                    string json = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        { "controllerUrl", _config.ControllerUrl },
                        { "networkId", _config.NetworkId },
                        { "apiToken", _config.ApiToken }
                    });
                    LinuxSecretStorage.SaveSecureConfig(json);
                }
                catch { }
            }
            ConfigChanged?.Invoke();
            TriggerRefresh();
        }

        public void TriggerRefresh()
        {
            if (_isFetching || !HasToken) return;
            _isFetching = true;
            Task.Run(async () =>
            {
                try
                {
                    UpdateStatus("⏳ 正在同步...");
                    string url, nwid, token;
                    lock (_lock)
                    {
                        url = _config.ControllerUrl;
                        nwid = _config.NetworkId;
                        token = _config.ApiToken;
                    }

                    var result = await QueryMembersApiAsync(url, nwid, token);
                    if (result.success)
                    {
                        lock (_lock)
                        {
                            _members = result.members;
                        }
                        UpdateStatus(string.Format("已同步 {0} 节点", result.members.Count));
                        MembersUpdated?.Invoke(result.members);
                    }
                    else
                    {
                        UpdateStatus("⚠️ " + (result.error ?? "同步失败"));
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus("⚠️ " + ex.Message);
                }
                finally
                {
                    _isFetching = false;
                }
            });
        }

        public async Task TestConnectionAsync(string url, string nwid, string token, Action<bool, int, string> callback)
        {
            var result = await QueryMembersApiAsync(url, nwid, token);
            callback?.Invoke(result.success, result.members?.Count ?? 0, result.error);
        }

        private async Task<(bool success, List<LinuxMemberNode> members, string error)> QueryMembersApiAsync(string url, string nwid, string token)
        {
            try
            {
                string baseUrl = (url ?? "").Trim().TrimEnd('/');
                if (string.IsNullOrEmpty(baseUrl)) baseUrl = "https://api.zerotier.com";
                string endpoint = $"{baseUrl}/api/v1/networks/{nwid}/members";

                using (var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true })
                using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) })
                {
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + (token ?? "").Trim());
                    client.DefaultRequestHeaders.Add("X-ZT1-Auth", (token ?? "").Trim());

                    var resp = await client.GetAsync(endpoint);
                    if (!resp.IsSuccessStatusCode)
                    {
                        // Try secondary endpoint: /controller/network/{networkId}/member
                        string fallbackEndpoint = $"{baseUrl}/controller/network/{nwid}/member";
                        resp = await client.GetAsync(fallbackEndpoint);
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        int code = (int)resp.StatusCode;
                        if (code == 401) return (false, null, "401未授权 (Token 错误)");
                        if (code == 403) return (false, null, "403拒绝访问 (无权限)");
                        return (false, null, $"HTTP {code} {resp.ReasonPhrase}");
                    }

                    string body = await resp.Content.ReadAsStringAsync();
                    var list = ParseMembersJson(body);
                    return (true, list, null);
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private List<LinuxMemberNode> ParseMembersJson(string json)
        {
            var list = new List<LinuxMemberNode>();
            if (string.IsNullOrWhiteSpace(json)) return list;

            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in root.EnumerateArray())
                        {
                            var node = ParseSingleMember(el);
                            if (node != null) list.Add(node);
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                var node = ParseSingleMember(prop.Value);
                                if (node != null)
                                {
                                    if (string.IsNullOrEmpty(node.Id)) node.Id = prop.Name;
                                    list.Add(node);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        private LinuxMemberNode ParseSingleMember(JsonElement el)
        {
            var node = new LinuxMemberNode();

            // ID
            if (el.TryGetProperty("id", out var id)) node.Id = id.GetString() ?? "";
            else if (el.TryGetProperty("nodeId", out var nid)) node.Id = nid.GetString() ?? "";

            // Name
            if (el.TryGetProperty("name", out var n)) node.Name = n.GetString() ?? "";
            else if (el.TryGetProperty("description", out var desc)) node.Name = desc.GetString() ?? "";

            // IP Assignments
            if (el.TryGetProperty("config", out var cfg) && cfg.TryGetProperty("ipAssignments", out var ips))
            {
                ExtractIps(ips, node);
            }
            else if (el.TryGetProperty("ipAssignments", out var ips2))
            {
                ExtractIps(ips2, node);
            }

            // Latency & Peer
            if (el.TryGetProperty("peer", out var peer))
            {
                if (peer.TryGetProperty("latency", out var lat)) node.Latency = lat.GetInt32();
                if (peer.TryGetProperty("role", out var r)) node.Role = r.GetString() ?? "LEAF";
                if (peer.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.Array)
                {
                    foreach (var path in paths.EnumerateArray())
                    {
                        if (path.TryGetProperty("address", out var paddr))
                        {
                            node.PhysicalAddress = paddr.GetString() ?? "";
                            break;
                        }
                    }
                }
            }

            node.IsOnline = (node.Latency >= 0) || !string.IsNullOrEmpty(node.PhysicalAddress);
            node.PinyinInitials = GetPinyinInitials(node.Name);
            return node;
        }

        private void ExtractIps(JsonElement ipsEl, LinuxMemberNode node)
        {
            if (ipsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var ip in ipsEl.EnumerateArray())
                {
                    string ipStr = ip.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(ipStr))
                    {
                        node.AllIps.Add(ipStr);
                        if (string.IsNullOrEmpty(node.Ip) && ipStr.Contains(".") && !ipStr.Contains(":"))
                        {
                            node.Ip = ipStr;
                        }
                    }
                }
            }
            if (string.IsNullOrEmpty(node.Ip) && node.AllIps.Count > 0)
            {
                node.Ip = node.AllIps[0];
            }
        }

        private void UpdateStatus(string st)
        {
            _lastStatusText = st;
            StatusChanged?.Invoke(st);
        }

        public List<LinuxMemberNode> GetAllMembers()
        {
            lock (_lock)
            {
                return new List<LinuxMemberNode>(_members);
            }
        }

        public List<LinuxMemberNode> Search(string query)
        {
            List<LinuxMemberNode> all = GetAllMembers();
            if (string.IsNullOrEmpty(query))
            {
                all.Sort((a, b) =>
                {
                    if (a.IsOnline != b.IsOnline) return a.IsOnline ? -1 : 1;
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
                return all;
            }

            string q = query.Trim().ToLowerInvariant();
            var filtered = all.Where(m =>
                (!string.IsNullOrEmpty(m.Name) && m.Name.ToLowerInvariant().Contains(q)) ||
                (!string.IsNullOrEmpty(m.Ip) && m.Ip.Contains(q)) ||
                (!string.IsNullOrEmpty(m.Id) && m.Id.ToLowerInvariant().Contains(q)) ||
                (!string.IsNullOrEmpty(m.PinyinInitials) && m.PinyinInitials.Contains(q))
            ).ToList();

            filtered.Sort((a, b) =>
            {
                bool aExact = (!string.IsNullOrEmpty(a.Name) && a.Name.ToLowerInvariant().StartsWith(q)) || (!string.IsNullOrEmpty(a.Ip) && a.Ip.StartsWith(q));
                bool bExact = (!string.IsNullOrEmpty(b.Name) && b.Name.ToLowerInvariant().StartsWith(q)) || (!string.IsNullOrEmpty(b.Ip) && b.Ip.StartsWith(q));
                if (aExact != bExact) return aExact ? -1 : 1;
                if (a.IsOnline != b.IsOnline) return a.IsOnline ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return filtered;
        }

        public static string GetPinyinInitials(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Encoding gb = Encoding.GetEncoding("GB2312");
                foreach (char c in text)
                {
                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    {
                        sb.Append(char.ToLowerInvariant(c));
                    }
                    else if (c >= 0x4e00 && c <= 0x9fa5)
                    {
                        byte[] arr = gb.GetBytes(c.ToString());
                        if (arr.Length >= 2)
                        {
                            int code = (arr[0] << 8) + arr[1];
                            int[] areacode = { 45217, 45253, 45761, 46318, 46826, 47010, 47297, 47614, 48119, 48119, 49062, 49324, 49896, 50371, 50614, 50622, 50906, 51387, 51446, 52218, 52698, 52698, 52698, 52980, 53689, 54481 };
                            for (int i = 0; i < 26; i++)
                            {
                                int max = (i == 25) ? 55290 : areacode[i + 1];
                                if (areacode[i] <= code && code < max)
                                {
                                    sb.Append((char)('a' + i));
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                foreach (char c in text)
                {
                    if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }
    }
}
