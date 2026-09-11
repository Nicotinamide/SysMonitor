using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace SysMonitor.Linux.UI
{
    public class LinuxFloatingWindow : Window
    {
        private const double WidgetWidth = 126;

        private LinuxDetailWindow _detailWindow;
        private Border _rootBorder;
        private StackPanel _spMain;

        // Complication Tile Controls - Power
        private Border _bPowerTile;
        private TextBlock _tbBatteryPercent;
        private TextBlock _tbPcWatts;
        private Border _pBatteryTrack;
        private Border _rectBatteryFill;
        private TextBlock _tbWatts;

        // Complication Tile Controls - Network
        private Border _bNetTile;
        private TextBlock _tbNetRates;
        private TextBlock _tbFlagEmoji;
        private TextBlock _tbPublicIp;

        // Complication Tile Controls - ZeroTier
        private Border _bZtTile;
        private Border _elZtDot;
        private TextBlock _tbZtTitle;
        private TextBlock _tbZtStatus;
        private TextBlock _tbZtSub;

        // Telemetry Engine & Cache
        private LinuxTelemetryEngine _engine;
        private LinuxSystemLoadData _lastLoad;
        private LinuxPowerData _lastPower;
        private LinuxNetworkData _lastNet;
        private LinuxZeroTierData _lastZt;

        // Drag & Click Management
        private Point _pointerDownPos;
        private PointerPressedEventArgs _pointerPressedArgs;
        private bool _isDragging = false;

        public LinuxFloatingWindow()
        {
            Title = "SysMonitorWidget";
            Width = WidgetWidth;
            SystemDecorations = SystemDecorations.None;
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.Manual;

            BuildUi();
            SetupContextMenu();

            // Drag & Click tracking (1:1 with Windows behavior)
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;

            LinuxTheme.ThemeChanged += () =>
            {
                BuildUi();
                SetupContextMenu();
                ReplayTelemetry();
            };

            Opened += (s, e) =>
            {
                var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
                if (screen != null)
                {
                    Position = new PixelPoint(screen.WorkingArea.X + screen.WorkingArea.Width - (int)WidgetWidth - 24, screen.WorkingArea.Y + 60);
                }

                _detailWindow = new LinuxDetailWindow(this);

                _engine = new LinuxTelemetryEngine();
                _engine.SystemLoadUpdated += (load) => Dispatcher.UIThread.Post(() => OnSystemLoadUpdated(load));
                _engine.PowerUpdated += (pwr) => Dispatcher.UIThread.Post(() => OnPowerUpdated(pwr));
                _engine.NetworkUpdated += (net) => Dispatcher.UIThread.Post(() => OnNetworkUpdated(net));
                _engine.ZeroTierUpdated += (zt) => Dispatcher.UIThread.Post(() => OnZeroTierUpdated(zt));
            };
        }

        public LinuxTelemetryEngine Engine => _engine;

        private void BuildUi()
        {
            var theme = LinuxTheme.Current;

            _rootBorder = new Border
            {
                Width = WidgetWidth,
                CornerRadius = new CornerRadius(16),
                Background = theme.WindowBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 6, 6, 8),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            _spMain = new StackPanel { Spacing = 4 };

            // 1. Power Tile (供电与功耗)
            _spMain.Children.Add(BuildPowerTile(theme));

            // 2. Network Tile (网络与公网出口)
            _spMain.Children.Add(BuildNetworkTile(theme));

            // 3. ZeroTier Tile (虚拟局域网与 Moon)
            _spMain.Children.Add(BuildZeroTierTile(theme));

            // (Compute Tile is OFF by default on the widget, matches Windows 1:1)

            _rootBorder.Child = _spMain;
            Content = _rootBorder;
        }

        private Border BuildPowerTile(LinuxThemePalette theme)
        {
            _bPowerTile = LinuxTheme.CreateComplicationBorder();
            var grid = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.RowDefinitions.Add(new RowDefinition(3, GridUnitType.Pixel)); // gap
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star)); // left (battery % + bar)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));       // right (watts + state)

            // Top-left: ⚡ 100%
            var spBat = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            var iconBat = new TextBlock
            {
                Text = "⚡",
                FontSize = 10,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0)
            };
            _tbBatteryPercent = new TextBlock
            {
                Text = "100%",
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            spBat.Children.Add(iconBat);
            spBat.Children.Add(_tbBatteryPercent);
            Grid.SetRow(spBat, 0);
            Grid.SetColumn(spBat, 0);
            grid.Children.Add(spBat);

            // Top-right: 37.5W
            _tbPcWatts = new TextBlock
            {
                Text = "--",
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(_tbPcWatts, 0);
            Grid.SetColumn(_tbPcWatts, 1);
            grid.Children.Add(_tbPcWatts);

            // Bottom-left: progress bar
            _pBatteryTrack = new Border
            {
                Background = theme.ProgressBarTrack,
                CornerRadius = new CornerRadius(1.75),
                Height = 3.5,
                Margin = new Thickness(0, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };
            _rectBatteryFill = new Border
            {
                Background = theme.AccentEmerald,
                CornerRadius = new CornerRadius(1.75),
                Height = 3.5,
                Width = 20,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _pBatteryTrack.Child = _rectBatteryFill;
            Grid.SetRow(_pBatteryTrack, 2);
            Grid.SetColumn(_pBatteryTrack, 0);
            grid.Children.Add(_pBatteryTrack);

            // Bottom-right: 满电 / 市电 / +XX.XW
            _tbWatts = new TextBlock
            {
                Text = "满电",
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = theme.AccentEmerald,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(_tbWatts, 2);
            Grid.SetColumn(_tbWatts, 1);
            grid.Children.Add(_tbWatts);

            _bPowerTile.Child = grid;
            return _bPowerTile;
        }

        private Border BuildNetworkTile(LinuxThemePalette theme)
        {
            _bNetTile = LinuxTheme.CreateComplicationBorder();
            var sp = new StackPanel { Margin = new Thickness(6, 4, 6, 4) };

            _tbNetRates = new TextBlock
            {
                Text = "↓ 0.0K   ↑ 0.0K",
                FontSize = 9.5,
                FontWeight = FontWeight.Medium,
                Foreground = theme.TextSecondary,
                FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
            };
            sp.Children.Add(_tbNetRates);

            var ipRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _tbFlagEmoji = new TextBlock
            {
                Text = "🌐",
                FontSize = 9.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            _tbPublicIp = new TextBlock
            {
                Text = "获取中...",
                FontSize = 9.5,
                FontWeight = FontWeight.Bold,
                Foreground = theme.AccentBlue,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 85,
                FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
            };
            ipRow.Children.Add(_tbFlagEmoji);
            ipRow.Children.Add(_tbPublicIp);
            sp.Children.Add(ipRow);

            _bNetTile.Child = sp;
            return _bNetTile;
        }

        private Border BuildZeroTierTile(LinuxThemePalette theme)
        {
            _bZtTile = LinuxTheme.CreateComplicationBorder();
            var grid = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // Row 0: Dot + Moon (left), Status (right)
            var spLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _elZtDot = new Border
            {
                Width = 6.5,
                Height = 6.5,
                CornerRadius = new CornerRadius(3.25),
                Background = theme.AccentEmerald,
                Margin = new Thickness(0, 0, 4.5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _tbZtTitle = new TextBlock
            {
                Text = "Moon",
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            spLeft.Children.Add(_elZtDot);
            spLeft.Children.Add(_tbZtTitle);
            Grid.SetRow(spLeft, 0);
            Grid.SetColumn(spLeft, 0);
            grid.Children.Add(spLeft);

            _tbZtStatus = new TextBlock
            {
                Text = "--",
                FontSize = 10.5,
                FontWeight = FontWeight.Bold,
                Foreground = theme.AccentEmerald,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(_tbZtStatus, 0);
            Grid.SetColumn(_tbZtStatus, 1);
            grid.Children.Add(_tbZtStatus);

            // Row 1: Node ID or Subtext
            _tbZtSub = new TextBlock
            {
                Text = "ZeroTier Mesh",
                FontSize = 9.5,
                FontWeight = FontWeight.Medium,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
            };
            Grid.SetRow(_tbZtSub, 1);
            Grid.SetColumn(_tbZtSub, 0);
            Grid.SetColumnSpan(_tbZtSub, 2);
            grid.Children.Add(_tbZtSub);

            _bZtTile.Child = grid;
            return _bZtTile;
        }

        private void SetupContextMenu()
        {
            var menu = new ContextMenu();
            var miDetail = new MenuItem { Header = "📋 展开 / 收起详情看板" };
            miDetail.Click += (s, e) => ToggleDetailWindow();
            menu.Items.Add(miDetail);

            var miSearch = new MenuItem { Header = "🔍 搜索节点与 IP" };
            miSearch.Click += (s, e) =>
            {
                if (_detailWindow == null || !_detailWindow.IsVisible) ToggleDetailWindow();
                _detailWindow?.FocusSearch();
            };
            menu.Items.Add(miSearch);

            var miRefresh = new MenuItem { Header = "⟳ 刷新公网出口与遥测" };
            miRefresh.Click += (s, e) => _engine?.TriggerGeoIpRefresh();
            menu.Items.Add(miRefresh);

            menu.Items.Add(new Separator());

            var miTheme = new MenuItem { Header = LinuxTheme.Current.IsDark ? "☀️ 切换为浅色模式" : "🌙 切换为深色模式" };
            miTheme.Click += (s, e) => LinuxTheme.SetDark(!LinuxTheme.Current.IsDark);
            menu.Items.Add(miTheme);

            menu.Items.Add(new Separator());

            var miExit = new MenuItem { Header = "🚪 退出 SysMonitor" };
            miExit.Click += (s, e) =>
            {
                _detailWindow?.Close();
                Close();
            };
            menu.Items.Add(miExit);

            ContextMenu = menu;
        }

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _pointerDownPos = e.GetPosition(this);
                _pointerPressedArgs = e;
                _isDragging = false;
            }
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && _pointerPressedArgs != null)
            {
                var curPos = e.GetPosition(this);
                double dist = Math.Sqrt(Math.Pow(curPos.X - _pointerDownPos.X, 2) + Math.Pow(curPos.Y - _pointerDownPos.Y, 2));
                if (dist > 4 && !_isDragging)
                {
                    _isDragging = true;
                    BeginMoveDrag(_pointerPressedArgs);
                }
            }
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                if (!_isDragging)
                {
                    ToggleDetailWindow();
                }
                _isDragging = false;
            }
        }

        public void ToggleDetailWindow()
        {
            if (_detailWindow == null) _detailWindow = new LinuxDetailWindow(this);

            if (_detailWindow.IsVisible)
            {
                _detailWindow.Hide();
            }
            else
            {
                if ((DateTime.UtcNow - _detailWindow.LastDeactivatedTime).TotalMilliseconds < 350)
                    return;

                PositionDetailWindow();
                _detailWindow.Show();
                _detailWindow.Activate();
            }
        }

        private void PositionDetailWindow()
        {
            if (_detailWindow == null) return;
            var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (screen == null) return;

            int widgetX = Position.X;
            int widgetY = Position.Y;
            int detailW = (int)_detailWindow.Width;
            int screenRight = screen.WorkingArea.X + screen.WorkingArea.Width;
            int screenBottom = screen.WorkingArea.Y + screen.WorkingArea.Height;

            int targetX;
            if (widgetX + (int)WidgetWidth + detailW + 12 <= screenRight)
            {
                targetX = widgetX + (int)WidgetWidth + 8;
            }
            else
            {
                targetX = Math.Max(screen.WorkingArea.X + 8, widgetX - detailW - 8);
            }

            int targetY = Math.Min(Math.Max(screen.WorkingArea.Y + 8, widgetY - 10), screenBottom - 580);
            _detailWindow.Position = new PixelPoint(targetX, targetY);
        }

        private void OnSystemLoadUpdated(LinuxSystemLoadData data)
        {
            _lastLoad = data;
            _detailWindow?.UpdateSystemLoad(data);
        }

        private void OnPowerUpdated(LinuxPowerData data)
        {
            _lastPower = data;
            var theme = LinuxTheme.Current;

            if (_tbBatteryPercent != null)
                _tbBatteryPercent.Text = $"{data.BatteryPercent}%";

            if (_tbPcWatts != null)
                _tbPcWatts.Text = data.CpuWatts > 0.5 ? $"{data.CpuWatts:0.0}W" : "--";

            IBrush pBrush = (data.StateKind == LinuxPowerStateKind.ChargedFull || data.StateKind == LinuxPowerStateKind.AcDirect)
                ? theme.AccentEmerald
                : (data.StateKind == LinuxPowerStateKind.ChargingFast ? theme.AccentBlue : theme.AccentAmber);

            if (_rectBatteryFill != null)
            {
                _rectBatteryFill.Background = pBrush;
                double trackW = 44.0;
                double fillW = Math.Max(2, Math.Min(trackW, (data.BatteryPercent / 100.0) * trackW));
                _rectBatteryFill.Width = fillW;
            }

            if (_tbWatts != null)
            {
                _tbWatts.Text = data.StatusText;
                _tbWatts.Foreground = pBrush;
            }

            _detailWindow?.UpdatePower(data);
        }

        private void OnNetworkUpdated(LinuxNetworkData data)
        {
            _lastNet = data;
            if (_tbNetRates != null)
                _tbNetRates.Text = $"{data.DownSpeedStr}   {data.UpSpeedStr}";

            if (_tbPublicIp != null)
                _tbPublicIp.Text = string.IsNullOrEmpty(data.PublicIp) ? "获取中..." : data.PublicIp;

            if (_tbFlagEmoji != null)
                _tbFlagEmoji.Text = LinuxTelemetryEngine.CountryCodeToEmoji(data.CountryCode);

            _detailWindow?.UpdateNetwork(data);
        }

        private void OnZeroTierUpdated(LinuxZeroTierData data)
        {
            _lastZt = data;
            var theme = LinuxTheme.Current;

            if (_elZtDot != null && _tbZtTitle != null && _tbZtStatus != null && _tbZtSub != null)
            {
                if (!data.IsRunning)
                {
                    _elZtDot.Background = theme.TextMuted;
                    _tbZtTitle.Text = "ZeroTier";
                    _tbZtStatus.Text = "未运行";
                    _tbZtStatus.Foreground = theme.TextMuted;
                    _tbZtSub.Text = !string.IsNullOrEmpty(data.LocalNodeId) ? ("Node: " + data.LocalNodeId) : "ZeroTier Mesh";
                }
                else if (data.HasDroppedMoons)
                {
                    _elZtDot.Background = theme.AccentRed;
                    _tbZtTitle.Text = "Moon";
                    _tbZtStatus.Text = "掉线";
                    _tbZtStatus.Foreground = theme.AccentRed;
                    _tbZtSub.Text = "Node: " + data.LocalNodeId;
                }
                else
                {
                    bool isDirect = data.DirectMoons > 0;
                    _elZtDot.Background = isDirect ? theme.AccentEmerald : theme.AccentAmber;
                    _tbZtStatus.Foreground = isDirect ? theme.AccentEmerald : theme.AccentAmber;

                    if (data.TotalMoons > 0)
                    {
                        _tbZtTitle.Text = $"Moon {data.DirectMoons}/{data.TotalMoons}";
                        _tbZtStatus.Text = isDirect ? $"直连 {data.MinLatency}ms" : "中继";
                    }
                    else
                    {
                        _tbZtTitle.Text = "ZeroTier";
                        _tbZtStatus.Text = "已连入";
                    }

                    _tbZtSub.Text = !string.IsNullOrEmpty(data.LocalNodeId) ? ("Node: " + data.LocalNodeId) : "ZeroTier Mesh";
                }
            }

            _detailWindow?.UpdateZeroTier(data);
        }

        private void ReplayTelemetry()
        {
            if (_lastLoad != null) OnSystemLoadUpdated(_lastLoad);
            if (_lastPower != null) OnPowerUpdated(_lastPower);
            if (_lastNet != null) OnNetworkUpdated(_lastNet);
            if (_lastZt != null) OnZeroTierUpdated(_lastZt);
        }
    }
}
