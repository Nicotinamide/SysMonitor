using System;

namespace SysMonitor
{
    public enum AppLanguage
    {
        Zh,
        En
    }

    public class TranslationSet
    {
        public AppLanguage Lang { get; set; }

        // Float Widget
        public string Power { get; set; }
        public string PowerCharging { get; set; }
        public string PowerDischarging { get; set; }
        public string PowerAcOnline { get; set; }
        public string PowerFull { get; set; }
        public string NetPublicIp { get; set; }
        public string NetFetching { get; set; }
        public string NetLocalLan { get; set; }
        public string ZtOnline { get; set; }
        public string ZtOffline { get; set; }
        public string ZtNotInstalled { get; set; }

        // Detail Window Titles & Labels
        public string DetailWinTitle { get; set; }
        public string SectionCpuRam { get; set; }
        public string CpuUsage { get; set; }
        public string Memory { get; set; }
        public string SectionPower { get; set; }
        public string PowerPc { get; set; }
        public string PowerBattery { get; set; }
        public string PowerSupply { get; set; }
        public string BatteryCapacity { get; set; }
        public string RunningOnAc { get; set; }
        public string RunningOnBattery { get; set; }
        public string EstBatteryLife { get; set; }
        public string SectionNetwork { get; set; }
        public string ActiveIface { get; set; }
        public string LinkSpeed { get; set; }
        public string LocalIp { get; set; }
        public string SessionTraffic { get; set; }
        public string PublicExitIsp { get; set; }
        public string SectionZeroTier { get; set; }
        public string LocalNodeId { get; set; }
        public string MoonRelays { get; set; }
        public string MoonAlertDropped { get; set; }
        public string Direct { get; set; }
        public string Relay { get; set; }
        public string Latency { get; set; }
        public string Dropped { get; set; }

        // Localized Telemetry States
        public string PowerChargedFull { get; set; }
        public string PowerDesktop { get; set; }
        public string PowerChargingFast { get; set; }
        public string PowerAcDirect { get; set; }
        public string PowerAcBypass { get; set; }
        public string PowerAcCharging { get; set; }
        public string PowerBatteryProtected { get; set; }
        public string PowerBatteryConserve { get; set; }
        public string PowerNoBatteryDrain { get; set; }
        public string PowerChargingEtaFormat { get; set; }
        public string PowerChargingEtaHoursFormat { get; set; }
        public string PowerCpuSuffix { get; set; }
        public string PowerTotalSuffix { get; set; }
        public string PowerNoBattery { get; set; }
        public string MoonNoneJoined { get; set; }
        public string MoonDroppedSummary { get; set; }
        public string Close { get; set; }

        // Member Directory
        public string SectionMembers { get; set; }
        public string FilterAll { get; set; }
        public string NoOnlineMembers { get; set; }
        public string NoOfflineMembers { get; set; }
        public string ToggleSearch { get; set; }
        public string SearchPlaceholder { get; set; }
        public string SyncedNodesFormat { get; set; }
        public string CachedNodesFormat { get; set; }
        public string Syncing { get; set; }
        public string NoTokenHint { get; set; }
        public string Forbidden403 { get; set; }
        public string Unauthorized401 { get; set; }
        public string CopiedIpFormat { get; set; }
        public string CopiedIdFormat { get; set; }
        public string CopiedAll { get; set; }
        public string Online { get; set; }
        public string Offline { get; set; }
        public string NoIp { get; set; }

        // Member Context Menu
        public string MenuCopyIpFormat { get; set; }
        public string MenuCopyIdFormat { get; set; }
        public string MenuCopyAll { get; set; }
        public string MenuRdp { get; set; }
        public string MenuPing { get; set; }
        public string MenuOpenHttp { get; set; }

        // Settings Panel
        public string SettingsTitle { get; set; }
        public string SettingsDesc { get; set; }
        public string ControllerUrl { get; set; }
        public string NetworkId { get; set; }
        public string ApiToken { get; set; }
        public string TestConnection { get; set; }
        public string SaveConfig { get; set; }
        public string TestingConn { get; set; }
        public string ConnSuccessFormat { get; set; }
        public string ConnFailedFormat { get; set; }
        public string SavedToast { get; set; }
        public string ThemeSelect { get; set; }

