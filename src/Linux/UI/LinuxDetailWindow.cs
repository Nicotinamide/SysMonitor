using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SysMonitor;

namespace SysMonitor.Linux.UI
{
    public class LinuxDetailWindow : Window
    {
        private LinuxFloatingWindow _parentFloat;

        // Root & Container
        private Border _rootBorder;
        private StackPanel _cardsStack;
        private ScrollViewer _svRoot;
        private Border _overlaySettings;
        private Border _bToast;
        private TextBlock _tbToast;
        private DispatcherTimer _toastTimer;

        // Card 1: CPU & RAM
        private TextBlock _tbCpuVal;
        private Border _rectCpuBar;
        private TextBlock _tbRamPercent;
        private TextBlock _tbRamVal;
        private Border _rectRamBar;

        // Card 2: Power
        private Border _bPowerBadge;
        private TextBlock _tbPowerBadge;
        private TextBlock _tbPowerPcWatts;
        private TextBlock _tbPowerBatWatts;
        private TextBlock _tbPowerBatLevel;
        private TextBlock _tbPowerBatEta;

        // Card 3: Network
        private TextBlock _tbNetIface;
        private TextBlock _tbNetLinkSpeed;
        private TextBlock _tbNetLocalIp;
        private TextBlock _tbNetSessionTraffic;
        private TextBlock _tbNetFlag;
        private TextBlock _tbNetPublicIp;
        private TextBlock _tbNetGeoIsp;

        // Card 4: ZeroTier
        private Border _cardZt;
        private Border _bZtBadge;
        private TextBlock _tbZtBadge;
        private TextBlock _tbZtNode;
        private StackPanel _spMoons;

        // Card 5: Member Directory
        private Border _cardMember;
        private LinuxMemberDirectoryEngine _memberDir;
        private TextBlock _tbMemberStatus;
        private Border _chipAll;
        private TextBlock _tbChipAll;
        private Border _chipOnline;
        private TextBlock _tbChipOnline;
        private Border _chipOffline;
        private TextBlock _tbChipOffline;
        private Button _btnToggleSearch;
        private Border _searchBoxBorder;
        private TextBox _tbMemberSearch;
        private ScrollViewer _svMembers;
        private StackPanel _spMemberResults;

        private enum MemberFilterType { All, Online, Offline }
        private MemberFilterType _currentMemberFilter = MemberFilterType.All;

        // Settings Fields
        private TextBox _txtSettingUrl;
        private TextBox _txtSettingNwid;
        private TextBox _txtSettingToken;
        private TextBlock _lblTestStatus;
        private TextBlock _tbUpdateStatus;
        private Button _btnPullUpdate;
        private string _latestDownloadUrl;

        // Cached Telemetry
        private LinuxSystemLoadData _lastLoad;
        private LinuxPowerData _lastPower;
        private LinuxNetworkData _lastNet;
        private LinuxZeroTierData _lastZt;
        private List<LinuxMemberNode> _lastMemberList = new List<LinuxMemberNode>();

        public DateTime LastDeactivatedTime { get; private set; }

        public LinuxDetailWindow(LinuxFloatingWindow parentFloat)
        {
            _parentFloat = parentFloat;

            Title = "SysMonitor Details";
            RequestedThemeVariant = LinuxSettings.IsDark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
            SystemDecorations = SystemDecorations.None;
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            Width = 385;
            MaxHeight = 650;
            SizeToContent = SizeToContent.Height;

            Deactivated += (s, e) =>
            {
                LastDeactivatedTime = DateTime.UtcNow;
                this.Hide();
            };

            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                if (_bToast != null) _bToast.IsVisible = false;
            };

            _memberDir = new LinuxMemberDirectoryEngine();
            _memberDir.StatusChanged += (st) => Dispatcher.UIThread.Post(() => OnMemberStatusChanged(st));
            _memberDir.MembersUpdated += (list) => Dispatcher.UIThread.Post(() => OnMembersUpdated(list));
            _memberDir.ConfigChanged += () => Dispatcher.UIThread.Post(() => UpdateZeroTierCardsVisibility());

            BuildUi();

            LinuxSettings.SettingsChanged += () =>
            {
                Dispatcher.UIThread.Post(() => RebuildUi());
            };
        }

        public void FocusSearch()
        {
            Dispatcher.UIThread.Post(() =>
            {
                ExpandSearchBox();
            });
        }

        public void RebuildUi()
        {
            RequestedThemeVariant = LinuxSettings.IsDark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
            bool wasSettingsOpen = _overlaySettings != null && _overlaySettings.IsVisible;
            bool wasSearchOpen = _searchBoxBorder != null && _searchBoxBorder.IsVisible;
            string prevSearch = _tbMemberSearch?.Text ?? "";

            BuildUi();

            if (wasSettingsOpen && _overlaySettings != null) _overlaySettings.IsVisible = true;
            if (wasSearchOpen && _searchBoxBorder != null)
            {
                _searchBoxBorder.IsVisible = true;
                if (_btnToggleSearch != null) _btnToggleSearch.Foreground = LinuxTheme.Current.AccentBlue;
            }
            if (!string.IsNullOrEmpty(prevSearch) && _tbMemberSearch != null) _tbMemberSearch.Text = prevSearch;

            ReplayTelemetry();
        }

        private void BuildUi()
        {
            var theme = LinuxTheme.Current;
            var i18n = I18n.Current;

            _rootBorder = new Border
            {
                Width = 385,
                CornerRadius = new CornerRadius(14),
                Background = theme.WindowBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                BoxShadow = BoxShadows.Parse(theme.IsDark ? "0 4 22 #50000000" : "0 4 22 #25000000")
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Row 0: Pinned Top Header
            rootGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star)); // Row 1: Scrollable Cards Body

