using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace SysMonitor.Linux.UI
{
    public class LinuxDetailWindow : Window
    {
        private LinuxFloatingWindow _parentFloat;

        // UI References
        private Border _rootBorder;
        private StackPanel _spContent;
        private Border _overlaySettings;

        // Card CPU/RAM
        private TextBlock _tbCpuVal;
        private Border _barCpuFill;
        private TextBlock _tbRamVal;
        private Border _barRamFill;

        // Card PWR
        private TextBlock _tbPwrStatus;
        private TextBlock _tbPwrWatts;

        // Card NET
        private TextBlock _tbNetDown;
        private TextBlock _tbNetUp;

        // Card ZT
        private TextBlock _tbZtNode;
        private TextBlock _tbZtStatus;
        private TextBlock _tbZtMoon;

        public DateTime LastDeactivatedTime { get; private set; }

        public LinuxDetailWindow(LinuxFloatingWindow parentFloat)
        {
            _parentFloat = parentFloat;

            Title = "SysMonitor Details";
            SystemDecorations = SystemDecorations.None;
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            Width = 385;
            MaxHeight = 620;
            SizeToContent = SizeToContent.Height;

            // 失焦自动隐藏收起，并记录失焦时间用于点击防抖
            Deactivated += (s, e) =>
            {
                LastDeactivatedTime = DateTime.UtcNow;
                this.Hide();
            };

            BuildUi();

            LinuxTheme.ThemeChanged += () =>
            {
                var theme = LinuxTheme.Current;
                if (_rootBorder != null)
                {
                    _rootBorder.Background = theme.CardBg;
                    _rootBorder.BorderBrush = theme.BorderBrush;
                }
            };
        }

        private void BuildUi()
        {
            var theme = LinuxTheme.Current;

            _rootBorder = new Border
            {
                Background = theme.CardBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 10, 12, 12),
                BoxShadow = BoxShadows.Parse("0 4 18 #32000000")
            };

            var gridRoot = new Grid();

            _spContent = new StackPanel { Spacing = 6 };

            // 1. Title Header Bar
            var gridHeader = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            gridHeader.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gridHeader.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var spTitle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            var tbTitle = new TextBlock
            {
                Text = "SysMonitor",
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            var chipSys = new Border
            {
                Background = theme.InnerTileBg,
                BorderBrush = theme.BorderMuted,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            var tbSys = new TextBlock
            {
                Text = Environment.Is64BitOperatingSystem ? "LINUX 64-BIT" : "LINUX 32-BIT",
                FontSize = 8.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = theme.TextSecondary
            };
            chipSys.Child = tbSys;
            spTitle.Children.Add(tbTitle);
            spTitle.Children.Add(chipSys);
            Grid.SetColumn(spTitle, 0);
            gridHeader.Children.Add(spTitle);

            var spTopBtns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            var btnSettings = LinuxTheme.CreateIconButton("⚙", "设置", () => ToggleSettings());
            var btnClose = LinuxTheme.CreateIconButton("✕", "关闭", () => this.Hide());
            spTopBtns.Children.Add(btnSettings);
            spTopBtns.Children.Add(btnClose);
            Grid.SetColumn(spTopBtns, 1);
            gridHeader.Children.Add(spTopBtns);
            _spContent.Children.Add(gridHeader);

            // 2. Card: CPU & RAM
            var cardCpuRam = LinuxTheme.CreateCardBorder();
            var spCpuRam = new StackPanel { Margin = new Thickness(10, 8), Spacing = 6 };
            spCpuRam.Children.Add(LinuxTheme.CreateHeader("💻 CPU & 内存负载"));

            var gridLoad = new Grid();
            gridLoad.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gridLoad.ColumnDefinitions.Add(new ColumnDefinition(10, GridUnitType.Pixel));
            gridLoad.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            // CPU
            var spCpu = new StackPanel { Spacing = 3 };
            var gCpuTop = new Grid();
            gCpuTop.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gCpuTop.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var lCpu = LinuxTheme.CreateMuted("CPU 使用率");
            Grid.SetColumn(lCpu, 0);
            _tbCpuVal = new TextBlock { Text = "0.0%", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = theme.AccentBlue, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(_tbCpuVal, 1);
            gCpuTop.Children.Add(lCpu);
            gCpuTop.Children.Add(_tbCpuVal);
            spCpu.Children.Add(gCpuTop);

            var bCpuBg = new Border { Background = theme.BorderMuted, CornerRadius = new CornerRadius(2), Height = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
            _barCpuFill = new Border { Background = theme.AccentBlue, CornerRadius = new CornerRadius(2), Height = 4, Width = 0, HorizontalAlignment = HorizontalAlignment.Left };
            bCpuBg.Child = _barCpuFill;
            spCpu.Children.Add(bCpuBg);
            Grid.SetColumn(spCpu, 0);
            gridLoad.Children.Add(spCpu);

            // RAM
            var spRam = new StackPanel { Spacing = 3 };
            var gRamTop = new Grid();
            gRamTop.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gRamTop.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var lRam = LinuxTheme.CreateMuted("物理内存");
            Grid.SetColumn(lRam, 0);
            _tbRamVal = new TextBlock { Text = "0G (0%)", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = theme.AccentEmerald, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(_tbRamVal, 1);
            gRamTop.Children.Add(lRam);
            gRamTop.Children.Add(_tbRamVal);
            spRam.Children.Add(gRamTop);

            var bRamBg = new Border { Background = theme.BorderMuted, CornerRadius = new CornerRadius(2), Height = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
            _barRamFill = new Border { Background = theme.AccentEmerald, CornerRadius = new CornerRadius(2), Height = 4, Width = 0, HorizontalAlignment = HorizontalAlignment.Left };
            bRamBg.Child = _barRamFill;
            spRam.Children.Add(bRamBg);
            Grid.SetColumn(spRam, 2);
            gridLoad.Children.Add(spRam);

            spCpuRam.Children.Add(gridLoad);
            cardCpuRam.Child = spCpuRam;
            _spContent.Children.Add(cardCpuRam);

            // 3. Card: Power & Battery
            var cardPwr = LinuxTheme.CreateCardBorder();
            var spPwr = new StackPanel { Margin = new Thickness(10, 8), Spacing = 4 };
            spPwr.Children.Add(LinuxTheme.CreateHeader("⚡ 供电与电池状态"));
            var gPwr = new Grid();
            gPwr.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gPwr.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _tbPwrStatus = new TextBlock { Text = "正在充电 (100%)", FontSize = 10, Foreground = theme.TextPrimary, FontWeight = FontWeight.Medium };
            Grid.SetColumn(_tbPwrStatus, 0);
            _tbPwrWatts = new TextBlock { Text = "0.0 W", FontSize = 10, Foreground = theme.AccentAmber, FontWeight = FontWeight.Bold };
            Grid.SetColumn(_tbPwrWatts, 1);
            gPwr.Children.Add(_tbPwrStatus);
            gPwr.Children.Add(_tbPwrWatts);
            spPwr.Children.Add(gPwr);
            cardPwr.Child = spPwr;
            _spContent.Children.Add(cardPwr);

            // 4. Card: Network Throughput
            var cardNet = LinuxTheme.CreateCardBorder();
            var spNet = new StackPanel { Margin = new Thickness(10, 8), Spacing = 4 };
            spNet.Children.Add(LinuxTheme.CreateHeader("🌐 实时网络吞吐"));
            var gNet = new Grid();
            gNet.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gNet.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            _tbNetDown = new TextBlock { Text = "↓ 下载: 0 KB/s", FontSize = 10, Foreground = theme.AccentBlue, FontWeight = FontWeight.Medium };
            Grid.SetColumn(_tbNetDown, 0);
            _tbNetUp = new TextBlock { Text = "↑ 上传: 0 KB/s", FontSize = 10, Foreground = theme.AccentEmerald, FontWeight = FontWeight.Medium };
            Grid.SetColumn(_tbNetUp, 1);
            gNet.Children.Add(_tbNetDown);
            gNet.Children.Add(_tbNetUp);
            spNet.Children.Add(gNet);
            cardNet.Child = spNet;
            _spContent.Children.Add(cardNet);

            // 5. Card: ZeroTier & Moon
            var cardZt = LinuxTheme.CreateCardBorder();
            var spZt = new StackPanel { Margin = new Thickness(10, 8), Spacing = 4 };
            spZt.Children.Add(LinuxTheme.CreateHeader("🔗 ZeroTier 虚拟局域网 & Moon 列表"));
            _tbZtNode = LinuxTheme.CreateMuted("本地节点: 探测中...");
            _tbZtStatus = new TextBlock { Text = "客户端状态: 未连接", FontSize = 10, Foreground = theme.TextSecondary };
            _tbZtMoon = new TextBlock { Text = "Moon 节点: 直连或中继探测中", FontSize = 10, Foreground = theme.AccentEmerald, FontWeight = FontWeight.Medium };
            spZt.Children.Add(_tbZtNode);
            spZt.Children.Add(_tbZtStatus);
            spZt.Children.Add(_tbZtMoon);
            cardZt.Child = spZt;
            _spContent.Children.Add(cardZt);

            gridRoot.Children.Add(_spContent);

            // 6. Settings Overlay
            BuildSettingsOverlay(gridRoot);

            _rootBorder.Child = gridRoot;
            Content = _rootBorder;
        }

        private void BuildSettingsOverlay(Grid gridRoot)
        {
            var theme = LinuxTheme.Current;

            _overlaySettings = new Border
            {
                Background = theme.CardBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 12),
                IsVisible = false
            };

            var spSet = new StackPanel { Spacing = 8 };

            var gSetHead = new Grid();
            gSetHead.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            gSetHead.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var tbSetTitle = new TextBlock { Text = "⚙ 设置与偏好", FontSize = 12, FontWeight = FontWeight.Bold, Foreground = theme.TextPrimary };
            Grid.SetColumn(tbSetTitle, 0);
            var btnSetClose = LinuxTheme.CreateIconButton("✕", "关闭设置", () => ToggleSettings());
            Grid.SetColumn(btnSetClose, 1);
            gSetHead.Children.Add(tbSetTitle);
            gSetHead.Children.Add(btnSetClose);
            spSet.Children.Add(gSetHead);

            // Theme toggle button
            var btnThemeToggle = new Button
            {
                Content = "🎨 切换主题 (亮色 / 暗色)",
                FontSize = 10,
                Foreground = theme.AccentBlue,
                Background = theme.InnerTileBg,
                BorderBrush = theme.BorderMuted,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            btnThemeToggle.Click += (s, e) =>
            {
                LinuxTheme.SetDark(!LinuxTheme.Current.IsDark);
            };
            spSet.Children.Add(btnThemeToggle);

            // Version info
            var tbVer = LinuxTheme.CreateMuted("当前版本: v1.0.5 (Linux Native)", 10);
            spSet.Children.Add(tbVer);

            // GitHub link button
            var btnGithub = new Button
            {
                Content = "🌐 查看 GitHub 官方仓库",
                FontSize = 10,
                Foreground = theme.TextSecondary,
                Background = theme.InnerTileBg,
                BorderBrush = theme.BorderMuted,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            btnGithub.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = "https://github.com/Nicotinamide/SysMonitor",
                        UseShellExecute = false
                    });
                }
                catch { }
            };
            spSet.Children.Add(btnGithub);

            // Exit application button
            var btnExit = new Button
            {
                Content = "🚪 退出 SysMonitor",
                FontSize = 10,
                Foreground = theme.AccentRed,
                Background = theme.InnerTileBg,
                BorderBrush = theme.BorderMuted,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            btnExit.Click += (s, e) =>
            {
                _parentFloat?.Close();
                Close();
            };
            spSet.Children.Add(btnExit);

            _overlaySettings.Child = spSet;
            gridRoot.Children.Add(_overlaySettings);
        }

        private void ToggleSettings()
        {
            if (_overlaySettings != null)
            {
                _overlaySettings.IsVisible = !_overlaySettings.IsVisible;
            }
        }

        public void PositionNear(int left, int top, int width, int height)
        {
            var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (screen == null)
            {
                Position = new PixelPoint(left - 385 - 8, top);
                return;
            }

            var wa = screen.WorkingArea;
            int detailW = 385;
            int detailH = (int)Bounds.Height > 0 ? (int)Bounds.Height : 480;

            int targetLeft;
            if (left + width + detailW + 10 <= wa.X + wa.Width)
            {
                targetLeft = left + width + 8;
            }
            else
            {
                targetLeft = left - detailW - 8;
            }

            if (targetLeft < wa.X + 8) targetLeft = wa.X + 8;
            if (targetLeft + detailW > wa.X + wa.Width - 8) targetLeft = wa.X + wa.Width - detailW - 8;

            int targetTop;
            if ((top + height / 2.0) > (wa.Y + wa.Height / 2.0))
            {
                targetTop = (top + height) - detailH;
            }
            else
            {
                targetTop = top;
            }

            if (targetTop + detailH > wa.Y + wa.Height - 8) targetTop = wa.Y + wa.Height - detailH - 8;
            if (targetTop < wa.Y + 8) targetTop = wa.Y + 8;

            Position = new PixelPoint(targetLeft, targetTop);
        }

        public void UpdateTelemetry(double cpu, MemorySnapshot mem, NetworkRateSnapshot net, BatterySnapshot bat, ZeroTierLocalSnapshot zt)
        {
            var theme = LinuxTheme.Current;

            // CPU
            _tbCpuVal.Text = string.Format("{0:F1}%", cpu);
            _barCpuFill.Width = Math.Max(0, Math.Min(140, (cpu / 100.0) * 140));

            // RAM
            _tbRamVal.Text = string.Format("{0:F1}G / {1:F1}G ({2}%)", mem.UsedGB, mem.TotalGB, mem.UsagePercent);
            _barRamFill.Width = Math.Max(0, Math.Min(140, (mem.UsagePercent / 100.0) * 140));

            // PWR
            if (bat.IsPluggedIn || bat.IsCharging)
            {
                _tbPwrStatus.Text = string.Format("⚡ 正在充电 ({0}%)", bat.Percent);
                _tbPwrWatts.Text = string.Format("{0:F1} W", bat.RateWatts);
            }
            else
            {
                _tbPwrStatus.Text = string.Format("🔋 电池供电 ({0}%)", bat.Percent);
                _tbPwrWatts.Text = string.Format("{0:F1} W", bat.RateWatts);
            }

            // NET
            _tbNetDown.Text = string.Format("↓ 下载: {0}", FormatRate(net.DownloadKBps));
            _tbNetUp.Text = string.Format("↑ 上传: {0}", FormatRate(net.UploadKBps));

            // ZeroTier
            if (zt.IsRunning)
            {
                _tbZtNode.Text = string.Format("本地节点 ID: {0}", zt.NodeId ?? "N/A");
                _tbZtStatus.Text = zt.PeerCount > 0
                    ? string.Format("客户端状态: 正常运行 (已发现 {0} 个对等节点)", zt.PeerCount)
                    : "客户端状态: 正常运行";
                if (zt.MoonCount > 0)
                {
                    _tbZtMoon.Text = string.Format("Moon 轨道: 直连已建立 ({0}ms)", zt.MinMoonLatency);
                    _tbZtMoon.Foreground = theme.AccentEmerald;
                }
                else
                {
                    _tbZtMoon.Text = "Moon 轨道: 中继探测中";
                    _tbZtMoon.Foreground = theme.AccentAmber;
                }
            }
            else
            {
                _tbZtNode.Text = "本地节点 ID: 未运行";
                _tbZtStatus.Text = "客户端状态: ZeroTier 守护进程未运行";
                _tbZtMoon.Text = "Moon 轨道: 未连接";
                _tbZtMoon.Foreground = theme.TextMuted;
            }
        }

        private static string FormatRate(double kbps)
        {
            if (kbps >= 1024)
                return string.Format("{0:F1} MB/s", kbps / 1024.0);
            return string.Format("{0:F0} KB/s", kbps);
        }
    }
}