        // Update
        public string CheckUpdate { get; set; }
        public string CheckingUpdate { get; set; }
        public string AlreadyLatest { get; set; }
        public string NewVersionFound { get; set; }
        public string DownloadUpdate { get; set; }
        public string UpdateFailed { get; set; }
        public string CurrentVersionFormat { get; set; }
        public string ViewOnGitHub { get; set; }
        public string LangSelect { get; set; }
        public string ThemeDark { get; set; }
        public string ThemeLight { get; set; }
        public string ToggleToken { get; set; }

        // Widget Modules Customization
        public string SettingsModulesTitle { get; set; }
        public string ModPowerTitle { get; set; }
        public string ModNetTitle { get; set; }
        public string ModZtTitle { get; set; }
        public string ModComputeTitle { get; set; }
        public string MoveUp { get; set; }
        public string MoveDown { get; set; }
        public string AtLeastOneModule { get; set; }

        // Context Menu
        public string MenuDetail { get; set; }
        public string MenuSearch { get; set; }
        public string MenuTheme { get; set; }
        public string MenuThemeDark { get; set; }
        public string MenuThemeLight { get; set; }
        public string MenuLang { get; set; }
        public string MenuLangZh { get; set; }
        public string MenuLangEn { get; set; }
        public string MenuStartup { get; set; }
        public string MenuExit { get; set; }

        // Balloon Notifications (Concise & Crisp)
        public string NotifyMoonDirectTitle { get; set; }
        public string NotifyMoonDirectFormat { get; set; }
        public string NotifyMoonRelayTitle { get; set; }
        public string NotifyMoonRelayFormat { get; set; }
        public string NotifyMoonOnlineTitle { get; set; }
        public string NotifyMoonOnlineFormat { get; set; }
        public string NotifyMoonDroppedTitle { get; set; }
        public string NotifyMoonDroppedFormat { get; set; }
        public string NotifyAcConnectedTitle { get; set; }
        public string NotifyAcConnectedText { get; set; }
        public string NotifyBatteryModeTitle { get; set; }
        public string NotifyBatteryModeFormat { get; set; }
        public string NotifyLowBatteryTitle { get; set; }
        public string NotifyLowBatteryFormat { get; set; }
        public string NotifyBatteryFullTitle { get; set; }
        public string NotifyBatteryFullText { get; set; }
        public string NotifySettingsSavedTitle { get; set; }
        public string NotifySettingsSavedText { get; set; }

