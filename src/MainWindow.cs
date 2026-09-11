using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SysMonitor
{
    public enum DockState
    {
        Floating,
        DockedLeft,
        DockedRight,
        DockedTop
    }

    public struct ScreenWorkArea
    {
        public double Left;
        public double Top;
        public double Right;
        public double Bottom;
        public bool HasScreenToLeft;
        public bool HasScreenToRight;
        public bool HasScreenToTop;
    }

    public class MainWindow : Window
    {
        private const double WidgetWidth = 126;
        private const double WidgetHeight = 126;
        private const double DockHandleWidth = 10;

        // UI Controls - Power
        private Border _bPowerTile;
        private TextBlock _tbPcWatts;    // Row 0: PC/CPU power (top)
        private TextBlock _tbBatteryPercent;
        private TextBlock _tbWatts;      // Row 1 right: battery state text
        private Run _runPcWatts;         // (unused – kept for compat)
        private Run _runSep;
        private Run _runBat;             // battery/AC compact state
        private Grid _pTrack;
        private Rectangle _rectBatteryFill;

        // UI Controls - Network
        private Border _bNetTile;
        private TextBlock _tbNetRates;
        private Image _imgFlag;
        private TextBlock _tbFlagEmoji;
        private TextBlock _tbPublicIp;

        // UI Controls - Tile ZeroTier
        private Border _bTile3;
        private Ellipse _elTile3Dot;
        private TextBlock _tbTile3Title;
        private TextBlock _tbTile3Status;
        private TextBlock _tbTile3Sub;

        // UI Controls - Tile Compute (CPU & RAM)
        private Border _bComputeTile;
        private TextBlock _tbComputeCpuVal;
        private TextBlock _tbComputeRamVal;
        private Grid _pComputeCpuTrack;
        private Rectangle _rectComputeCpuBar;
        private Grid _pComputeRamTrack;
        private Rectangle _rectComputeRamBar;

        // Docking & Animation
        private DockState _dockState = DockState.Floating;
        private DispatcherTimer _autoHideTimer;
        private Point _mouseDownScreenPos;
        private bool _isDragging = false;

        // Secondary Window & Telemetry Engine
        private DetailWindow _detailWin;
        private TelemetryEngine _engine;
        private bool _ztInstalled = true;
        private double _lastCpu = 0.0;

        // Cached Telemetry for Rebuild
        private PowerData _lastPower;
        private NetworkData _lastNet;
        private SystemLoadData _lastLoad;
        private ZeroTierData _lastZt;

        // Notification States
        private bool _alertedLowBattery = false;
        private bool _alertedFullBattery = false;
        private bool? _prevAcOnline = null;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private System.Windows.Forms.NotifyIcon _notifyIcon;

        private const string RunRegKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "SysMonitorWidget";

        public MainWindow()
        {
            Title = "SysMonitorWidget";
            Width = WidgetWidth;
            Height = WidgetHeight;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            Closed += delegate
            {
                try
                {
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                        _notifyIcon = null;
                    }
                }
                catch { }
                try { if (_detailWin != null) _detailWin.Close(); } catch { }
                try { Application.Current.Shutdown(); } catch { }
            };

            // Default position: top-right corner of screen
            Rect workArea = SystemParameters.WorkArea;
            Left = workArea.Right - WidgetWidth - 24;
            Top = workArea.Top + 60;

            _detailWin = new DetailWindow();
            _detailWin.RequestNotification = delegate(string title, string text, ToastType type, string iconEmoji)
            {
                ShowNotification(title, text, type, iconEmoji);
            };
            _detailWin.TokenConfigChanged += delegate
            {
                Dispatcher.Invoke(new Action(delegate
                {
                    RebuildUi();
                }));
            };

            BuildUi();
            SetupContextMenu();
            SetupAutoHideTimer();
            InitNotifyIcon();

            // Register mouse event handling ONCE to avoid duplicate handlers on theme/language switches
            MouseLeftButtonDown += OnMouseLeftDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftUp;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;

            AppSettings.SettingsChanged += delegate
            {
                Dispatcher.BeginInvoke(new Action(RebuildUi));
            };

            _engine = new TelemetryEngine();
            _engine.SystemLoadUpdated += OnSystemLoadUpdated;
            _engine.PowerUpdated += OnPowerUpdated;
            _engine.NetworkUpdated += OnNetworkUpdated;
            _engine.ZeroTierUpdated += OnZeroTierUpdated;
            _engine.MoonDirectAlert += OnMoonDirectAlert;
            _engine.MoonRelayAlert += OnMoonRelayAlert;
        }

        private void RebuildUi()
        {
            BuildUi();
            SetupContextMenu();
            if (_notifyIcon != null)
            {
                string title = I18n.Current.DetailWinTitle;
                _notifyIcon.Text = title.Length > 63 ? title.Substring(0, 63) : title;
            }
            if (_lastPower != null) OnPowerUpdated(_lastPower);
            if (_lastNet != null) OnNetworkUpdated(_lastNet);
            if (_lastLoad != null) OnSystemLoadUpdated(_lastLoad);
            if (_lastZt != null) OnZeroTierUpdated(_lastZt);
        }

        private void BuildUi()
        {
            ThemePalette theme = AppTheme.Current;
            TranslationSet i18n = I18n.Current;

            bool hasToken = _detailWin != null && _detailWin.HasConfiguredToken;

            System.Collections.Generic.List<string> rawActiveMods = AppSettings.GetActiveModules();
            System.Collections.Generic.List<string> activeMods = new System.Collections.Generic.List<string>();
            for (int i = 0; i < rawActiveMods.Count; i++)
            {
                string m = rawActiveMods[i];
                if (m == AppSettings.ModuleZeroTier && !hasToken)
                {
                    continue; // Token unconfigured: omit ZeroTier from homepage
                }
                activeMods.Add(m);
            }

            // Fallback safety: ensure at least one module is visible
            if (activeMods.Count == 0)
            {
                if (rawActiveMods.Contains(AppSettings.ModuleCompute)) activeMods.Add(AppSettings.ModuleCompute);
                else if (rawActiveMods.Contains(AppSettings.ModulePower)) activeMods.Add(AppSettings.ModulePower);
                else activeMods.Add(AppSettings.ModuleNetwork);
            }

            int count = Math.Max(1, activeMods.Count);
            const double padTop = 6;
            const double padBottom = 8;
            const double borderThick = 1;
            const double tileHeight = 38;
            const double tileGap = 4;
            double contentHeight = count * tileHeight + (count - 1) * tileGap;
            double targetHeight = borderThick * 2 + padTop + padBottom + contentHeight;
            Height = targetHeight;

            Border rootBorder = new Border
            {
                Width = WidgetWidth,
                Height = targetHeight,
                CornerRadius = new CornerRadius(16),
                Background = theme.WindowBg,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(borderThick),
                Padding = new Thickness(6, padTop, 6, padBottom)
            };

            Grid mainGrid = new Grid();
            int rowIdx = 0;
            for (int i = 0; i < activeMods.Count; i++)
            {
                string modKey = activeMods[i];
                Border tile = null;
                if (modKey == AppSettings.ModulePower)
                {
                    tile = BuildPowerTile(theme, i18n);
                }
                else if (modKey == AppSettings.ModuleNetwork)
                {
                    tile = BuildNetworkTile(theme, i18n);
                }
                else if (modKey == AppSettings.ModuleZeroTier)
                {
                    tile = BuildZeroTierTile(theme, i18n);
                }
                else if (modKey == AppSettings.ModuleCompute)
                {
                    tile = BuildComputeTile(theme, i18n);
                }

                if (tile != null)
                {
                    if (rowIdx > 0)
                    {
                        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
                        rowIdx++;
                    }
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
                    Grid.SetRow(tile, rowIdx);
                    mainGrid.Children.Add(tile);
                    rowIdx++;
                }
            }

            rootBorder.Child = mainGrid;
            Content = rootBorder;
        }

        private Border CreateComplicationBorder()
        {
            return new Border
            {
                Height = 38,
                Background = AppTheme.Current.CardBg,
                CornerRadius = new CornerRadius(7),
                BorderBrush = AppTheme.Current.BorderBrush,
                BorderThickness = new Thickness(0.8)
            };
        }

        private Border BuildPowerTile(ThemePalette theme, TranslationSet i18n)
        {
            _bPowerTile = CreateComplicationBorder();
            Grid pg = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3) });  // gap
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            pg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // left (battery% + bar)
            pg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                       // right (PC watts + state)

            // Top-left: ⚡ 24%
            StackPanel spBat = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            System.Windows.Shapes.Path iconBat = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 3.2,0 L 0.5,5.2 L 3.2,5.2 L 2.0,9.5 L 6.5,4.0 L 3.8,4.0 Z"),
                Fill = theme.TextSecondary,
                Width = 7,
                Height = 10,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            _tbBatteryPercent = new TextBlock
            {
                Text = "100%",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI")
            };
            spBat.Children.Add(iconBat);
            spBat.Children.Add(_tbBatteryPercent);
            Grid.SetRow(spBat, 0);
            Grid.SetColumn(spBat, 0);
            pg.Children.Add(spBat);

            // Top-right: 30.6W (PC power, primary color)
            _tbPcWatts = new TextBlock
            {
                Text = "--",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI")
            };
            Grid.SetRow(_tbPcWatts, 0);
            Grid.SetColumn(_tbPcWatts, 1);
            pg.Children.Add(_tbPcWatts);

            // Bottom-left: progress bar (stretches to fill column)
            _pTrack = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 3.5,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Rectangle trackBg = new Rectangle
            {
                Fill = theme.ProgressBarTrack,
                RadiusX = 1.75,
                RadiusY = 1.75,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _pTrack.Children.Add(trackBg);
            _rectBatteryFill = new Rectangle
            {
                Fill = theme.AccentBlue,
                RadiusX = 1.75,
                RadiusY = 1.75,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 20
            };
            _pTrack.Children.Add(_rectBatteryFill);
            _pTrack.SizeChanged += delegate
            {
                if (_lastPower != null && _pTrack.ActualWidth > 0)
                {
                    double fillW = (_lastPower.BatteryPercent / 100.0) * _pTrack.ActualWidth;
                    _rectBatteryFill.Width = Math.Max(2, Math.Min(_pTrack.ActualWidth, fillW));
                }
            };
            Grid.SetRow(_pTrack, 2);
            Grid.SetColumn(_pTrack, 0);
            pg.Children.Add(_pTrack);

            // Bottom-right: +37.7W / 市电 (battery state, state color)
            _tbWatts = new TextBlock
            {
                Text = "--",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI")
            };
            _runPcWatts = new Run("");
            _runSep = new Run("");
            _runBat = new Run("--");
            Grid.SetRow(_tbWatts, 2);
            Grid.SetColumn(_tbWatts, 1);
            pg.Children.Add(_tbWatts);

            _bPowerTile.Child = pg;
            return _bPowerTile;
        }

        private Border BuildNetworkTile(ThemePalette theme, TranslationSet i18n)
        {
            _bNetTile = CreateComplicationBorder();
            StackPanel ng = new StackPanel { Margin = new Thickness(6, 4, 6, 4) };

            _tbNetRates = new TextBlock
            {
                Text = "↓ 0.0K   ↑ 0.0K",
                FontSize = 9.5,
                FontWeight = FontWeights.Medium,
                Foreground = theme.TextSecondary,
                FontFamily = new FontFamily("Consolas, Segoe UI")
            };
            ng.Children.Add(_tbNetRates);

            StackPanel ipRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            _imgFlag = new Image
            {
                Width = 14,
                Height = 10,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = Visibility.Collapsed
            };
            _tbFlagEmoji = new TextBlock
            {
                Text = "🌐",
                FontSize = 9,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            ipRow.Children.Add(_imgFlag);
            ipRow.Children.Add(_tbFlagEmoji);

            _tbPublicIp = new TextBlock
            {
                Text = i18n.NetFetching,
                FontSize = 9.5,
                FontWeight = FontWeights.Medium,
                Foreground = theme.AccentBlue,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 85,
                FontFamily = new FontFamily("Consolas, Segoe UI")
            };
            ipRow.Children.Add(_tbPublicIp);
            ng.Children.Add(ipRow);

            _bNetTile.Child = ng;
            return _bNetTile;
        }

        private Border BuildZeroTierTile(ThemePalette theme, TranslationSet i18n)
        {
            _bTile3 = CreateComplicationBorder();
            Grid zg = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            zg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            zg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            zg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            zg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Top row: Dot + Moon title (left), Status (right)
            StackPanel ztLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _elTile3Dot = new Ellipse
            {
                Width = 6.5,
                Height = 6.5,
                Fill = theme.AccentEmerald,
                Margin = new Thickness(0, 0, 4.5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _tbTile3Title = new TextBlock
            {
                Text = "Moon",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI")
            };
            ztLeft.Children.Add(_elTile3Dot);
            ztLeft.Children.Add(_tbTile3Title);
            Grid.SetRow(ztLeft, 0);
            Grid.SetColumn(ztLeft, 0);
            zg.Children.Add(ztLeft);

            _tbTile3Status = new TextBlock
            {
                Text = "--",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = theme.AccentEmerald,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI")
            };
            Grid.SetRow(_tbTile3Status, 0);
            Grid.SetColumn(_tbTile3Status, 1);
            zg.Children.Add(_tbTile3Status);

            // Bottom row: Node ID / subtext - crisp high contrast
            _tbTile3Sub = new TextBlock
            {
                Text = "ZeroTier",
                FontSize = 9.5,
                FontWeight = FontWeights.Medium,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = new FontFamily("Consolas, Segoe UI")
            };
            Grid.SetRow(_tbTile3Sub, 1);
            Grid.SetColumn(_tbTile3Sub, 0);
            Grid.SetColumnSpan(_tbTile3Sub, 2);
            zg.Children.Add(_tbTile3Sub);

            _bTile3.Child = zg;
            return _bTile3;
        }

        private Border BuildComputeTile(ThemePalette theme, TranslationSet i18n)
        {
            _bComputeTile = CreateComplicationBorder();
            Grid cg = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            cg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3.5) });

            // Top Row: CPU {val}% (left) and RAM {val}% (right)
            Grid topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel spCpu = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock tbCpuLabel = new TextBlock
            {
                Text = "CPU ",
                FontSize = 9,
                FontWeight = FontWeights.Medium,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI")
            };
            _tbComputeCpuVal = new TextBlock
            {
                Text = "0.0%",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = theme.AccentBlue,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI")
            };
            spCpu.Children.Add(tbCpuLabel);
            spCpu.Children.Add(_tbComputeCpuVal);
            Grid.SetColumn(spCpu, 0);
            topGrid.Children.Add(spCpu);

            StackPanel spRam = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            TextBlock tbRamLabel = new TextBlock
            {
                Text = "RAM ",
                FontSize = 9,
                FontWeight = FontWeights.Medium,
                Foreground = theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI")
            };
            _tbComputeRamVal = new TextBlock
            {
                Text = "0%",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI")
            };
            spRam.Children.Add(tbRamLabel);
            spRam.Children.Add(_tbComputeRamVal);
            Grid.SetColumn(spRam, 1);
            topGrid.Children.Add(spRam);

            Grid.SetRow(topGrid, 0);
            cg.Children.Add(topGrid);

            // Bottom Row: Dual Mini Progress Bars with gap
            Grid barGrid = new Grid { Margin = new Thickness(0, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // CPU Bar
            _pComputeCpuTrack = new Grid { Height = 3.5, HorizontalAlignment = HorizontalAlignment.Stretch };
            Rectangle cpuBg = new Rectangle
            {
                Fill = theme.ProgressBarTrack,
                RadiusX = 1.75,
                RadiusY = 1.75,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _pComputeCpuTrack.Children.Add(cpuBg);
            _rectComputeCpuBar = new Rectangle
            {
                Fill = theme.AccentBlue,
                RadiusX = 1.75,
                RadiusY = 1.75,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 2
            };
            _pComputeCpuTrack.Children.Add(_rectComputeCpuBar);
            Grid.SetColumn(_pComputeCpuTrack, 0);
            barGrid.Children.Add(_pComputeCpuTrack);

            // RAM Bar
            _pComputeRamTrack = new Grid { Height = 3.5, HorizontalAlignment = HorizontalAlignment.Stretch };
            Rectangle ramBg = new Rectangle
            {
                Fill = theme.ProgressBarTrack,
                RadiusX = 1.75,
                RadiusY = 1.75,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _pComputeRamTrack.Children.Add(ramBg);
            _rectComputeRamBar = new Rectangle
            {
                Fill = theme.AccentEmerald,
                RadiusX = 1.75,
                RadiusY = 1.75,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 2
            };
            _pComputeRamTrack.Children.Add(_rectComputeRamBar);
            Grid.SetColumn(_pComputeRamTrack, 2);
            barGrid.Children.Add(_pComputeRamTrack);

            Grid.SetRow(barGrid, 1);
            cg.Children.Add(barGrid);

            _bComputeTile.Child = cg;
            return _bComputeTile;
        }

        private void SetupContextMenu()
        {
            ContextMenu cm = AppTheme.CreateModernContextMenu();
            cm.MinWidth = 210;

            TranslationSet i18n = I18n.Current;

            // 1. Detail MenuItem
            cm.Items.Add(AppTheme.CreateModernMenuItem(i18n.MenuDetail, delegate { ToggleDetailWindow(); }));

            // 2. Search Node & IP MenuItem
            cm.Items.Add(AppTheme.CreateModernMenuItem(i18n.MenuSearch, delegate { OpenNodeSearch(); }));

            // 3. Refresh GeoIP
            cm.Items.Add(AppTheme.CreateModernMenuItem(i18n.Lang == AppLanguage.Zh ? "⟳ 刷新公网出口与归属" : "⟳ Refresh Public IP & Geo", delegate
            {
                if (_engine != null) _engine.TriggerGeoIpRefresh();
            }));

            // Separator
            cm.Items.Add(AppTheme.CreateModernSeparator());

            // 4. Theme Toggle (Direct 1-Click Fast Toggle)
            string themeToggleHeader = AppSettings.Theme == ThemeMode.Dark ? i18n.ThemeLight : i18n.ThemeDark;
            cm.Items.Add(AppTheme.CreateModernMenuItem(themeToggleHeader, delegate
            {
                AppSettings.SetTheme(AppSettings.Theme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark);
            }));

            // 5. Language Toggle (Direct 1-Click Fast Toggle)
            string langToggleHeader = AppSettings.Language == AppLanguage.Zh ? "🇺🇸 English" : "🇨🇳 简体中文";
            cm.Items.Add(AppTheme.CreateModernMenuItem(langToggleHeader, delegate
            {
                AppSettings.SetLanguage(AppSettings.Language == AppLanguage.Zh ? AppLanguage.En : AppLanguage.Zh);
            }));

            // Separator
            cm.Items.Add(AppTheme.CreateModernSeparator());

            // 6. Auto-start Toggle Item
            bool isAuto = IsAutoStartEnabled();
            string autoText = (isAuto ? "✓ " : "   ") + i18n.MenuStartup;
            cm.Items.Add(AppTheme.CreateModernMenuItem(autoText, delegate
            {
                bool newState = !IsAutoStartEnabled();
                SetAutoStart(newState);
                SetupContextMenu();
            }, isAuto));

            // Separator
            cm.Items.Add(AppTheme.CreateModernSeparator());

            // 7. Exit MenuItem
            cm.Items.Add(AppTheme.CreateModernMenuItem(i18n.MenuExit, delegate
            {
                try { if (_detailWin != null) _detailWin.Close(); } catch { }
                Application.Current.Shutdown();
            }));

            ContextMenu = cm;
        }

        private void InitNotifyIcon()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                System.Drawing.Icon appIcon = null;
                try
                {
                    appIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                }
                catch { }

                if (appIcon == null && File.Exists("app.ico"))
                {
                    try { appIcon = new System.Drawing.Icon("app.ico"); } catch { }
                }

                if (appIcon == null)
                {
                    string fallbackIco = @"C:\Users\4955\.gemini\antigravity\scratch\SysMonitor_Native\app.ico";
                    if (File.Exists(fallbackIco))
                    {
                        try { appIcon = new System.Drawing.Icon(fallbackIco); } catch { }
                    }
                }

                if (appIcon == null)
                {
                    appIcon = System.Drawing.SystemIcons.Application;
                }

                _notifyIcon.Icon = appIcon;
                string title = I18n.Current.DetailWinTitle;
                _notifyIcon.Text = title.Length > 63 ? title.Substring(0, 63) : title;
                _notifyIcon.Visible = true;

                _notifyIcon.MouseClick += delegate(object sender, System.Windows.Forms.MouseEventArgs e)
                {
                    if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                            Show();
                            Activate();
                        }));
                    }
                    else if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            try
                            {
                                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                                SetForegroundWindow(helper.Handle);
                                PostMessage(helper.Handle, 0, IntPtr.Zero, IntPtr.Zero);
                            }
                            catch { }

                            if (ContextMenu != null)
                            {
                                ContextMenu.Placement = PlacementMode.MousePoint;
                                ContextMenu.IsOpen = true;
                            }
                        }));
                    }
                };

                _notifyIcon.DoubleClick += delegate
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        ToggleDetailWindow();
                    }));
                };

                _notifyIcon.BalloonTipClicked += delegate
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (_detailWin != null)
                        {
                            if (!_detailWin.IsVisible)
                            {
                                ToggleDetailWindow();
                            }
                            else
                            {
                                _detailWin.Activate();
                            }
                        }
                    }));
                };
            }
            catch { }
        }

        private void SetupAutoHideTimer()
        {
            _autoHideTimer = new DispatcherTimer();
            _autoHideTimer.Interval = TimeSpan.FromMilliseconds(400);
            _autoHideTimer.Tick += delegate
            {
                _autoHideTimer.Stop();
                if (!IsMouseOver && !_detailWin.IsVisible)
                {
                    CollapseToDock();
                }
            };
        }

        private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                _mouseDownScreenPos = PointToScreen(e.GetPosition(this));
                _isDragging = false;
                CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
            {
                Point currPos = PointToScreen(e.GetPosition(this));
                if (Math.Abs(currPos.X - _mouseDownScreenPos.X) > 4 || Math.Abs(currPos.Y - _mouseDownScreenPos.Y) > 4)
                {
                    _isDragging = true;
                    ReleaseMouseCapture();
                    if (_detailWin.IsVisible) _detailWin.Hide();
                    try
                    {
                        DragMove();
                    }
                    catch { }
                    finally
                    {
                        CheckDockingState();
                    }
                }
            }
        }

        private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            if (!_isDragging)
            {
                ToggleDetailWindow();
            }
            else
            {
                CheckDockingState();
            }
            _isDragging = false;
        }

        private void ToggleDetailWindow()
        {
            if (_detailWin.IsVisible)
            {
                _detailWin.Hide();
            }
            else
            {
                _detailWin.PositionNear(Left, Top, Width, Height);
                _detailWin.Show();
            }
        }

        private void OpenNodeSearch()
        {
            if (!_detailWin.IsVisible)
            {
                _detailWin.PositionNear(Left, Top, Width, Height);
                _detailWin.Show();
            }
            _detailWin.Activate();
            _detailWin.FocusSearch();
        }

        private ScreenWorkArea GetCurrentScreenInfo()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            double sx = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            double sy = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

            double testX;
            double testY;
            if (_dockState == DockState.DockedRight)
            {
                testX = Left + 2;
                testY = Top + Height / 2.0;
            }
            else if (_dockState == DockState.DockedLeft)
            {
                testX = Left + Width - 2;
                testY = Top + Height / 2.0;
            }
            else if (_dockState == DockState.DockedTop)
            {
                testX = Left + Width / 2.0;
                testY = Top + Height - 2;
            }
            else
            {
                testX = Left + Width / 2.0;
                testY = Top + Height / 2.0;
            }

            int physX = (int)(testX * sx);
            int physY = (int)(testY * sy);

            System.Windows.Forms.Screen cur = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(physX, physY));

            int rightPhys = cur.Bounds.Right + 12;
            int leftPhys = cur.Bounds.Left - 12;
            int topBoundaryPhys = cur.Bounds.Top - 12;
            int topPhys = (int)((Top + 10) * sy);
            int midPhys = physY;
            int botPhys = (int)((Top + Height - 10) * sy);

            int x1Phys = (int)((Left + 10) * sx);
            int x2Phys = physX;
            int x3Phys = (int)((Left + Width - 10) * sx);

            bool hasRight = false;
            bool hasLeft = false;
            bool hasTop = false;

            foreach (System.Windows.Forms.Screen s in System.Windows.Forms.Screen.AllScreens)
            {
                if (s.Bounds.Equals(cur.Bounds)) continue;
                if (s.Bounds.Contains(rightPhys, topPhys) || s.Bounds.Contains(rightPhys, midPhys) || s.Bounds.Contains(rightPhys, botPhys))
                    hasRight = true;
                if (s.Bounds.Contains(leftPhys, topPhys) || s.Bounds.Contains(leftPhys, midPhys) || s.Bounds.Contains(leftPhys, botPhys))
                    hasLeft = true;
                if (s.Bounds.Contains(x1Phys, topBoundaryPhys) || s.Bounds.Contains(x2Phys, topBoundaryPhys) || s.Bounds.Contains(x3Phys, topBoundaryPhys))
                    hasTop = true;
            }

            ScreenWorkArea area = new ScreenWorkArea
            {
                Left = cur.WorkingArea.Left / sx,
                Top = cur.WorkingArea.Top / sy,
                Right = cur.WorkingArea.Right / sx,
                Bottom = cur.WorkingArea.Bottom / sy,
                HasScreenToLeft = hasLeft,
                HasScreenToRight = hasRight,
                HasScreenToTop = hasTop
            };
            return area;
        }

        private void CheckDockingState()
        {
            ScreenWorkArea area = GetCurrentScreenInfo();

            if (Top + Height > area.Bottom) Top = area.Bottom - Height;
            if (Left < area.Left - WidgetWidth + DockHandleWidth) Left = area.Left;
            if (Left > area.Right) Left = area.Right - WidgetWidth;

            if (!area.HasScreenToTop && Top <= area.Top + 12)
            {
                _dockState = DockState.DockedTop;
                CollapseToDock();
            }
            else if (!area.HasScreenToRight && Left >= area.Right - WidgetWidth - 12)
            {
                _dockState = DockState.DockedRight;
                CollapseToDock();
            }
            else if (!area.HasScreenToLeft && Left <= area.Left + 12)
            {
                _dockState = DockState.DockedLeft;
                CollapseToDock();
            }
            else
            {
                _dockState = DockState.Floating;
                if (Top < area.Top) Top = area.Top;
            }
        }

        private void CollapseToDock()
        {
            if (_dockState == DockState.Floating) return;

            ScreenWorkArea area = GetCurrentScreenInfo();
            if (_dockState == DockState.DockedTop)
            {
                Top = area.Top - Height + DockHandleWidth;
            }
            else if (_dockState == DockState.DockedRight)
            {
                Left = area.Right - DockHandleWidth;
            }
            else if (_dockState == DockState.DockedLeft)
            {
                Left = area.Left - Width + DockHandleWidth;
            }
        }

        private void ExpandFromDock()
        {
            if (_dockState == DockState.Floating) return;

            ScreenWorkArea area = GetCurrentScreenInfo();
            if (_dockState == DockState.DockedTop)
            {
                Top = area.Top;
            }
            else if (_dockState == DockState.DockedRight)
            {
                Left = area.Right - Width;
            }
            else if (_dockState == DockState.DockedLeft)
            {
                Left = area.Left;
            }
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            _autoHideTimer.Stop();
            if (_dockState != DockState.Floating)
            {
                ExpandFromDock();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_dockState != DockState.Floating && !_detailWin.IsVisible)
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Start();
            }
        }

        private void OnSystemLoadUpdated(SystemLoadData data)
        {
            _lastLoad = data;
            Dispatcher.Invoke(new Action(delegate
            {
                _lastCpu = data.CpuPercent;
                if (_detailWin != null) _detailWin.UpdateSystemLoad(data);

                // Update dedicated Compute Tile if active
                if (_tbComputeCpuVal != null)
                {
                    _tbComputeCpuVal.Text = string.Format("{0:0.0}%", data.CpuPercent);
                    if (data.CpuPercent > 80.0)
                        _tbComputeCpuVal.Foreground = AppTheme.Current.AccentRed;
                    else if (data.CpuPercent > 50.0)
                        _tbComputeCpuVal.Foreground = AppTheme.Current.AccentAmber;
                    else
                        _tbComputeCpuVal.Foreground = AppTheme.Current.AccentBlue;
                }

                if (_tbComputeRamVal != null)
                {
                    _tbComputeRamVal.Text = string.Format("{0}%", data.RamPercent);
                    if (data.RamPercent > 85.0)
                        _tbComputeRamVal.Foreground = AppTheme.Current.AccentRed;
                    else
                        _tbComputeRamVal.Foreground = AppTheme.Current.TextPrimary;
                }

                if (_pComputeCpuTrack != null && _rectComputeCpuBar != null)
                {
                    double trackW = _pComputeCpuTrack.ActualWidth > 0 ? _pComputeCpuTrack.ActualWidth : 48.0;
                    double fillW = (data.CpuPercent / 100.0) * trackW;
                    if (fillW < 2) fillW = 2;
                    if (fillW > trackW) fillW = trackW;
                    _rectComputeCpuBar.Width = fillW;
                }

                if (_pComputeRamTrack != null && _rectComputeRamBar != null)
                {
                    double trackW = _pComputeRamTrack.ActualWidth > 0 ? _pComputeRamTrack.ActualWidth : 48.0;
                    double fillW = (data.RamPercent / 100.0) * trackW;
                    if (fillW < 2) fillW = 2;
                    if (fillW > trackW) fillW = trackW;
                    _rectComputeRamBar.Width = fillW;
                }

                if (_bComputeTile != null)
                {
                    TranslationSet i18n = I18n.Current;
                    _bComputeTile.ToolTip = string.Format("{0}: {1:0.0}%\n{2}: {3}% ({4:0.0} / {5:0.0} GB)",
                        i18n.CpuUsage, data.CpuPercent,
                        i18n.Memory, data.RamPercent, data.RamUsedGb, data.RamTotalGb);
                }
            }));
        }

        private void OnPowerUpdated(PowerData data)
        {
            _lastPower = data;
            Dispatcher.Invoke(new Action(delegate
            {
                if (_tbBatteryPercent != null)
                {
                    // battery %
                    _tbBatteryPercent.Text = string.Format("{0}%", data.BatteryPercent);
                    SolidColorBrush pBrush = data.GetStateBrush(AppTheme.Current);
                    if (_rectBatteryFill != null)
                    {
                        _rectBatteryFill.Fill = pBrush;
                        double trackW = (_pTrack != null && _pTrack.ActualWidth > 0) ? _pTrack.ActualWidth : 44.0;
                        double fillW = (data.BatteryPercent / 100.0) * trackW;
                        if (fillW < 2) fillW = 2;
                        if (fillW > trackW) fillW = trackW;
                        _rectBatteryFill.Width = fillW;
                    }

                    // Top-right: PC功率
                    if (_tbPcWatts != null)
                    {
                        if (data.CpuWatts > 0.5)
                        {
                            _tbPcWatts.Text = string.Format("{0:0.0}W", data.CpuWatts);
                            _tbPcWatts.Foreground = AppTheme.Current.TextPrimary;
                        }
                        else
                        {
                            _tbPcWatts.Text = "--";
                            _tbPcWatts.Foreground = AppTheme.Current.TextSecondary;
                        }
                    }

                    // Bottom-right: 电池充放电状态
                    if (_tbWatts != null)
                    {
                        _tbWatts.Text = data.GetCompactStatusText(I18n.Current);
                        _tbWatts.Foreground = pBrush;
                    }

                    if (_bPowerTile != null)
                    {
                        TranslationSet i18n = I18n.Current;
                        string tip;
                        if (!data.HasBattery)
                        {
                            tip = string.Format("{0}: {1}\n{2}: {3:0.0} W", 
                                i18n.PowerSupply, i18n.PowerDesktop, i18n.Power, data.CpuWatts);
                        }
                        else if (data.IsAcOnline)
                        {
                            if (data.IsCharging)
                            {
                                tip = string.Format("{0}: {1}\n{2}: +{3:0.0} W\nCPU: {4:0.0} W\n{5}: {6}",
                                    i18n.PowerSupply, i18n.PowerAcCharging,
                                    i18n.PowerCharging, Math.Abs(data.Watts),
                                    data.CpuWatts,
                                    i18n.EstBatteryLife, data.GetEstimatedTimeText(i18n));
                            }
                            else
                            {
                                string batDesc = data.BatteryPercent >= 95 ? i18n.PowerBatteryProtected : i18n.PowerBatteryConserve;
                                tip = string.Format("{0}: {1}\n{2}: {3} ({4})\nCPU: {5:0.0} W",
                                    i18n.PowerSupply, i18n.PowerAcBypass,
                                    i18n.BatteryCapacity, data.BatteryPercent + "%", batDesc,
                                    data.CpuWatts);
                            }
                        }
                        else
                        {
                            tip = string.Format("{0}: {1}\n{2}: {3:0.0} W\nCPU: {4:0.0} W\n{5}: {6}",
                                i18n.PowerSupply, i18n.RunningOnBattery,
                                i18n.Power, Math.Abs(data.Watts),
                                data.CpuWatts,
                                i18n.EstBatteryLife, data.GetEstimatedTimeText(i18n));
                        }
                        _bPowerTile.ToolTip = tip;
                    }
                }

                // Power state transition notifications (AC plugged in / unplugged)
                if (_prevAcOnline.HasValue && _prevAcOnline.Value != data.IsAcOnline && data.HasBattery)
                {
                    TranslationSet i18n = I18n.Current;
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
                        TranslationSet i18n = I18n.Current;
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
                        TranslationSet i18n = I18n.Current;
                        ShowNotification(i18n.NotifyBatteryFullTitle, i18n.NotifyBatteryFullText, ToastType.Success, "🔋");
                    }
                }
                else if (!data.IsAcOnline || data.BatteryPercent < 98)
                {
                    _alertedFullBattery = false;
                }

                if (_detailWin != null) _detailWin.UpdatePower(data);
            }));
        }

        private void OnNetworkUpdated(NetworkData data)
        {
            _lastNet = data;
            Dispatcher.Invoke(new Action(delegate
            {
                if (_tbNetRates != null)
                {
                    _tbNetRates.Text = string.Format("{0}   {1}", data.DownSpeedStr, data.UpSpeedStr);
                }
                if (_tbPublicIp != null)
                {
                    _tbPublicIp.Text = string.IsNullOrEmpty(data.PublicIp) ? I18n.Current.NetFetching : data.PublicIp;
                }

                if (_imgFlag != null && _tbFlagEmoji != null)
                {
                    if (!string.IsNullOrEmpty(data.CountryCode))
                    {
                        BitmapSource flag = FlagAssets.GetFlagImage(data.CountryCode);
                        if (flag != null)
                        {
                            _imgFlag.Source = flag;
                            _imgFlag.Visibility = Visibility.Visible;
                            _tbFlagEmoji.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            _imgFlag.Visibility = Visibility.Collapsed;
                            _tbFlagEmoji.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        _imgFlag.Visibility = Visibility.Collapsed;
                        _tbFlagEmoji.Visibility = Visibility.Visible;
                    }
                }

                if (_detailWin != null) _detailWin.UpdateNetwork(data);
            }));
        }

        private void OnZeroTierUpdated(ZeroTierData data)
        {
            _lastZt = data;
            Dispatcher.Invoke(new Action(delegate
            {
                _ztInstalled = data.IsInstalled;
                bool hasToken = _detailWin != null && _detailWin.HasConfiguredToken;

                if (_tbTile3Title != null && _tbTile3Status != null && _elTile3Dot != null)
                {
                    if (!data.IsInstalled)
                    {
                        _elTile3Dot.Fill = AppTheme.Current.TextMuted;
                        _tbTile3Title.Text = "ZeroTier";
                        _tbTile3Title.Foreground = AppTheme.Current.TextSecondary;
                        _tbTile3Status.Text = I18n.Current.ZtNotInstalled;
                        _tbTile3Status.Foreground = AppTheme.Current.TextMuted;
                        if (_tbTile3Sub != null) _tbTile3Sub.Text = "--";
                    }
                    else if (!hasToken)
                    {
                        _elTile3Dot.Fill = AppTheme.Current.TextMuted;
                        _tbTile3Title.Text = "ZeroTier";
                        _tbTile3Title.Foreground = AppTheme.Current.TextSecondary;
                        _tbTile3Status.Text = I18n.Current.NoTokenHint;
                        _tbTile3Status.Foreground = AppTheme.Current.TextMuted;
                        if (_tbTile3Sub != null) _tbTile3Sub.Text = !string.IsNullOrEmpty(data.LocalNodeId) ? ("Node: " + data.LocalNodeId) : "--";
                    }
                    else
                    {
                        SolidColorBrush ztBrush = data.GetSummaryBrush(AppTheme.Current);
                        string ztText = data.GetSummaryText(I18n.Current);

                        if (data.HasDroppedMoons)
                        {
                            _elTile3Dot.Fill = AppTheme.Current.AccentRed;
                            _tbTile3Title.Text = "Moon";
                            _tbTile3Title.Foreground = AppTheme.Current.AccentRed;
                            _tbTile3Status.Text = ztText;
                            _tbTile3Status.Foreground = AppTheme.Current.AccentRed;
                        }
                        else
                        {
                            _elTile3Dot.Fill = ztBrush;
                            _tbTile3Title.Foreground = AppTheme.Current.TextPrimary;
                            _tbTile3Status.Text = ztText;
                            _tbTile3Status.Foreground = ztBrush;

                            if (data.TotalMoons > 0)
                            {
                                _tbTile3Title.Text = string.Format("Moon {0}/{1}", data.DirectMoons, data.TotalMoons);
                            }
                            else
                            {
                                _tbTile3Title.Text = "Moon";
                            }
                        }

                        if (_tbTile3Sub != null)
                        {
                            _tbTile3Sub.Foreground = AppTheme.Current.TextSecondary;
                            if (!string.IsNullOrEmpty(data.LocalNodeId))
                                _tbTile3Sub.Text = "Node: " + data.LocalNodeId;
                            else
                                _tbTile3Sub.Text = "ZeroTier Mesh";
                        }
                    }
                }

                if (_detailWin != null) _detailWin.UpdateZeroTier(data);
            }));
        }

        public void ShowNotification(string title, string text, ToastType type = ToastType.Info, string iconEmoji = "ℹ️")
        {
            Dispatcher.Invoke(new Action(delegate
            {
                try
                {
                    ToastNotification.Show(title, text, iconEmoji, type, delegate
                    {
                        if (_detailWin != null)
                        {
                            if (!_detailWin.IsVisible)
                            {
                                ToggleDetailWindow();
                            }
                            else
                            {
                                _detailWin.Activate();
                            }
                        }
                    });
                }
                catch { }
            }));
        }

        private void OnMoonDirectAlert(string moonAddr, int latency)
        {
            if (_detailWin == null || !_detailWin.HasConfiguredToken) return;
            Dispatcher.Invoke(new Action(delegate
            {
                try
                {
                    TranslationSet i18n = I18n.Current;
                    string title = i18n.NotifyMoonDirectTitle;
                    string msg = latency >= 0
                        ? string.Format(i18n.NotifyMoonDirectFormat, moonAddr, latency)
                        : (i18n.Lang == AppLanguage.Zh ? string.Format("节点 {0} 已直连", moonAddr) : string.Format("Node {0} is Direct", moonAddr));
                    ShowNotification(title, msg, ToastType.Success, "⚡");
                }
                catch { }
            }));
        }

        private void OnMoonRelayAlert(string moonAddr)
        {
            if (_detailWin == null || !_detailWin.HasConfiguredToken) return;
            Dispatcher.Invoke(new Action(delegate
            {
                try
                {
                    TranslationSet i18n = I18n.Current;
                    string title = i18n.NotifyMoonRelayTitle;
                    string msg = string.Format(i18n.NotifyMoonRelayFormat, moonAddr);
                    ShowNotification(title, msg, ToastType.Warning, "🔄");
                }
                catch { }
            }));
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegKey, false))
                {
                    if (key == null) return false;
                    return key.GetValue(AppName) != null;
                }
            }
            catch { return false; }
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegKey, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key.SetValue(AppName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch { }
        }
    }
}
