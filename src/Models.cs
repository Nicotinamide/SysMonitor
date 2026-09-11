using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace SysMonitor
{
    public class SystemLoadData
    {
        public double CpuPercent { get; set; }
        public double RamUsedGb { get; set; }
        public double RamTotalGb { get; set; }
        public int RamPercent { get; set; }

        public SystemLoadData()
        {
            CpuPercent = 0.0;
            RamUsedGb = 0.0;
            RamTotalGb = 0.0;
            RamPercent = 0;
        }
    }

    public enum PowerStateKind
    {
        Unknown,
        DesktopAc,
        ChargedFull,
        ChargingFast,
        AcDirect,
        DischargingNormal,
        DischargingLow
    }

    public class PowerData
    {
        public int BatteryPercent { get; set; }
        public double Watts { get; set; }
        public double CpuWatts { get; set; }
        public bool HasBattery { get; set; }
        public bool IsCharging { get; set; }
        public bool IsDischarging { get; set; }
        public bool IsAcOnline { get; set; }
        public PowerStateKind StateKind { get; set; }
        public uint EstimatedSeconds { get; set; }
        public string StatusText { get; set; }
        public Brush StateColor { get; set; }
        public uint RemainingCapacity { get; set; }
        public uint MaxCapacity { get; set; }
        public int RateMw { get; set; }
        public string EstimatedTimeStr { get; set; }

        public PowerData()
        {
            BatteryPercent = 100;
            Watts = 0.0;
            CpuWatts = 0.0;
            HasBattery = true;
            StateKind = PowerStateKind.Unknown;
            EstimatedSeconds = 0;
            StatusText = "";
            EstimatedTimeStr = "--";
            StateColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // #10b981
        }

        public string GetStatusText(TranslationSet i18n)
        {
            switch (StateKind)
            {
                case PowerStateKind.DesktopAc:
                    return i18n.PowerDesktop;
                case PowerStateKind.ChargedFull:
                    return i18n.PowerFull;
                case PowerStateKind.ChargingFast:
                    return string.Format(i18n.Lang == AppLanguage.Zh ? "⚡ 充电中 +{0:0.0}W" : "⚡ Charging +{0:0.0}W", Math.Abs(Watts));
                case PowerStateKind.AcDirect:
                    return i18n.PowerAcDirect;
                case PowerStateKind.DischargingNormal:
                    return string.Format(i18n.Lang == AppLanguage.Zh ? "电池供电 -{0:0.0}W" : "Battery -{0:0.0}W", Math.Abs(Watts));
                case PowerStateKind.DischargingLow:
                    return string.Format(i18n.Lang == AppLanguage.Zh ? "低电量 -{0:0.0}W" : "Low Battery -{0:0.0}W", Math.Abs(Watts));
                default:
                    return !string.IsNullOrEmpty(StatusText) ? StatusText : "--";
            }
        }

        public string GetCompactStatusText(TranslationSet i18n)
        {
            switch (StateKind)
            {
                case PowerStateKind.DesktopAc:
                    return i18n.Lang == AppLanguage.Zh ? "市电" : "AC";
                case PowerStateKind.ChargedFull:
                    return i18n.Lang == AppLanguage.Zh ? "满电" : "FULL";
                case PowerStateKind.ChargingFast:
                    return string.Format("+{0:0.0}W", Math.Abs(Watts));
                case PowerStateKind.AcDirect:
                    return i18n.Lang == AppLanguage.Zh ? "市电" : "AC";
                case PowerStateKind.DischargingNormal:
                case PowerStateKind.DischargingLow:
                    return string.Format("-{0:0.0}W", Math.Abs(Watts));
                default:
                    return !string.IsNullOrEmpty(StatusText) ? StatusText : "--";
            }
        }

        public string GetEstimatedTimeText(TranslationSet i18n)
        {
            switch (StateKind)
            {
                case PowerStateKind.DesktopAc:
                    return i18n.PowerNoBatteryDrain;
                case PowerStateKind.ChargedFull:
                    return i18n.PowerNoBatteryDrain;
                case PowerStateKind.ChargingFast:
                    if (EstimatedSeconds > 0 && EstimatedSeconds < 86400)
                    {
                        int h = (int)(EstimatedSeconds / 3600);
                        int m = (int)((EstimatedSeconds % 3600) / 60);
                        if (h > 0)
                        {
                            return string.Format(i18n.PowerChargingEtaHoursFormat, h, m);
                        }
                        else
                        {
                            return string.Format(i18n.PowerChargingEtaFormat, Math.Max(1, m));
                        }
                    }
                    return i18n.PowerCharging;
                case PowerStateKind.AcDirect:
                    return i18n.PowerNoBatteryDrain;
                case PowerStateKind.DischargingNormal:
                case PowerStateKind.DischargingLow:
                    if (EstimatedSeconds > 0 && EstimatedSeconds < 86400)
                    {
                        int h = (int)(EstimatedSeconds / 3600);
                        int m = (int)((EstimatedSeconds % 3600) / 60);
                        return i18n.Lang == AppLanguage.Zh ? string.Format("{0}小时{1}分", h, m) : string.Format("{0}h {1}m", h, m);
                    }
                    return "--";
                default:
                    return !string.IsNullOrEmpty(EstimatedTimeStr) ? EstimatedTimeStr : "--";
            }
        }

        public SolidColorBrush GetStateBrush(ThemePalette theme)
        {
            switch (StateKind)
            {
                case PowerStateKind.DesktopAc:
                case PowerStateKind.ChargedFull:
                case PowerStateKind.AcDirect:
                    return theme.AccentEmerald;
                case PowerStateKind.ChargingFast:
                    return theme.AccentBlue;
                case PowerStateKind.DischargingLow:
                    return theme.AccentRed;
                case PowerStateKind.DischargingNormal:
                    return theme.AccentAmber;
                default:
                    return theme.AccentEmerald;
            }
        }
    }

    public class NetworkData
    {
        public string DownSpeedStr { get; set; }
        public string UpSpeedStr { get; set; }
        public double DownBytesPerSec { get; set; }
        public double UpBytesPerSec { get; set; }
        public double SessionRecvMb { get; set; }
        public double SessionSentMb { get; set; }
        public string PublicIp { get; set; }
        public string CountryCode { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Isp { get; set; }
        public string ActiveInterface { get; set; }
        public string LocalIp { get; set; }
        public string LinkSpeedStr { get; set; }

        public NetworkData()
        {
            DownSpeedStr = "↓ 0.0K";
            UpSpeedStr = "↑ 0.0K";
            SessionRecvMb = 0.0;
            SessionSentMb = 0.0;
            PublicIp = "";
            CountryCode = "";
            Country = "";
            City = "";
            Isp = "";
            ActiveInterface = "";
            LocalIp = "127.0.0.1";
            LinkSpeedStr = "--";
        }
    }

    public class MoonNode
    {
        public string Address { get; set; }
        public int Latency { get; set; }
        public bool IsDirect { get; set; }
        public bool IsOffline { get; set; }
        public string PhysicalAddress { get; set; }
        public string Role { get; set; }
        public string StatusMsg { get; set; }
    }

    public enum MoonLinkType
    {
        Unknown,
        Direct,
        Relay
    }

    public enum ZtSummaryState
    {
        Detecting,
        NotInstalled,
        NotRunning,
        Dropped,
        Direct,
        Relay,
        NoneJoined
    }

    public class ZeroTierData
    {
        public bool IsInstalled { get; set; }
        public bool IsRunning { get; set; }
        public string LocalNodeId { get; set; }
        public List<MoonNode> Moons { get; set; }
        public int TotalMoons { get; set; }
        public int DirectMoons { get; set; }
        public bool HasDroppedMoons { get; set; }
        public int DroppedMoonCount { get; set; }
        public int MinLatency { get; set; }
        public ZtSummaryState SummaryState { get; set; }
        public string SummaryText { get; set; }
        public Brush SummaryColor { get; set; }

        public ZeroTierData()
        {
            IsInstalled = false;
            IsRunning = false;
            LocalNodeId = "";
            Moons = new List<MoonNode>();
            TotalMoons = 0;
            DirectMoons = 0;
            HasDroppedMoons = false;
            DroppedMoonCount = 0;
            MinLatency = -1;
            SummaryState = ZtSummaryState.Detecting;
            SummaryText = "";
            SummaryColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
        }

        public string GetSummaryText(TranslationSet i18n)
        {
            switch (SummaryState)
            {
                case ZtSummaryState.NotInstalled:
                    return i18n.ZtNotInstalled;
                case ZtSummaryState.NotRunning:
                    return i18n.ZtOffline;
                case ZtSummaryState.Dropped:
                    return string.Format(i18n.MoonDroppedSummary, DroppedMoonCount);
                case ZtSummaryState.Direct:
                    return MinLatency >= 0 ? string.Format("{0}ms", MinLatency) : i18n.Direct;
                case ZtSummaryState.Relay:
                    return i18n.Relay;
                case ZtSummaryState.NoneJoined:
                    return i18n.Lang == AppLanguage.Zh ? "未加入" : "None";
                default:
                    return !string.IsNullOrEmpty(SummaryText) ? SummaryText : i18n.NetFetching;
            }
        }

        public SolidColorBrush GetSummaryBrush(ThemePalette theme)
        {
            switch (SummaryState)
            {
                case ZtSummaryState.NotInstalled:
                case ZtSummaryState.NotRunning:
                    return theme.TextMuted;
                case ZtSummaryState.Dropped:
                    return theme.AccentRed;
                case ZtSummaryState.Direct:
                    return theme.AccentEmerald;
                case ZtSummaryState.Relay:
                    return theme.AccentAmber;
                default:
                    return theme.TextMuted;
            }
        }
    }

    public class MemberNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Ip { get; set; }
        public List<string> AllIps { get; set; }
        public bool Authorized { get; set; }
        public bool IsOnline { get; set; }
        public int Latency { get; set; }
        public string PhysicalAddress { get; set; }
        public string Role { get; set; }
        public string PinyinInitials { get; set; }

        public MemberNode()
        {
            Id = "";
            Name = "";
            Ip = "";
            AllIps = new List<string>();
            Authorized = true;
            IsOnline = false;
            Latency = -1;
            PhysicalAddress = "";
            Role = "LEAF";
            PinyinInitials = "";
        }
    }
}