        public static TranslationSet CreateZh()
        {
            return new TranslationSet
            {
                Lang = AppLanguage.Zh,
                Power = "实时功耗",
                PowerCharging = "充电中",
                PowerDischarging = "耗电中",
                PowerAcOnline = "交流供电",
                PowerFull = "已充满",
                NetPublicIp = "公网 IP",
                NetFetching = "获取中...",
                NetLocalLan = "局域网",
                ZtOnline = "已连入",
                ZtOffline = "已断开",
                ZtNotInstalled = "未安装",

                DetailWinTitle = "系统遥测与网络",
                SectionCpuRam = "核心计算负载",
                CpuUsage = "CPU 使用率",
                Memory = "物理内存",
                SectionPower = "供电与续航",
                PowerPc = "电脑功耗",
                PowerBattery = "电池功率",
                PowerSupply = "供电方式",
                BatteryCapacity = "电池电量",
                RunningOnAc = "市电供电",
                RunningOnBattery = "电池供电",
                EstBatteryLife = "预估续航",
                SectionNetwork = "物理网卡与流量",
                ActiveIface = "活动网卡",
                LinkSpeed = "物理协商",
                LocalIp = "局域网 IP",
                SessionTraffic = "本次流量",
                PublicExitIsp = "公网出口与归属",
                SectionZeroTier = "ZeroTier 虚拟局域网",
                LocalNodeId = "本地节点 ID",
                MoonRelays = "Moon 节点直连中继",
                MoonAlertDropped = "Moon 节点连接中断!",
                Direct = "直连",
                Relay = "中继",
                Latency = "延时",
                Dropped = "离线",

                PowerChargedFull = "已充满",
                PowerDesktop = "交流供电",
                PowerChargingFast = "充电中",
                PowerAcDirect = "市电直供",
                PowerAcBypass = "交流供电",
                PowerAcCharging = "交流供电",
                PowerBatteryProtected = "满电保护",
                PowerBatteryConserve = "养护模式",
                PowerNoBatteryDrain = "市电直供",
                PowerChargingEtaFormat = "{0}分钟充满",
                PowerChargingEtaHoursFormat = "{0}小时{1}分充满",
                PowerCpuSuffix = "",
                PowerTotalSuffix = "",
                PowerNoBattery = "无电池",
                MoonNoneJoined = "未加入 Moon 加速节点",
                MoonDroppedSummary = "{0} 个掉线",
                Close = "关闭",

                SectionMembers = "成员设备",
                FilterAll = "全部",
                NoOnlineMembers = "暂无在线成员",
                NoOfflineMembers = "暂无离线成员",
                ToggleSearch = "展开/收起搜索框",
                SearchPlaceholder = "输入姓名 / 拼音首字母 / IP / 节点ID...",
                SyncedNodesFormat = "已同步 {0} 节点",
                CachedNodesFormat = "已缓存 {0} 节点",
                Syncing = "同步中...",
                NoTokenHint = "未配置 Token",
                Forbidden403 = "403 禁止访问",
                Unauthorized401 = "401 Token 无效",
                CopiedIpFormat = "✓ 已复制 IP: {0}",
                CopiedIdFormat = "✓ 已复制 Node ID: {0}",
                CopiedAll = "✓ 已复制全部信息",
                Online = "在线",
                Offline = "离线",
                NoIp = "无IP",

                MenuCopyIpFormat = "📋 复制 IP: {0}",
                MenuCopyIdFormat = "📋 复制 Node ID: {0}",
                MenuCopyAll = "📋 复制设备全称与 IP",
                MenuRdp = "🖥️ 远程桌面连接",
                MenuPing = "📡 Ping 连通性测试",
                MenuOpenHttp = "🌐 浏览器打开",

                SettingsTitle = "控制器接入配置",
                SettingsDesc = "凭据经由 Windows 本机主密钥强加密，非明文存储，杜绝泄露",
                ControllerUrl = "控制器地址:",
                NetworkId = "网络 ID:",
                ApiToken = "API Token:",
                TestConnection = "测试连接",
                SaveConfig = "保存配置",
                TestingConn = "测试中...",
                ConnSuccessFormat = "✓ 连接成功，在线节点: {0}",
                ConnFailedFormat = "✕ 连接失败: {0}",
                SavedToast = "✓ 接入配置已成功加密保存！",
                ThemeSelect = "界面主题风格:",
                CheckUpdate = "检查更新",
                CheckingUpdate = "正在检查更新...",
                AlreadyLatest = "✓ 当前已是最新版本",
                NewVersionFound = "发现新版本",
                DownloadUpdate = "拉取更新",
                UpdateFailed = "检查更新失败",
                CurrentVersionFormat = "当前版本: {0}",
                ViewOnGitHub = "前往 GitHub 查看",
                LangSelect = "系统界面语言:",
                ThemeDark = "🌙 深色模式",
                ThemeLight = "☀️ 浅色模式",
                ToggleToken = "显隐 Token",

                SettingsModulesTitle = "首页微件模块定制",
                ModPowerTitle = "⚡ 供电与续航",
                ModNetTitle = "🌐 网卡与流量",
                ModZtTitle = "🔗 ZeroTier 互联",
                ModComputeTitle = "💻 CPU 与内存",
                MoveUp = "上移",
                MoveDown = "下移",
                AtLeastOneModule = "请至少保留一个微件模块",

                MenuDetail = "📊 查看遥测详情",
                MenuSearch = "🔎 搜索成员设备",
                MenuTheme = "🌓 切换主题",
                MenuThemeDark = "🌙 深色模式",
                MenuThemeLight = "☀️ 浅色模式",
                MenuLang = "🌐 界面语言",
                MenuLangZh = "🇨🇳 简体中文",
                MenuLangEn = "🇺🇸 English",
                MenuStartup = "🚀 开机自动启动",
                MenuExit = "✕ 退出监视微件",

                // Balloon Notifications (Concise & Crisp)
                NotifyMoonDirectTitle = "Moon 节点直连",
                NotifyMoonDirectFormat = "节点 {0} 已直连 ({1}ms)",
                NotifyMoonRelayTitle = "Moon 节点中继",
                NotifyMoonRelayFormat = "节点 {0} 直连丢失，转为中继",
                NotifyMoonOnlineTitle = "Moon 节点上线",
                NotifyMoonOnlineFormat = "节点 {0} 已连接",
                NotifyMoonDroppedTitle = "Moon 节点掉线",
                NotifyMoonDroppedFormat = "节点 {0} 离线",
                NotifyAcConnectedTitle = "已连接市电",
                NotifyAcConnectedText = "交流供电中",
                NotifyBatteryModeTitle = "电池供电",
                NotifyBatteryModeFormat = "剩余 {0}%",
                NotifyLowBatteryTitle = "电量偏低",
                NotifyLowBatteryFormat = "仅剩 {0}%，请充电",
                NotifyBatteryFullTitle = "电量已满",
                NotifyBatteryFullText = "100% 直供保护",
                NotifySettingsSavedTitle = "配置已保存",
                NotifySettingsSavedText = "参数已生效"
            };
        }

