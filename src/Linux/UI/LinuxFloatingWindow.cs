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

        // Complication Tile Controls - Compute
        private Border _bComputeTile;
        private TextBlock _tbComputeCpuVal;
        private TextBlock _tbComputeRamVal;
        private Border _pComputeCpuTrack;
        private Border _pComputeRamTrack;
        private Border _rectComputeCpuBar;
        private Border _rectComputeRamBar;

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

        // Notification alert state
        private bool? _prevAcOnline = null;
        private bool _alertedLowBattery = false;
        private bool _alertedFullBattery = false;

        public LinuxFloatingWindow()
        {
            Title = "SysMonitorWidget";
            Width = WidgetWidth;
            RequestedThemeVariant = LinuxSettings.IsDark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
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

            LinuxSettings.SettingsChanged += () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    RequestedThemeVariant = LinuxSettings.IsDark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
                    BuildUi();
                    SetupContextMenu();
                    ReplayTelemetry();
                });
            };

            Opened += (s, e) =>
            {
                try
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
                    _engine.MoonDirectAlert += (addr, lat) => Dispatcher.UIThread.Post(() =>
                    {
                        var i18n = I18n.Current;
                        ShowNotification(i18n.NotifyMoonDirectTitle, string.Format(i18n.NotifyMoonDirectFormat, addr, lat), ToastType.Success, "⚡");
                    });
                    _engine.MoonRelayAlert += (addr) => Dispatcher.UIThread.Post(() =>
                    {
                        var i18n = I18n.Current;
                        ShowNotification(i18n.NotifyMoonRelayTitle, string.Format(i18n.NotifyMoonRelayFormat, addr), ToastType.Warning, "🔄");
                    });
                }
                catch { }
            };
        }

        public LinuxTelemetryEngine Engine => _engine;

        public void ShowNotification(string title, string text, ToastType type = ToastType.Info, string iconEmoji = "ℹ️")
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    LinuxToastNotification.Show(title, text, iconEmoji, type, () =>
                    {
                        if (_detailWindow != null)
                        {
                            if (!_detailWindow.IsVisible)
                            {
                                ToggleDetailWindow();
                            }
                            else
                            {
                                _detailWindow.Activate();
                            }
                        }
                    });
                }
                catch { }
            });
        }

        private void BuildUi()
        {
            var theme = LinuxTheme.Current;
            var i18n = I18n.Current;

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

            var order = LinuxSettings.ModuleOrder;
            var enabled = LinuxSettings.ModuleEnabled;

            foreach (var mod in order)
            {
                if (!enabled.Contains(mod)) continue;

                if (mod == LinuxSettings.ModulePower)
                {
                    _spMain.Children.Add(BuildPowerTile(theme, i18n));
                }
                else if (mod == LinuxSettings.ModuleNetwork)
                {
                    _spMain.Children.Add(BuildNetworkTile(theme, i18n));
                }
                else if (mod == LinuxSettings.ModuleZeroTier)
                {
                    _spMain.Children.Add(BuildZeroTierTile(theme, i18n));
                }
                else if (mod == LinuxSettings.ModuleCompute)
                {
                    _spMain.Children.Add(BuildComputeTile(theme, i18n));
                }
            }

            _rootBorder.Child = _spMain;
            Content = _rootBorder;
        }

        private Border BuildPowerTile(LinuxThemePalette theme, TranslationSet i18n)
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
            _pBatteryTrack.SizeChanged += (s, e) =>
            {
                if (_lastPower != null && e.NewSize.Width > 0)
                {
                    double fillW = Math.Max(2, Math.Min(e.NewSize.Width, (_lastPower.BatteryPercent / 100.0) * e.NewSize.Width));
                    _rectBatteryFill.Width = fillW;
                }
            };
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

        private Border BuildNetworkTile(LinuxThemePalette theme, TranslationSet i18n)
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
                Text = i18n.NetFetching,
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

        private Border BuildZeroTierTile(LinuxThemePalette theme, TranslationSet i18n)
        {
            _bZtTile = LinuxTheme.CreateComplicationBorder();
            var grid = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // Row 0: Dot + Moon (left), Status (right)
            var leftGrid = new Grid { VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
            leftGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            leftGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

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
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_elZtDot, 0);
            Grid.SetColumn(_tbZtTitle, 1);
            leftGrid.Children.Add(_elZtDot);
            leftGrid.Children.Add(_tbZtTitle);
            Grid.SetRow(leftGrid, 0);
            Grid.SetColumn(leftGrid, 0);
            grid.Children.Add(leftGrid);

            _tbZtStatus = new TextBlock
            {
                Text = "--",
                FontSize = 10.5,
                FontWeight = FontWeight.Bold,
                Foreground = theme.AccentEmerald,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(4, 0, 0, 0),
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

        private Border BuildComputeTile(LinuxThemePalette theme, TranslationSet i18n)
        {
            _bComputeTile = LinuxTheme.CreateComplicationBorder();
            var grid = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.RowDefinitions.Add(new RowDefinition(3, GridUnitType.Pixel));
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

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
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            spRam.Children.Add(tbRamLabel);
            spRam.Children.Add(_tbComputeRamVal);
            Grid.SetColumn(spRam, 1);
            topGrid.Children.Add(spRam);

            Grid.SetRow(topGrid, 0);
            grid.Children.Add(topGrid);

            var barGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(6, GridUnitType.Pixel));
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            _pComputeCpuTrack = new Border
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
                Width = 2,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _pComputeCpuTrack.Child = _rectComputeCpuBar;
            _pComputeCpuTrack.SizeChanged += (s, e) =>
            {
                if (_lastLoad != null && e.NewSize.Width > 0)
                {
                    _rectComputeCpuBar.Width = Math.Max(2, Math.Min(e.NewSize.Width, (_lastLoad.CpuPercent / 100.0) * e.NewSize.Width));
                }
            };
            Grid.SetColumn(_pComputeCpuTrack, 0);
            barGrid.Children.Add(_pComputeCpuTrack);

            _pComputeRamTrack = new Border
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
                Width = 2,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _pComputeRamTrack.Child = _rectComputeRamBar;
            _pComputeRamTrack.SizeChanged += (s, e) =>
            {
                if (_lastLoad != null && e.NewSize.Width > 0)
                {
                    _rectComputeRamBar.Width = Math.Max(2, Math.Min(e.NewSize.Width, (_lastLoad.RamPercent / 100.0) * e.NewSize.Width));
                }
            };
            Grid.SetColumn(_pComputeRamTrack, 2);
            barGrid.Children.Add(_pComputeRamTrack);

            Grid.SetRow(barGrid, 2);
            grid.Children.Add(barGrid);

            _bComputeTile.Child = grid;
            return _bComputeTile;
        }

        private void SetupContextMenu()
        {
            var theme = LinuxTheme.Current;
            var i18n = I18n.Current;
            var menu = new ContextMenu();

            var miDetail = new MenuItem { Header = "📋 " + i18n.MenuDetail };
            miDetail.Click += (s, e) => ToggleDetailWindow();
            menu.Items.Add(miDetail);

            var miSearch = new MenuItem { Header = "🔍 " + i18n.MenuSearch };
            miSearch.Click += (s, e) =>
            {
                if (_detailWindow == null || !_detailWindow.IsVisible) ToggleDetailWindow();
                _detailWindow?.FocusSearch();
            };
            menu.Items.Add(miSearch);

            var miRefresh = new MenuItem { Header = i18n.Lang == AppLanguage.Zh ? "⟳ 刷新公网出口与遥测" : "⟳ Refresh Public IP & Telemetry" };
            miRefresh.Click += (s, e) => _engine?.TriggerGeoIpRefresh();
            menu.Items.Add(miRefresh);

            menu.Items.Add(new Separator());

            var miTheme = new MenuItem { Header = LinuxSettings.IsDark ? i18n.ThemeLight : i18n.ThemeDark };
            miTheme.Click += (s, e) => LinuxSettings.SetTheme(!LinuxSettings.IsDark);
            menu.Items.Add(miTheme);

            var miLang = new MenuItem { Header = LinuxSettings.Language == AppLanguage.Zh ? "🇺🇸 English" : "🇨🇳 简体中文" };
            miLang.Click += (s, e) => LinuxSettings.SetLanguage(LinuxSettings.Language == AppLanguage.Zh ? AppLanguage.En : AppLanguage.Zh);
            menu.Items.Add(miLang);

            menu.Items.Add(new Separator());

            var miExit = new MenuItem { Header = "🚪 " + i18n.MenuExit };
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
            var theme = LinuxTheme.Current;

            if (_tbComputeCpuVal != null)
            {
                _tbComputeCpuVal.Text = $"{data.CpuPercent:0.0}%";
                _tbComputeCpuVal.Foreground = data.CpuPercent > 80.0 ? theme.AccentRed : (data.CpuPercent > 50.0 ? theme.AccentAmber : theme.AccentBlue);
            }
            if (_rectComputeCpuBar != null)
            {
                _rectComputeCpuBar.Background = data.CpuPercent > 80.0 ? theme.AccentRed : (data.CpuPercent > 50.0 ? theme.AccentAmber : theme.AccentBlue);
                double trackW = (_pComputeCpuTrack != null && _pComputeCpuTrack.Bounds.Width > 0) ? _pComputeCpuTrack.Bounds.Width : 60.0;
                _rectComputeCpuBar.Width = Math.Max(2, Math.Min(trackW, (data.CpuPercent / 100.0) * trackW));
            }

            if (_tbComputeRamVal != null)
            {
                _tbComputeRamVal.Text = $"{data.RamPercent}%";
                _tbComputeRamVal.Foreground = data.RamPercent > 85 ? theme.AccentRed : theme.TextPrimary;
            }
            if (_rectComputeRamBar != null)
            {
                _rectComputeRamBar.Background = data.RamPercent > 85 ? theme.AccentRed : theme.AccentEmerald;
                double trackW = (_pComputeRamTrack != null && _pComputeRamTrack.Bounds.Width > 0) ? _pComputeRamTrack.Bounds.Width : 60.0;
                _rectComputeRamBar.Width = Math.Max(2, Math.Min(trackW, (data.RamPercent / 100.0) * trackW));
            }

            _detailWindow?.UpdateSystemLoad(data);
        }

        private void OnPowerUpdated(LinuxPowerData data)
        {
            _lastPower = data;
            var theme = LinuxTheme.Current;
            var i18n = I18n.Current;

            if (_tbBatteryPercent != null)
                _tbBatteryPercent.Text = $"{data.BatteryPercent}%";

            if (_tbPcWatts != null)
                _tbPcWatts.Text = data.CpuWatts > 0.5 ? $"{data.CpuWatts:0.0}W" : "--";

            bool isEmerald = data.StateKind == LinuxPowerStateKind.ChargedFull
                          || data.StateKind == LinuxPowerStateKind.AcDirect
                          || data.StateKind == LinuxPowerStateKind.DesktopAc
                          || data.BatteryPercent >= 98;

            IBrush pBrush = isEmerald
                ? theme.AccentEmerald
                : (data.StateKind == LinuxPowerStateKind.ChargingFast ? theme.AccentBlue : theme.AccentAmber);

            if (_rectBatteryFill != null)
            {
                _rectBatteryFill.Background = pBrush;
                double trackW = (_pBatteryTrack != null && _pBatteryTrack.Bounds.Width > 0) ? _pBatteryTrack.Bounds.Width : 60.0;
                double fillW = Math.Max(2, Math.Min(trackW, (data.BatteryPercent / 100.0) * trackW));
                _rectBatteryFill.Width = fillW;
            }

            if (_tbWatts != null)
            {
                _tbWatts.Text = data.GetCompactStatusText(i18n);
                _tbWatts.Foreground = pBrush;
            }

            // Power state transition notifications (AC plugged in / unplugged)
            if (_prevAcOnline.HasValue && _prevAcOnline.Value != data.IsAcOnline && data.HasBattery)
            {
                if (data.IsAcOnline)
                {
                    ShowNotification(i18n.NotifyAcConnectedTitle, i18n.NotifyAcConnectedText, ToastType.Info, "⚡");
                }
                else
                {
                    ShowNotification(i18n.NotifyBatteryModeTitle, string.Format(i18n.NotifyBatteryModeFormat, data.BatteryPercent), ToastType.Info, "🔋");
                }
            }
            _prevAcOnline = data.IsAcOnline;

            // Low battery alert (<= 20%)
            if (!data.IsAcOnline && data.BatteryPercent <= 20)
            {
                if (!_alertedLowBattery)
                {
                    _alertedLowBattery = true;
                    ShowNotification(i18n.NotifyLowBatteryTitle, string.Format(i18n.NotifyLowBatteryFormat, data.BatteryPercent), ToastType.Warning, "🪫");
                }
            }
            else if (data.IsAcOnline || data.BatteryPercent > 25)
            {
                _alertedLowBattery = false;
            }

            // Battery full alert (100%)
            if (data.IsAcOnline && data.BatteryPercent >= 100)
            {
                if (!_alertedFullBattery)
                {
                    _alertedFullBattery = true;
                    ShowNotification(i18n.NotifyBatteryFullTitle, i18n.NotifyBatteryFullText, ToastType.Success, "🔋");
                }
            }
            else if (!data.IsAcOnline || data.BatteryPercent < 98)
            {
                _alertedFullBattery = false;
            }

            _detailWindow?.UpdatePower(data);
        }

        private void OnNetworkUpdated(LinuxNetworkData data)
        {
            _lastNet = data;
            var i18n = I18n.Current;

            if (_tbNetRates != null)
                _tbNetRates.Text = $"{data.DownSpeedStr}   {data.UpSpeedStr}";

            if (_tbPublicIp != null)
                _tbPublicIp.Text = string.IsNullOrEmpty(data.PublicIp) ? i18n.NetFetching : data.PublicIp;

            if (_tbFlagEmoji != null)
                _tbFlagEmoji.Text = LinuxTelemetryEngine.CountryCodeToEmoji(data.CountryCode);

            _detailWindow?.UpdateNetwork(data);
        }

        private void OnZeroTierUpdated(LinuxZeroTierData data)
        {
            _lastZt = data;
            var theme = LinuxTheme.Current;
            var i18n = I18n.Current;

            if (_elZtDot != null && _tbZtTitle != null && _tbZtStatus != null && _tbZtSub != null)
            {
                if (!data.IsRunning)
                {
                    _elZtDot.Background = theme.TextMuted;
                    _tbZtTitle.Text = "ZeroTier";
                    _tbZtStatus.Text = i18n.ZtOffline;
                    _tbZtStatus.Foreground = theme.TextMuted;
                    _tbZtSub.Text = !string.IsNullOrEmpty(data.LocalNodeId) ? ("Node: " + data.LocalNodeId) : "ZeroTier Mesh";
                }
                else if (data.HasDroppedMoons)
                {
                    _elZtDot.Background = theme.AccentRed;
                    _tbZtTitle.Text = "Moon";
                    _tbZtStatus.Text = i18n.Dropped;
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
                        _tbZtStatus.Text = isDirect ? (data.MinLatency >= 0 ? $"{data.MinLatency}ms" : i18n.Direct) : i18n.Relay;
                    }
                    else
                    {
                        _tbZtTitle.Text = "ZeroTier";
                        _tbZtStatus.Text = i18n.ZtOnline;
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