            // 1. Pinned Header (Title, Tag, Settings, Close)
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var tbTitle = new TextBlock
            {
                Text = "⚡ " + i18n.DetailWinTitle,
                Foreground = theme.TextPrimary,
                FontSize = 12.5,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            var sysChip = new Border
            {
                Background = theme.CardBg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(8, 0, 0, 0),
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(0.6),
                VerticalAlignment = VerticalAlignment.Center
            };
            string osName = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "WINDOWS" :
                            (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? "MACOS" : "LINUX");
            var tbSys = new TextBlock
            {
                Text = string.Format("{0} {1}-BIT", osName, Environment.Is64BitOperatingSystem ? "64" : "32"),
                FontSize = 9,
                FontWeight = FontWeight.SemiBold,
                Foreground = theme.TextSecondary
            };
            sysChip.Child = tbSys;
            titleSp.Children.Add(tbTitle);
            titleSp.Children.Add(sysChip);
            Grid.SetColumn(titleSp, 0);
            headerGrid.Children.Add(titleSp);

            var topBtns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            var btnSettings = LinuxTheme.CreateIconButton("⚙", i18n.SettingsTitle, () => ToggleSettingsView(), 12);
            var btnClose = LinuxTheme.CreateIconButton("✕", i18n.Close, () => this.Hide(), 11);
            topBtns.Children.Add(btnSettings);
            topBtns.Children.Add(btnClose);
            Grid.SetColumn(topBtns, 1);
            headerGrid.Children.Add(topBtns);
            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // Container for all 5 cards
            _cardsStack = new StackPanel { Spacing = 8 };

            // Card 1: 💻 核心计算负载
            _cardsStack.Children.Add(BuildLoadCard(theme, i18n));

            // Card 2: 🔋 供电与续航
            _cardsStack.Children.Add(BuildPowerCard(theme, i18n));

            // Card 3: 🌐 物理网卡与流量
            _cardsStack.Children.Add(BuildNetworkCard(theme, i18n));

            // Card 4: 🔗 ZeroTier 虚拟局域网
            _cardZt = BuildZeroTierCard(theme, i18n);
            _cardsStack.Children.Add(_cardZt);

            // Card 5: 👥 成员设备
            _cardMember = BuildMemberCard(theme, i18n);
            _cardsStack.Children.Add(_cardMember);

            // ScrollViewer for cards
            _svRoot = new ScrollViewer
            {
                MaxHeight = 560,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = _cardsStack
            };
            Grid.SetRow(_svRoot, 1);
            rootGrid.Children.Add(_svRoot);

            // Settings Overlay (Floating on top of cards)
            _overlaySettings = BuildSettingsOverlay(theme, i18n);
            Grid.SetRow(_overlaySettings, 1);
            rootGrid.Children.Add(_overlaySettings);

            // Toast Popup at bottom
            _bToast = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 16, 185, 129)),
                BorderBrush = theme.AccentEmerald,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 16),
                IsVisible = false
            };
            _tbToast = new TextBlock
            {
                Text = "",
                FontSize = 10.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White
            };
            _bToast.Child = _tbToast;
            Grid.SetRow(_bToast, 1);
            rootGrid.Children.Add(_bToast);

            UpdateZeroTierCardsVisibility();

            _rootBorder.Child = rootGrid;
            Content = _rootBorder;
        }

        // ==========================================
        // Card 1: 💻 核心计算负载
        // ==========================================
        private Border BuildLoadCard(LinuxThemePalette theme, TranslationSet i18n)
        {
            var card = LinuxTheme.CreateCardBorder();
            var sp = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
            sp.Children.Add(LinuxTheme.CreateCardHeader("💻 " + i18n.SectionCpuRam));

            var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(10, GridUnitType.Pixel)); // gap
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            // CPU Col
            var spCpu = new StackPanel();
            var gCpuTop = new Grid();
            gCpuTop.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gCpuTop.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var lblCpu = LinuxTheme.CreateMutedText(i18n.CpuUsage);
            Grid.SetColumn(lblCpu, 0);
            _tbCpuVal = new TextBlock
            {
                Text = "0.0%",
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                Foreground = theme.AccentBlue,
                FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
            };
            Grid.SetColumn(_tbCpuVal, 1);
            gCpuTop.Children.Add(lblCpu);
            gCpuTop.Children.Add(_tbCpuVal);
            spCpu.Children.Add(gCpuTop);

            var trackCpu = LinuxTheme.CreateProgressBar(out _rectCpuBar, theme.AccentBlue);
            spCpu.Children.Add(trackCpu);
            Grid.SetColumn(spCpu, 0);
            grid.Children.Add(spCpu);

            // RAM Col
            var spRam = new StackPanel();
            var gRamTop = new Grid();
            gRamTop.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gRamTop.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _tbRamPercent = LinuxTheme.CreateMutedText(i18n.Memory + " 0%");
            Grid.SetColumn(_tbRamPercent, 0);
            _tbRamVal = new TextBlock
            {
                Text = "--/--G",
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = theme.AccentEmerald,
                FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
            };
            Grid.SetColumn(_tbRamVal, 1);
            gRamTop.Children.Add(_tbRamPercent);
            gRamTop.Children.Add(_tbRamVal);
            spRam.Children.Add(gRamTop);

            var trackRam = LinuxTheme.CreateProgressBar(out _rectRamBar, theme.AccentEmerald);
            spRam.Children.Add(trackRam);
            Grid.SetColumn(spRam, 2);
            grid.Children.Add(spRam);

            sp.Children.Add(grid);
            card.Child = sp;
            return card;
        }

        // ==========================================
        // Card 2: 🔋 供电与续航
        // ==========================================
        private Border BuildPowerCard(LinuxThemePalette theme, TranslationSet i18n)
        {
            var card = LinuxTheme.CreateCardBorder();
            var sp = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };

            var headGrid = new Grid();
            headGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var lblHead = LinuxTheme.CreateCardHeader("🔋 " + i18n.SectionPower);
            Grid.SetColumn(lblHead, 0);
            headGrid.Children.Add(lblHead);

            _tbPowerBadge = new TextBlock { Text = i18n.PowerChargedFull, FontSize = 9.5, FontWeight = FontWeight.Bold, Foreground = theme.AccentEmerald };
            _bPowerBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(35, 5, 150, 105)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 5, 150, 105)),
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                VerticalAlignment = VerticalAlignment.Center,
                Child = _tbPowerBadge
            };
            Grid.SetColumn(_bPowerBadge, 1);
            headGrid.Children.Add(_bPowerBadge);
            sp.Children.Add(headGrid);

            // Row 0: 电脑功耗 | 电池功率
            _tbPowerPcWatts = LinuxTheme.CreateValueText("--");
            _tbPowerBatWatts = LinuxTheme.CreateValueText("--");
            sp.Children.Add(CreateMetricsRow(theme, i18n.PowerPc + ":", _tbPowerPcWatts, i18n.PowerBattery + ":", _tbPowerBatWatts));

            // Row 1: 电池电量 | 预估续航
            _tbPowerBatLevel = LinuxTheme.CreateValueText("--");
            _tbPowerBatEta = LinuxTheme.CreateValueText("--");
            sp.Children.Add(CreateMetricsRow(theme, i18n.BatteryCapacity + ":", _tbPowerBatLevel, i18n.EstBatteryLife + ":", _tbPowerBatEta));

            card.Child = sp;
            return card;
        }

        // ==========================================
        // Card 3: 🌐 物理网卡与流量
        // ==========================================
        private Border BuildNetworkCard(LinuxThemePalette theme, TranslationSet i18n)
        {
            var card = LinuxTheme.CreateCardBorder();
            var sp = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
            sp.Children.Add(LinuxTheme.CreateCardHeader("🌐 " + i18n.SectionNetwork));

            _tbNetIface = LinuxTheme.CreateValueText("eth0");
            _tbNetLinkSpeed = LinuxTheme.CreateValueText("--");
            _tbNetLocalIp = LinuxTheme.CreateValueText("127.0.0.1");
            _tbNetSessionTraffic = LinuxTheme.CreateValueText("↓ 0.0M   ↑ 0.0M");

            sp.Children.Add(CreateMetricsGrid(theme,
                i18n.ActiveIface + ":", _tbNetIface,
                i18n.LinkSpeed + ":", _tbNetLinkSpeed,
                i18n.LocalIp + ":", _tbNetLocalIp,
                i18n.SessionTraffic + ":", _tbNetSessionTraffic));

            // Highlighted Public IP Box
            var ipBox = LinuxTheme.CreateInnerBorder();
            ipBox.Padding = new Thickness(8, 6, 8, 6);
            ipBox.Margin = new Thickness(0, 6, 0, 2);

            var ipSp = new StackPanel();
            var ipRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var lblPub = LinuxTheme.CreateMutedText(i18n.PublicExitIsp + ": ");
            lblPub.Margin = new Thickness(0, 0, 6, 0);
            ipRow.Children.Add(lblPub);

            _tbNetFlag = new TextBlock { Text = "🌐", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            _tbNetPublicIp = new TextBlock
            {
                Text = i18n.NetFetching,
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                Foreground = theme.AccentBlue,
                FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center
            };
            ipRow.Children.Add(_tbNetFlag);
            ipRow.Children.Add(_tbNetPublicIp);
            ipSp.Children.Add(ipRow);

            _tbNetGeoIsp = new TextBlock
            {
                Text = i18n.NetFetching,
                FontSize = 10,
                Foreground = theme.TextSecondary,
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            ipSp.Children.Add(_tbNetGeoIsp);
            ipBox.Child = ipSp;
            sp.Children.Add(ipBox);

            card.Child = sp;
            return card;
        }

        // ==========================================
        // Card 4: 🔗 ZeroTier 虚拟局域网
        // ==========================================
        private Border BuildZeroTierCard(LinuxThemePalette theme, TranslationSet i18n)
        {
            var card = LinuxTheme.CreateCardBorder();
            var sp = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };

            var zHead = new Grid();
            zHead.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            zHead.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var lblZt = LinuxTheme.CreateCardHeader("🔗 " + i18n.SectionZeroTier);
            Grid.SetColumn(lblZt, 0);
            zHead.Children.Add(lblZt);

            _tbZtBadge = new TextBlock { Text = i18n.ZtOnline, FontSize = 9.5, FontWeight = FontWeight.Bold, Foreground = theme.AccentEmerald };
            _bZtBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(35, 5, 150, 105)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 5, 150, 105)),
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                VerticalAlignment = VerticalAlignment.Center,
                Child = _tbZtBadge
            };
            Grid.SetColumn(_bZtBadge, 1);
            zHead.Children.Add(_bZtBadge);
            sp.Children.Add(zHead);

            _tbZtNode = LinuxTheme.CreateValueText("--");
            var nodeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 4) };
            var nodeLabel = LinuxTheme.CreateMutedText(i18n.LocalNodeId + ": ");
            nodeLabel.Margin = new Thickness(0, 0, 6, 0);
            nodeRow.Children.Add(nodeLabel);
            nodeRow.Children.Add(_tbZtNode);
            sp.Children.Add(nodeRow);

            _spMoons = new StackPanel { Spacing = 4 };
            sp.Children.Add(_spMoons);

            card.Child = sp;
            return card;
        }

        // ==========================================
        // Card 5: 👥 成员设备
        // ==========================================
        private Border BuildMemberCard(LinuxThemePalette theme, TranslationSet i18n)
        {
            var card = LinuxTheme.CreateCardBorder();
            var sp = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };

            var mHead = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            mHead.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            mHead.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var lblMemberHead = LinuxTheme.CreateCardHeader("👥 " + i18n.SectionMembers);
            Grid.SetColumn(lblMemberHead, 0);
            mHead.Children.Add(lblMemberHead);

            var spHeadRight = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 4
            };

            _tbMemberStatus = new TextBlock
            {
                Text = _memberDir.GetCurrentStatusText(),
                FontSize = 10,
                FontWeight = FontWeight.Medium,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                IsVisible = false
            };
            spHeadRight.Children.Add(_tbMemberStatus);

            // Filter Chips
            _chipAll = CreateChipPill(i18n.FilterAll + " 0", MemberFilterType.All, out _tbChipAll);
            spHeadRight.Children.Add(_chipAll);

            _chipOnline = CreateChipPill("🟢 0", MemberFilterType.Online, out _tbChipOnline);
            spHeadRight.Children.Add(_chipOnline);

            _chipOffline = CreateChipPill("⚪ 0", MemberFilterType.Offline, out _tbChipOffline);
            spHeadRight.Children.Add(_chipOffline);

            _btnToggleSearch = LinuxTheme.CreateIconButton("🔍", i18n.ToggleSearch, () => ToggleSearchBox(), 11);
            spHeadRight.Children.Add(_btnToggleSearch);

            var btnRefresh = LinuxTheme.CreateIconButton("↻", i18n.Syncing, () => _memberDir?.TriggerRefresh(), 12);
            spHeadRight.Children.Add(btnRefresh);

            Grid.SetColumn(spHeadRight, 1);
            mHead.Children.Add(spHeadRight);
            sp.Children.Add(mHead);

            // Collapsible Search Box
            _searchBoxBorder = LinuxTheme.CreateInnerBorder();
            _searchBoxBorder.Padding = new Thickness(6, 4, 6, 4);
            _searchBoxBorder.Margin = new Thickness(0, 0, 0, 6);
            _searchBoxBorder.IsVisible = false;

            var sGrid = new Grid();
            sGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            sGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _tbMemberSearch = new TextBox
            {
                Watermark = i18n.SearchPlaceholder,
                Background = Brushes.Transparent,
                Foreground = theme.TextPrimary,
                CaretBrush = theme.TextPrimary,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                FontSize = 10.5
            };
            _tbMemberSearch.KeyUp += (s, e) => ApplyMemberFilterAndSearch();
            Grid.SetColumn(_tbMemberSearch, 0);
            sGrid.Children.Add(_tbMemberSearch);

            var btnClear = LinuxTheme.CreateIconButton("✕", i18n.Close, () =>
            {
                if (_tbMemberSearch != null) _tbMemberSearch.Text = "";
                ApplyMemberFilterAndSearch();
            }, 10);
            Grid.SetColumn(btnClear, 1);
            sGrid.Children.Add(btnClear);

            _searchBoxBorder.Child = sGrid;
            sp.Children.Add(_searchBoxBorder);

            // Scrollable Member Results
            _spMemberResults = new StackPanel { Spacing = 3 };
            _svMembers = new ScrollViewer
            {
                MaxHeight = 180,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = _spMemberResults
            };
            sp.Children.Add(_svMembers);

            card.Child = sp;
            return card;
        }

        private Border CreateChipPill(string text, MemberFilterType filterType, out TextBlock tbRef)
        {
            var theme = LinuxTheme.Current;
            tbRef = new TextBlock
            {
                Text = text,
                FontSize = 9.5,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            var pill = new Border
            {
                Background = theme.InnerTileBg,
                BorderBrush = theme.BorderMuted,
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = tbRef
            };
            pill.PointerEntered += (s, e) =>
            {
                if ((filterType == MemberFilterType.All && _currentMemberFilter != MemberFilterType.All) ||
                    (filterType == MemberFilterType.Online && _currentMemberFilter != MemberFilterType.Online) ||
                    (filterType == MemberFilterType.Offline && _currentMemberFilter != MemberFilterType.Offline))
                {
                    pill.Background = LinuxTheme.Current.CardHoverBg;
                }
            };
            pill.PointerExited += (s, e) =>
            {
                UpdateChipVisuals();
            };
            pill.PointerPressed += (s, e) =>
            {
                _currentMemberFilter = (_currentMemberFilter == filterType && filterType != MemberFilterType.All) ? MemberFilterType.All : filterType;
                UpdateChipVisuals();
                ApplyMemberFilterAndSearch();
            };
            return pill;
        }

        private void UpdateChipVisuals()
        {
            var theme = LinuxTheme.Current;
            if (_chipAll != null && _tbChipAll != null)
            {
                bool active = _currentMemberFilter == MemberFilterType.All;
                _chipAll.Background = active ? new SolidColorBrush(Color.FromArgb(45, 9, 105, 218)) : theme.InnerTileBg;
                _chipAll.BorderBrush = active ? theme.AccentBlue : theme.BorderMuted;
                _tbChipAll.Foreground = active ? theme.AccentBlue : theme.TextSecondary;
                _tbChipAll.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
            }
            if (_chipOnline != null && _tbChipOnline != null)
            {
                bool active = _currentMemberFilter == MemberFilterType.Online;
                _chipOnline.Background = active ? new SolidColorBrush(Color.FromArgb(45, 5, 150, 105)) : theme.InnerTileBg;
                _chipOnline.BorderBrush = active ? theme.AccentEmerald : theme.BorderMuted;
                _tbChipOnline.Foreground = active ? theme.AccentEmerald : theme.TextSecondary;
                _tbChipOnline.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
            }
            if (_chipOffline != null && _tbChipOffline != null)
            {
                bool active = _currentMemberFilter == MemberFilterType.Offline;
                _chipOffline.Background = active ? new SolidColorBrush(Color.FromArgb(45, 100, 116, 139)) : theme.InnerTileBg;
                _chipOffline.BorderBrush = active ? theme.TextMuted : theme.BorderMuted;
                _tbChipOffline.Foreground = active ? theme.TextPrimary : theme.TextSecondary;
                _tbChipOffline.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
            }
        }

        private void ToggleSearchBox()
        {
            if (_searchBoxBorder == null) return;
            if (_searchBoxBorder.IsVisible)
            {
                _searchBoxBorder.IsVisible = false;
                if (_btnToggleSearch != null) _btnToggleSearch.Foreground = LinuxTheme.Current.TextSecondary;
                if (_tbMemberSearch != null) _tbMemberSearch.Text = "";
                ApplyMemberFilterAndSearch();
            }
            else
            {
                ExpandSearchBox();
            }
        }

        private void ExpandSearchBox()
        {
            if (_searchBoxBorder == null) return;
            _searchBoxBorder.IsVisible = true;
            if (_btnToggleSearch != null) _btnToggleSearch.Foreground = LinuxTheme.Current.AccentBlue;
            _tbMemberSearch?.Focus();
        }

        // ==========================================
        // ==========================================
        // Settings Overlay
        // ==========================================
        private Border BuildSettingsOverlay(LinuxThemePalette theme, TranslationSet i18n)
        {
            var overlay = new Border
            {
                IsVisible = false,
                Background = theme.CardBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(2, 0, 2, 8),
                Padding = new Thickness(12, 10, 12, 10),
                VerticalAlignment = VerticalAlignment.Top,
                BoxShadow = BoxShadows.Parse(theme.IsDark ? "0 4 16 #60000000" : "0 4 16 #30000000")
            };

            var sp = new StackPanel { Spacing = 6 };

            // Top bar
            var topRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var lblTitle = new TextBlock
            {
                Text = "⚙ " + i18n.SettingsTitle,
                FontSize = 11.5,
                FontWeight = FontWeight.Bold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblTitle, 0);
            topRow.Children.Add(lblTitle);

            var btnClose = LinuxTheme.CreateIconButton("✕", i18n.Close, () => overlay.IsVisible = false, 11);
            Grid.SetColumn(btnClose, 1);
            topRow.Children.Add(btnClose);
            sp.Children.Add(topRow);

            // Theme Switcher & Language Switcher
            var prefGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            prefGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            prefGrid.ColumnDefinitions.Add(new ColumnDefinition(6, GridUnitType.Pixel));
            prefGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            var btnTheme = LinuxTheme.CreateActionButton(
                LinuxSettings.IsDark ? i18n.ThemeLight : i18n.ThemeDark,
                theme.TextPrimary,
                theme.InnerTileBg,
                theme.BorderBrush,
                () => LinuxSettings.SetTheme(!LinuxSettings.IsDark),
                new Thickness(6, 4),
                10
            );
            btnTheme.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(btnTheme, 0);
            prefGrid.Children.Add(btnTheme);

            var btnLang = LinuxTheme.CreateActionButton(
                LinuxSettings.Language == AppLanguage.Zh ? "🇺🇸 English" : "🇨🇳 简体中文",
                theme.TextPrimary,
                theme.InnerTileBg,
                theme.BorderBrush,
                () => LinuxSettings.SetLanguage(LinuxSettings.Language == AppLanguage.Zh ? AppLanguage.En : AppLanguage.Zh),
                new Thickness(6, 4),
                10
            );
            btnLang.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(btnLang, 2);
            prefGrid.Children.Add(btnLang);
            sp.Children.Add(prefGrid);

            // Section: 首页微件模块定制 (Widget Modules Customization)
            BuildModulesSection(sp, theme, i18n);

            // ZeroTier Controller Settings
            var cfg = _memberDir?.CurrentConfig ?? new LinuxMemberConfig();
            sp.Children.Add(LinuxTheme.CreateMutedText(i18n.ControllerUrl));
            _txtSettingUrl = LinuxTheme.CreateInputTextBox(cfg.ControllerUrl ?? "");
            _txtSettingUrl.Watermark = "https://api.zerotier.com";
            sp.Children.Add(_txtSettingUrl);

            var idTokenGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            idTokenGrid.ColumnDefinitions.Add(new ColumnDefinition(135, GridUnitType.Pixel));
            idTokenGrid.ColumnDefinitions.Add(new ColumnDefinition(8, GridUnitType.Pixel));
            idTokenGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            var spNwid = new StackPanel();
            spNwid.Children.Add(LinuxTheme.CreateMutedText(i18n.NetworkId));
            _txtSettingNwid = LinuxTheme.CreateInputTextBox(cfg.NetworkId);
            spNwid.Children.Add(_txtSettingNwid);
            Grid.SetColumn(spNwid, 0);
            idTokenGrid.Children.Add(spNwid);

            var spToken = new StackPanel();
            spToken.Children.Add(LinuxTheme.CreateMutedText(i18n.ApiToken));

            var tokenRow = new Grid();
            tokenRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            tokenRow.ColumnDefinitions.Add(new ColumnDefinition(28, GridUnitType.Pixel));

            _txtSettingToken = LinuxTheme.CreateInputTextBox(cfg.ApiToken);
            _txtSettingToken.PasswordChar = '●';
            _txtSettingToken.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(_txtSettingToken, 0);
            tokenRow.Children.Add(_txtSettingToken);

            bool isTokenMasked = true;
            Button btnEye = null;
            btnEye = LinuxTheme.CreateIconButton("👁", i18n.ToggleToken, () =>
            {
                isTokenMasked = !isTokenMasked;
                _txtSettingToken.PasswordChar = isTokenMasked ? '●' : '\0';
                btnEye.Content = isTokenMasked ? "👁" : "🙈";
            }, 11);
            btnEye.VerticalAlignment = VerticalAlignment.Center;
            btnEye.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(btnEye, 1);
            tokenRow.Children.Add(btnEye);

            spToken.Children.Add(tokenRow);
            Grid.SetColumn(spToken, 2);
            idTokenGrid.Children.Add(spToken);
            sp.Children.Add(idTokenGrid);

            _lblTestStatus = new TextBlock { Text = "", FontSize = 10, Margin = new Thickness(0, 2, 0, 0) };
            sp.Children.Add(_lblTestStatus);

            // GitHub & Update Box
            var updateBox = LinuxTheme.CreateInnerBorder();
            updateBox.Padding = new Thickness(8, 6, 8, 6);
            updateBox.Margin = new Thickness(0, 4, 0, 4);

            var upSp = new StackPanel();
            var upHeadGrid = new Grid();
            upHeadGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            upHeadGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var lblVer = LinuxTheme.CreateMutedText(string.Format(i18n.CurrentVersionFormat, UpdateChecker.CurrentVersion));
            lblVer.FontWeight = FontWeight.SemiBold;
            Grid.SetColumn(lblVer, 0);
            upHeadGrid.Children.Add(lblVer);

            var lblBranch = LinuxTheme.CreateMutedText("main");
            Grid.SetColumn(lblBranch, 1);
            upHeadGrid.Children.Add(lblBranch);
            upSp.Children.Add(upHeadGrid);

            _tbUpdateStatus = new TextBlock
            {
                Text = "",
                FontSize = 9.5,
                Margin = new Thickness(0, 4, 0, 2),
                Foreground = theme.TextMuted,
                TextWrapping = TextWrapping.Wrap,
                IsVisible = false
            };
            upSp.Children.Add(_tbUpdateStatus);

            // 3 Buttons in ONE ROW
            var pnlUpdateActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0), Spacing = 6 };

            var btnCheckUp = LinuxTheme.CreateActionButton(
                "🔄 " + i18n.CheckUpdate,
                theme.AccentBlue,
                new SolidColorBrush(Color.FromArgb(25, 9, 105, 218)),
                new SolidColorBrush(Color.FromArgb(80, 9, 105, 218)),
                () => CheckForAppUpdates(),
                new Thickness(8, 3),
                10
            );
            pnlUpdateActions.Children.Add(btnCheckUp);

            _btnPullUpdate = LinuxTheme.CreateActionButton(
                "⬇ " + i18n.DownloadUpdate,
                theme.AccentEmerald,
                new SolidColorBrush(Color.FromArgb(30, 5, 150, 105)),
                new SolidColorBrush(Color.FromArgb(90, 5, 150, 105)),
                () => PerformPullUpdate(),
                new Thickness(8, 3),
                10
            );
            _btnPullUpdate.IsVisible = false;
            pnlUpdateActions.Children.Add(_btnPullUpdate);

            var btnOpenRepo = LinuxTheme.CreateActionButton(
                "🌐 GitHub",
                theme.TextSecondary,
                theme.CardBg,
                theme.BorderBrush,
                () =>
                {
                    try { Process.Start(new ProcessStartInfo("https://github.com/Nicotinamide/SysMonitor") { UseShellExecute = true }); } catch { }
                },
                new Thickness(8, 3),
                10
            );
            pnlUpdateActions.Children.Add(btnOpenRepo);

            upSp.Children.Add(pnlUpdateActions);
            updateBox.Child = upSp;
            sp.Children.Add(updateBox);

            // Bottom Action Row: [⚡ 测试连接]  [💾 保存配置]  [✕ 关闭]
            var btnGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition(6, GridUnitType.Pixel));
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var btnTest = LinuxTheme.CreateActionButton(
                "⚡ " + i18n.TestConnection,
                theme.AccentBlue,
                new SolidColorBrush(Color.FromArgb(30, 9, 105, 218)),
                new SolidColorBrush(Color.FromArgb(90, 9, 105, 218)),
                () =>
                {
                    _lblTestStatus.Text = "⏳ " + i18n.TestingConn;
                    _lblTestStatus.Foreground = theme.AccentAmber;
                    _ = _memberDir.TestConnectionAsync(_txtSettingUrl.Text, _txtSettingNwid.Text, _txtSettingToken.Text, (success, count, err) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (success)
                            {
                                _lblTestStatus.Text = string.Format(i18n.ConnSuccessFormat, count);
                                _lblTestStatus.Foreground = theme.AccentEmerald;
                            }
                            else
                            {
                                _lblTestStatus.Text = string.Format(i18n.ConnFailedFormat, err);
                                _lblTestStatus.Foreground = theme.AccentRed;
                            }
                        });
                    });
                },
                new Thickness(8, 3),
                10.5
            );
            Grid.SetColumn(btnTest, 0);
            btnGrid.Children.Add(btnTest);

            var btnSave = LinuxTheme.CreateActionButton(
                "💾 " + i18n.SaveConfig,
                theme.AccentEmerald,
                new SolidColorBrush(Color.FromArgb(35, 5, 150, 105)),
                new SolidColorBrush(Color.FromArgb(100, 5, 150, 105)),
                () =>
                {
                    _memberDir.SaveEncryptedConfig(_txtSettingUrl.Text, _txtSettingNwid.Text, _txtSettingToken.Text);
                    overlay.IsVisible = false;
                    ShowToast("✓ " + (i18n.Lang == AppLanguage.Zh ? "设置已保存" : "Settings saved"));
                    _parentFloat?.ShowNotification(i18n.NotifySettingsSavedTitle, i18n.NotifySettingsSavedText, ToastType.Success, "💾");
                    UpdateZeroTierCardsVisibility();
                },
                new Thickness(10, 3),
                10.5
            );
            Grid.SetColumn(btnSave, 2);
            btnGrid.Children.Add(btnSave);

            var btnCancel = LinuxTheme.CreateActionButton(
                "✕ " + i18n.Close,
                theme.TextSecondary,
                Brushes.Transparent,
                theme.BorderBrush,
                () => overlay.IsVisible = false,
                new Thickness(8, 3),
                10.5
            );
            Grid.SetColumn(btnCancel, 4);
            btnGrid.Children.Add(btnCancel);

            sp.Children.Add(btnGrid);
            overlay.Child = sp;
            return overlay;
        }

        private void BuildModulesSection(StackPanel sfSp, LinuxThemePalette theme, TranslationSet i18n)
        {
            var headerGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var lblModulesHeader = LinuxTheme.CreateMutedText("❖ " + i18n.SettingsModulesTitle);
            lblModulesHeader.FontWeight = FontWeight.Bold;
            Grid.SetColumn(lblModulesHeader, 0);
            headerGrid.Children.Add(lblModulesHeader);

            var lblHint = new TextBlock
            {
                Text = i18n.Lang == AppLanguage.Zh ? "拖动排序 · 单击/右击启闭" : "Drag to reorder · Click to toggle",
                FontSize = 9.5,
                Foreground = theme.TextDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblHint, 1);
            headerGrid.Children.Add(lblHint);
            sfSp.Children.Add(headerGrid);

            var bModulesContainer = new Border
            {
                Background = theme.InnerTileBg,
                BorderBrush = theme.BorderMuted,
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 8),
                Height = 46
            };

            const double SlotWidth = 82.0;
            var order = LinuxSettings.ModuleOrder;
            double totalCanvasWidth = order.Count * SlotWidth;

            var canvas = new Canvas
            {
                Width = totalCanvasWidth,
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = false
            };
            bModulesContainer.Child = canvas;
            sfSp.Children.Add(bModulesContainer);

            RenderModuleChips(canvas, theme, i18n);
        }

        private void RenderModuleChips(Canvas canvas, LinuxThemePalette theme, TranslationSet i18n)
        {
            canvas.Children.Clear();
            var order = LinuxSettings.ModuleOrder;
            var enabled = LinuxSettings.ModuleEnabled;

            const double SlotWidth = 82.0;
            const double ChipWidth = 76.0;
            const double ChipHeight = 36.0;
            int totalSlots = order.Count;
            double slotPadding = (SlotWidth - ChipWidth) / 2.0;

            var chipMap = new Dictionary<string, Border>();

            for (int i = 0; i < order.Count; i++)
            {
                int slotIndex = i;
                string key = order[slotIndex];
                bool isAct = enabled.Contains(key);

                string icon = "⚡";
                string name = (i18n.Lang == AppLanguage.Zh) ? "供电" : "Power";
                string fullTitle = i18n.ModPowerTitle;
                Color col = ((SolidColorBrush)theme.AccentAmber).Color;

                if (key == LinuxSettings.ModulePower)
                {
                    icon = "⚡";
                    name = (i18n.Lang == AppLanguage.Zh) ? "供电" : "Power";
                    fullTitle = i18n.ModPowerTitle;
                    col = ((SolidColorBrush)theme.AccentAmber).Color;
                }
                else if (key == LinuxSettings.ModuleNetwork)
                {
                    icon = "🌐";
                    name = (i18n.Lang == AppLanguage.Zh) ? "网卡" : "Net";
                    fullTitle = i18n.ModNetTitle;
                    col = ((SolidColorBrush)theme.AccentBlue).Color;
                }
                else if (key == LinuxSettings.ModuleZeroTier)
                {
                    icon = "🔗";
                    name = (i18n.Lang == AppLanguage.Zh) ? "互联" : "ZT";
                    fullTitle = i18n.ModZtTitle;
                    col = ((SolidColorBrush)theme.AccentEmerald).Color;
                }
                else if (key == LinuxSettings.ModuleCompute)
                {
                    icon = "💻";
                    name = (i18n.Lang == AppLanguage.Zh) ? "算力" : "Load";
                    fullTitle = i18n.ModComputeTitle;
                    col = Color.FromRgb(168, 85, 247);
                }

                var chip = new Border
                {
                    Width = ChipWidth,
                    Height = ChipHeight,
                    CornerRadius = new CornerRadius(7),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = key,
                    RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
                };
                ToolTip.SetTip(chip, fullTitle + (i18n.Lang == AppLanguage.Zh ? "\n左键拖动调序 · 单击/右键启闭" : "\nDrag to reorder · Click/Right-click to toggle"));

                if (isAct)
                {
                    chip.Background = new SolidColorBrush(Color.FromArgb(38, col.R, col.G, col.B));
                    chip.BorderBrush = new SolidColorBrush(Color.FromArgb(160, col.R, col.G, col.B));
                    chip.BorderThickness = new Thickness(1);
                    chip.Opacity = 1.0;
                }
                else
                {
                    chip.Background = theme.CardBg;
                    chip.BorderBrush = theme.BorderMuted;
                    chip.BorderThickness = new Thickness(0.8);
                    chip.Opacity = 0.5;
                }

                var sp = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var tbIcon = new TextBlock
                {
                    Text = icon,
                    FontSize = 12,
                    Foreground = isAct ? new SolidColorBrush(col) : theme.TextDim,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var tbText = new TextBlock
                {
                    Text = name,
                    FontSize = 10.5,
                    FontWeight = isAct ? FontWeight.SemiBold : FontWeight.Normal,
                    Foreground = isAct ? new SolidColorBrush(col) : theme.TextDim,
                    VerticalAlignment = VerticalAlignment.Center
                };

                sp.Children.Add(tbIcon);
                sp.Children.Add(tbText);
                chip.Child = sp;

                double initLeft = slotIndex * SlotWidth + slotPadding;
                Canvas.SetLeft(chip, initLeft);
                Canvas.SetTop(chip, 0);

                chipMap[key] = chip;

                Point dragStartMouse = new Point();
                double chipStartLeft = 0;
                bool isDragging = false;
                bool isCaptured = false;

                chip.PointerPressed += (s, e) =>
                {
                    var cp = e.GetCurrentPoint(canvas);
                    if (cp.Properties.IsLeftButtonPressed)
                    {
                        dragStartMouse = cp.Position;
                        chipStartLeft = Canvas.GetLeft(chip);
                        isDragging = false;
                        isCaptured = true;
                        e.Pointer.Capture(chip);
                        e.Handled = true;
                    }
                    else if (cp.Properties.IsRightButtonPressed)
                    {
                        ToggleModule(key, order, enabled, canvas, theme, i18n);
                        e.Handled = true;
                    }
                };

                chip.PointerMoved += (s, e) =>
                {
                    if (isCaptured)
                    {
                        var curMouse = e.GetCurrentPoint(canvas).Position;
                        double deltaX = curMouse.X - dragStartMouse.X;

                        if (!isDragging && Math.Abs(deltaX) > 4)
                        {
                            isDragging = true;
                            chip.ZIndex = 999;
                            chip.RenderTransform = new ScaleTransform(1.08, 1.08);
                            chip.BoxShadow = BoxShadows.Parse(string.Format("0 3 14 #{0:X2}{1:X2}{2:X2}{3:X2}", (byte)(isAct ? 140 : 80), col.R, col.G, col.B));
                        }

                        if (isDragging)
                        {
                            double newLeft = chipStartLeft + deltaX;
                            double minLeft = slotPadding;
                            double maxLeft = (totalSlots - 1) * SlotWidth + slotPadding;
                            if (newLeft < minLeft - 12) newLeft = minLeft - 12;
                            if (newLeft > maxLeft + 12) newLeft = maxLeft + 12;

                            Canvas.SetLeft(chip, newLeft);

                            double chipCenter = newLeft + ChipWidth / 2.0;
                            int targetSlot = (int)Math.Floor(chipCenter / SlotWidth);
                            if (targetSlot < 0) targetSlot = 0;
                            if (targetSlot >= totalSlots) targetSlot = totalSlots - 1;

                            int currentSlot = order.IndexOf(key);
                            if (targetSlot != currentSlot)
                            {
                                order.RemoveAt(currentSlot);
                                order.Insert(targetSlot, key);

                                for (int j = 0; j < order.Count; j++)
                                {
                                    string otherKey = order[j];
                                    if (otherKey != key && chipMap.ContainsKey(otherKey))
                                    {
                                        Border otherChip = chipMap[otherKey];
                                        double otherTargetLeft = j * SlotWidth + slotPadding;
                                        Canvas.SetLeft(otherChip, otherTargetLeft);
                                    }
                                }
                            }
                        }
                    }
                };

                chip.PointerReleased += (s, e) =>
                {
                    if (isCaptured)
                    {
                        isCaptured = false;
                        e.Pointer.Capture(null);

                        if (isDragging)
                        {
                            chip.RenderTransform = new ScaleTransform(1.0, 1.0);
                            chip.BoxShadow = new BoxShadows();
                            chip.ZIndex = 0;

                            int finalSlot = order.IndexOf(key);
                            double finalLeft = finalSlot * SlotWidth + slotPadding;
                            Canvas.SetLeft(chip, finalLeft);

                            LinuxSettings.SetWidgetModules(order, enabled);
                        }
                        else
                        {
                            ToggleModule(key, order, enabled, canvas, theme, i18n);
                        }
                        isDragging = false;
                        e.Handled = true;
                    }
                };

                canvas.Children.Add(chip);
            }
        }

        private void ToggleModule(string key, List<string> order, List<string> enabled, Canvas canvas, LinuxThemePalette theme, TranslationSet i18n)
        {
            if (enabled.Contains(key))
            {
                if (enabled.Count <= 1)
                {
                    ShowToast(i18n.AtLeastOneModule);
                    return;
                }
                enabled.Remove(key);
            }
            else
            {
                enabled.Add(key);
            }

            LinuxSettings.SetWidgetModules(order, enabled);
            RenderModuleChips(canvas, theme, i18n);
        }

        private void ToggleSettingsView()
        {
            if (_overlaySettings == null) return;
            _overlaySettings.IsVisible = !_overlaySettings.IsVisible;
            if (_overlaySettings.IsVisible)
            {
                var cfg = _memberDir?.CurrentConfig;
                if (cfg != null)
                {
                    if (_txtSettingUrl != null) _txtSettingUrl.Text = cfg.ControllerUrl ?? "";
                    if (_txtSettingNwid != null) _txtSettingNwid.Text = cfg.NetworkId ?? "";
                    if (_txtSettingToken != null) _txtSettingToken.Text = cfg.ApiToken ?? "";
                }
                if (_lblTestStatus != null) _lblTestStatus.Text = "";
            }
        }

        private void CheckForAppUpdates()
        {
            var i18n = I18n.Current;
            if (_tbUpdateStatus != null)
            {
                _tbUpdateStatus.IsVisible = true;
                _tbUpdateStatus.Text = "⏳ " + i18n.CheckingUpdate;
                _tbUpdateStatus.Foreground = LinuxTheme.Current.TextMuted;
            }
            if (_btnPullUpdate != null) _btnPullUpdate.IsVisible = false;

            UpdateChecker.CheckForUpdatesAsync((info) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_tbUpdateStatus == null) return;
                    var theme = LinuxTheme.Current;
                    var curI18n = I18n.Current;

                    if (!info.Success)
                    {
                        _tbUpdateStatus.Text = $"✕ {curI18n.UpdateFailed}: {info.ErrorMessage}";
                        _tbUpdateStatus.Foreground = theme.AccentRed;
                        return;
                    }

                    if (info.HasUpdate)
                    {
                        _latestDownloadUrl = info.DownloadUrl;
                        string notes = !string.IsNullOrEmpty(info.ReleaseNotes) ? ("\n" + info.ReleaseNotes.Trim()) : "";
                        _tbUpdateStatus.Text = $"🚀 {curI18n.NewVersionFound}: {info.LatestVersion}{notes}";
                        _tbUpdateStatus.Foreground = theme.AccentEmerald;

                        if (_btnPullUpdate != null)
                        {
                            _btnPullUpdate.Content = $"⬇ {curI18n.DownloadUpdate} ({info.LatestVersion})";
                            _btnPullUpdate.IsVisible = true;
                        }
                    }
                    else
                    {
                        _tbUpdateStatus.Text = string.Format("{0} ({1})", curI18n.AlreadyLatest, UpdateChecker.CurrentVersion);
                        _tbUpdateStatus.Foreground = theme.AccentEmerald;
                    }
                });
            });
        }

        private void PerformPullUpdate()
        {
            if (string.IsNullOrEmpty(_latestDownloadUrl)) return;
            var i18n = I18n.Current;
            _tbUpdateStatus.Text = "⏳ " + (i18n.Lang == AppLanguage.Zh ? "正在拉取更新..." : "Pulling update...");
            _btnPullUpdate.IsEnabled = false;

            UpdateChecker.DownloadAndApplyUpdateAsync(_latestDownloadUrl,
                (pct) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_tbUpdateStatus != null)
                            _tbUpdateStatus.Text = string.Format(I18n.Current.Lang == AppLanguage.Zh ? "⏳ 下载中... {0}%" : "⏳ Downloading... {0}%", pct);
                    });
                },
                (ok, msg) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _btnPullUpdate.IsEnabled = true;
                        if (_tbUpdateStatus != null)
                        {
                            _tbUpdateStatus.Text = (ok ? "✓ " : "✕ ") + msg;
                            _tbUpdateStatus.Foreground = ok ? LinuxTheme.Current.AccentEmerald : LinuxTheme.Current.AccentRed;
                        }
                    });
                });
        }

        private void UpdateZeroTierCardsVisibility()
        {
            if (_cardMember != null)
            {
                _cardMember.IsVisible = _memberDir != null && _memberDir.HasToken;
            }
        }

        public void ShowToast(string message)
        {
            if (_tbToast != null && _bToast != null)
            {
                _tbToast.Text = message;
                _bToast.IsVisible = true;
                _toastTimer.Stop();
                _toastTimer.Start();
            }
        }

        // ==========================================
        // Telemetry Update Handlers
        // ==========================================
        public void UpdateSystemLoad(LinuxSystemLoadData load)
        {
            _lastLoad = load;
            var theme = LinuxTheme.Current;
            var i18n = I18n.Current;

            if (_tbCpuVal != null)
            {
                _tbCpuVal.Text = $"{load.CpuPercent:0.0}%";
                _tbCpuVal.Foreground = load.CpuPercent > 80.0 ? theme.AccentRed : (load.CpuPercent > 50.0 ? theme.AccentAmber : theme.AccentBlue);
            }
            if (_rectCpuBar != null)
            {
                _rectCpuBar.Width = Math.Max(2, (load.CpuPercent / 100.0) * 165.0);
            }

            if (_tbRamPercent != null)
                _tbRamPercent.Text = $"{i18n.Memory} {load.RamPercent}%";

            if (_tbRamVal != null)
            {
                _tbRamVal.Text = $"{load.RamUsedGb:0.0}/{load.RamTotalGb:0.0}G";
                _tbRamVal.Foreground = load.RamPercent > 85 ? theme.AccentRed : theme.AccentEmerald;
            }
            if (_rectRamBar != null)
            {
                _rectRamBar.Width = Math.Max(2, (load.RamPercent / 100.0) * 165.0);
            }
        }

        public void UpdatePower(LinuxPowerData power)
        {
            _lastPower = power;
            var theme = LinuxTheme.Current;
            var i18n = I18n.Current;

            bool isEmerald = power.StateKind == LinuxPowerStateKind.ChargedFull
                          || power.StateKind == LinuxPowerStateKind.AcDirect
                          || power.StateKind == LinuxPowerStateKind.DesktopAc
                          || power.BatteryPercent >= 98;
            bool isCharging = power.StateKind == LinuxPowerStateKind.ChargingFast;

            if (_tbPowerBadge != null && _bPowerBadge != null)
            {
                if (isEmerald)
                {
                    _tbPowerBadge.Text = power.BatteryPercent >= 95 ? i18n.PowerChargedFull : i18n.RunningOnAc;
                    _tbPowerBadge.Foreground = theme.AccentEmerald;
                    _bPowerBadge.Background = new SolidColorBrush(Color.FromArgb(35, 5, 150, 105));
                    _bPowerBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 5, 150, 105));
                }
                else if (isCharging)
                {
                    _tbPowerBadge.Text = i18n.PowerCharging;
                    _tbPowerBadge.Foreground = theme.AccentAmber;
                    _bPowerBadge.Background = new SolidColorBrush(Color.FromArgb(35, 217, 119, 6));
                    _bPowerBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 217, 119, 6));
                }
                else
                {
                    _tbPowerBadge.Text = i18n.RunningOnBattery;
                    _tbPowerBadge.Foreground = theme.AccentAmber;
                    _bPowerBadge.Background = new SolidColorBrush(Color.FromArgb(35, 217, 119, 6));
                    _bPowerBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 217, 119, 6));
                }
            }

            if (_tbPowerPcWatts != null)
                _tbPowerPcWatts.Text = power.CpuWatts > 0.5 ? $"{power.CpuWatts:0.0} W" : "--";

            if (_tbPowerBatWatts != null)
            {
                if (power.IsCharging) _tbPowerBatWatts.Text = $"+{power.Watts:0.0}W";
                else if (power.IsDischarging) _tbPowerBatWatts.Text = $"-{power.Watts:0.0}W";
                else if (isEmerald) _tbPowerBatWatts.Text = i18n.PowerAcDirect;
                else _tbPowerBatWatts.Text = "0.0W";
            }

            if (_tbPowerBatLevel != null)
            {
                if (power.BatteryWh > 0)
                    _tbPowerBatLevel.Text = $"{power.BatteryWh:0.0}wh · {power.BatteryPercent}%";
                else
                    _tbPowerBatLevel.Text = $"{power.BatteryPercent}%";
            }

            if (_tbPowerBatEta != null)
            {
                if (isEmerald) _tbPowerBatEta.Text = i18n.PowerAcDirect;
                else _tbPowerBatEta.Text = power.EstimatedTimeStr;
            }
        }

        public void UpdateNetwork(LinuxNetworkData net)
        {
            _lastNet = net;
            var i18n = I18n.Current;

            string iface = net.ActiveInterface ?? "";
            if (iface.StartsWith("wl")) iface = $"WLAN ({iface})";
            else if (iface.StartsWith("en") || iface.StartsWith("eth")) iface = $"{(i18n.Lang == AppLanguage.Zh ? "以太网" : "Ethernet")} ({iface})";

            if (_tbNetIface != null) _tbNetIface.Text = iface;
            if (_tbNetLinkSpeed != null) _tbNetLinkSpeed.Text = net.LinkSpeedStr;
            if (_tbNetLocalIp != null) _tbNetLocalIp.Text = net.LocalIp;
            if (_tbNetSessionTraffic != null) _tbNetSessionTraffic.Text = $"↓ {net.SessionRecvMb:0.1}M   ↑ {net.SessionSentMb:0.1}M";

            if (_tbNetFlag != null) _tbNetFlag.Text = LinuxTelemetryEngine.CountryCodeToEmoji(net.CountryCode);
            if (_tbNetPublicIp != null) _tbNetPublicIp.Text = net.PublicIp;

            if (_tbNetGeoIsp != null)
            {
                string loc = "";
                if (!string.IsNullOrEmpty(net.Country)) loc += net.Country;
                if (!string.IsNullOrEmpty(net.City) && !string.Equals(net.Country, net.City, StringComparison.OrdinalIgnoreCase))
                {
                    loc += (loc.Length > 0 ? " · " : "") + net.City;
                }
                if (!string.IsNullOrEmpty(net.Isp)) loc += (loc.Length > 0 ? " · " : "") + net.Isp;
                _tbNetGeoIsp.Text = !string.IsNullOrEmpty(loc) ? loc : i18n.NetFetching;
            }
        }

        public void UpdateZeroTier(LinuxZeroTierData zt)
        {
            _lastZt = zt;
            var theme = LinuxTheme.Current;
            var i18n = I18n.Current;

            if (_tbZtBadge != null && _bZtBadge != null)
            {
                if (zt.IsRunning)
                {
                    _tbZtBadge.Text = i18n.ZtOnline;
                    _tbZtBadge.Foreground = theme.AccentEmerald;
                    _bZtBadge.Background = new SolidColorBrush(Color.FromArgb(35, 5, 150, 105));
                }
                else
                {
                    _tbZtBadge.Text = i18n.ZtOffline;
                    _tbZtBadge.Foreground = theme.AccentRed;
                    _bZtBadge.Background = new SolidColorBrush(Color.FromArgb(35, 220, 38, 38));
                }
            }

            if (_tbZtNode != null)
            {
                _tbZtNode.Text = !string.IsNullOrEmpty(zt.LocalNodeId) ? zt.LocalNodeId : "--";
            }

            if (_spMoons != null)
            {
                _spMoons.Children.Clear();
                if (zt.Moons.Count == 0)
                {
                    var empty = LinuxTheme.CreateMutedText(i18n.MoonNoneJoined);
                    _spMoons.Children.Add(empty);
                }
                else
                {
                    foreach (var m in zt.Moons)
                    {
                        var mCard = LinuxTheme.CreateInnerBorder();
                        mCard.Padding = new Thickness(8, 6, 8, 6);

                        var mg = new Grid();
                        mg.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                        mg.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                        var leftSp = new StackPanel();
                        var addr = new TextBlock
                        {
                            Text = "Moon: " + m.Address,
                            Foreground = theme.TextPrimary,
                            FontSize = 11,
                            FontWeight = FontWeight.SemiBold,
                            FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
                        };
                        string physText = m.IsOffline ? i18n.Dropped : (!string.IsNullOrEmpty(m.PhysicalAddress) ? m.PhysicalAddress : i18n.Relay);
                        var phys = new TextBlock
                        {
                            Text = physText,
                            Foreground = theme.TextSecondary,
                            FontSize = 10,
                            Margin = new Thickness(0, 2, 0, 0),
                            FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
                        };
                        leftSp.Children.Add(addr);
                        leftSp.Children.Add(phys);
                        Grid.SetColumn(leftSp, 0);
                        mg.Children.Add(leftSp);

                        string badgeText = m.IsOffline ? i18n.Dropped : (m.IsDirect ? $"{i18n.Direct} {m.Latency}ms" : i18n.Relay);
                        var badgeColor = m.IsOffline ? theme.AccentRed : (m.IsDirect ? theme.AccentEmerald : theme.AccentAmber);
                        var badge = LinuxTheme.CreateBadge(badgeText, badgeColor,
                            new SolidColorBrush(Color.FromArgb(35, 5, 150, 105)),
                            new SolidColorBrush(Color.FromArgb(90, 5, 150, 105)));
                        Grid.SetColumn(badge, 1);
                        mg.Children.Add(badge);

                        mCard.Child = mg;
                        _spMoons.Children.Add(mCard);
                    }
                }
            }
        }

        private void OnMemberStatusChanged(string status)
        {
            if (_tbMemberStatus != null)
            {
                _tbMemberStatus.Text = status;
                _tbMemberStatus.IsVisible = !string.IsNullOrEmpty(status) && (status.Contains("⏳") || status.Contains("⚠️") || status.Contains("错误") || status.Contains("Error"));
            }
        }

        private void OnMembersUpdated(List<LinuxMemberNode> list)
        {
            _lastMemberList = list ?? new List<LinuxMemberNode>();
            int total = _lastMemberList.Count;
            int online = _lastMemberList.Count(m => m.IsOnline);
            int offline = total - online;
            var i18n = I18n.Current;

            if (_tbChipAll != null) _tbChipAll.Text = $"{i18n.FilterAll} {total}";
            if (_tbChipOnline != null) _tbChipOnline.Text = $"🟢 {online}";
            if (_tbChipOffline != null) _tbChipOffline.Text = $"⚪ {offline}";

            ApplyMemberFilterAndSearch();
        }

        private void ApplyMemberFilterAndSearch()
        {
            if (_spMemberResults == null) return;
            _spMemberResults.Children.Clear();

            var theme = LinuxTheme.Current;
            var i18n = I18n.Current;
            string query = _tbMemberSearch?.Text ?? "";
            var list = _memberDir.Search(query);

            if (_currentMemberFilter == MemberFilterType.Online)
                list = list.Where(m => m.IsOnline).ToList();
            else if (_currentMemberFilter == MemberFilterType.Offline)
                list = list.Where(m => !m.IsOnline).ToList();

            if (list.Count == 0)
            {
                string emptyMsg;
                if (!string.IsNullOrEmpty(query))
                    emptyMsg = i18n.Lang == AppLanguage.Zh ? "未找到匹配的成员" : "No matching members found";
                else if (_currentMemberFilter == MemberFilterType.Online)
                    emptyMsg = i18n.NoOnlineMembers;
                else if (_currentMemberFilter == MemberFilterType.Offline)
                    emptyMsg = i18n.NoOfflineMembers;
                else if (_memberDir != null && _memberDir.HasToken)
                    emptyMsg = "⏳ " + i18n.Syncing;
                else
                    emptyMsg = i18n.NoTokenHint;

                var empty = new TextBlock
                {
                    Text = emptyMsg,
                    FontSize = 10.5,
                    Foreground = theme.TextDim,
                    Margin = new Thickness(4, 8, 4, 8),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                _spMemberResults.Children.Add(empty);
                return;
            }

            foreach (var m in list.Take(60))
            {
                var itemBorder = LinuxTheme.CreateInnerBorder();
                itemBorder.Padding = new Thickness(6, 4, 6, 4);
                itemBorder.Cursor = new Cursor(StandardCursorType.Hand);
                itemBorder.PointerEntered += (s, e) =>
                {
                    itemBorder.Background = LinuxTheme.Current.CardHoverBg;
                    itemBorder.BorderBrush = LinuxTheme.Current.AccentBlue;
                };
                itemBorder.PointerExited += (s, e) =>
                {
                    itemBorder.Background = LinuxTheme.Current.InnerTileBg;
                    itemBorder.BorderBrush = LinuxTheme.Current.BorderBrush;
                };

                var itemGrid = new Grid();
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(14, GridUnitType.Pixel));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                // Dot
                var dot = new Border
                {
                    Width = 6,
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = m.IsOnline ? theme.AccentEmerald : theme.TextDim,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetColumn(dot, 0);
                itemGrid.Children.Add(dot);

                // Info
                var spInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var tbName = new TextBlock
                {
                    Text = string.IsNullOrEmpty(m.Name) ? "未命名设备" : m.Name,
                    FontSize = 10.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = theme.TextPrimary,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                string subText = m.Latency >= 0 ? $"{m.Id} · {m.Latency}ms" : $"{m.Id} · {(m.IsOnline ? i18n.Online : i18n.Offline)}";
                var tbSub = new TextBlock
                {
                    Text = subText,
                    FontSize = 9.5,
                    Foreground = theme.TextSecondary,
                    FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
                };
                spInfo.Children.Add(tbName);
                spInfo.Children.Add(tbSub);
                Grid.SetColumn(spInfo, 1);
                itemGrid.Children.Add(spInfo);

                // IP Chip
                var ipChip = new Border
                {
                    Background = theme.CardBg,
                    BorderBrush = theme.BorderBrush,
                    BorderThickness = new Thickness(0.6),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(5, 2, 5, 2),
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var tbIp = new TextBlock
                {
                    Text = string.IsNullOrEmpty(m.Ip) ? i18n.NoIp : m.Ip,
                    FontSize = 10.5,
                    FontWeight = FontWeight.Bold,
                    Foreground = string.IsNullOrEmpty(m.Ip) ? theme.TextDim : theme.AccentBlue,
                    FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
                };
                ipChip.Child = tbIp;
                Grid.SetColumn(ipChip, 2);
                itemGrid.Children.Add(ipChip);

                itemBorder.Child = itemGrid;

                // Click to copy IP
                string copyTarget = !string.IsNullOrEmpty(m.Ip) ? m.Ip : m.Id;
                itemBorder.PointerReleased += async (s, e) =>
                {
                    if (e.InitialPressMouseButton == MouseButton.Left)
                    {
                        try
                        {
                            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                            if (clipboard != null)
                            {
                                await clipboard.SetTextAsync(copyTarget);
                                ShowToast(string.Format(i18n.CopiedIpFormat, copyTarget));
                            }
                        }
                        catch { }
                    }
                };

                // Context menu
                var ctx = new ContextMenu();
                var miCopyIp = new MenuItem { Header = string.Format(i18n.MenuCopyIpFormat, copyTarget) };
                miCopyIp.Click += async (s, e) =>
                {
                    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(copyTarget);
                        ShowToast(string.Format(i18n.CopiedIpFormat, copyTarget));
                    }
                };
                ctx.Items.Add(miCopyIp);

                var miCopyId = new MenuItem { Header = string.Format(i18n.MenuCopyIdFormat, m.Id) };
                miCopyId.Click += async (s, e) =>
                {
                    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(m.Id);
                        ShowToast(string.Format(i18n.CopiedIdFormat, m.Id));
                    }
                };
                ctx.Items.Add(miCopyId);

                itemBorder.ContextMenu = ctx;
                _spMemberResults.Children.Add(itemBorder);
            }
        }

        private Grid CreateMetricsRow(LinuxThemePalette theme, string lbl1, TextBlock val1, string lbl2, TextBlock val2)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            g.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            g.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            var t1 = LinuxTheme.CreateMutedText(lbl1);
            t1.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(t1, 0); Grid.SetColumn(val1, 1);
            g.Children.Add(t1); g.Children.Add(val1);

            var t2 = LinuxTheme.CreateMutedText(lbl2);
            t2.Margin = new Thickness(10, 0, 4, 0);
            Grid.SetColumn(t2, 2); Grid.SetColumn(val2, 3);
            g.Children.Add(t2); g.Children.Add(val2);

            return g;
        }

        private Grid CreateMetricsGrid(LinuxThemePalette theme,
            string lbl1, TextBlock val1, string lbl2, TextBlock val2,
            string lbl3, TextBlock val3, string lbl4, TextBlock val4)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 2) };
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            g.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            g.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            g.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            // Row 0
            var t1 = LinuxTheme.CreateMutedText(lbl1);
            t1.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetRow(t1, 0); Grid.SetColumn(t1, 0);
            Grid.SetRow(val1, 0); Grid.SetColumn(val1, 1);
            g.Children.Add(t1); g.Children.Add(val1);

            var t2 = LinuxTheme.CreateMutedText(lbl2);
            t2.Margin = new Thickness(10, 0, 4, 0);
            Grid.SetRow(t2, 0); Grid.SetColumn(t2, 2);
            Grid.SetRow(val2, 0); Grid.SetColumn(val2, 3);
            g.Children.Add(t2); g.Children.Add(val2);

            // Row 2
            var t3 = LinuxTheme.CreateMutedText(lbl3);
            t3.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetRow(t3, 2); Grid.SetColumn(t3, 0);
            Grid.SetRow(val3, 2); Grid.SetColumn(val3, 1);
            g.Children.Add(t3); g.Children.Add(val3);

            var t4 = LinuxTheme.CreateMutedText(lbl4);
            t4.Margin = new Thickness(10, 0, 4, 0);
            Grid.SetRow(t4, 2); Grid.SetColumn(t4, 2);
            Grid.SetRow(val4, 2); Grid.SetColumn(val4, 3);
            g.Children.Add(t4); g.Children.Add(val4);

            return g;
        }

        private void ReplayTelemetry()
        {
            if (_lastLoad != null) UpdateSystemLoad(_lastLoad);
            if (_lastPower != null) UpdatePower(_lastPower);
            if (_lastNet != null) UpdateNetwork(_lastNet);
            if (_lastZt != null) UpdateZeroTier(_lastZt);
            UpdateChipVisuals();
            ApplyMemberFilterAndSearch();
        }
    }
}