        public static TranslationSet CreateEn()
        {
            return new TranslationSet
            {
                Lang = AppLanguage.En,
                Power = "Power",
                PowerCharging = "Charging",
                PowerDischarging = "Discharging",
                PowerAcOnline = "AC Power",
                PowerFull = "Full",
                NetPublicIp = "Public IP",
                NetFetching = "Fetching...",
                NetLocalLan = "LAN",
                ZtOnline = "Online",
                ZtOffline = "Offline",
                ZtNotInstalled = "Not Installed",

                DetailWinTitle = "System Telemetry",
                SectionCpuRam = "Compute Load",
                CpuUsage = "CPU Usage",
                Memory = "RAM",
                SectionPower = "Power & Battery",
                PowerPc = "PC Power",
                PowerBattery = "Battery Flow",
                PowerSupply = "Source",
                BatteryCapacity = "Battery Level",
                RunningOnAc = "AC Power",
                RunningOnBattery = "Battery",
                EstBatteryLife = "Est. Life",
                SectionNetwork = "Network & Traffic",
                ActiveIface = "Adapter",
                LinkSpeed = "Speed",
                LocalIp = "LAN IP",
                SessionTraffic = "Traffic",
                PublicExitIsp = "Public Exit & ISP",
                SectionZeroTier = "ZeroTier Virtual LAN",
                LocalNodeId = "Local Node ID",
                MoonRelays = "Moon P2P Relays",
                MoonAlertDropped = "Moon Relay Disconnected!",
                Direct = "DIRECT",
                Relay = "RELAY",
                Latency = "Latency",
                Dropped = "DROPPED",

                PowerChargedFull = "Full",
                PowerDesktop = "Desktop AC",
                PowerChargingFast = "Charging",
                PowerAcDirect = "AC Direct",
                PowerAcBypass = "AC Power",
                PowerAcCharging = "AC Power",
                PowerBatteryProtected = "Protected",
                PowerBatteryConserve = "Conserve",
                PowerNoBatteryDrain = "AC Direct",
                PowerChargingEtaFormat = "{0}m to full",
                PowerChargingEtaHoursFormat = "{0}h {1}m to full",
                PowerCpuSuffix = "",
                PowerTotalSuffix = "",
                PowerNoBattery = "No Battery",
                MoonNoneJoined = "No Moon nodes joined",
                MoonDroppedSummary = "{0} Dropped",
                Close = "Close",

                SectionMembers = "Member Directory",
                FilterAll = "All",
                NoOnlineMembers = "No online members",
                NoOfflineMembers = "No offline members",
                ToggleSearch = "Toggle search box",
                SearchPlaceholder = "Search name, pinyin, IP, Node ID...",
                SyncedNodesFormat = "Synced {0} nodes",
                CachedNodesFormat = "Cached {0} nodes",
                Syncing = "Syncing...",
                NoTokenHint = "Token Missing",
                Forbidden403 = "403 Forbidden",
                Unauthorized401 = "401 Invalid Token",
                CopiedIpFormat = "✓ Copied IP: {0}",
                CopiedIdFormat = "✓ Copied Node ID: {0}",
                CopiedAll = "✓ Copied all info",
                Online = "ONLINE",
                Offline = "OFFLINE",
                NoIp = "NO IP",

                MenuCopyIpFormat = "📋 Copy IP: {0}",
                MenuCopyIdFormat = "📋 Copy Node ID: {0}",
                MenuCopyAll = "📋 Copy Device Name & IP",
                MenuRdp = "🖥️ Remote Desktop",
                MenuPing = "📡 Ping Test",
                MenuOpenHttp = "🌐 Open in Browser",

                SettingsTitle = "Controller Settings",
                SettingsDesc = "Credentials secured with Windows Master Key encryption",
                ControllerUrl = "Controller URL:",
                NetworkId = "Network ID:",
                ApiToken = "API Token:",
                TestConnection = "Test Connection",
                SaveConfig = "Save Config",
                TestingConn = "Testing...",
                ConnSuccessFormat = "✓ Connected: {0} active nodes",
                ConnFailedFormat = "✕ Connection failed: {0}",
                SavedToast = "✓ Settings encrypted and saved!",
                ThemeSelect = "Interface Theme:",
                CheckUpdate = "Check Update",
                CheckingUpdate = "Checking for updates...",
                AlreadyLatest = "✓ Already up to date",
                NewVersionFound = "New version available",
                DownloadUpdate = "Update Now",
                UpdateFailed = "Failed to check update",
                CurrentVersionFormat = "Version: {0}",
                ViewOnGitHub = "View on GitHub",
                LangSelect = "Display Language:",
                ThemeDark = "🌙 Dark Mode",
                ThemeLight = "☀️ Light Mode",
                ToggleToken = "Show/Hide Token",

                SettingsModulesTitle = "Widget Modules & Order",
                ModPowerTitle = "⚡ Power & Battery",
                ModNetTitle = "🌐 Network & Traffic",
                ModZtTitle = "🔗 ZeroTier Mesh",
                ModComputeTitle = "💻 CPU & Memory",
                MoveUp = "Up",
                MoveDown = "Down",
                AtLeastOneModule = "Keep at least one module enabled",

                MenuDetail = "📊 Telemetry & Members",
                MenuSearch = "🔎 Search Members",
                MenuTheme = "🌓 Switch Theme",
                MenuThemeDark = "🌙 Dark Mode",
                MenuThemeLight = "☀️ Light Mode",
                MenuLang = "🌐 Language",
                MenuLangZh = "🇨🇳 简体中文",
                MenuLangEn = "🇺🇸 English",
                MenuStartup = "🚀 Launch at Startup",
                MenuExit = "✕ Exit SysMonitor",

                // Balloon Notifications (Concise & Crisp)
                NotifyMoonDirectTitle = "Moon Direct Connected",
                NotifyMoonDirectFormat = "Node {0} switched to Direct ({1}ms)",
                NotifyMoonRelayTitle = "Moon Relayed",
                NotifyMoonRelayFormat = "Node {0} direct lost, fallback to Relay",
                NotifyMoonOnlineTitle = "Moon Online",
                NotifyMoonOnlineFormat = "Node {0} connected",
                NotifyMoonDroppedTitle = "Moon Offline",
                NotifyMoonDroppedFormat = "Node {0} disconnected",
                NotifyAcConnectedTitle = "AC Connected",
                NotifyAcConnectedText = "Running on mains power",
                NotifyBatteryModeTitle = "Battery Mode",
                NotifyBatteryModeFormat = "{0}% remaining",
                NotifyLowBatteryTitle = "Low Battery",
                NotifyLowBatteryFormat = "{0}% remaining, plug in",
                NotifyBatteryFullTitle = "Fully Charged",
                NotifyBatteryFullText = "100% on bypass",
                NotifySettingsSavedTitle = "Settings Saved",
                NotifySettingsSavedText = "Config applied"
            };
        }
    }

    public static class I18n
    {
        private static TranslationSet _current = TranslationSet.CreateZh();

        public static TranslationSet Current
        {
            get { return _current; }
        }

        public static AppLanguage CurrentLanguage
        {
            get { return _current.Lang; }
        }

        public static void SetLanguage(AppLanguage lang)
        {
            _current = lang == AppLanguage.En ? TranslationSet.CreateEn() : TranslationSet.CreateZh();
        }
    }
}
