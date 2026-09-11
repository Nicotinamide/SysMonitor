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

        // Tile 1: Compute (CPU & RAM)
        private TextBlock _tbComputeCpuVal;
        private Border _rectComputeCpuBar;
        private TextBlock _tbComputeRamVal;
        private Border _rectComputeRamBar;

        // Tile 2: Network
        private TextBlock _tbNetRates;
        private TextBlock _tbPublicIp;

        // Tile 3: ZeroTier & Moon
        private Border _elTile3Dot;
        private TextBlock _tbTile3Title;
        private TextBlock _tbTile3Status;
        private TextBlock _tbTile3Sub;

        // Tile 4: Power
        private TextBlock _tbBatteryPercent;
        private TextBlock _tbPcWatts;
        private Border _rectBatteryFill;
        private TextBlock _tbWatts;

        // Drag & Click tracking (1:1 with Windows)
        private Point _pointerDownPos;
        private PointerPressedEventArgs _pointerPressedArgs;
        private bool _isDragging = false;

        private DispatcherTimer _timer;

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

            // 单击展开 / 拖拽移动 (与 Windows 1:1 交互)
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;

            LinuxTheme.ThemeChanged += () =>
            {
                BuildUi();
                SetupContextMenu();
                RefreshTelemetry();
            };

            Opened += (s, e) =>
            {
                var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
                if (screen != null)
                {
                    Position = new PixelPoint(screen.WorkingArea.X + screen.WorkingArea.Width - (int)WidgetWidth - 24, screen.WorkingArea.Y + 60);
                }

                _detailWindow = new LinuxDetailWindow(this);

                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += (ts, te) => RefreshTelemetry();
                _timer.Start();
                RefreshTelemetry();
            };
        }

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

            var spMain = new StackPanel { Spacing = 4 };

            // 1. Power Tile (供电与功耗)
            spMain.Children.Add(BuildPowerTile(theme));

            // 2. Network Tile (网络与公网出口)
            spMain.Children.Add(BuildNetworkTile(theme));

            // 3. ZeroTier Tile (虚拟局域网与 Moon)
            spMain.Children.Add(BuildZeroTierTile(theme));

            // 4. Compute Tile (CPU & RAM 负载)
            spMain.Children.Add(BuildComputeTile(theme));

            _rootBorder.Child = spMain;
            Content = _rootBorder;
        }

        private Border BuildComputeTile(LinuxThemePalette theme)
        {
            var tile = LinuxTheme.CreateComplicationBorder();
            var grid = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.RowDefinitions.Add(new RowDefinition(3.5, GridUnitType.Pixel));

            // Row 0: CPU {val}% (left) and RAM {val}% (right)
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            topGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var spCpu = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var tbCpuLabel = new TextBlock
            {
                Text = "CPU ",
                FontSize = 9,
                FontWeight = FontWeight.Medium,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            _tbComputeCpuVal = new TextBlock
            {
                Text = "0.0%",
                FontSize = 9.5,
                FontWeight = FontWeight.Bold,
                Foreground = theme.AccentBlue,
                VerticalAlignment = VerticalAlignment.Center
            };
            spCpu.Children.Add(tbCpuLabel);
            spCpu.Children.Add(_tbComputeCpuVal);
            Grid.SetColumn(spCpu, 0);
            topGrid.Children.Add(spCpu);

            var spRam = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var tbRamLabel = new TextBlock
            {
                Text = "RAM ",
                FontSize = 9,
                FontWeight = FontWeight.Medium,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            _tbComputeRamVal = new TextBlock
            {
                Text = "0%",
                FontSize = 9.5,
                FontWeight = FontWeight.Bold,
                Foreground = theme.AccentEmerald,
                VerticalAlignment = VerticalAlignment.Center
            };
            spRam.Children.Add(tbRamLabel);
            spRam.Children.Add(_tbComputeRamVal);
            Grid.SetColumn(spRam, 1);
            topGrid.Children.Add(spRam);

            Grid.SetRow(topGrid, 0);
            grid.Children.Add(topGrid);

            // Row 1: Split progress bars
            var barGrid = new Grid();
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            var trackCpu = new Border
            {
                Background = theme.ProgressBarTrack,
                CornerRadius = new CornerRadius(1.75),
                Height = 3.5,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _rectComputeCpuBar = new Border
            {
                Background = theme.AccentBlue,
                CornerRadius = new CornerRadius(1.75),
                Height = 3.5,
                Width = 0,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            trackCpu.Child = _rectComputeCpuBar;
            Grid.SetColumn(trackCpu, 0);
            barGrid.Children.Add(trackCpu);

            var trackRam = new Border
            {
                Background = theme.ProgressBarTrack,
                CornerRadius = new CornerRadius(1.75),
                Height = 3.5,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _rectComputeRamBar = new Border
            {
                Background = theme.AccentEmerald,
                CornerRadius = new CornerRadius(1.75),
                Height = 3.5,
                Width = 0,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            trackRam.Child = _rectComputeRamBar;
            Grid.SetColumn(trackRam, 2);
            barGrid.Children.Add(trackRam);

            Grid.SetRow(barGrid, 1);
            grid.Children.Add(barGrid);

            tile.Child = grid;
            return tile;
        }

        private Border BuildNetworkTile(LinuxThemePalette theme)
        {
            var tile = LinuxTheme.CreateComplicationBorder();
            var sp = new StackPanel { Margin = new Thickness(6, 4, 6, 4) };

            _tbNetRates = new TextBlock
            {
                Text = "↓ 0.0K   ↑ 0.0K",
                FontSize = 9.5,
                FontWeight = FontWeight.Medium,
                Foreground = theme.TextSecondary
            };
            sp.Children.Add(_tbNetRates);

            var ipRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var tbIcon = new TextBlock
            {
                Text = "🌐",
                FontSize = 9,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            _tbPublicIp = new TextBlock
            {
                Text = "127.0.0.1",
                FontSize = 9.5,
                FontWeight = FontWeight.Medium,
                Foreground = theme.AccentBlue,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 85
            };
            ipRow.Children.Add(tbIcon);
            ipRow.Children.Add(_tbPublicIp);
            sp.Children.Add(ipRow);

            tile.Child = sp;
            return tile;
        }

        private Border BuildZeroTierTile(LinuxThemePalette theme)
        {
            var tile = LinuxTheme.CreateComplicationBorder();
            var grid = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // Row 0: Dot + Moon (left), Status (right)
            var spLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _elTile3Dot = new Border
            {
                Width = 6.5,
                Height = 6.5,
                CornerRadius = new CornerRadius(3.25),
                Background = theme.AccentEmerald,
                Margin = new Thickness(0, 0, 4.5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _tbTile3Title = new TextBlock
            {
                Text = "Moon",
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            spLeft.Children.Add(_elTile3Dot);
            spLeft.Children.Add(_tbTile3Title);
            Grid.SetRow(spLeft, 0);
            Grid.SetColumn(spLeft, 0);
            grid.Children.Add(spLeft);

            _tbTile3Status = new TextBlock
            {
                Text = "--",
                FontSize = 10.5,
                FontWeight = FontWeight.Bold,
                Foreground = theme.AccentEmerald,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(_tbTile3Status, 0);
            Grid.SetColumn(_tbTile3Status, 1);
            grid.Children.Add(_tbTile3Status);

            // Row 1: Node ID or Subtext
            _tbTile3Sub = new TextBlock
            {
                Text = "ZeroTier",
                FontSize = 9.5,
                FontWeight = FontWeight.Medium,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(_tbTile3Sub, 1);
            Grid.SetColumn(_tbTile3Sub, 0);
            Grid.SetColumnSpan(_tbTile3Sub, 2);
            grid.Children.Add(_tbTile3Sub);

            tile.Child = grid;
            return tile;
        }

        private Border BuildPowerTile(LinuxThemePalette theme)
        {
            var tile = LinuxTheme.CreateComplicationBorder();
            var grid = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.RowDefinitions.Add(new RowDefinition(3, GridUnitType.Pixel));
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // Row 0: ⚡ 100% (left) and 30.6W (right)
            var spBat = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            var iconBat = new TextBlock
            {
                Text = "⚡",
                FontSize = 10,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 2, 0)
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

            _tbPcWatts = new TextBlock
            {
                Text = "0.0W",
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

            // Row 2: Progress bar (left) and state text (right)
            var trackBat = new Border
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
                Background = theme.AccentBlue,
                CornerRadius = new CornerRadius(1.75),
                Height = 3.5,
                Width = 20,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            trackBat.Child = _rectBatteryFill;
            Grid.SetRow(trackBat, 2);
            Grid.SetColumn(trackBat, 0);
            grid.Children.Add(trackBat);

            _tbWatts = new TextBlock
            {
                Text = "市电",
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

            tile.Child = grid;
            return tile;
        }

        private void SetupContextMenu()
        {
            var menu = new ContextMenu();
            var miDetail = new MenuItem { Header = "📋 展开 / 收起详情看板" };
            miDetail.Click += (s, e) => ToggleDetailWindow();

            var miRefresh = new MenuItem { Header = "⟳ 刷新公网出口与遥测" };
            miRefresh.Click += (s, e) => RefreshTelemetry();

            var miTheme = new MenuItem { Header = LinuxTheme.Current.IsDark ? "☀️ 切换为浅色模式" : "🌙 切换为深色模式" };
            miTheme.Click += (s, e) => LinuxTheme.SetDark(!LinuxTheme.Current.IsDark);

            var miExit = new MenuItem { Header = "🚪 退出 SysMonitor" };
            miExit.Click += (s, e) =>
            {
                _detailWindow?.Close();
                Close();
            };

            menu.Items.Add(miDetail);
            menu.Items.Add(miRefresh);
            menu.Items.Add(new Separator());
            menu.Items.Add(miTheme);
            menu.Items.Add(new Separator());
            menu.Items.Add(miExit);
            ContextMenu = menu;
        }

        // ==========================================
        // 鼠标事件：实现 Windows 1:1 单击展开与拖拽
        // ==========================================
        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _pointerPressedArgs = e;
                _pointerDownPos = e.GetPosition(this);
                _isDragging = false;
                e.Pointer.Capture(this);
            }
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && ReferenceEquals(e.Pointer.Captured, this))
            {
                Point currPos = e.GetPosition(this);
                if (Math.Abs(currPos.X - _pointerDownPos.X) > 4 || Math.Abs(currPos.Y - _pointerDownPos.Y) > 4)
                {
                    _isDragging = true;
                    e.Pointer.Capture(null);
                    if (_detailWindow != null && _detailWindow.IsVisible)
                    {
                        _detailWindow.Hide();
                    }
                    if (_pointerPressedArgs != null)
                    {
                        BeginMoveDrag(_pointerPressedArgs);
                    }
                }
            }
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (ReferenceEquals(e.Pointer.Captured, this))
            {
                e.Pointer.Capture(null);
            }

            if (!_isDragging)
            {
                ToggleDetailWindow();
            }
            _isDragging = false;
            _pointerPressedArgs = null;
        }

        public void ToggleDetailWindow()
        {
            if (_detailWindow == null) return;
            if (_detailWindow.IsVisible)
            {
                _detailWindow.Hide();
            }
            else
            {
                // 若刚刚因失焦而隐藏（例如点击首页微件收起），防抖 350ms 避免重复触发重开
                if ((DateTime.UtcNow - _detailWindow.LastDeactivatedTime).TotalMilliseconds < 350)
                {
                    return;
                }
                _detailWindow.PositionNear(Position.X, Position.Y, (int)Width, (int)Bounds.Height);
                _detailWindow.Show();
                _detailWindow.Activate();
            }
        }

        private async void RefreshTelemetry()
        {
            try
            {
                double cpu = LinuxMonitors.GetCpuUsage();
                MemorySnapshot mem = LinuxMonitors.GetMemoryStatus();
                NetworkRateSnapshot net = LinuxMonitors.GetNetworkRates();
                BatterySnapshot bat = LinuxMonitors.GetBatteryStatus();
                ZeroTierLocalSnapshot zt = await LinuxMonitors.GetZeroTierLocalStatusAsync();

                Dispatcher.UIThread.Post(() =>
                {
                    var theme = LinuxTheme.Current;

                    // 1. CPU & RAM
                    _tbComputeCpuVal.Text = string.Format("{0:F1}%", cpu);
                    double cpuBarWidth = Math.Max(0, Math.Min(48, (cpu / 100.0) * 48));
                    _rectComputeCpuBar.Width = cpuBarWidth;

                    _tbComputeRamVal.Text = string.Format("{0}%", mem.UsagePercent);
                    double ramBarWidth = Math.Max(0, Math.Min(48, (mem.UsagePercent / 100.0) * 48));
                    _rectComputeRamBar.Width = ramBarWidth;

                    // 2. Network
                    string downFmt = FormatCompactRate(net.DownloadKBps);
                    string upFmt = FormatCompactRate(net.UploadKBps);
                    _tbNetRates.Text = string.Format("↓ {0}   ↑ {1}", downFmt, upFmt);

                    // 3. ZeroTier
                    if (zt.IsRunning)
                    {
                        _elTile3Dot.Background = theme.AccentEmerald;
                        _tbTile3Title.Text = zt.MoonCount > 0 ? "Moon" : "ZeroTier";
                        _tbTile3Status.Text = zt.MoonCount > 0
                            ? (zt.MinMoonLatency >= 0 ? (zt.MinMoonLatency + "ms") : "直连")
                            : "PLANET";
                        _tbTile3Status.Foreground = theme.AccentEmerald;
                        _tbTile3Sub.Text = !string.IsNullOrEmpty(zt.NodeId) ? ("Node: " + zt.NodeId) : "ZeroTier";
                    }
                    else
                    {
                        _elTile3Dot.Background = theme.TextMuted;
                        _tbTile3Title.Text = "ZeroTier";
                        _tbTile3Status.Text = "未运行";
                        _tbTile3Status.Foreground = theme.TextMuted;
                        _tbTile3Sub.Text = "守护进程离线";
                    }

                    // 4. Power
                    _tbBatteryPercent.Text = string.Format("{0}%", bat.Percent);
                    _tbPcWatts.Text = bat.RateWatts > 0 ? string.Format("{0:F1}W", bat.RateWatts) : "0.0W";
                    double batFillWidth = Math.Max(2, Math.Min(48, (bat.Percent / 100.0) * 48));
                    _rectBatteryFill.Width = batFillWidth;

                    if (bat.IsCharging)
                    {
                        _tbWatts.Text = "正在充电";
                        _tbWatts.Foreground = theme.AccentAmber;
                    }
                    else if (bat.IsPluggedIn)
                    {
                        _tbWatts.Text = "市电供电";
                        _tbWatts.Foreground = theme.AccentEmerald;
                    }
                    else
                    {
                        _tbWatts.Text = "放电中";
                        _tbWatts.Foreground = bat.Percent < 20 ? theme.AccentRed : theme.TextSecondary;
                    }

                    // 同步刷新展开中的详情页
                    if (_detailWindow != null && _detailWindow.IsVisible)
                    {
                        _detailWindow.UpdateTelemetry(cpu, mem, net, bat, zt);
                    }
                });
            }
            catch { }
        }

        private static string FormatCompactRate(double kbps)
        {
            if (kbps >= 1024)
                return string.Format("{0:F1}M", kbps / 1024.0);
            return string.Format("{0:F0}K", kbps);
        }
    }
}
