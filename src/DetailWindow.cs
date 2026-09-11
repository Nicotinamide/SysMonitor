using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Markup;

namespace SysMonitor
{
    public class DetailWindow : Window
    {
        // UI Controls - CPU & RAM
        private TextBlock _tbCpuVal;
        private Rectangle _rectCpuBar;
        private TextBlock _tbRamVal;
        private TextBlock _tbRamPercent;
        private Rectangle _rectRamBar;

        // UI Controls - Power
        private Border _bPowerBadge;
        private TextBlock _tbPowerBadge;
        private TextBlock _tbPowerPcWatts;   // 电脑功耗
        private TextBlock _tbPowerBatWatts;  // 电池功率 (charge/discharge)
        private TextBlock _tbPowerBatLevel;  // 电池电量 Wh + %
        private TextBlock _tbPowerBatEta;    // 预估续航 / 充电时间
        // legacy kept for compatibility
        private TextBlock _tbPowerWatts;
        private TextBlock _tbPowerCapacity;
        private TextBlock _tbPowerAc;
        private TextBlock _tbPowerEstimate;

        // UI Controls - Network
        private TextBlock _tbNetIface;
        private TextBlock _tbNetLinkSpeed;
        private TextBlock _tbNetLocalIp;
        private TextBlock _tbNetSessionTraffic;
        private Image _imgDetailFlag;
        private TextBlock _tbNetPublicIp;
        private TextBlock _tbNetGeoIsp;

        // UI Controls - ZeroTier
        private Border _cardZt;
        private Border _bZtBadge;
        private TextBlock _tbZtBadge;
        private TextBlock _tbZtNode;
        private StackPanel _spMoons;

        // UI Controls - Member Directory & Settings
        private Border _cardMember;
        private MemberDirectoryEngine _memberDir;
        private StackPanel _pnlMemberSearch;
        private Border _overlaySettings;
        public event Action TokenConfigChanged;

        public bool HasConfiguredToken
        {
            get { return _memberDir != null && _memberDir.HasToken; }
        }

        public void UpdateZeroTierCardsVisibility()
        {
            // 上方：ZeroTier 虚拟局域网 & Moon 列表 —— 本地客户端状态，完全不受到 Web API Token 的影响！
            if (_cardZt != null)
            {
                bool isZtAvailable = _lastZt == null || _lastZt.IsInstalled || _lastZt.IsRunning;
                _cardZt.Visibility = isZtAvailable ? Visibility.Visible : Visibility.Collapsed;
            }

            // 下方：成员设备列表 —— 远程 Web 控制器 API 数据，严格受到 Token 影响！未配置 Token 时折叠隐藏，配置后正常展示
            if (_cardMember != null)
            {
                _cardMember.Visibility = HasConfiguredToken ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        public DateTime LastDeactivatedTime { get; private set; }
        private TextBlock _tbUpdateStatus;
        private StackPanel _pnlUpdateActions;
        private Button _btnPullUpdate;
        private string _latestDownloadUrl;
        private string _latestReleasePageUrl;
        private TextBox _tbMemberSearch;
        private TextBlock _tbMemberSearchPlaceholder;
        private Button _btnMemberClear;
        private TextBlock _tbMemberStatus;
        private StackPanel _spMemberResults;
        private Border _bMemberToast;
        private TextBlock _tbMemberToast;
        private DispatcherTimer _toastTimer;
        public Action<string, string, ToastType, string> RequestNotification;
        private ScrollViewer _svMembers;
        private ScrollViewer _svRoot;
        private double _currentWaTop = 0;
        private double _currentWaBottom = 0;

        // Member Filtering & Collapsible Search Controls
        private enum MemberFilterType { All, Online, Offline }
        private MemberFilterType _currentMemberFilter = MemberFilterType.All;
        private Button _btnFilterAll;
        private Border _chipAll;
        private TextBlock _tbChipAll;
        private Button _btnFilterOnline;
        private Border _chipOnline;
        private TextBlock _tbChipOnline;
        private Button _btnFilterOffline;
        private Border _chipOffline;
        private TextBlock _tbChipOffline;
        private Button _btnToggleSearch;
        private Border _searchBoxBorder;

        // Settings Fields
        private TextBox _txtSettingUrl;
        private PasswordBox _pbSettingToken;
        private TextBox _txtSettingTokenPlain;
        private bool _isTokenPlainVisible = false;
        private Button _btnToggleTokenEye;
        private TextBox _txtSettingNwid;
        private TextBlock _lblTestStatus;
        private Button _btnTestConn;
        private Button _btnSaveConfig;

        // Telemetry Cache for instant Theme/Language hot re-render
        private SystemLoadData _lastLoad;
        private PowerData _lastPower;
        private NetworkData _lastNet;
        private ZeroTierData _lastZt;
        private string _lastStatusText;
        private List<MemberNode> _lastMemberList;

        public DetailWindow()
        {
            Width = 385;
            SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            // 当鼠标点击了其他窗口、桌面或外部区域导致详情页失焦时，自动收起关闭，避免遮挡用户其他工作
            Deactivated += delegate
            {
                LastDeactivatedTime = DateTime.UtcNow;
                Hide();
            };

            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromSeconds(2.5);
            _toastTimer.Tick += delegate
            {
                _toastTimer.Stop();
                if (_bMemberToast != null) _bMemberToast.Visibility = Visibility.Collapsed;
            };

            _memberDir = new MemberDirectoryEngine();
            _lastMemberList = _memberDir.GetAllMembers();
            _memberDir.StatusChanged += OnMemberStatusChanged;
            _memberDir.MembersUpdated += OnMembersUpdated;
            _memberDir.SyncError += OnMemberSyncError;
            _memberDir.ConfigChanged += delegate
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    UpdateZeroTierCardsVisibility();
                    if (TokenConfigChanged != null) TokenConfigChanged();
                }));
            };

