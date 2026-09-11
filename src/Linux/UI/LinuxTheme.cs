using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace SysMonitor.Linux.UI
{
    public class LinuxThemePalette
    {
        public bool IsDark { get; set; }

        public IBrush WindowBg { get; set; }
        public IBrush CardBg { get; set; }
        public IBrush CardHoverBg { get; set; }
        public IBrush InnerTileBg { get; set; }
        public IBrush BorderBrush { get; set; }
        public IBrush BorderMuted { get; set; }
        public IBrush InputBg { get; set; }
        public IBrush InputBorder { get; set; }

        public IBrush TextPrimary { get; set; }
        public IBrush TextSecondary { get; set; }
        public IBrush TextMuted { get; set; }
        public IBrush TextDim { get; set; }

        public IBrush AccentBlue { get; set; }
        public IBrush AccentEmerald { get; set; }
        public IBrush AccentAmber { get; set; }
        public IBrush AccentRed { get; set; }
        public IBrush ProgressBarTrack { get; set; }

        public static LinuxThemePalette CreateDark()
        {
            return new LinuxThemePalette
            {
                IsDark = true,
                WindowBg = new SolidColorBrush(Color.FromRgb(13, 17, 23)),          // #0d1117
                CardBg = new SolidColorBrush(Color.FromRgb(22, 27, 34)),            // #161b22
                CardHoverBg = new SolidColorBrush(Color.FromRgb(33, 38, 45)),       // #21262d
                InnerTileBg = new SolidColorBrush(Color.FromRgb(13, 17, 23)),       // #0d1117
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),       // #30363d
                BorderMuted = new SolidColorBrush(Color.FromRgb(33, 38, 45)),       // #21262d
                InputBg = new SolidColorBrush(Color.FromRgb(13, 17, 23)),           // #0d1117
                InputBorder = new SolidColorBrush(Color.FromRgb(48, 54, 61)),       // #30363d

                TextPrimary = new SolidColorBrush(Color.FromRgb(240, 246, 252)),    // #f0f6fc
                TextSecondary = new SolidColorBrush(Color.FromRgb(203, 213, 225)),  // #cbd5e1
                TextMuted = new SolidColorBrush(Color.FromRgb(148, 163, 184)),      // #94a3b8
                TextDim = new SolidColorBrush(Color.FromRgb(100, 116, 139)),        // #64748b

                AccentBlue = new SolidColorBrush(Color.FromRgb(88, 166, 255)),     // #58a6ff
                AccentEmerald = new SolidColorBrush(Color.FromRgb(16, 185, 129)),  // #10b981
                AccentAmber = new SolidColorBrush(Color.FromRgb(245, 158, 11)),    // #f59e0b
                AccentRed = new SolidColorBrush(Color.FromRgb(248, 81, 73)),       // #f85149
                ProgressBarTrack = new SolidColorBrush(Color.FromRgb(33, 38, 45))  // #21262d
            };
        }

        public static LinuxThemePalette CreateLight()
        {
            return new LinuxThemePalette
            {
                IsDark = false,
                WindowBg = new SolidColorBrush(Color.FromRgb(246, 248, 250)),       // #f6f8fa
                CardBg = new SolidColorBrush(Color.FromRgb(255, 255, 255)),         // #ffffff
                CardHoverBg = new SolidColorBrush(Color.FromRgb(241, 245, 249)),    // #f1f5f9
                InnerTileBg = new SolidColorBrush(Color.FromRgb(248, 250, 252)),    // #f8fafc
                BorderBrush = new SolidColorBrush(Color.FromRgb(208, 215, 222)),    // #d0d7de
                BorderMuted = new SolidColorBrush(Color.FromRgb(226, 232, 240)),    // #e2e8f0
                InputBg = new SolidColorBrush(Color.FromRgb(255, 255, 255)),        // #ffffff
                InputBorder = new SolidColorBrush(Color.FromRgb(208, 215, 222)),    // #d0d7de

                TextPrimary = new SolidColorBrush(Color.FromRgb(15, 23, 42)),       // #0f172a
                TextSecondary = new SolidColorBrush(Color.FromRgb(51, 65, 85)),     // #334155
                TextMuted = new SolidColorBrush(Color.FromRgb(100, 116, 139)),      // #64748b
                TextDim = new SolidColorBrush(Color.FromRgb(148, 163, 184)),        // #94a3b8

                AccentBlue = new SolidColorBrush(Color.FromRgb(9, 105, 218)),      // #0969da
                AccentEmerald = new SolidColorBrush(Color.FromRgb(5, 150, 105)),   // #059669
                AccentAmber = new SolidColorBrush(Color.FromRgb(217, 119, 6)),     // #d97706
                AccentRed = new SolidColorBrush(Color.FromRgb(220, 38, 38)),       // #dc2626
                ProgressBarTrack = new SolidColorBrush(Color.FromRgb(226, 232, 240)) // #e2e8f0
            };
        }
    }

    public static class LinuxTheme
    {
        public static event Action ThemeChanged;
        private static string _configPath;
        public static LinuxThemePalette Current { get; private set; }

        static LinuxTheme()
        {
            try
            {
                string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "sysmonitor");
                if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
                _configPath = Path.Combine(configDir, "theme.cfg");

                if (File.Exists(_configPath))
                {
                    string saved = File.ReadAllText(_configPath).Trim().ToLowerInvariant();
                    if (saved == "dark")
                    {
                        Current = LinuxThemePalette.CreateDark();
                        return;
                    }
                }
            }
            catch { }
            // 默认对齐 Windows 初始风格：亮色模式
            Current = LinuxThemePalette.CreateLight();
        }

        public static void SetDark(bool isDark)
        {
            Current = isDark ? LinuxThemePalette.CreateDark() : LinuxThemePalette.CreateLight();
            try
            {
                if (!string.IsNullOrEmpty(_configPath))
                {
                    File.WriteAllText(_configPath, isDark ? "dark" : "light");
                }
            }
            catch { }
            ThemeChanged?.Invoke();
        }

        public static Border CreateComplicationBorder()
        {
            return new Border
            {
                Height = 38,
                Background = Current.CardBg,
                CornerRadius = new CornerRadius(7),
                BorderBrush = Current.BorderBrush,
                BorderThickness = new Thickness(0.8)
            };
        }

        public static Border CreateCardBorder()
        {
            return new Border
            {
                Background = Current.CardBg,
                BorderBrush = Current.BorderBrush,
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        public static Border CreateInnerBorder()
        {
            return new Border
            {
                Background = Current.InnerTileBg,
                BorderBrush = Current.BorderMuted,
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(6)
            };
        }

        public static TextBlock CreateCardHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = Current.TextPrimary,
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        public static TextBlock CreateMutedText(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                Foreground = Current.TextMuted,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static TextBlock CreateValueText(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                Foreground = Current.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
            };
        }

        public static Border CreateBadge(string text, IBrush fg, IBrush bg, IBrush border)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 9.5,
                FontWeight = FontWeight.Bold,
                Foreground = fg
            };
            return new Border
            {
                Background = bg,
                BorderBrush = border,
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                VerticalAlignment = VerticalAlignment.Center,
                Child = tb
            };
        }

        public static Border CreateProgressBar(out Border fillBar, IBrush fillBrush)
        {
            fillBar = new Border
            {
                Background = fillBrush,
                CornerRadius = new CornerRadius(2),
                Height = 4,
                Width = 20,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            return new Border
            {
                Background = Current.ProgressBarTrack,
                CornerRadius = new CornerRadius(2),
                Height = 4,
                Margin = new Thickness(0, 4, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = fillBar
            };
        }

        public static Button CreateIconButton(string icon, string tooltip, Action onClick, double fontSize = 11)
        {
            var btn = new Button
            {
                Content = icon,
                FontSize = fontSize,
                Foreground = Current.TextSecondary,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5, 3),
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            if (onClick != null) btn.Click += (s, e) => onClick();
            ToolTip.SetTip(btn, tooltip);
            return btn;
        }

        public static TextBox CreateInputTextBox(string text)
        {
            return new TextBox
            {
                Text = text,
                Background = Current.InputBg,
                Foreground = Current.TextPrimary,
                BorderBrush = Current.InputBorder,
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(4),
                Height = 28,
                MinHeight = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(7, 0, 7, 0),
                FontSize = 11,
                FontFamily = new FontFamily("Consolas, Courier New, monospace, Segoe UI")
            };
        }
    }
}
