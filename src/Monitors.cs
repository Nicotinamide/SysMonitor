using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Media;

namespace SysMonitor
{
    public class TelemetryEngine
    {
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
            public int Rate; // in mW
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

        [StructLayout(LayoutKind.Sequential)]
        private struct PDH_FMT_COUNTERVALUE_DOUBLE
        {
            public uint CStatus;
            public double doubleValue;
        }

        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int PdhOpenQuery(string szDataSource, IntPtr dwUserData, out IntPtr phQuery);

        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int PdhAddCounter(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

        [DllImport("pdh.dll")]
        private static extern int PdhCollectQueryData(IntPtr hQuery);

        [DllImport("pdh.dll")]
        private static extern int PdhGetFormattedCounterValue(IntPtr hCounter, uint dwFormat, out uint lpdwType, out PDH_FMT_COUNTERVALUE_DOUBLE pValue);

        [DllImport("pdh.dll")]
        private static extern int PdhCloseQuery(IntPtr hQuery);

        public event Action<SystemLoadData> SystemLoadUpdated;
        public event Action<PowerData> PowerUpdated;
        public event Action<NetworkData> NetworkUpdated;
        public event Action<ZeroTierData> ZeroTierUpdated;
        public event Action<string, int> MoonDirectAlert;
        public event Action<string> MoonRelayAlert;

        private Dictionary<string, MoonNode> _knownMoons = new Dictionary<string, MoonNode>();
        private Dictionary<string, MoonLinkType> _moonLinkStates = new Dictionary<string, MoonLinkType>();
        private Dictionary<string, int> _moonRelayCounters = new Dictionary<string, int>();
        private bool _moonInitialScanDone = false;

        private Timer _timer1s;
        private Timer _timer2s;
        private Timer _timerGeoIp;

        private long _prevCpuIdle = 0;
        private long _prevCpuKernel = 0;
        private long _prevCpuUser = 0;

        private long _prevBytesRecv = 0;
        private long _prevBytesSent = 0;
        private long _sessionBytesRecv = 0;
        private long _sessionBytesSent = 0;
        private DateTime _prevNetTime = DateTime.UtcNow;

        private string _cachedPublicIp = "";
        private string _cachedCountryCode = "";
        private string _cachedCountryZh = "";
        private string _cachedCityZh = "";
        private string _cachedCountryEn = "";
        private string _cachedCityEn = "";
        private string _cachedIsp = "";

        private string _ztToken = "";
        private int _ztPort = 9993;
        private bool _ztDirFound = false;

        private IntPtr _hPdhQuery = IntPtr.Zero;
        private IntPtr _hPdhCounter = IntPtr.Zero;
        private bool _energyMeterChecked = false;

        private static readonly SolidColorBrush BrushEmerald = new SolidColorBrush(Color.FromRgb(16, 185, 129));  // #10b981
        private static readonly SolidColorBrush BrushAmber = new SolidColorBrush(Color.FromRgb(245, 158, 11));   // #f59e0b
        private static readonly SolidColorBrush BrushCyan = new SolidColorBrush(Color.FromRgb(0, 210, 255));     // #00d2ff
        private static readonly SolidColorBrush BrushRed = new SolidColorBrush(Color.FromRgb(239, 68, 68));      // #ef4444
        private static readonly SolidColorBrush BrushGray = new SolidColorBrush(Color.FromRgb(139, 148, 158));  // #8b949e

        public TelemetryEngine()
        {
            BrushEmerald.Freeze();
            BrushAmber.Freeze();
            BrushCyan.Freeze();
            BrushRed.Freeze();
            BrushGray.Freeze();

            InitZeroTierAuth();
            InitCpuSample();
            SampleInitialNetwork();

            AppSettings.SettingsChanged += delegate
            {
                bool needFetch = (AppSettings.Language == AppLanguage.En && (string.IsNullOrEmpty(_cachedCityEn) || string.IsNullOrEmpty(_cachedCountryEn))) ||
                                 (AppSettings.Language == AppLanguage.Zh && (string.IsNullOrEmpty(_cachedCityZh) || string.IsNullOrEmpty(_cachedCountryZh)));
                if (needFetch && !string.IsNullOrEmpty(_cachedPublicIp))
                {
                    TriggerGeoIpRefresh();
                }
                else
                {
                    UpdateNetworkRate();
                }
            };

            _timer1s = new Timer(OnTimer1s, null, 0, 1000);
            _timer2s = new Timer(OnTimer2s, null, 150, 2000);
            _timerGeoIp = new Timer(OnTimerGeoIp, null, 200, 600000); // 10 minutes
        }

        private void InitCpuSample()
        {
            try
            {
                GetSystemTimes(out _prevCpuIdle, out _prevCpuKernel, out _prevCpuUser);
            }
            catch { }
        }

        private void InitZeroTierAuth()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidateDirs = new string[]
            {
                Path.Combine(localAppData, "ZeroTier"),
                @"C:\ProgramData\ZeroTier\One",
                Path.Combine(localAppData, @"ZeroTier\One")
            };

            foreach (string dir in candidateDirs)
            {
                if (Directory.Exists(dir))
                {
                    _ztDirFound = true;
                    string tokenFile = Path.Combine(dir, "authtoken.secret");
                    if (File.Exists(tokenFile))
                    {
                        try
                        {
                            string t = File.ReadAllText(tokenFile).Trim();
                            if (!string.IsNullOrEmpty(t))
                            {
                                _ztToken = t;
                            }
                        }
                        catch { }
                    }

                    string portFile = Path.Combine(dir, "zerotier-one.port");
                    if (File.Exists(portFile))
                    {
                        try
                        {
                            string p = File.ReadAllText(portFile).Trim();
                            int portVal;
                            if (int.TryParse(p, out portVal))
                            {
                                _ztPort = portVal;
                            }
                        }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(_ztToken)) break;
                }
            }
        }

