using System;
using System.Linq;
using System.Threading.Tasks;
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
        private LinuxDetailWindow _detailWindow;

        // UI TextBlocks
        private TextBlock _tbCpuVal;
        private Border _barCpuFill;
        private TextBlock _tbRamVal;
        private Border _barRamFill;
        private TextBlock _tbNetDown;
        private TextBlock _tbNetUp;
        private TextBlock _tbPower;
        private Border _dotZt;
        private TextBlock _tbZtText;
        private Border _rootPill;

        private DispatcherTimer _timer;

        public LinuxFloatingWindow()
        {
            Title = "SysMonitor";
            SystemDecorations = SystemDecorations.None;
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;

            BuildUi();

            // 右键快捷菜单 (上下文菜单)
            var menu = new ContextMenu();
            var miDetail = new MenuItem { Header = "展开 / 收起详情看板" };
            miDetail.Click += (s, e) => ToggleDetailWindow();

            var miTheme = new MenuItem { Header = "切换深色 / 浅色主题" };
            miTheme.Click += (s, e) => LinuxTheme.SetDark(!LinuxTheme.Current.IsDark);

            var miExit = new MenuItem { Header = "退出 SysMonitor" };
            miExit.Click += (s, e) =>
            {
                _detailWindow?.Close();
                Close();
            };

            menu.Items.Add(miDetail);
            menu.Items.Add(miTheme);
            menu.Items.Add(new Separator());
            menu.Items.Add(miExit);
            ContextMenu = menu;

            PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(e);
                }
            };

            DoubleTapped += (s, e) => ToggleDetailWindow();

            LinuxTheme.ThemeChanged += () =>
            {
                if (_rootPill != null)
                {
                    _rootPill.Background = LinuxTheme.Current.PillBg;
                    _rootPill.BorderBrush = LinuxTheme.Current.BorderBrush;
                }
            };

            Opened += (s, e) =>
            {
                var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
                if (screen != null)
                {
                    Position = new PixelPoint(screen.WorkingArea.X + screen.WorkingArea.Width - 140, screen.WorkingArea.Y + 70);
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

            _rootPill = new Border
            {
                Background = theme.PillBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 7, 8, 7),
                Width = 120,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var spMain = new StackPanel { Spacing = 5 };

            // 1. Header (Icon + Title)
            var spHead = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            var tbIcon = new TextBlock
            {
                Text = "❖",
                FontSize = 11,
                Foreground = theme.AccentBlue,
                FontWeight = FontWeight.Bold
            };
            var tbTitle = new TextBlock
            {
                Text = "SYSMONITOR",
                FontSize = 9.5,
                FontWeight = FontWeight.Bold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            spHead.Children.Add(tbIcon);
            spHead.Children.Add(tbTitle);
            spMain.Children.Add(spHead);

            // 2. CPU Module
            var spCpu = new StackPanel { Spacing = 1 };
            var gridCpuTop = new Grid();
            gridCpuTop.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gridCpuTop.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var lblCpu = LinuxTheme.CreateMuted("CPU", 8.5);
            Grid.SetColumn(lblCpu, 0);
            _tbCpuVal = new TextBlock
            {
                Text = "0.0%",
                FontSize = 9.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = theme.AccentBlue,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(_tbCpuVal, 1);
            gridCpuTop.Children.Add(lblCpu);
            gridCpuTop.Children.Add(_tbCpuVal);
            spCpu.Children.Add(gridCpuTop);

            var barCpuBg = new Border
            {
                Background = theme.BorderMuted,
                CornerRadius = new CornerRadius(1.5),
                Height = 3,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _barCpuFill = new Border
            {
                Background = theme.AccentBlue,
                CornerRadius = new CornerRadius(1.5),
                Height = 3,
                Width = 0,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            barCpuBg.Child = _barCpuFill;
            spCpu.Children.Add(barCpuBg);
            spMain.Children.Add(spCpu);

            // 3. RAM Module
            var spRam = new StackPanel { Spacing = 1 };
            var gridRamTop = new Grid();
            gridRamTop.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gridRamTop.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var lblRam = LinuxTheme.CreateMuted("RAM", 8.5);
            Grid.SetColumn(lblRam, 0);
            _tbRamVal = new TextBlock
            {
                Text = "0%",
                FontSize = 9.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = theme.AccentEmerald,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(_tbRamVal, 1);
            gridRamTop.Children.Add(lblRam);
            gridRamTop.Children.Add(_tbRamVal);
            spRam.Children.Add(gridRamTop);

            var barRamBg = new Border
            {
                Background = theme.BorderMuted,
                CornerRadius = new CornerRadius(1.5),
                Height = 3,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _barRamFill = new Border
            {
                Background = theme.AccentEmerald,
                CornerRadius = new CornerRadius(1.5),
                Height = 3,
                Width = 0,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            barRamBg.Child = _barRamFill;
            spRam.Children.Add(barRamBg);
            spMain.Children.Add(spRam);

            // 4. NET Module
            var spNet = new StackPanel { Spacing = 1 };
            _tbNetDown = new TextBlock
            {
                Text = "↓ 0 K/s",
                FontSize = 9,
                FontWeight = FontWeight.Medium,
                Foreground = theme.AccentBlue
            };
            _tbNetUp = new TextBlock
            {
                Text = "↑ 0 K/s",
                FontSize = 9,
                FontWeight = FontWeight.Medium,
                Foreground = theme.AccentEmerald
            };
            spNet.Children.Add(_tbNetDown);
            spNet.Children.Add(_tbNetUp);
            spMain.Children.Add(spNet);

            // 5. Power Module
            _tbPower = new TextBlock
            {
                Text = "⚡ 100%",
                FontSize = 9,
                FontWeight = FontWeight.Medium,
                Foreground = theme.AccentAmber
            };
            spMain.Children.Add(_tbPower);

            // 6. ZeroTier Module
            var spZt = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            _dotZt = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = theme.AccentEmerald,
                VerticalAlignment = VerticalAlignment.Center
            };
            _tbZtText = new TextBlock
            {
                Text = "ZT --",
                FontSize = 9,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            spZt.Children.Add(_dotZt);
            spZt.Children.Add(_tbZtText);
            spMain.Children.Add(spZt);

            _rootPill.Child = spMain;
            Content = _rootPill;
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
                int targetX = Position.X - (int)_detailWindow.Width - 10;
                if (targetX < 10)
                {
                    targetX = Position.X + 130;
                }
                _detailWindow.Position = new PixelPoint(targetX, Position.Y);
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

                    // CPU
                    _tbCpuVal.Text = string.Format("{0:F1}%", cpu);
                    double cpuBarWidth = Math.Max(0, Math.Min(104, (cpu / 100.0) * 104));
                    _barCpuFill.Width = cpuBarWidth;

                    // RAM
                    _tbRamVal.Text = string.Format("{0:F1}G ({1}%)", mem.UsedGB, mem.UsagePercent);
                    double ramBarWidth = Math.Max(0, Math.Min(104, (mem.UsagePercent / 100.0) * 104));
                    _barRamFill.Width = ramBarWidth;

                    // NET
                    _tbNetDown.Text = string.Format("↓ {0}", FormatRate(net.DownloadKBps));
                    _tbNetUp.Text = string.Format("↑ {0}", FormatRate(net.UploadKBps));

                    // PWR
                    if (bat.IsPluggedIn || bat.IsCharging)
                    {
                        _tbPower.Text = string.Format("⚡ {0}% {1:F1}W", bat.Percent, bat.RateWatts);
                        _tbPower.Foreground = theme.AccentAmber;
                    }
                    else
                    {
                        _tbPower.Text = string.Format("🔋 {0}%", bat.Percent);
                        _tbPower.Foreground = bat.Percent < 20 ? theme.AccentRed : theme.AccentEmerald;
                    }

                    // ZeroTier
                    if (zt.IsRunning)
                    {
                        _dotZt.Background = zt.MoonCount > 0 ? theme.AccentEmerald : theme.AccentBlue;
                        string pingStr = zt.MinMoonLatency > 0 ? (zt.MinMoonLatency + "ms") : "直连";
                        _tbZtText.Text = string.Format("ZT {0}", pingStr);
                    }
                    else
                    {
                        _dotZt.Background = theme.TextMuted;
                        _tbZtText.Text = "ZT 未运行";
                    }

                    // 同步刷新详情页（若展开）
                    if (_detailWindow != null && _detailWindow.IsVisible)
                    {
                        _detailWindow.UpdateTelemetry(cpu, mem, net, bat, zt);
                    }
                });
            }
            catch { }
        }

        private static string FormatRate(double kbps)
        {
            if (kbps >= 1024)
                return string.Format("{0:F1} MB/s", kbps / 1024.0);
            return string.Format("{0:F0} KB/s", kbps);
        }
    }
}