            BuildUi();

            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DependencyObject src = e.OriginalSource as DependencyObject;
                    while (src != null && src != this)
                    {
                        if (src is Button || src is TextBox || src is PasswordBox || src is ScrollViewer)
                            return;
                        src = VisualTreeHelper.GetParent(src);
                    }
                    try { DragMove(); } catch { }
                }
            };

            AppSettings.SettingsChanged += delegate
            {
                Dispatcher.BeginInvoke(new Action(RebuildUi));
            };

            UpdateChipVisuals();
            ApplyMemberFilterAndSearch();
        }

        public void RebuildUi()
        {
            bool wasSettingsOpen = (_overlaySettings != null && _overlaySettings.Visibility == Visibility.Visible);
            bool wasSearchOpen = (_searchBoxBorder != null && _searchBoxBorder.Visibility == Visibility.Visible);
            string prevSearchText = (_tbMemberSearch != null) ? _tbMemberSearch.Text : "";
            string prevUrl = (_txtSettingUrl != null) ? _txtSettingUrl.Text : null;
            string prevNwid = (_txtSettingNwid != null) ? _txtSettingNwid.Text : null;
            string prevToken = _isTokenPlainVisible ? (_txtSettingTokenPlain != null ? _txtSettingTokenPlain.Text : null) : (_pbSettingToken != null ? _pbSettingToken.Password : null);

            BuildUi();

            if (wasSettingsOpen && _overlaySettings != null)
            {
                _overlaySettings.Visibility = Visibility.Visible;
                if (prevUrl != null && _txtSettingUrl != null) _txtSettingUrl.Text = prevUrl;
                if (prevNwid != null && _txtSettingNwid != null) _txtSettingNwid.Text = prevNwid;
                if (prevToken != null)
                {
                    if (_pbSettingToken != null) _pbSettingToken.Password = prevToken;
                    if (_txtSettingTokenPlain != null) _txtSettingTokenPlain.Text = prevToken;
                }
            }
            if (wasSearchOpen && _searchBoxBorder != null)
            {
                _searchBoxBorder.Visibility = Visibility.Visible;
                if (_btnToggleSearch != null) _btnToggleSearch.Foreground = AppTheme.Current.AccentBlue;
            }
            if (!string.IsNullOrEmpty(prevSearchText) && _tbMemberSearch != null)
            {
                _tbMemberSearch.Text = prevSearchText;
            }

            if (_lastLoad != null) UpdateSystemLoad(_lastLoad);
            if (_lastPower != null) UpdatePower(_lastPower);
            if (_lastNet != null) UpdateNetwork(_lastNet);
            if (_lastZt != null) UpdateZeroTier(_lastZt);
            if (_memberDir != null)
            {
                string st = _memberDir.GetCurrentStatusText();
                if (!string.IsNullOrEmpty(st)) OnMemberStatusChanged(st);
            }
            UpdateChipVisuals();
            ApplyMemberFilterAndSearch();
            EnsureWindowWithinScreen();
        }

        private void BuildUi()
        {
            ThemePalette theme = AppTheme.Current;
            TranslationSet i18n = I18n.Current;

            Border rootBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = theme.WindowBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 22,
                    ShadowDepth = 4,
                    Color = theme.ShadowColor,
                    Opacity = theme.ShadowOpacity
                }
            };

            Grid rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: Pinned Top Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 1: Scrollable Cards Body

            // 1. Header (Title, OS Tag, Settings Button, Close Button)
            Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock title = new TextBlock
            {
                Text = "⚡ " + i18n.DetailWinTitle,
                Foreground = theme.TextPrimary,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border sysChip = new Border
            {
                Background = theme.CardBg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(8, 0, 0, 0),
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(0.6)
            };
            TextBlock tbSys = new TextBlock
            {
                Text = Environment.Is64BitOperatingSystem ? "WIN 64-BIT" : "WIN 32-BIT",
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.TextSecondary
            };
            sysChip.Child = tbSys;
            titleSp.Children.Add(title);
            titleSp.Children.Add(sysChip);
            Grid.SetColumn(titleSp, 0);
            headerGrid.Children.Add(titleSp);

            StackPanel topBtns = new StackPanel { Orientation = Orientation.Horizontal };
            Button btnSettings = AppTheme.CreateIconButton("⚙", i18n.SettingsTitle, delegate
            {
                ToggleSettingsView();
            }, 12);
            btnSettings.Margin = new Thickness(0, 0, 4, 0);
            topBtns.Children.Add(btnSettings);

            Button btnClose = AppTheme.CreateIconButton("✕", i18n.Close, delegate { this.Hide(); }, 11);
            topBtns.Children.Add(btnClose);

            Grid.SetColumn(topBtns, 1);
            headerGrid.Children.Add(topBtns);
            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // Container for all scrollable cards (100% preserves original order)
            StackPanel cardsStack = new StackPanel();

            // 1. Card: CPU & RAM Compute Load
            Border cardLoad = CreateCardBorder(theme);
            StackPanel spLoad = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
            spLoad.Children.Add(CreateCardHeader(theme, "💻 " + i18n.SectionCpuRam));

            Grid gLoad = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            gLoad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gLoad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // gap
            gLoad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // CPU Col
            StackPanel spCpu = new StackPanel();
            Grid gCpuTop = new Grid();
            gCpuTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gCpuTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock lblCpu = CreateMutedText(theme, i18n.CpuUsage);
            Grid.SetColumn(lblCpu, 0);
            _tbCpuVal = new TextBlock
            {
                Text = "0.0%",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = theme.AccentBlue,
                FontFamily = new FontFamily("Consolas, Segoe UI")
            };
            Grid.SetColumn(_tbCpuVal, 1);
            gCpuTop.Children.Add(lblCpu);
            gCpuTop.Children.Add(_tbCpuVal);
            spCpu.Children.Add(gCpuTop);

            Grid gCpuBar = CreateProgressBar(theme, out _rectCpuBar, theme.AccentBlue.Color);
            spCpu.Children.Add(gCpuBar);
            Grid.SetColumn(spCpu, 0);
            gLoad.Children.Add(spCpu);

            // RAM Col
            StackPanel spRam = new StackPanel();
            Grid gRamTop = new Grid();
            gRamTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gRamTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _tbRamPercent = CreateMutedText(theme, i18n.Memory + " 0%");
            Grid.SetColumn(_tbRamPercent, 0);
            _tbRamVal = new TextBlock
            {
                Text = "-- / -- GB",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = theme.AccentEmerald,
                FontFamily = new FontFamily("Consolas, Segoe UI")
            };
            Grid.SetColumn(_tbRamVal, 1);
            gRamTop.Children.Add(_tbRamPercent);
            gRamTop.Children.Add(_tbRamVal);
            spRam.Children.Add(gRamTop);

            Grid gRamBar = CreateProgressBar(theme, out _rectRamBar, theme.AccentEmerald.Color);
            spRam.Children.Add(gRamBar);
            Grid.SetColumn(spRam, 2);
            gLoad.Children.Add(spRam);

            spLoad.Children.Add(gLoad);
            cardLoad.Child = spLoad;
            cardsStack.Children.Add(cardLoad);

            // 2. Card: Power & Battery — 3-row clean layout, no brackets
            Border cardPower = CreateCardBorder(theme);
            cardPower.Margin = new Thickness(0, 8, 0, 0);
            StackPanel spPower = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };

            // Header row: title + badge (supply source)
            Grid gpHead = new Grid();
            gpHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gpHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock lblPowerHead = CreateCardHeader(theme, "🔋 " + i18n.SectionPower);
            Grid.SetColumn(lblPowerHead, 0);
            gpHead.Children.Add(lblPowerHead);

            _bPowerBadge = CreateBadge("...", theme.AccentAmber.Color, out _tbPowerBadge);
            Grid.SetColumn(_bPowerBadge, 1);
            gpHead.Children.Add(_bPowerBadge);
            spPower.Children.Add(gpHead);

            // Row 1: 电脑功耗 | 电池功率
            _tbPowerPcWatts = CreateValueText(theme, "--");
            _tbPowerBatWatts = CreateValueText(theme, "--");
            spPower.Children.Add(CreateMetricsRow(theme,
                i18n.PowerPc + ":", _tbPowerPcWatts,
                i18n.PowerBattery + ":", _tbPowerBatWatts));

            // Row 2: 电池电量 | 预估续航
            _tbPowerBatLevel = CreateValueText(theme, "--");
            _tbPowerBatEta = CreateValueText(theme, "--");
            spPower.Children.Add(CreateMetricsRow(theme,
                i18n.BatteryCapacity + ":", _tbPowerBatLevel,
                i18n.EstBatteryLife + ":", _tbPowerBatEta));

            // keep legacy refs pointing to the new controls for compat
            _tbPowerWatts = _tbPowerPcWatts;
            _tbPowerCapacity = _tbPowerBatLevel;
            _tbPowerAc = _tbPowerBatWatts;
            _tbPowerEstimate = _tbPowerBatEta;

            cardPower.Child = spPower;
            cardsStack.Children.Add(cardPower);

            // 3. Card: Network & Public IP
            Border cardNet = CreateCardBorder(theme);
            cardNet.Margin = new Thickness(0, 8, 0, 0);
            StackPanel spNet = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
            spNet.Children.Add(CreateCardHeader(theme, "🌐 " + i18n.SectionNetwork));

            _tbNetIface = CreateValueText(theme, i18n.NetFetching);
            _tbNetLinkSpeed = CreateValueText(theme, "--");
            _tbNetLocalIp = CreateValueText(theme, "127.0.0.1");
            _tbNetSessionTraffic = CreateValueText(theme, "↓ 0MB  ↑ 0MB");

            spNet.Children.Add(CreateMetricsGrid(theme, 
                i18n.ActiveIface + ":", _tbNetIface,
                i18n.LinkSpeed + ":", _tbNetLinkSpeed,
                i18n.LocalIp + ":", _tbNetLocalIp,
                i18n.SessionTraffic + ":", _tbNetSessionTraffic));

            // Highlighted Public IP Box
            Border ipBox = new Border
            {
                Background = theme.InnerTileBg,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 6, 0, 2),
                BorderBrush = theme.BorderMuted,
                BorderThickness = new Thickness(0.8)
            };
            StackPanel ipSp = new StackPanel();

            StackPanel ipRow = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock lblPub = CreateMutedText(theme, i18n.PublicExitIsp + ": ");
            lblPub.Margin = new Thickness(0, 0, 6, 0);
            ipRow.Children.Add(lblPub);

            _imgDetailFlag = new Image
            {
                Width = 16,
                Height = 11,
                Margin = new Thickness(0, 0, 6, 0),
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            ipRow.Children.Add(_imgDetailFlag);

            _tbNetPublicIp = new TextBlock
            {
                Text = i18n.NetFetching,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = theme.AccentBlue,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center
            };
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
            spNet.Children.Add(ipBox);

            cardNet.Child = spNet;
            cardsStack.Children.Add(cardNet);

            // 4. Card: ZeroTier Mesh & Moons
            Border cardZt = CreateCardBorder(theme);
            _cardZt = cardZt;
            cardZt.Margin = new Thickness(0, 8, 0, 0);
            StackPanel spZt = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };

            Grid zHead = new Grid();
            zHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            zHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock lblZtHead = CreateCardHeader(theme, "🔗 " + i18n.SectionZeroTier);
            Grid.SetColumn(lblZtHead, 0);
            zHead.Children.Add(lblZtHead);

            _bZtBadge = CreateBadge(i18n.NetFetching, theme.TextMuted.Color, out _tbZtBadge);
            Grid.SetColumn(_bZtBadge, 1);
            zHead.Children.Add(_bZtBadge);
            spZt.Children.Add(zHead);

            _tbZtNode = CreateValueText(theme, i18n.ZtOffline);
            StackPanel nodeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 4) };
            TextBlock nodeLabel = CreateMutedText(theme, i18n.LocalNodeId + ": ");
            nodeLabel.Margin = new Thickness(0, 0, 6, 0);
            nodeRow.Children.Add(nodeLabel);
            nodeRow.Children.Add(_tbZtNode);
            spZt.Children.Add(nodeRow);

            _spMoons = new StackPanel();
            spZt.Children.Add(_spMoons);

            cardZt.Child = spZt;
            cardsStack.Children.Add(cardZt);

            // 5. Card: ZeroTier Member Directory & Fast Search
            Border cardMember = CreateCardBorder(theme);
            _cardMember = cardMember;
            cardMember.Margin = new Thickness(0, 8, 0, 0);
            StackPanel spMember = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };

            Grid mHead = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            mHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock lblMemberHead = CreateCardHeader(theme, "👥 " + i18n.SectionMembers);
            Grid.SetColumn(lblMemberHead, 0);
            mHead.Children.Add(lblMemberHead);

            StackPanel spHeadRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Status label: shown when syncing or error
            _tbMemberStatus = new TextBlock
            {
                Text = _memberDir != null ? _memberDir.GetCurrentStatusText() : i18n.NetFetching,
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Visibility = Visibility.Collapsed
            };
            spHeadRight.Children.Add(_tbMemberStatus);

            // Filter Chips: All, Online, Offline
            Border chipAllBorder;
            TextBlock tbChipAll;
            _btnFilterAll = CreateChipPill(string.Format("{0} 0", i18n.FilterAll), delegate(object sender, RoutedEventArgs e)
            {
                _currentMemberFilter = MemberFilterType.All;
                UpdateChipVisuals();
                ApplyMemberFilterAndSearch();
                if (_svMembers != null) _svMembers.ScrollToTop();
            }, out chipAllBorder, out tbChipAll);
            _chipAll = chipAllBorder;
            _tbChipAll = tbChipAll;
            _btnFilterAll.ToolTip = i18n.Lang == AppLanguage.Zh ? "显示全部设备" : "Show All Devices";
            spHeadRight.Children.Add(_btnFilterAll);

            Border chipOnlineBorder;
            TextBlock tbChipOnline;
            _btnFilterOnline = CreateChipPill("🟢 0", delegate(object sender, RoutedEventArgs e)
            {
                if (_currentMemberFilter == MemberFilterType.Online)
                    _currentMemberFilter = MemberFilterType.All;
                else
                    _currentMemberFilter = MemberFilterType.Online;
                UpdateChipVisuals();
                ApplyMemberFilterAndSearch();
                if (_svMembers != null) _svMembers.ScrollToTop();
            }, out chipOnlineBorder, out tbChipOnline);
            _chipOnline = chipOnlineBorder;
            _tbChipOnline = tbChipOnline;
            _btnFilterOnline.ToolTip = i18n.Lang == AppLanguage.Zh ? "仅显示在线设备" : "Show Online Devices Only";
            spHeadRight.Children.Add(_btnFilterOnline);

            Border chipOfflineBorder;
            TextBlock tbChipOffline;
            _btnFilterOffline = CreateChipPill("⚪ 0", delegate(object sender, RoutedEventArgs e)
            {
                if (_currentMemberFilter == MemberFilterType.Offline)
                    _currentMemberFilter = MemberFilterType.All;
                else
                    _currentMemberFilter = MemberFilterType.Offline;
                UpdateChipVisuals();
                ApplyMemberFilterAndSearch();
                if (_svMembers != null) _svMembers.ScrollToTop();
            }, out chipOfflineBorder, out tbChipOffline);
            _chipOffline = chipOfflineBorder;
            _tbChipOffline = tbChipOffline;
            _btnFilterOffline.ToolTip = i18n.Lang == AppLanguage.Zh ? "仅显示离线设备" : "Show Offline Devices Only";
            spHeadRight.Children.Add(_btnFilterOffline);

            // Toggle Search Button [🔍]
            _btnToggleSearch = AppTheme.CreateIconButton("🔍", i18n.ToggleSearch, delegate
            {
                ToggleSearchBox();
            }, 11);
            _btnToggleSearch.Margin = new Thickness(3, 0, 1, 0);
            spHeadRight.Children.Add(_btnToggleSearch);

            // Refresh Button [↻]
            Button btnRefreshMembers = AppTheme.CreateIconButton("↻", i18n.Syncing, delegate
            {
                if (_memberDir != null) _memberDir.TriggerRefresh();
            }, 12);
            spHeadRight.Children.Add(btnRefreshMembers);

            Grid.SetColumn(spHeadRight, 1);
            mHead.Children.Add(spHeadRight);
            spMember.Children.Add(mHead);

            // ==========================================
            // Panel 1: Search & Results View
            // ==========================================
            _pnlMemberSearch = new StackPanel();

            // Search input box (collapsible)
            _searchBoxBorder = new Border
            {
                Background = theme.InnerTileBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 4),
                Visibility = Visibility.Collapsed
            };

            Grid searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock tbSearchIcon = new TextBlock
            {
                Text = "🔎",
                FontSize = 10,
                Foreground = theme.TextDim,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(tbSearchIcon, 0);
            searchGrid.Children.Add(tbSearchIcon);

            Grid inputLayer = new Grid();
            _tbMemberSearchPlaceholder = new TextBlock
            {
                Text = i18n.SearchPlaceholder,
                FontSize = 10.5,
                Foreground = theme.TextDim,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            inputLayer.Children.Add(_tbMemberSearchPlaceholder);

            _tbMemberSearch = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = theme.TextPrimary,
                CaretBrush = theme.AccentBlue,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            _tbMemberSearch.TextChanged += delegate
            {
                string text = _tbMemberSearch.Text;
                _tbMemberSearchPlaceholder.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
                _btnMemberClear.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
                ApplyMemberFilterAndSearch();
            };
            _tbMemberSearch.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    if (!string.IsNullOrEmpty(_tbMemberSearch.Text))
                    {
                        _tbMemberSearch.Text = "";
                    }
                    else
                    {
                        CollapseSearchBox();
                    }
                }
            };
            inputLayer.Children.Add(_tbMemberSearch);
            Grid.SetColumn(inputLayer, 1);
            searchGrid.Children.Add(inputLayer);

            _btnMemberClear = AppTheme.CreateIconButton("✕", "", delegate
            {
                _tbMemberSearch.Text = "";
                _tbMemberSearch.Focus();
            }, 9.5);
            _btnMemberClear.Visibility = Visibility.Collapsed;
            Grid.SetColumn(_btnMemberClear, 2);
            searchGrid.Children.Add(_btnMemberClear);

            _searchBoxBorder.Child = searchGrid;
            _pnlMemberSearch.Children.Add(_searchBoxBorder);

            // Copy Toast Box
            _bMemberToast = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B)),
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 0, 4),
                Visibility = Visibility.Collapsed
            };
            _tbMemberToast = new TextBlock
            {
                Text = "✓ " + i18n.CopiedAll,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.AccentEmerald,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _bMemberToast.Child = _tbMemberToast;
            _pnlMemberSearch.Children.Add(_bMemberToast);

            // ScrollViewer for results with MODERN SLIM SCROLLBAR (NO WHITE STRIP!)
            _svMembers = new ScrollViewer
            {
                MaxHeight = 180,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            AppTheme.ApplyModernScrollBarStyle(_svMembers);

            // Forward mouse wheel when at scroll boundaries to outer root ScrollViewer
            _svMembers.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e)
            {
                if (_svRoot != null)
                {
                    bool atTop = _svMembers.VerticalOffset <= 0.001;
                    bool atBottom = _svMembers.VerticalOffset >= (_svMembers.ExtentHeight - _svMembers.ViewportHeight - 0.5);
                    if ((e.Delta > 0 && atTop) || (e.Delta < 0 && atBottom))
                    {
                        e.Handled = true;
                        _svRoot.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                        {
                            RoutedEvent = UIElement.MouseWheelEvent,
                            Source = sender
                        });
                    }
                }
            };

            _spMemberResults = new StackPanel();
            _svMembers.Content = _spMemberResults;
            _pnlMemberSearch.Children.Add(_svMembers);
            spMember.Children.Add(_pnlMemberSearch);

            cardMember.Child = spMember;
            cardsStack.Children.Add(cardMember);

            // Wrap cardsStack in modern root ScrollViewer with slim scrollbar
            _svRoot = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                Content = cardsStack
            };
            AppTheme.ApplyModernScrollBarStyle(_svRoot);
            Grid.SetRow(_svRoot, 1);
            rootGrid.Children.Add(_svRoot);

            // Upper Overlay Settings Layer (Floating directly on top of cards, zero layout shift)
            _overlaySettings = BuildSettingsOverlay(theme, i18n);
            Grid.SetRow(_overlaySettings, 1);
            rootGrid.Children.Add(_overlaySettings);

            UpdateZeroTierCardsVisibility();

            rootBorder.Child = rootGrid;
            Content = rootBorder;
        }

        private Border BuildSettingsOverlay(ThemePalette theme, TranslationSet i18n)
        {
            Border overlay = new Border
            {
                Visibility = Visibility.Collapsed,
                Background = theme.CardBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(2, 0, 2, 8),
                Padding = new Thickness(12, 10, 12, 10),
                VerticalAlignment = VerticalAlignment.Top,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 16,
                    ShadowDepth = 4,
                    Color = theme.ShadowColor,
                    Opacity = 0.35
                }
            };

            StackPanel sfSp = new StackPanel();

            // Overlay Top Bar: Title & Close Button
            Grid topRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock lblSettingsTitle = new TextBlock
            {
                Text = "⚙ " + i18n.SettingsTitle,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblSettingsTitle, 0);
            topRow.Children.Add(lblSettingsTitle);

            Button btnOverlayClose = AppTheme.CreateIconButton("✕", i18n.Close, delegate
            {
                ResetTokenPlainVisibility();
                _overlaySettings.Visibility = Visibility.Collapsed;
            }, 11);
            Grid.SetColumn(btnOverlayClose, 1);
            topRow.Children.Add(btnOverlayClose);

            sfSp.Children.Add(topRow);

            // Row: Theme Switcher & Language Switcher
            Grid prefGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            prefGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            prefGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            prefGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Theme toggle button
            Button btnThemeToggle = new Button
            {
                Content = AppSettings.Theme == ThemeMode.Dark ? i18n.ThemeLight : i18n.ThemeDark,
                FontSize = 10,
                Foreground = theme.TextPrimary,
                Background = theme.InnerTileBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(6, 4, 6, 4),
                Cursor = Cursors.Hand
            };
            btnThemeToggle.Click += delegate
            {
                AppSettings.SetTheme(AppSettings.Theme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark);
            };
            Grid.SetColumn(btnThemeToggle, 0);
            prefGrid.Children.Add(btnThemeToggle);

            // Language toggle button
            Button btnLangToggle = new Button
            {
                Content = AppSettings.Language == AppLanguage.Zh ? "🇺🇸 English" : "🇨🇳 简体中文",
                FontSize = 10,
                Foreground = theme.TextPrimary,
                Background = theme.InnerTileBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(6, 4, 6, 4),
                Cursor = Cursors.Hand
            };
            btnLangToggle.Click += delegate
            {
                AppSettings.SetLanguage(AppSettings.Language == AppLanguage.Zh ? AppLanguage.En : AppLanguage.Zh);
            };
            Grid.SetColumn(btnLangToggle, 2);
            prefGrid.Children.Add(btnLangToggle);

            sfSp.Children.Add(prefGrid);

            // Section: 首页微件模块定制
            BuildModulesSection(sfSp, theme, i18n);

            // Field: URL
            TextBlock lblUrl = CreateMutedText(theme, i18n.ControllerUrl);
            lblUrl.Margin = new Thickness(0, 0, 0, 2);
            sfSp.Children.Add(lblUrl);
            _txtSettingUrl = CreateInputTextBox(theme, _memberDir != null ? _memberDir.CurrentConfig.ControllerUrl : "");
            sfSp.Children.Add(_txtSettingUrl);

            // Side-by-side row: Network ID (left, 135px) and API Token (right, 1fr)
            Grid idTokenGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            idTokenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) });
            idTokenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            idTokenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Network ID column
            StackPanel spNwidCol = new StackPanel();
            TextBlock lblNwid = CreateMutedText(theme, i18n.NetworkId);
            lblNwid.Margin = new Thickness(0, 0, 0, 2);
            spNwidCol.Children.Add(lblNwid);
            _txtSettingNwid = CreateInputTextBox(theme, _memberDir != null ? _memberDir.CurrentConfig.NetworkId : "");
            spNwidCol.Children.Add(_txtSettingNwid);
            Grid.SetColumn(spNwidCol, 0);
            idTokenGrid.Children.Add(spNwidCol);

            // API Token column
            StackPanel spTokenCol = new StackPanel();
            TextBlock lblToken = CreateMutedText(theme, i18n.ApiToken);
            lblToken.Margin = new Thickness(0, 0, 0, 2);
            spTokenCol.Children.Add(lblToken);

            Grid tokenGrid = new Grid();
            tokenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tokenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            string initToken = _memberDir != null ? _memberDir.CurrentConfig.ApiToken : "";
            _pbSettingToken = new PasswordBox
            {
                Background = theme.InputBg,
                Foreground = theme.TextPrimary,
                BorderBrush = theme.InputBorder,
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 11,
                PasswordChar = '●',
                Password = initToken
            };
            _pbSettingToken.PasswordChanged += delegate
            {
                if (!_isTokenPlainVisible && _txtSettingTokenPlain != null)
                {
                    _txtSettingTokenPlain.Text = _pbSettingToken.Password;
                }
            };
            Grid.SetColumn(_pbSettingToken, 0);
            tokenGrid.Children.Add(_pbSettingToken);

            _txtSettingTokenPlain = new TextBox
            {
                Background = theme.InputBg,
                Foreground = theme.TextPrimary,
                BorderBrush = theme.InputBorder,
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 11,
                Visibility = Visibility.Collapsed,
                Text = initToken
            };
            _txtSettingTokenPlain.TextChanged += delegate
            {
                if (_isTokenPlainVisible && _pbSettingToken != null)
                {
                    _pbSettingToken.Password = _txtSettingTokenPlain.Text;
                }
            };
            Grid.SetColumn(_txtSettingTokenPlain, 0);
            tokenGrid.Children.Add(_txtSettingTokenPlain);

            _btnToggleTokenEye = AppTheme.CreateIconButton("👁", i18n.ToggleToken, delegate
            {
                _isTokenPlainVisible = !_isTokenPlainVisible;
                if (_isTokenPlainVisible)
                {
                    _txtSettingTokenPlain.Text = _pbSettingToken.Password;
                    _pbSettingToken.Visibility = Visibility.Collapsed;
                    _txtSettingTokenPlain.Visibility = Visibility.Visible;
                    _btnToggleTokenEye.Content = "🙈";
                }
                else
                {
                    _pbSettingToken.Password = _txtSettingTokenPlain.Text;
                    _txtSettingTokenPlain.Visibility = Visibility.Collapsed;
                    _pbSettingToken.Visibility = Visibility.Visible;
                    _btnToggleTokenEye.Content = "👁";
                }
            }, 11);
            Grid.SetColumn(_btnToggleTokenEye, 1);
            tokenGrid.Children.Add(_btnToggleTokenEye);
            spTokenCol.Children.Add(tokenGrid);
            Grid.SetColumn(spTokenCol, 2);
            idTokenGrid.Children.Add(spTokenCol);

            sfSp.Children.Add(idTokenGrid);

            // Test status feedback label
            _lblTestStatus = new TextBlock
            {
                Text = "",
                FontSize = 10,
                Margin = new Thickness(0, 4, 0, 2),
                TextWrapping = TextWrapping.Wrap
            };
            sfSp.Children.Add(_lblTestStatus);

            // Section: GitHub 状态与在线更新
            Border updateBox = new Border
            {
                Background = theme.InnerTileBg,
                CornerRadius = new CornerRadius(6),
                BorderBrush = theme.BorderMuted,
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 4, 0, 4)
            };

            StackPanel updateSp = new StackPanel();
            Grid upHeadGrid = new Grid();
            upHeadGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            upHeadGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock lblVer = CreateMutedText(theme, string.Format(i18n.CurrentVersionFormat, UpdateChecker.CurrentVersion));
            lblVer.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(lblVer, 0);
            upHeadGrid.Children.Add(lblVer);

            TextBlock lblBranch = CreateMutedText(theme, "main");
            lblBranch.FontSize = 9.5;
            lblBranch.Opacity = 0.6;
            Grid.SetColumn(lblBranch, 1);
            upHeadGrid.Children.Add(lblBranch);

            updateSp.Children.Add(upHeadGrid);

            // Update status text
            _tbUpdateStatus = new TextBlock
            {
                Text = "",
                FontSize = 9.5,
                Margin = new Thickness(0, 4, 0, 2),
                Foreground = theme.TextMuted,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            updateSp.Children.Add(_tbUpdateStatus);

            // Action row: 检查更新、拉取更新（仅有更新时显示）、GitHub 放在同一排
            _pnlUpdateActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };

            Button btnCheckUp = CreateSmallActionButton(
                "🔄 " + i18n.CheckUpdate,
                theme.AccentBlue,
                new SolidColorBrush(Color.FromArgb(25, theme.AccentBlue.Color.R, theme.AccentBlue.Color.G, theme.AccentBlue.Color.B)),
                new SolidColorBrush(Color.FromArgb(80, theme.AccentBlue.Color.R, theme.AccentBlue.Color.G, theme.AccentBlue.Color.B)),
                delegate { CheckForAppUpdates(true); }
            );
            _pnlUpdateActions.Children.Add(btnCheckUp);

            _btnPullUpdate = CreateSmallActionButton(
                "⬇ " + i18n.DownloadUpdate,
                theme.AccentEmerald,
                new SolidColorBrush(Color.FromArgb(30, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B)),
                new SolidColorBrush(Color.FromArgb(90, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B)),
                delegate
                {
                    if (!string.IsNullOrEmpty(_latestDownloadUrl))
                    {
                        _tbUpdateStatus.Text = "⏳ " + I18n.Current.CheckingUpdate;
                        _btnPullUpdate.IsEnabled = false;
                        UpdateChecker.DownloadAndApplyUpdateAsync(_latestDownloadUrl,
                            delegate(int pct)
                            {
                                Dispatcher.BeginInvoke(new Action(delegate
                                {
                                    _tbUpdateStatus.Text = string.Format("⏳ 下载中... {0}%", pct);
                                }));
                            },
                            delegate(bool ok, string msg)
                            {
                                Dispatcher.BeginInvoke(new Action(delegate
                                {
                                    _btnPullUpdate.IsEnabled = true;
                                    _tbUpdateStatus.Text = (ok ? "✓ " : "✕ ") + msg;
                                }));
                            });
                    }
                    else if (!string.IsNullOrEmpty(_latestReleasePageUrl))
                    {
                        try { Process.Start(_latestReleasePageUrl); } catch { }
                    }
                }
            );
            _btnPullUpdate.Visibility = Visibility.Collapsed;
            _pnlUpdateActions.Children.Add(_btnPullUpdate);

            Button btnOpenRepo = CreateSmallActionButton(
                "🌐 GitHub",
                theme.TextSecondary,
                theme.CardBg,
                theme.BorderBrush,
                delegate
                {
                    try { Process.Start("https://github.com/Nicotinamide/SysMonitor"); } catch { }
                }
            );
            _pnlUpdateActions.Children.Add(btnOpenRepo);

            updateSp.Children.Add(_pnlUpdateActions);
            updateBox.Child = updateSp;
            sfSp.Children.Add(updateBox);

            // Actions row: [⚡ 测试连接]  [💾 保存]  [✕ 关闭]
            Grid btnGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _btnTestConn = new Button
            {
                Content = "⚡ " + i18n.TestConnection,
                FontSize = 10.5,
                Foreground = theme.AccentBlue,
                Background = new SolidColorBrush(Color.FromArgb(30, theme.AccentBlue.Color.R, theme.AccentBlue.Color.G, theme.AccentBlue.Color.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, theme.AccentBlue.Color.R, theme.AccentBlue.Color.G, theme.AccentBlue.Color.B)),
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(8, 3, 8, 3),
                Cursor = Cursors.Hand
            };
            _btnTestConn.Click += delegate
            {
                string url = _txtSettingUrl.Text.Trim();
                string nwid = _txtSettingNwid.Text.Trim();
                string token = _isTokenPlainVisible ? _txtSettingTokenPlain.Text.Trim() : _pbSettingToken.Password.Trim();

                _lblTestStatus.Text = "⏳ " + i18n.TestingConn;
                _lblTestStatus.Foreground = theme.AccentAmber;

                _memberDir.TestConnectionAsync(url, nwid, token, delegate(bool success, int count, string err)
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (success)
                        {
                            _lblTestStatus.Text = string.Format(i18n.ConnSuccessFormat, count);
                            _lblTestStatus.Foreground = theme.AccentEmerald;
                        }
                        else
                        {
                            _lblTestStatus.Text = string.Format(i18n.ConnFailedFormat, err ?? "Error");
                            _lblTestStatus.Foreground = theme.AccentRed;
                        }
                    }));
                });
            };
            Grid.SetColumn(_btnTestConn, 0);
            btnGrid.Children.Add(_btnTestConn);

            _btnSaveConfig = new Button
            {
                Content = "💾 " + i18n.SaveConfig,
                FontSize = 10.5,
                Foreground = theme.AccentEmerald,
                Background = new SolidColorBrush(Color.FromArgb(35, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B)),
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(10, 3, 10, 3),
                Cursor = Cursors.Hand
            };
            _btnSaveConfig.Click += delegate
            {
                string url = _txtSettingUrl.Text.Trim();
                string nwid = _txtSettingNwid.Text.Trim();
                string token = _isTokenPlainVisible ? _txtSettingTokenPlain.Text.Trim() : _pbSettingToken.Password.Trim();

                ResetTokenPlainVisibility();

                _memberDir.SaveEncryptedConfig(url, nwid, token);
                UpdateZeroTierCardsVisibility();
                if (TokenConfigChanged != null) TokenConfigChanged();
                _overlaySettings.Visibility = Visibility.Collapsed;
                if (RequestNotification != null)
                {
                    RequestNotification(i18n.NotifySettingsSavedTitle,
                                        i18n.NotifySettingsSavedText,
                                        ToastType.Success,
                                        "✓");
                }
                ApplyMemberFilterAndSearch();
            };
            Grid.SetColumn(_btnSaveConfig, 2);
            btnGrid.Children.Add(_btnSaveConfig);

            Button btnCancel = new Button
            {
                Content = "✕ " + (i18n.Lang == AppLanguage.Zh ? "关闭" : "Close"),
                FontSize = 10.5,
                Foreground = theme.TextSecondary,
                Background = Brushes.Transparent,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(8, 3, 8, 3),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += delegate
            {
                ResetTokenPlainVisibility();
                _overlaySettings.Visibility = Visibility.Collapsed;
            };
            Grid.SetColumn(btnCancel, 4);
            btnGrid.Children.Add(btnCancel);

            sfSp.Children.Add(btnGrid);

            ScrollViewer svOverlay = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 440,
                Content = sfSp
            };
            AppTheme.ApplyModernScrollBarStyle(svOverlay);
            overlay.Child = svOverlay;

            return overlay;
        }

        public void CheckForAppUpdates(bool isInteractive)
        {
            if (_overlaySettings != null && _overlaySettings.Visibility != Visibility.Visible && isInteractive)
            {
                ToggleSettingsView();
            }

            if (_tbUpdateStatus != null)
            {
                _tbUpdateStatus.Visibility = Visibility.Visible;
                _tbUpdateStatus.Text = "⏳ " + I18n.Current.CheckingUpdate;
                _tbUpdateStatus.Foreground = AppTheme.Current.TextMuted;
            }
            if (_btnPullUpdate != null)
            {
                _btnPullUpdate.Visibility = Visibility.Collapsed;
            }

            UpdateChecker.CheckForUpdatesAsync(delegate(UpdateInfo info)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (_tbUpdateStatus == null) return;
                    ThemePalette theme = AppTheme.Current;
                    TranslationSet i18n = I18n.Current;

                    if (!info.Success)
                    {
                        _tbUpdateStatus.Text = string.Format("✕ {0}: {1}", i18n.UpdateFailed, info.ErrorMessage);
                        _tbUpdateStatus.Foreground = theme.AccentRed;
                        if (_btnPullUpdate != null) _btnPullUpdate.Visibility = Visibility.Collapsed;
                        return;
                    }

                    if (info.HasUpdate)
                    {
                        _latestDownloadUrl = info.DownloadUrl;
                        _latestReleasePageUrl = info.ReleasePageUrl;

                        string notes = !string.IsNullOrEmpty(info.ReleaseNotes) ? ("\n" + info.ReleaseNotes.Trim()) : "";
                        _tbUpdateStatus.Text = string.Format("🚀 {0}: {1}{2}", i18n.NewVersionFound, info.LatestVersion, notes);
                        _tbUpdateStatus.Foreground = theme.AccentEmerald;

                        if (_btnPullUpdate != null)
                        {
                            Border b = _btnPullUpdate.Content as Border;
                            TextBlock t = b != null ? b.Child as TextBlock : null;
                            if (t != null)
                            {
                                t.Text = "⬇ " + i18n.DownloadUpdate;
                            }
                            else
                            {
                                _btnPullUpdate.Content = "⬇ " + i18n.DownloadUpdate;
                            }
                            _btnPullUpdate.ToolTip = string.Format("{0} ({1})", i18n.DownloadUpdate, info.LatestVersion);
                            _btnPullUpdate.Visibility = Visibility.Visible;
                        }
                    }
                    else if (info.IsCommitBased)
                    {
                        string msg = !string.IsNullOrEmpty(info.ReleaseNotes) ? ("\n" + info.ReleaseNotes.Trim()) : "";
                        _tbUpdateStatus.Text = string.Format("{0} ({1})\nGitHub: [{2}]{3}",
                            i18n.AlreadyLatest, UpdateChecker.CurrentVersion, info.LatestCommitSha, msg);
                        _tbUpdateStatus.Foreground = theme.AccentEmerald;
                        if (_btnPullUpdate != null) _btnPullUpdate.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        _tbUpdateStatus.Text = string.Format("{0} ({1})", i18n.AlreadyLatest, UpdateChecker.CurrentVersion);
                        _tbUpdateStatus.Foreground = theme.AccentEmerald;
                        if (_btnPullUpdate != null) _btnPullUpdate.Visibility = Visibility.Collapsed;
                    }
                }));
            });
        }

        private void BuildModulesSection(StackPanel sfSp, ThemePalette theme, TranslationSet i18n)
        {
            Grid headerGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock lblModulesHeader = CreateMutedText(theme, "❖ " + i18n.SettingsModulesTitle);
            lblModulesHeader.FontWeight = FontWeights.Bold;
            Grid.SetColumn(lblModulesHeader, 0);
            headerGrid.Children.Add(lblModulesHeader);

            TextBlock lblHint = new TextBlock
            {
                Text = i18n.Lang == AppLanguage.Zh ? "拖动排序 · 单击/右击启闭" : "Drag to reorder · Click to toggle",
                FontSize = 9.5,
                Foreground = theme.TextDim,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblHint, 1);
            headerGrid.Children.Add(lblHint);

            sfSp.Children.Add(headerGrid);

            Border bModulesContainer = new Border
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
            List<string> order = AppSettings.ModuleOrder;
            double totalCanvasWidth = order.Count * SlotWidth;

            Canvas canvas = new Canvas
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

        private void RenderModuleChips(Canvas canvas, ThemePalette theme, TranslationSet i18n)
        {
            canvas.Children.Clear();
            List<string> order = AppSettings.ModuleOrder;
            List<string> enabled = AppSettings.ModuleEnabled;

            const double SlotWidth = 82.0;
            const double ChipWidth = 76.0;
            const double ChipHeight = 36.0;
            int totalSlots = order.Count;
            double slotPadding = (SlotWidth - ChipWidth) / 2.0;

            Dictionary<string, Border> chipMap = new Dictionary<string, Border>();

            for (int i = 0; i < order.Count; i++)
            {
                int slotIndex = i;
                string key = order[slotIndex];
                bool isAct = enabled.Contains(key);

                string icon = "⚡";
                string name = (i18n.Lang == AppLanguage.Zh) ? "供电" : "Power";
                string fullTitle = i18n.ModPowerTitle;
                Color col = theme.AccentAmber.Color;

                if (key == AppSettings.ModulePower)
                {
                    icon = "⚡";
                    name = (i18n.Lang == AppLanguage.Zh) ? "供电" : "Power";
                    fullTitle = i18n.ModPowerTitle;
                    col = theme.AccentAmber.Color;
                }
                else if (key == AppSettings.ModuleNetwork)
                {
                    icon = "🌐";
                    name = (i18n.Lang == AppLanguage.Zh) ? "网卡" : "Net";
                    fullTitle = i18n.ModNetTitle;
                    col = theme.AccentBlue.Color;
                }
                else if (key == AppSettings.ModuleZeroTier)
                {
                    icon = "🔗";
                    name = (i18n.Lang == AppLanguage.Zh) ? "互联" : "ZT";
                    fullTitle = i18n.ModZtTitle;
                    col = theme.AccentEmerald.Color;
                }
                else if (key == AppSettings.ModuleCompute)
                {
                    icon = "💻";
                    name = (i18n.Lang == AppLanguage.Zh) ? "算力" : "Load";
                    fullTitle = i18n.ModComputeTitle;
                    col = Color.FromRgb(168, 85, 247);
                }

                Border chip = new Border
                {
                    Width = ChipWidth,
                    Height = ChipHeight,
                    CornerRadius = new CornerRadius(7),
                    Cursor = Cursors.Hand,
                    Tag = key,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    ToolTip = fullTitle + (i18n.Lang == AppLanguage.Zh ? "\n左键拖动调序 · 单击/右键启闭" : "\nDrag to reorder · Click/Right-click to toggle")
                };

                ScaleTransform st = new ScaleTransform(1.0, 1.0);
                chip.RenderTransform = st;

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

                StackPanel sp = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                TextBlock tbIcon = new TextBlock
                {
                    Text = icon,
                    FontSize = 12,
                    Foreground = isAct ? new SolidColorBrush(col) : theme.TextDim,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                TextBlock tbText = new TextBlock
                {
                    Text = name,
                    FontSize = 10.5,
                    FontWeight = isAct ? FontWeights.SemiBold : FontWeights.Normal,
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

                // Physical drag & drop logic
                Point dragStartMouse = new Point();
                double chipStartLeft = 0;
                bool isDragging = false;

                chip.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    dragStartMouse = e.GetPosition(canvas);
                    chipStartLeft = Canvas.GetLeft(chip);
                    isDragging = false;

                    chip.BeginAnimation(Canvas.LeftProperty, null);
                    Canvas.SetLeft(chip, chipStartLeft);

                    chip.CaptureMouse();
                    e.Handled = true;
                };

                chip.PreviewMouseMove += delegate(object s, MouseEventArgs e)
                {
                    if (chip.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
                    {
                        Point curMouse = e.GetPosition(canvas);
                        double deltaX = curMouse.X - dragStartMouse.X;

                        if (!isDragging && Math.Abs(deltaX) > 4)
                        {
                            isDragging = true;

                            // PHYSICAL LIFT: Float on top, scale up 1.08x, glow shadow
                            Panel.SetZIndex(chip, 999);
                            DoubleAnimation saX = new DoubleAnimation(1.08, TimeSpan.FromMilliseconds(100));
                            DoubleAnimation saY = new DoubleAnimation(1.08, TimeSpan.FromMilliseconds(100));
                            st.BeginAnimation(ScaleTransform.ScaleXProperty, saX);
                            st.BeginAnimation(ScaleTransform.ScaleYProperty, saY);

                            chip.Effect = new DropShadowEffect
                            {
                                BlurRadius = 14,
                                ShadowDepth = 3,
                                Color = isAct ? col : Color.FromRgb(80, 80, 90),
                                Opacity = 0.55
                            };
                            Mouse.OverrideCursor = Cursors.Hand;
                        }

                        if (isDragging)
                        {
                            // Card physically glides 1:1 with cursor
                            double newLeft = chipStartLeft + deltaX;
                            double minLeft = slotPadding;
                            double maxLeft = (totalSlots - 1) * SlotWidth + slotPadding;
                            if (newLeft < minLeft - 12) newLeft = minLeft - 12;
                            if (newLeft > maxLeft + 12) newLeft = maxLeft + 12;

                            Canvas.SetLeft(chip, newLeft);

                            // Detect target slot by dragged card's center
                            double chipCenter = newLeft + ChipWidth / 2.0;
                            int targetSlot = (int)Math.Floor(chipCenter / SlotWidth);
                            if (targetSlot < 0) targetSlot = 0;
                            if (targetSlot >= totalSlots) targetSlot = totalSlots - 1;

                            int currentSlot = order.IndexOf(key);
                            if (targetSlot != currentSlot)
                            {
                                order.RemoveAt(currentSlot);
                                order.Insert(targetSlot, key);

                                // Sibling cards smoothly glide out of the way!
                                for (int j = 0; j < order.Count; j++)
                                {
                                    string otherKey = order[j];
                                    if (otherKey != key && chipMap.ContainsKey(otherKey))
                                    {
                                        Border otherChip = chipMap[otherKey];
                                        double otherTargetLeft = j * SlotWidth + slotPadding;
                                        DoubleAnimation anim = new DoubleAnimation(otherTargetLeft, TimeSpan.FromMilliseconds(160))
                                        {
                                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                                        };
                                        otherChip.BeginAnimation(Canvas.LeftProperty, anim);
                                    }
                                }
                            }
                        }
                    }
                };

                chip.PreviewMouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    if (chip.IsMouseCaptured)
                    {
                        chip.ReleaseMouseCapture();
                        Mouse.OverrideCursor = null;

                        if (isDragging)
                        {
                            // Settle back to ground: scale 1.0, clear shadow
                            DoubleAnimation saX = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120));
                            DoubleAnimation saY = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120));
                            st.BeginAnimation(ScaleTransform.ScaleXProperty, saX);
                            st.BeginAnimation(ScaleTransform.ScaleYProperty, saY);
                            chip.Effect = null;

                            // Smoothly slide into destination slot
                            int finalSlot = order.IndexOf(key);
                            double finalLeft = finalSlot * SlotWidth + slotPadding;
                            DoubleAnimation dropAnim = new DoubleAnimation(finalLeft, TimeSpan.FromMilliseconds(140))
                            {
                                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                            };
                            dropAnim.Completed += delegate
                            {
                                Panel.SetZIndex(chip, 0);
                                AppSettings.SetWidgetModules(order, enabled);
                            };
                            chip.BeginAnimation(Canvas.LeftProperty, dropAnim);
                        }
                        else
                        {
                            // Clicked without dragging: Toggle!
                            ToggleModule(key, order, enabled, canvas, theme, i18n);
                        }
                        isDragging = false;
                    }
                };

                chip.PreviewMouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    ToggleModule(key, order, enabled, canvas, theme, i18n);
                    e.Handled = true;
                };

                canvas.Children.Add(chip);
            }
        }

        private void ToggleModule(string key, List<string> order, List<string> enabled, Canvas canvas, ThemePalette theme, TranslationSet i18n)
        {
            if (enabled.Contains(key))
            {
                if (enabled.Count <= 1)
                {
                    return;
                }
                enabled.Remove(key);
            }
            else
            {
                enabled.Add(key);
            }

            AppSettings.SetWidgetModules(order, enabled);
            RenderModuleChips(canvas, theme, i18n);
        }

        private Border CreateCardBorder(ThemePalette theme)
        {
            return new Border
            {
                Background = theme.CardBg,
                CornerRadius = new CornerRadius(10),
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(0.8)
            };
        }

        private TextBlock CreateCardHeader(ThemePalette theme, string title)
        {
            return new TextBlock
            {
                Text = title,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = theme.TextPrimary,
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private TextBlock CreateMutedText(ThemePalette theme, string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                Foreground = theme.TextMuted,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private TextBlock CreateValueText(ThemePalette theme, string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = new FontFamily("Consolas, Segoe UI")
            };
        }

        private TextBox CreateInputTextBox(ThemePalette theme, string defaultVal)
        {
            return new TextBox
            {
                Text = defaultVal,
                Background = theme.InputBg,
                Foreground = theme.TextPrimary,
                BorderBrush = theme.InputBorder,
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 11,
                FontFamily = new FontFamily("Consolas, Segoe UI")
            };
        }

        private Border CreateBadge(string text, Color c, out TextBlock tb)
        {
            Border b = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(35, c.R, c.G, c.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, c.R, c.G, c.B)),
                BorderThickness = new Thickness(0.8),
                VerticalAlignment = VerticalAlignment.Center
            };
            tb = new TextBlock
            {
                Text = text,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(c)
            };
            b.Child = tb;
            return b;
        }

        private Grid CreateProgressBar(ThemePalette theme, out Rectangle fillBar, Color fillCol)
        {
            Grid g = new Grid { Height = 4, Margin = new Thickness(0, 4, 0, 2) };
            Rectangle bg = new Rectangle
            {
                Fill = theme.ProgressBarTrack,
                RadiusX = 2,
                RadiusY = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            g.Children.Add(bg);

            fillBar = new Rectangle
            {
                Fill = new SolidColorBrush(fillCol),
                RadiusX = 2,
                RadiusY = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 20
            };
            g.Children.Add(fillBar);
            return g;
        }

        private Grid CreateMetricsGrid(ThemePalette theme,
                                       string lbl1, TextBlock val1, string lbl2, TextBlock val2,
                                       string lbl3, TextBlock val3, string lbl4, TextBlock val4)
        {
            Grid g = new Grid { Margin = new Thickness(0, 3, 0, 2) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Row 0 - Col 0 & 1 (Left metric)
            TextBlock t1 = CreateMutedText(theme, lbl1);
            t1.Margin = new Thickness(0, 0, 4, 0);
            val1.HorizontalAlignment = HorizontalAlignment.Left;
            val1.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(t1, 0); Grid.SetColumn(t1, 0);
            Grid.SetRow(val1, 0); Grid.SetColumn(val1, 1);
            g.Children.Add(t1);
            g.Children.Add(val1);

            // Row 0 - Col 2 & 3 (Right metric)
            TextBlock t2 = CreateMutedText(theme, lbl2);
            t2.Margin = new Thickness(10, 0, 4, 0);
            val2.HorizontalAlignment = HorizontalAlignment.Left;
            val2.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(t2, 0); Grid.SetColumn(t2, 2);
            Grid.SetRow(val2, 0); Grid.SetColumn(val2, 3);
            g.Children.Add(t2);
            g.Children.Add(val2);

            // Row 2 - Col 0 & 1 (Left metric)
            TextBlock t3 = CreateMutedText(theme, lbl3);
            t3.Margin = new Thickness(0, 0, 4, 0);
            val3.HorizontalAlignment = HorizontalAlignment.Left;
            val3.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(t3, 2); Grid.SetColumn(t3, 0);
            Grid.SetRow(val3, 2); Grid.SetColumn(val3, 1);
            g.Children.Add(t3);
            g.Children.Add(val3);

            // Row 2 - Col 2 & 3 (Right metric)
            TextBlock t4 = CreateMutedText(theme, lbl4);
            t4.Margin = new Thickness(10, 0, 4, 0);
            val4.HorizontalAlignment = HorizontalAlignment.Left;
            val4.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(t4, 2); Grid.SetColumn(t4, 2);
            Grid.SetRow(val4, 2); Grid.SetColumn(val4, 3);
            g.Children.Add(t4);
            g.Children.Add(val4);

            return g;
        }

        // Single-row metrics: (label1, val1) left | (label2, val2) right
        private Grid CreateMetricsRow(ThemePalette theme,
                                      string lbl1, TextBlock val1,
                                      string lbl2, TextBlock val2)
        {
            Grid g = new Grid { Margin = new Thickness(0, 3, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock t1 = CreateMutedText(theme, lbl1);
            t1.Margin = new Thickness(0, 0, 4, 0);
            val1.HorizontalAlignment = HorizontalAlignment.Left;
            val1.VerticalAlignment = VerticalAlignment.Center;
            val1.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(t1, 0); Grid.SetColumn(val1, 1);
            g.Children.Add(t1); g.Children.Add(val1);

            TextBlock t2 = CreateMutedText(theme, lbl2);
            t2.Margin = new Thickness(10, 0, 4, 0);
            val2.HorizontalAlignment = HorizontalAlignment.Left;
            val2.VerticalAlignment = VerticalAlignment.Center;
            val2.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(t2, 2); Grid.SetColumn(val2, 3);
            g.Children.Add(t2); g.Children.Add(val2);

            return g;
        }

        public void PositionNear(double left, double top, double width, double height)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            double sx = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            double sy = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

            int physCenterX = (int)((left + width / 2.0) * sx);
            int physCenterY = (int)((top + height / 2.0) * sy);
            System.Windows.Forms.Screen curScreen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(physCenterX, physCenterY));

            // Convert working area to DIPs
            double waLeft = curScreen.WorkingArea.Left / sx;
            double waTop = curScreen.WorkingArea.Top / sy;
            double waRight = curScreen.WorkingArea.Right / sx;
            double waBottom = curScreen.WorkingArea.Bottom / sy;

            _currentWaTop = waTop;
            _currentWaBottom = waBottom;
            double maxAllowedH = Math.Max(380, waBottom - waTop - 20);
            MaxHeight = maxAllowedH;

            double detailW = Width > 0 ? Width : 385;
            double detailH = 460;
            FrameworkElement fe = Content as FrameworkElement;
            if (fe != null)
            {
                fe.Measure(new Size(detailW, double.PositiveInfinity));
                if (fe.DesiredSize.Height > 0)
                {
                    detailH = Math.Min(fe.DesiredSize.Height, maxAllowedH);
                }
            }

            double targetLeft;
            if (left + width + detailW + 10 <= waRight)
            {
                targetLeft = left + width + 8;
            }
            else
            {
                targetLeft = left - detailW - 8;
            }

            if (targetLeft < waLeft + 8) targetLeft = waLeft + 8;
            if (targetLeft + detailW > waRight - 8) targetLeft = waRight - detailW - 8;

            double targetTop;
            if ((top + height / 2.0) > (waTop + (waBottom - waTop) / 2.0))
            {
                targetTop = (top + height) - detailH;
            }
            else
            {
                targetTop = top;
            }

            if (targetTop + detailH > waBottom - 8) targetTop = waBottom - detailH - 8;
            if (targetTop < waTop + 8) targetTop = waTop + 8;

            Left = targetLeft;
            Top = targetTop;
        }

        public void EnsureWindowWithinScreen()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
            {
                if (_currentWaBottom <= _currentWaTop) return;

                double curH = ActualHeight;
                FrameworkElement fe = Content as FrameworkElement;
                if (curH <= 0 && fe != null)
                {
                    fe.Measure(new Size(Width, double.PositiveInfinity));
                    curH = fe.DesiredSize.Height;
                }
                if (curH <= 0) curH = 460;

                double maxAllowedH = Math.Max(380, _currentWaBottom - _currentWaTop - 20);
                if (MaxHeight != maxAllowedH)
                {
                    MaxHeight = maxAllowedH;
                }
                if (curH > maxAllowedH) curH = maxAllowedH;

                if (Top + curH > _currentWaBottom - 8)
                {
                    double newTop = _currentWaBottom - curH - 8;
                    if (newTop < _currentWaTop + 8) newTop = _currentWaTop + 8;
                    Top = newTop;
                }
            }));
        }

        public void UpdateSystemLoad(SystemLoadData data)
        {
            _lastLoad = data;
            Dispatcher.Invoke(new Action(delegate
            {
                if (_tbCpuVal == null) return;
                _tbCpuVal.Text = string.Format("{0:0.0}%", data.CpuPercent);
                double cpuBarW = (data.CpuPercent / 100.0) * 135;
                if (cpuBarW < 2) cpuBarW = 2;
                if (cpuBarW > 135) cpuBarW = 135;
                _rectCpuBar.Width = cpuBarW;

                _tbRamPercent.Text = string.Format("{0} {1}%", I18n.Current.Memory, data.RamPercent);
                _tbRamVal.Text = string.Format("{0:0.0}/{1:0.0}G", data.RamUsedGb, data.RamTotalGb);
                double ramBarW = (data.RamPercent / 100.0) * 135;
                if (ramBarW < 2) ramBarW = 2;
                if (ramBarW > 135) ramBarW = 135;
                _rectRamBar.Width = ramBarW;
            }));
        }

        public void UpdatePower(PowerData data)
        {
            _lastPower = data;
            Dispatcher.Invoke(new Action(delegate
            {
                if (_tbPowerPcWatts == null) return;
                ThemePalette theme = AppTheme.Current;
                TranslationSet i18n = I18n.Current;

                // 1. 电脑功耗 — CPU/system power from RAPL or battery discharge
                if (data.CpuWatts > 0.5)
                {
                    _tbPowerPcWatts.Text = string.Format("{0:0.0} W", data.CpuWatts);
                }
                else if (!data.IsAcOnline && data.Watts < 0)
                {
                    // no RAPL on battery: approximate total system power from battery discharge rate
                    _tbPowerPcWatts.Text = string.Format("{0:0.0} W", Math.Abs(data.Watts));
                }
                else
                {
                    _tbPowerPcWatts.Text = "--";
                }

                // 2. 电池功率 — charging / discharging / protected / none
                if (!data.HasBattery)
                {
                    _tbPowerBatWatts.Text = i18n.PowerNoBattery;
                }
                else if (data.IsCharging && data.Watts > 0)
                {
                    _tbPowerBatWatts.Text = string.Format("+{0:0.0} W", data.Watts);
                    _tbPowerBatWatts.Foreground = theme.AccentEmerald;
                }
                else if (!data.IsAcOnline && data.Watts < 0)
                {
                    _tbPowerBatWatts.Text = string.Format("-{0:0.0} W", Math.Abs(data.Watts));
                    _tbPowerBatWatts.Foreground = theme.AccentAmber;
                }
                else
                {
                    // AC connected but not charging (conserve / protect mode)
                    _tbPowerBatWatts.Text = i18n.PowerNoBatteryDrain;
                    _tbPowerBatWatts.Foreground = theme.TextMuted;
                }

                // 3. 电池电量
                if (!data.HasBattery)
                {
                    _tbPowerBatLevel.Text = "--";
                }
                else if (data.MaxCapacity > 0)
                {
                    _tbPowerBatLevel.Text = string.Format("{0:0.0}Wh · {1}%", data.RemainingCapacity / 1000.0, data.BatteryPercent);
                }
                else
                {
                    _tbPowerBatLevel.Text = string.Format("{0}%", data.BatteryPercent);
                }

                // 4. 预估续航
                _tbPowerBatEta.Text = data.GetEstimatedTimeText(i18n);

                // 5. 顶部徽章 = 供电方式
                if (!data.HasBattery)
                {
                    _tbPowerBadge.Text = i18n.PowerDesktop;
                    _tbPowerBadge.Foreground = theme.AccentEmerald;
                }
                else
                {
                    _tbPowerBadge.Text = data.GetStatusText(i18n);
                    _tbPowerBadge.Foreground = data.GetStateBrush(theme);
                }
            }));
        }

        public void UpdateNetwork(NetworkData data)
        {
            _lastNet = data;
            Dispatcher.Invoke(new Action(delegate
            {
                if (_tbNetIface == null) return;
                _tbNetIface.Text = string.IsNullOrEmpty(data.ActiveInterface) ? (I18n.Current.Lang == AppLanguage.Zh ? "检测中" : "Detecting...") : data.ActiveInterface;
                _tbNetLinkSpeed.Text = data.LinkSpeedStr;
                _tbNetLocalIp.Text = data.LocalIp;
                _tbNetSessionTraffic.Text = string.Format("↓ {0:0.0}M  ↑ {1:0.0}M", data.SessionRecvMb, data.SessionSentMb);

                _tbNetPublicIp.Text = string.IsNullOrEmpty(data.PublicIp) ? I18n.Current.NetFetching : data.PublicIp;

                if (!string.IsNullOrEmpty(data.CountryCode))
                {
                    BitmapSource flag = FlagAssets.GetFlagImage(data.CountryCode);
                    if (flag != null)
                    {
                        _imgDetailFlag.Source = flag;
                        _imgDetailFlag.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        _imgDetailFlag.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    _imgDetailFlag.Visibility = Visibility.Collapsed;
                }

                string geo = "";
                if (!string.IsNullOrEmpty(data.Country)) geo += data.Country;
                if (!string.IsNullOrEmpty(data.City) && !string.Equals(data.Country, data.City, StringComparison.OrdinalIgnoreCase))
                {
                    geo += (geo.Length > 0 ? " · " : "") + data.City;
                }
                if (!string.IsNullOrEmpty(data.Isp)) geo += (geo.Length > 0 ? " · " : "") + data.Isp;
                _tbNetGeoIsp.Text = string.IsNullOrEmpty(geo) ? I18n.Current.NetFetching : geo;
            }));
        }

        public void UpdateZeroTier(ZeroTierData data)
        {
            _lastZt = data;
            Dispatcher.Invoke(new Action(delegate
            {
                if (_cardZt != null)
                {
                    bool isZtAvailable = data.IsInstalled || data.IsRunning;
                    _cardZt.Visibility = isZtAvailable ? Visibility.Visible : Visibility.Collapsed;
                }
                if (_tbZtBadge == null) return;
                ThemePalette theme = AppTheme.Current;

                if (!data.IsInstalled)
                {
                    _tbZtBadge.Text = I18n.Current.ZtNotInstalled;
                    _tbZtBadge.Foreground = theme.TextMuted;
                    _tbZtNode.Text = "--";
                    _spMoons.Children.Clear();
                    return;
                }

                if (data.HasDroppedMoons)
                {
                    _tbZtBadge.Text = string.Format("Alert: {0} Moon Dropped", data.DroppedMoonCount);
                    _tbZtBadge.Foreground = theme.AccentRed;
                    _bZtBadge.Background = new SolidColorBrush(Color.FromArgb(45, theme.AccentRed.Color.R, theme.AccentRed.Color.G, theme.AccentRed.Color.B));
                    _bZtBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(120, theme.AccentRed.Color.R, theme.AccentRed.Color.G, theme.AccentRed.Color.B));
                    _tbZtNode.Text = string.Format("{0}", data.LocalNodeId);
                }
                else if (data.IsRunning)
                {
                    _tbZtBadge.Text = I18n.Current.ZtOnline;
                    _tbZtBadge.Foreground = theme.AccentEmerald;
                    _bZtBadge.Background = new SolidColorBrush(Color.FromArgb(35, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B));
                    _bZtBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(90, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B));
                    _tbZtNode.Text = string.Format("{0}", data.LocalNodeId);
                }
                else
                {
                    _tbZtBadge.Text = I18n.Current.ZtOffline;
                    _tbZtBadge.Foreground = theme.AccentRed;
                    _bZtBadge.Background = new SolidColorBrush(Color.FromArgb(35, theme.AccentRed.Color.R, theme.AccentRed.Color.G, theme.AccentRed.Color.B));
                    _bZtBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(90, theme.AccentRed.Color.R, theme.AccentRed.Color.G, theme.AccentRed.Color.B));
                    _tbZtNode.Text = I18n.Current.ZtOffline;
                }

                _spMoons.Children.Clear();

                if (data.Moons.Count == 0)
                {
                    TextBlock empty = CreateMutedText(theme, I18n.Current.MoonNoneJoined);
                    _spMoons.Children.Add(empty);
                }
                else
                {
                    foreach (MoonNode m in data.Moons)
                    {
                        Border mCard = new Border
                        {
                            Background = theme.InnerTileBg,
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(8, 6, 8, 6),
                            Margin = new Thickness(0, 3, 0, 3),
                            BorderBrush = theme.BorderMuted,
                            BorderThickness = new Thickness(0.8)
                        };

                        Grid mg = new Grid();
                        mg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        mg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        StackPanel leftSp = new StackPanel();
                        TextBlock addr = new TextBlock
                        {
                            Text = "Moon: " + m.Address,
                            Foreground = theme.TextPrimary,
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            FontFamily = new FontFamily("Consolas, Segoe UI")
                        };
                        string physText;
                        if (m.IsOffline)
                        {
                            physText = I18n.Current.Lang == AppLanguage.Zh ? "节点离线" : "Offline";
                        }
                        else if (m.IsDirect && !string.IsNullOrEmpty(m.PhysicalAddress))
                        {
                            physText = m.PhysicalAddress;
                        }
                        else
                        {
                            physText = I18n.Current.Lang == AppLanguage.Zh ? "中继链路" : "Relay";
                        }

                        TextBlock phys = new TextBlock
                        {
                            Text = physText,
                            Foreground = theme.TextSecondary,
                            FontSize = 10,
                            Margin = new Thickness(0, 2, 0, 0),
                            FontFamily = new FontFamily("Consolas, Segoe UI")
                        };
                        leftSp.Children.Add(addr);
                        leftSp.Children.Add(phys);
                        Grid.SetColumn(leftSp, 0);
                        mg.Children.Add(leftSp);

                        Border badge = new Border
                        {
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        TextBlock badgeText = new TextBlock
                        {
                            FontSize = 10,
                            FontWeight = FontWeights.Bold
                        };

                        if (m.IsOffline)
                        {
                            mCard.Background = new SolidColorBrush(Color.FromArgb(35, theme.AccentRed.Color.R, theme.AccentRed.Color.G, theme.AccentRed.Color.B));
                            mCard.BorderBrush = new SolidColorBrush(Color.FromArgb(140, theme.AccentRed.Color.R, theme.AccentRed.Color.G, theme.AccentRed.Color.B));
                            mCard.BorderThickness = new Thickness(1);

                            badge.Background = new SolidColorBrush(Color.FromArgb(50, theme.AccentRed.Color.R, theme.AccentRed.Color.G, theme.AccentRed.Color.B));
                            badge.BorderBrush = theme.AccentRed;
                            badge.BorderThickness = new Thickness(1);
                            badgeText.Text = I18n.Current.Dropped;
                            badgeText.Foreground = theme.AccentRed;

                            phys.Text = I18n.Current.Dropped;
                            phys.Foreground = theme.AccentRed;
                        }
                        else if (m.IsDirect)
                        {
                            badge.Background = new SolidColorBrush(Color.FromArgb(35, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B));
                            badge.BorderBrush = new SolidColorBrush(Color.FromArgb(90, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B));
                            badge.BorderThickness = new Thickness(0.8);
                            badgeText.Text = string.Format("{0} {1}ms", I18n.Current.Direct, m.Latency);
                            badgeText.Foreground = theme.AccentEmerald;
                        }
                        else
                        {
                            badge.Background = new SolidColorBrush(Color.FromArgb(35, theme.AccentAmber.Color.R, theme.AccentAmber.Color.G, theme.AccentAmber.Color.B));
                            badge.BorderBrush = new SolidColorBrush(Color.FromArgb(90, theme.AccentAmber.Color.R, theme.AccentAmber.Color.G, theme.AccentAmber.Color.B));
                            badge.BorderThickness = new Thickness(0.8);
                            badgeText.Text = I18n.Current.Relay;
                            badgeText.Foreground = theme.AccentAmber;
                        }
                        badge.Child = badgeText;

                        Grid.SetColumn(badge, 1);
                        mg.Children.Add(badge);

                        mCard.Child = mg;
                        _spMoons.Children.Add(mCard);
                    }
                }
            }));
        }

        private void OnMemberSyncError(MemberSyncState state, string message)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                string title = (I18n.Current.Lang == AppLanguage.Zh) ? "ZeroTier 同步异常" : "ZeroTier Sync Error";
                ShowToast("⚠️ " + message, true);
                if (RequestNotification != null)
                {
                    RequestNotification(title, message, ToastType.Error, "⚠️");
                }
            }));
        }

        private void ResetTokenPlainVisibility()
        {
            if (_isTokenPlainVisible)
            {
                _isTokenPlainVisible = false;
                if (_pbSettingToken != null && _txtSettingTokenPlain != null)
                {
                    _pbSettingToken.Password = _txtSettingTokenPlain.Text;
                    _txtSettingTokenPlain.Visibility = Visibility.Collapsed;
                    _pbSettingToken.Visibility = Visibility.Visible;
                }
                if (_btnToggleTokenEye != null) _btnToggleTokenEye.Content = "👁";
            }
        }

        private void OnMemberStatusChanged(string status)
        {
            _lastStatusText = status;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (_tbMemberStatus != null)
                {
                    ThemePalette theme = AppTheme.Current;
                    _tbMemberStatus.Text = status;
                    TranslationSet i18n = I18n.Current;
                    bool isError = status.Contains("401") ||
                                   status.Contains("403") ||
                                   status.Contains("失败") ||
                                   status.Contains("Failed") ||
                                   status.Contains("⚠️");

                    if (isError)
                    {
                        _tbMemberStatus.Foreground = theme.AccentRed;
                    }
                    else if (status == i18n.Syncing)
                    {
                        _tbMemberStatus.Foreground = theme.AccentAmber;
                    }
                    else
                    {
                        _tbMemberStatus.Foreground = theme.TextSecondary;
                    }

                    _tbMemberStatus.Visibility = string.IsNullOrEmpty(status) ? Visibility.Collapsed : Visibility.Visible;
                }
            }));
        }

        private void OnMembersUpdated(List<MemberNode> list)
        {
            _lastMemberList = list;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                ApplyMemberFilterAndSearch();
            }));
        }

        public void FocusSearch()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate
            {
                ExpandSearchBox();
            }));
        }

        private void ToggleSearchBox()
        {
            if (_searchBoxBorder == null) return;
            if (_searchBoxBorder.Visibility == Visibility.Visible)
            {
                CollapseSearchBox();
            }
            else
            {
                ExpandSearchBox();
            }
        }

        private void ExpandSearchBox()
        {
            if (_searchBoxBorder == null) return;
            ThemePalette theme = AppTheme.Current;
            _searchBoxBorder.Visibility = Visibility.Visible;
            _searchBoxBorder.Opacity = 1;
            if (_btnToggleSearch != null)
            {
                _btnToggleSearch.Foreground = theme.AccentBlue;
            }
            if (_tbMemberSearch != null)
            {
                _tbMemberSearch.Focus();
                _tbMemberSearch.SelectAll();
            }
        }

        private void CollapseSearchBox()
        {
            if (_searchBoxBorder == null) return;
            ThemePalette theme = AppTheme.Current;
            _searchBoxBorder.Visibility = Visibility.Collapsed;
            if (_btnToggleSearch != null)
            {
                _btnToggleSearch.Foreground = theme.TextSecondary;
            }
            if (_tbMemberSearch != null && !string.IsNullOrEmpty(_tbMemberSearch.Text))
            {
                _tbMemberSearch.Text = "";
            }
        }

        private Button CreateChipPill(string initialText, RoutedEventHandler onClick, out Border pillBorder, out TextBlock tbRef)
        {
            ThemePalette theme = AppTheme.Current;
            Button btn = new Button
            {
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FocusVisualStyle = null
            };

            Border pill = new Border
            {
                Background = theme.InnerTileBg,
                BorderBrush = theme.BorderMuted,
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                SnapsToDevicePixels = true
            };

            TextBlock tb = new TextBlock
            {
                Text = initialText,
                FontSize = 9.5,
                FontWeight = FontWeights.Normal,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            pill.Child = tb;
            pillBorder = pill;
            tbRef = tb;

            string xaml = @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                            TargetType='Button'>
                <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </ControlTemplate>";
            btn.Template = (ControlTemplate)XamlReader.Parse(xaml);
            btn.Content = pill;

            btn.MouseEnter += delegate
            {
                if ((string)pill.Tag != "active")
                {
                    pill.Background = theme.CardHoverBg;
                }
            };
            btn.MouseLeave += delegate
            {
                if ((string)pill.Tag != "active")
                {
                    pill.Background = theme.InnerTileBg;
                }
            };

            if (onClick != null)
            {
                btn.Click += onClick;
            }

            return btn;
        }

        private Button CreateSmallActionButton(string content, SolidColorBrush fg, Brush bg, Brush border, RoutedEventHandler onClick)
        {
            Border pill = new Border
            {
                Background = bg,
                BorderBrush = border,
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2.5, 8, 2.5),
                SnapsToDevicePixels = true
            };

            TextBlock tb = new TextBlock
            {
                Text = content,
                FontSize = 10,
                Foreground = fg,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            pill.Child = tb;

            Button btn = new Button
            {
                Content = pill,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                FocusVisualStyle = null
            };

            string xaml = @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></ControlTemplate>";
            btn.Template = (ControlTemplate)XamlReader.Parse(xaml);

            btn.MouseEnter += delegate { pill.Opacity = 0.8; };
            btn.MouseLeave += delegate { pill.Opacity = 1.0; };

            if (onClick != null) btn.Click += onClick;
            return btn;
        }

        private void UpdateChipVisuals()
        {
            ThemePalette theme = AppTheme.Current;

            if (_chipAll != null && _tbChipAll != null)
            {
                if (_currentMemberFilter == MemberFilterType.All)
                {
                    _chipAll.Tag = "active";
                    _chipAll.Background = new SolidColorBrush(Color.FromArgb(45, theme.AccentBlue.Color.R, theme.AccentBlue.Color.G, theme.AccentBlue.Color.B));
                    _chipAll.BorderBrush = theme.AccentBlue;
                    _tbChipAll.Foreground = theme.AccentBlue;
                    _tbChipAll.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    _chipAll.Tag = "normal";
                    _chipAll.Background = theme.InnerTileBg;
                    _chipAll.BorderBrush = theme.BorderMuted;
                    _tbChipAll.Foreground = theme.TextSecondary;
                    _tbChipAll.FontWeight = FontWeights.Normal;
                }
            }

            if (_chipOnline != null && _tbChipOnline != null)
            {
                if (_currentMemberFilter == MemberFilterType.Online)
                {
                    _chipOnline.Tag = "active";
                    _chipOnline.Background = new SolidColorBrush(Color.FromArgb(45, theme.AccentEmerald.Color.R, theme.AccentEmerald.Color.G, theme.AccentEmerald.Color.B));
                    _chipOnline.BorderBrush = theme.AccentEmerald;
                    _tbChipOnline.Foreground = theme.AccentEmerald;
                    _tbChipOnline.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    _chipOnline.Tag = "normal";
                    _chipOnline.Background = theme.InnerTileBg;
                    _chipOnline.BorderBrush = theme.BorderMuted;
                    _tbChipOnline.Foreground = theme.TextSecondary;
                    _tbChipOnline.FontWeight = FontWeights.Normal;
                }
            }

            if (_chipOffline != null && _tbChipOffline != null)
            {
                if (_currentMemberFilter == MemberFilterType.Offline)
                {
                    _chipOffline.Tag = "active";
                    _chipOffline.Background = new SolidColorBrush(Color.FromArgb(45, theme.TextSecondary.Color.R, theme.TextSecondary.Color.G, theme.TextSecondary.Color.B));
                    _chipOffline.BorderBrush = theme.TextSecondary;
                    _tbChipOffline.Foreground = theme.TextPrimary;
                    _tbChipOffline.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    _chipOffline.Tag = "normal";
                    _chipOffline.Background = theme.InnerTileBg;
                    _chipOffline.BorderBrush = theme.BorderMuted;
                    _tbChipOffline.Foreground = theme.TextSecondary;
                    _tbChipOffline.FontWeight = FontWeights.Normal;
                }
            }
        }

        private void UpdateChipCounts(int total, int online, int offline)
        {
            TranslationSet i18n = I18n.Current;
            if (_tbChipAll != null)
                _tbChipAll.Text = string.Format("{0} {1}", i18n.FilterAll, total);
            if (_tbChipOnline != null)
                _tbChipOnline.Text = string.Format("🟢 {0}", online);
            if (_tbChipOffline != null)
                _tbChipOffline.Text = string.Format("⚪ {0}", offline);
        }

        private void ApplyMemberFilterAndSearch()
        {
            if (_memberDir == null) return;

            List<MemberNode> all = _lastMemberList != null ? new List<MemberNode>(_lastMemberList) : _memberDir.GetAllMembers();
            if (all == null) all = new List<MemberNode>();

            int total = all.Count;
            int online = 0;
            int offline = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsOnline) online++;
                else offline++;
            }

            UpdateChipCounts(total, online, offline);

            string query = (_tbMemberSearch != null) ? _tbMemberSearch.Text : "";
            List<MemberNode> searchMatched;
            if (string.IsNullOrEmpty(query))
            {
                searchMatched = all;
                searchMatched.Sort(delegate(MemberNode a, MemberNode b)
                {
                    if (a.IsOnline != b.IsOnline) return a.IsOnline ? -1 : 1;
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
            }
            else
            {
                searchMatched = _memberDir.Search(query);
            }

            List<MemberNode> finalFiltered = new List<MemberNode>();
            for (int i = 0; i < searchMatched.Count; i++)
            {
                MemberNode m = searchMatched[i];
                if (_currentMemberFilter == MemberFilterType.Online && !m.IsOnline) continue;
                if (_currentMemberFilter == MemberFilterType.Offline && m.IsOnline) continue;
                finalFiltered.Add(m);
            }

            UpdateMemberResults(finalFiltered);
        }

        private void ToggleSettingsView()
        {
            if (_overlaySettings == null) return;
            ResetTokenPlainVisibility();
            if (_overlaySettings.Visibility == Visibility.Visible)
            {
                _overlaySettings.Visibility = Visibility.Collapsed;
            }
            else
            {
                MemberDirectoryConfig cfg = _memberDir.CurrentConfig;
                _txtSettingUrl.Text = cfg.ControllerUrl;
                _txtSettingNwid.Text = cfg.NetworkId;
                _pbSettingToken.Password = cfg.ApiToken;
                _txtSettingTokenPlain.Text = cfg.ApiToken;
                _lblTestStatus.Text = "";

                _overlaySettings.Visibility = Visibility.Visible;
            }
        }

        private void ShowToast(string message, bool isError = false)
        {
            if (_tbMemberToast != null && _bMemberToast != null)
            {
                ThemePalette theme = AppTheme.Current;
                SolidColorBrush accent = isError ? theme.AccentRed : theme.AccentEmerald;
                _bMemberToast.Background = new SolidColorBrush(Color.FromArgb(40, accent.Color.R, accent.Color.G, accent.Color.B));
                _bMemberToast.BorderBrush = new SolidColorBrush(Color.FromArgb(120, accent.Color.R, accent.Color.G, accent.Color.B));
                _tbMemberToast.Foreground = accent;
                _tbMemberToast.Text = message;
                _bMemberToast.Visibility = Visibility.Visible;
                _toastTimer.Stop();
                _toastTimer.Start();
            }
        }

        private void UpdateMemberResults(List<MemberNode> list)
        {
            if (_spMemberResults == null) return;
            _spMemberResults.Children.Clear();

            ThemePalette theme = AppTheme.Current;
            TranslationSet i18n = I18n.Current;

            if (list == null || list.Count == 0)
            {
                string emptyMsg;
                if (!string.IsNullOrEmpty(_tbMemberSearch != null ? _tbMemberSearch.Text : ""))
                {
                    emptyMsg = i18n.Lang == AppLanguage.Zh ? "未找到匹配的成员" : "No matching members found";
                }
                else if (_currentMemberFilter == MemberFilterType.Online)
                {
                    emptyMsg = i18n.NoOnlineMembers;
                }
                else if (_currentMemberFilter == MemberFilterType.Offline)
                {
                    emptyMsg = i18n.NoOfflineMembers;
                }
                else if (_memberDir != null && _memberDir.HasToken)
                {
                    emptyMsg = "⏳ " + i18n.Syncing;
                }
                else
                {
                    emptyMsg = i18n.NoTokenHint;
                }

                TextBlock emptyTb = new TextBlock
                {
                    Text = emptyMsg,
                    FontSize = 10.5,
                    Foreground = theme.TextDim,
                    Margin = new Thickness(4, 8, 4, 8),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                _spMemberResults.Children.Add(emptyTb);
                return;
            }

            int count = Math.Min(list.Count, 60);
            for (int i = 0; i < count; i++)
            {
                MemberNode m = list[i];
                Border itemBorder = new Border
                {
                    Background = theme.InnerTileBg,
                    BorderBrush = theme.BorderMuted,
                    BorderThickness = new Thickness(0.8),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand
                };

                Grid itemGrid = new Grid();
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Dot
                Ellipse dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = m.IsOnline ? theme.AccentEmerald : theme.TextDim,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetColumn(dot, 0);
                itemGrid.Children.Add(dot);

                // Name & Subtext (CRISP HIGH CONTRAST)
                StackPanel spInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                TextBlock tbName = new TextBlock
                {
                    Text = string.IsNullOrEmpty(m.Name) ? (i18n.Lang == AppLanguage.Zh ? "未命名设备" : "Unnamed Device") : m.Name,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = theme.TextPrimary,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                string statusHint = m.Latency >= 0 ? string.Format(" · {0}ms", m.Latency) : (m.IsOnline ? " · " + i18n.Online : " · " + i18n.Offline);
                TextBlock tbSub = new TextBlock
                {
                    Text = m.Id + statusHint,
                    FontSize = 9.5,
                    Foreground = theme.TextSecondary,
                    FontFamily = new FontFamily("Consolas, Segoe UI")
                };
                spInfo.Children.Add(tbName);
                spInfo.Children.Add(tbSub);
                Grid.SetColumn(spInfo, 1);
                itemGrid.Children.Add(spInfo);

                // IP Chip
                StackPanel spRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Border ipChip = new Border
                {
                    Background = theme.CardBg,
                    BorderBrush = theme.BorderBrush,
                    BorderThickness = new Thickness(0.6),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(5, 2, 5, 2),
                    Margin = new Thickness(4, 0, 0, 0)
                };
                TextBlock tbIp = new TextBlock
                {
                    Text = string.IsNullOrEmpty(m.Ip) ? i18n.NoIp : m.Ip,
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = string.IsNullOrEmpty(m.Ip) ? theme.TextDim : theme.AccentBlue,
                    FontFamily = new FontFamily("Consolas, Segoe UI")
                };
                ipChip.Child = tbIp;
                spRight.Children.Add(ipChip);
                Grid.SetColumn(spRight, 2);
                itemGrid.Children.Add(spRight);

                itemBorder.Child = itemGrid;

                // Mouse hover feedback
                itemBorder.MouseEnter += delegate
                {
                    itemBorder.Background = theme.CardHoverBg;
                    itemBorder.BorderBrush = theme.AccentBlue;
                };
                itemBorder.MouseLeave += delegate
                {
                    itemBorder.Background = theme.InnerTileBg;
                    itemBorder.BorderBrush = theme.BorderMuted;
                };

                // Left click: copy IP
                string copyTarget = !string.IsNullOrEmpty(m.Ip) ? m.Ip : m.Id;
                itemBorder.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                };
                itemBorder.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    try
                    {
                        Clipboard.SetText(copyTarget);
                        ShowToast(string.Format(i18n.CopiedIpFormat, copyTarget));
                    }
                    catch { }
                };

                // Right click: modern context menu (NO ICON GUTTER WHITE STRIP!)
                itemBorder.ContextMenu = CreateMemberContextMenu(m);

                _spMemberResults.Children.Add(itemBorder);
            }
        }

        private ContextMenu CreateMemberContextMenu(MemberNode m)
        {
            ContextMenu cm = AppTheme.CreateModernContextMenu();
            TranslationSet i18n = I18n.Current;

            if (!string.IsNullOrEmpty(m.Ip))
            {
                cm.Items.Add(AppTheme.CreateModernMenuItem(string.Format(i18n.MenuCopyIpFormat, m.Ip), delegate
                {
                    try { Clipboard.SetText(m.Ip); ShowToast(string.Format(i18n.CopiedIpFormat, m.Ip)); } catch { }
                }));
            }

            cm.Items.Add(AppTheme.CreateModernMenuItem(string.Format(i18n.MenuCopyIdFormat, m.Id), delegate
            {
                try { Clipboard.SetText(m.Id); ShowToast(string.Format(i18n.CopiedIdFormat, m.Id)); } catch { }
            }));

            cm.Items.Add(AppTheme.CreateModernMenuItem(i18n.MenuCopyAll, delegate
            {
                try
                {
                    string info = string.Format("{0} | {1} | ID: {2}", m.Name, m.Ip, m.Id);
                    Clipboard.SetText(info);
                    ShowToast(i18n.CopiedAll);
                }
                catch { }
            }));

            if (!string.IsNullOrEmpty(m.Ip))
            {
                cm.Items.Add(AppTheme.CreateModernSeparator());

                cm.Items.Add(AppTheme.CreateModernMenuItem(i18n.MenuRdp, delegate
                {
                    try { Process.Start("mstsc.exe", "/v:" + m.Ip); } catch { }
                }));

                cm.Items.Add(AppTheme.CreateModernMenuItem(i18n.MenuPing, delegate
                {
                    try { Process.Start("cmd.exe", "/k ping " + m.Ip); } catch { }
                }));

                cm.Items.Add(AppTheme.CreateModernMenuItem(i18n.MenuOpenHttp, delegate
                {
                    try { Process.Start("http://" + m.Ip); } catch { }
                }));
            }

            return cm;
        }
    }
}