        private void SampleInitialNetwork()
        {
            try
            {
                long recv = 0;
                long sent = 0;
                GetPhysicalTraffic(out recv, out sent);
                _prevBytesRecv = recv;
                _prevBytesSent = sent;
                _prevNetTime = DateTime.UtcNow;
            }
            catch { }
        }

        private void GetPhysicalTraffic(out long totalRecv, out long totalSent)
        {
            totalRecv = 0;
            totalSent = 0;
            NetworkInterface[] ifaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface ni in ifaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                string desc = ni.Description.ToLower();
                string name = ni.Name.ToLower();
                if (desc.Contains("zerotier") || desc.Contains("virtual") || desc.Contains("vpn") || desc.Contains("pseudo") ||
                    desc.Contains("mihomo") || desc.Contains("clash") || desc.Contains("tun") || desc.Contains("tap") || desc.Contains("wsl") ||
                    name.Contains("mihomo") || name.Contains("clash") || name.Contains("zerotier") || name.Contains("vethernet"))
                    continue;

                IPInterfaceStatistics stats = ni.GetIPStatistics();
                totalRecv += stats.BytesReceived;
                totalSent += stats.BytesSent;
            }
        }

        private void OnTimer1s(object state)
        {
            UpdateSystemLoad();
            UpdatePower();
            UpdateNetworkRate();
        }

        private void OnTimer2s(object state)
        {
            UpdateZeroTier();
        }

        private void OnTimerGeoIp(object state)
        {
            FetchPublicGeoIp();
        }

        public void TriggerGeoIpRefresh()
        {
            ThreadPool.QueueUserWorkItem(delegate { FetchPublicGeoIp(); });
        }

        private void UpdateSystemLoad()
        {
            try
            {
                // 1. RAM
                MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                GlobalMemoryStatusEx(mem);
                double totalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                double usedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);

                // 2. CPU
                long currIdle, currKernel, currUser;
                GetSystemTimes(out currIdle, out currKernel, out currUser);

                long sysDiff = (currKernel - _prevCpuKernel) + (currUser - _prevCpuUser);
                long idleDiff = currIdle - _prevCpuIdle;
                long busy = sysDiff - idleDiff;

                double cpu = 0.0;
                if (sysDiff > 0)
                {
                    cpu = (busy * 100.0) / sysDiff;
                    if (cpu < 0) cpu = 0;
                    if (cpu > 100) cpu = 100;
                }

                _prevCpuIdle = currIdle;
                _prevCpuKernel = currKernel;
                _prevCpuUser = currUser;

                SystemLoadData sld = new SystemLoadData();
                sld.CpuPercent = cpu;
                sld.RamUsedGb = usedGb;
                sld.RamTotalGb = totalGb;
                sld.RamPercent = (int)mem.dwMemoryLoad;

                if (SystemLoadUpdated != null)
                {
                    SystemLoadUpdated(sld);
                }
            }
            catch { }
        }

        private void InitEnergyMeter()
        {
            if (_energyMeterChecked) return;
            _energyMeterChecked = true;
            try
            {
                IntPtr hQuery;
                if (PdhOpenQuery(null, IntPtr.Zero, out hQuery) == 0)
                {
                    IntPtr hCounter;
                    int status = PdhAddCounter(hQuery, @"\Energy Meter(RAPL_Package0_PKG)\Power", IntPtr.Zero, out hCounter);
                    if (status != 0)
                    {
                        status = PdhAddCounter(hQuery, @"\Energy Meter(_Total)\Power", IntPtr.Zero, out hCounter);
                    }
                    if (status == 0)
                    {
                        _hPdhQuery = hQuery;
                        _hPdhCounter = hCounter;
                        PdhCollectQueryData(_hPdhQuery);
                    }
                    else
                    {
                        PdhCloseQuery(hQuery);
                    }
                }
            }
            catch { }
        }

        private double SampleCpuWatts()
        {
            try
            {
                InitEnergyMeter();
                if (_hPdhQuery != IntPtr.Zero && _hPdhCounter != IntPtr.Zero)
                {
                    if (PdhCollectQueryData(_hPdhQuery) == 0)
                    {
                        uint type;
                        PDH_FMT_COUNTERVALUE_DOUBLE val;
                        if (PdhGetFormattedCounterValue(_hPdhCounter, 0x00000200 /* PDH_FMT_DOUBLE */, out type, out val) == 0)
                        {
                            if (val.CStatus == 0 && val.doubleValue > 0 && val.doubleValue < 500000)
                            {
                                return val.doubleValue / 1000.0;
                            }
                        }
                    }
                }
            }
            catch { }
            return 0.0;
        }

        private void UpdatePower()
        {
            try
            {
                SYSTEM_POWER_STATUS sps;
                GetSystemPowerStatus(out sps);

                SYSTEM_BATTERY_STATE sbs;
                CallNtPowerInformation(5, IntPtr.Zero, 0, out sbs, (uint)Marshal.SizeOf(typeof(SYSTEM_BATTERY_STATE)));

                double cpuWatts = SampleCpuWatts();

                PowerData p = new PowerData();
                p.CpuWatts = cpuWatts;

                // Check if device actually has a battery
                bool hasBattery = sbs.BatteryPresent && (sps.BatteryFlag != 128) && (sps.BatteryLifePercent != 255);
                p.HasBattery = hasBattery;

                if (!hasBattery)
                {
                    // Desktop PC / Workstation without battery
                    p.BatteryPercent = 100;
                    p.IsAcOnline = true;
                    p.IsCharging = false;
                    p.IsDischarging = false;
                    p.Watts = cpuWatts;
                    p.StateKind = PowerStateKind.DesktopAc;
                    p.StatusText = p.GetStatusText(I18n.Current);
                    p.EstimatedTimeStr = p.GetEstimatedTimeText(I18n.Current);
                    p.StateColor = p.GetStateBrush(AppTheme.Current);
                }
                else
                {
                    p.BatteryPercent = sps.BatteryLifePercent <= 100 ? sps.BatteryLifePercent : 100;
                    p.IsAcOnline = (sps.ACLineStatus == 1 || sbs.AcOnLine);
                    p.RemainingCapacity = sbs.RemainingCapacity;
                    p.MaxCapacity = sbs.MaxCapacity;
                    p.RateMw = sbs.Rate;

                    if (p.IsAcOnline)
                    {
                        bool isChargingFlag = (sps.BatteryFlag & 8) != 0;
                        bool isCharging = sbs.Charging || isChargingFlag || sbs.Rate > 0;
                        p.IsCharging = isCharging;
                        p.IsDischarging = false;

                        if (isCharging && sbs.Rate > 0)
                        {
                            p.Watts = Math.Abs(sbs.Rate) / 1000.0;
                            p.StateKind = PowerStateKind.ChargingFast;

                            // Calculate charging ETA
                            if (sbs.MaxCapacity > sbs.RemainingCapacity && sbs.Rate > 0)
                            {
                                double neededMwh = (double)(sbs.MaxCapacity - sbs.RemainingCapacity);
                                double hours = neededMwh / (double)sbs.Rate;
                                p.EstimatedSeconds = (uint)(hours * 3600);
                            }
                        }
                        else if (p.BatteryPercent >= 99)
                        {
                            p.Watts = 0.0;
                            p.StateKind = PowerStateKind.ChargedFull;
                        }
                        else
                        {
                            p.Watts = 0.0;
                            p.StateKind = PowerStateKind.AcDirect;
                        }
                    }
                    else
                    {
                        // On battery
                        p.IsCharging = false;
                        p.IsDischarging = true;
                        double w = Math.Abs(sbs.Rate) / 1000.0;
                        p.Watts = -w;
                        p.StateKind = p.BatteryPercent <= 20 ? PowerStateKind.DischargingLow : PowerStateKind.DischargingNormal;

                        // Calculate remaining time
                        uint estSec = sbs.EstimatedTime;
                        if (estSec > 0 && estSec < 86400)
                        {
                            p.EstimatedSeconds = estSec;
                        }
                        else if (sps.BatteryLifeTime > 0 && sps.BatteryLifeTime < 86400)
                        {
                            p.EstimatedSeconds = (uint)sps.BatteryLifeTime;
                        }
                    }

                    p.StatusText = p.GetStatusText(I18n.Current);
                    p.EstimatedTimeStr = p.GetEstimatedTimeText(I18n.Current);
                    p.StateColor = p.GetStateBrush(AppTheme.Current);
                }

                if (PowerUpdated != null)
                {
                    PowerUpdated(p);
                }
            }
            catch { }
        }

        private void UpdateNetworkRate()
        {
            try
            {
                long currRecv = 0;
                long currSent = 0;
                GetPhysicalTraffic(out currRecv, out currSent);

                DateTime now = DateTime.UtcNow;
                double seconds = (now - _prevNetTime).TotalSeconds;
                if (seconds <= 0) seconds = 1.0;

                long deltaRecv = currRecv - _prevBytesRecv;
                long deltaSent = currSent - _prevBytesSent;

                if (deltaRecv > 0) _sessionBytesRecv += deltaRecv;
                if (deltaSent > 0) _sessionBytesSent += deltaSent;

                double downRate = deltaRecv / seconds;
                double upRate = deltaSent / seconds;

                if (downRate < 0) downRate = 0;
                if (upRate < 0) upRate = 0;

                _prevBytesRecv = currRecv;
                _prevBytesSent = currSent;
                _prevNetTime = now;

                NetworkData net = new NetworkData();
                net.DownBytesPerSec = downRate;
                net.UpBytesPerSec = upRate;
                net.DownSpeedStr = "↓ " + FormatCompactSpeed(downRate);
                net.UpSpeedStr = "↑ " + FormatCompactSpeed(upRate);
                net.SessionRecvMb = _sessionBytesRecv / (1024.0 * 1024.0);
                net.SessionSentMb = _sessionBytesSent / (1024.0 * 1024.0);
                net.PublicIp = _cachedPublicIp;
                net.CountryCode = _cachedCountryCode;

                if (AppSettings.Language == AppLanguage.En)
                {
                    if (!string.IsNullOrEmpty(_cachedCountryEn))
                    {
                        net.Country = _cachedCountryEn;
                    }
                    else if (!string.IsNullOrEmpty(_cachedCountryCode))
                    {
                        try { net.Country = new System.Globalization.RegionInfo(_cachedCountryCode).EnglishName; }
                        catch { net.Country = _cachedCountryZh; }
                    }
                    else
                    {
                        net.Country = "";
                    }
                    net.City = _cachedCityEn;
                }
                else
                {
                    net.Country = !string.IsNullOrEmpty(_cachedCountryZh) ? _cachedCountryZh : _cachedCountryEn;
                    net.City = !string.IsNullOrEmpty(_cachedCityZh) ? _cachedCityZh : _cachedCityEn;
                }
                net.Isp = _cachedIsp;

                // Find active primary IP & adapter name
                bool foundActive = false;
                string ifaceName = "";
                string localIp = "127.0.0.1";
                string linkSpeed = "--";
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    string desc = ni.Description.ToLower();
                    string name = ni.Name.ToLower();
                    if (desc.Contains("zerotier") || desc.Contains("virtual") || desc.Contains("vpn") ||
                        desc.Contains("mihomo") || desc.Contains("clash") || desc.Contains("tun") || desc.Contains("tap") || desc.Contains("wsl") ||
                        name.Contains("mihomo") || name.Contains("clash") || name.Contains("zerotier") || name.Contains("vethernet"))
                        continue;

                    foreach (UnicastIPAddressInformation u in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address))
                        {
                            ifaceName = ni.Name;
                            foundActive = true;
                            localIp = u.Address.ToString();
                            if (ni.Speed > 0)
                            {
                                if (ni.Speed >= 1000000000)
                                    linkSpeed = string.Format("{0:0.#} Gbps", ni.Speed / 1000000000.0);
                                else
                                    linkSpeed = string.Format("{0} Mbps", ni.Speed / 1000000);
                            }
                            break;
                        }
                    }
                    if (foundActive) break;
                }
                net.ActiveInterface = foundActive ? ifaceName : (I18n.Current.Lang == AppLanguage.Zh ? "无网络" : "No Network");
                net.LocalIp = localIp;
                net.LinkSpeedStr = linkSpeed;

                if (NetworkUpdated != null)
                {
                    NetworkUpdated(net);
                }
            }
            catch { }
        }

        private static string FormatCompactSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024)
            {
                return string.Format("{0:0}B", bytesPerSec);
            }
            else if (bytesPerSec < 1024 * 1024)
            {
                return string.Format("{0:0.0}K", bytesPerSec / 1024.0);
            }
            else
            {
                return string.Format("{0:0.0}M", bytesPerSec / (1024.0 * 1024.0));
            }
        }

        private void FetchPublicGeoIp()
        {
            string queryLang = AppSettings.Language == AppLanguage.En ? "en" : "zh-CN";
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://ip-api.com/json/?lang=" + queryLang);
                req.Timeout = 3500;
                req.UserAgent = "SysMonitor/1.0";
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> dict = ser.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null && dict.ContainsKey("status") && dict["status"].ToString() == "success")
                    {
                        _cachedPublicIp = dict.ContainsKey("query") ? dict["query"].ToString() : _cachedPublicIp;
                        _cachedCountryCode = dict.ContainsKey("countryCode") ? dict["countryCode"].ToString() : _cachedCountryCode;
                        _cachedIsp = dict.ContainsKey("isp") ? dict["isp"].ToString() : _cachedIsp;

                        string c = dict.ContainsKey("country") ? dict["country"].ToString() : "";
                        string ci = dict.ContainsKey("city") ? dict["city"].ToString() : "";

                        if (queryLang == "en")
                        {
                            _cachedCountryEn = c;
                            _cachedCityEn = ci;
                        }
                        else
                        {
                            _cachedCountryZh = c;
                            _cachedCityZh = ci;
                        }

                        if (!string.IsNullOrEmpty(_cachedCountryCode) && string.IsNullOrEmpty(_cachedCountryEn))
                        {
                            try { _cachedCountryEn = new System.Globalization.RegionInfo(_cachedCountryCode).EnglishName; } catch { }
                        }

                        UpdateNetworkRate();
                        return;
                    }
                }
            }
            catch { }

            // Fallback to simple ipify if ip-api fails
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.ipify.org");
                req.Timeout = 3000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string ip = reader.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(ip))
                    {
                        _cachedPublicIp = ip;
                    }
                }
            }
            catch { }
        }

        private void UpdateZeroTier()
        {
            ZeroTierData data = new ZeroTierData();
            if (string.IsNullOrEmpty(_ztToken))
            {
                InitZeroTierAuth();
            }

            if (string.IsNullOrEmpty(_ztToken))
            {
                data.IsInstalled = _ztDirFound;
                data.IsRunning = false;
                data.SummaryState = _ztDirFound ? ZtSummaryState.NotRunning : ZtSummaryState.NotInstalled;
                data.SummaryText = data.GetSummaryText(I18n.Current);
                data.SummaryColor = data.GetSummaryBrush(AppTheme.Current);
                if (ZeroTierUpdated != null) ZeroTierUpdated(data);
                return;
            }

            data.IsInstalled = true;

            try
            {
                // 1. Fetch status
                string statusUrl = string.Format("http://127.0.0.1:{0}/status", _ztPort);
                HttpWebRequest sReq = (HttpWebRequest)WebRequest.Create(statusUrl);
                sReq.Headers.Add("X-ZT1-Auth", _ztToken);
                sReq.Timeout = 1500;
                using (HttpWebResponse sResp = (HttpWebResponse)sReq.GetResponse())
                using (StreamReader sReader = new StreamReader(sResp.GetResponseStream()))
                {
                    string sJson = sReader.ReadToEnd();
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> statusDict = ser.Deserialize<Dictionary<string, object>>(sJson);
                    if (statusDict != null)
                    {
                        data.IsRunning = true;
                        if (statusDict.ContainsKey("address")) data.LocalNodeId = statusDict["address"].ToString();
                    }
                }

                // 2. Fetch peers
                string peerUrl = string.Format("http://127.0.0.1:{0}/peer", _ztPort);
                HttpWebRequest pReq = (HttpWebRequest)WebRequest.Create(peerUrl);
                pReq.Headers.Add("X-ZT1-Auth", _ztToken);
                pReq.Timeout = 1500;
                using (HttpWebResponse pResp = (HttpWebResponse)pReq.GetResponse())
                using (StreamReader pReader = new StreamReader(pResp.GetResponseStream()))
                {
                    string pJson = pReader.ReadToEnd();
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    object[] peerArray = (object[])ser.DeserializeObject(pJson);

                    int minLatency = 9999;
                    int directCount = 0;
                    int totalMoons = 0;
                    HashSet<string> currentMoonAddresses = new HashSet<string>();

                    foreach (Dictionary<string, object> p in peerArray)
                    {
                        string role = p.ContainsKey("role") ? p["role"].ToString() : "";
                        string address = p.ContainsKey("address") ? p["address"].ToString() : "";

                        if (role == "MOON")
                        {
                            totalMoons++;
                            currentMoonAddresses.Add(address);
                            MoonNode m = new MoonNode();
                            m.Address = address;
                            m.Role = role;
                            m.Latency = p.ContainsKey("latency") ? Convert.ToInt32(p["latency"]) : -1;

                            object[] paths = p.ContainsKey("paths") ? (object[])p["paths"] : new object[0];
                            if (paths.Length > 0 && m.Latency >= 0)
                            {
                                m.IsDirect = true;
                                m.IsOffline = false;
                                directCount++;
                                if (m.Latency < minLatency) minLatency = m.Latency;

                                Dictionary<string, object> firstPath = (Dictionary<string, object>)paths[0];
                                m.PhysicalAddress = firstPath.ContainsKey("address") ? firstPath["address"].ToString() : "";
                            }
                            else
                            {
                                m.IsDirect = false;
                                m.IsOffline = false;
                                m.PhysicalAddress = "Relay";
                            }

                            // Direct <-> Relay link state transition tracking
                            MoonLinkType currentType = m.IsDirect ? MoonLinkType.Direct : MoonLinkType.Relay;
                            if (!_moonLinkStates.ContainsKey(address))
                            {
                                _moonLinkStates[address] = currentType;
                                _moonRelayCounters[address] = 0;
                            }
                            else
                            {
                                MoonLinkType lastType = _moonLinkStates[address];
                                if (currentType == MoonLinkType.Direct)
                                {
                                    _moonRelayCounters[address] = 0;
                                    if (lastType != MoonLinkType.Direct)
                                    {
                                        _moonLinkStates[address] = MoonLinkType.Direct;
                                        if (_moonInitialScanDone && MoonDirectAlert != null)
                                        {
                                            MoonDirectAlert(address, m.Latency);
                                        }
                                    }
                                }
                                else // currentType == MoonLinkType.Relay
                                {
                                    if (lastType == MoonLinkType.Direct)
                                    {
                                        if (!_moonRelayCounters.ContainsKey(address)) _moonRelayCounters[address] = 0;
                                        _moonRelayCounters[address]++;
                                        if (_moonRelayCounters[address] >= 2)
                                        {
                                            _moonLinkStates[address] = MoonLinkType.Relay;
                                            if (_moonInitialScanDone && MoonRelayAlert != null)
                                            {
                                                MoonRelayAlert(address);
                                            }
                                        }
                                    }
                                }
                            }

                            data.Moons.Add(m);
                            _knownMoons[address] = m;
                        }
                    }

                    // Check if any historically discovered Moon is missing from current peer list
                    foreach (KeyValuePair<string, MoonNode> kvp in _knownMoons)
                    {
                        string addr = kvp.Key;
                        if (!currentMoonAddresses.Contains(addr))
                        {
                            MoonNode offMoon = new MoonNode
                            {
                                Address = addr,
                                Role = "MOON",
                                Latency = -1,
                                IsDirect = false,
                                IsOffline = false,
                                PhysicalAddress = "Relay",
                                StatusMsg = "Relay"
                            };
                            data.Moons.Add(offMoon);

                            if (_moonLinkStates.ContainsKey(addr) && _moonLinkStates[addr] == MoonLinkType.Direct)
                            {
                                if (!_moonRelayCounters.ContainsKey(addr)) _moonRelayCounters[addr] = 0;
                                _moonRelayCounters[addr]++;
                                if (_moonRelayCounters[addr] >= 2)
                                {
                                    _moonLinkStates[addr] = MoonLinkType.Relay;
                                    if (_moonInitialScanDone && MoonRelayAlert != null)
                                    {
                                        MoonRelayAlert(addr);
                                    }
                                }
                            }
                        }
                    }

                    int droppedCount = 0;
                    foreach (MoonNode m in data.Moons)
                    {
                        if (m.IsOffline) droppedCount++;
                    }

                    data.TotalMoons = data.Moons.Count;
                    data.DirectMoons = directCount;
                    data.DroppedMoonCount = droppedCount;
                    data.HasDroppedMoons = (droppedCount > 0);

                    if (data.HasDroppedMoons)
                    {
                        data.SummaryState = ZtSummaryState.Dropped;
                        data.SummaryText = data.GetSummaryText(I18n.Current);
                        data.SummaryColor = data.GetSummaryBrush(AppTheme.Current);
                    }
                    else if (data.TotalMoons > 0)
                    {
                        if (directCount == data.TotalMoons)
                        {
                            data.MinLatency = minLatency;
                            data.SummaryState = ZtSummaryState.Direct;
                            data.SummaryText = data.GetSummaryText(I18n.Current);
                            data.SummaryColor = data.GetSummaryBrush(AppTheme.Current);
                        }
                        else
                        {
                            data.SummaryState = ZtSummaryState.Relay;
                            data.SummaryText = data.GetSummaryText(I18n.Current);
                            data.SummaryColor = data.GetSummaryBrush(AppTheme.Current);
                        }
                    }
                    else
                    {
                        data.SummaryState = ZtSummaryState.NoneJoined;
                        data.SummaryText = data.GetSummaryText(I18n.Current);
                        data.SummaryColor = data.GetSummaryBrush(AppTheme.Current);
                    }
                }
            }
            catch
            {
                data.IsRunning = false;
                data.SummaryState = ZtSummaryState.NotRunning;
                data.SummaryText = data.GetSummaryText(I18n.Current);
                data.SummaryColor = data.GetSummaryBrush(AppTheme.Current);
            }

            _moonInitialScanDone = true;

            if (ZeroTierUpdated != null)
            {
                ZeroTierUpdated(data);
            }
        }
    }
}
