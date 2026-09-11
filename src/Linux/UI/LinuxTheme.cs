using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SysMonitor.Linux.UI
{
    public class LinuxThemePalette
    {
        public bool IsDark { get; set; }

        public IBrush WindowBg { get; set; }
        public IBrush CardBg { get; set; }
        public IBrush BorderBrush { get; set; }
        public IBrush BorderMuted { get; set; }
        public IBrush InnerTileBg { get; set; }
        public IBrush ProgressBarTrack { get; set; }

        public IBrush TextPrimary { get; set; }
        public IBrush TextSecondary { get; set; }
        public IBrush TextMuted { get; set; }

        public IBrush AccentBlue { get; set; }
        public IBrush AccentEmerald { get; set; }
        public IBrush AccentAmber { get; set; }
        public IBrush AccentRed { get; set; }

        public static LinuxThemePalette CreateDark()
        {
            return new LinuxThemePalette
            {
                IsDark = true,
                WindowBg = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                CardBg = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderMuted = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                InnerTileBg = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                ProgressBarTrack = new SolidColorBrush(Color.FromRgb(33, 38, 45)),

                TextPrimary = new SolidColorBrush(Color.FromRgb(240, 246, 252)),
                TextSecondary = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                TextMuted = new SolidColorBrush(Color.FromRgb(148, 163, 184)),

                AccentBlue = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                AccentEmerald = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                AccentAmber = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                AccentRed = new SolidColorBrush(Color.FromRgb(248, 81, 73))
            };
        }

        public static LinuxThemePalette CreateLight()
        {
            return new LinuxThemePalette
            {
                IsDark = false,
                WindowBg = new SolidColorBrush(Color.FromRgb(246, 248, 250)),
                CardBg = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(208, 215, 222)),
                BorderMuted = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                InnerTileBg = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                ProgressBarTrack = new SolidColorBrush(Color.FromRgb(226, 232, 240)),

                TextPrimary = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                TextSecondary = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                TextMuted = new SolidColorBrush(Color.FromRgb(100, 116, 139)),

                AccentBlue = new SolidColorBrush(Color.FromRgb(9, 105, 218)),
                AccentEmerald = new SolidColorBrush(Color.FromRgb(5, 150, 105)),
                AccentAmber = new SolidColorBrush(Color.FromRgb(217, 119, 6)),
                AccentRed = new SolidColorBrush(Color.FromRgb(220, 38, 38))
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
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 4, 0, 4)
            };
        }

        public static TextBlock CreateHeader(string text)
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

        public static TextBlock CreateMuted(string text, double size = 10)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = Current.TextSecondary
            };
        }

        public static Button CreateIconButton(string icon, string tooltip, Action onClick)
        {
            var btn = new Button
            {
                Content = icon,
                FontSize = 11,
                Foreground = Current.TextSecondary,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            if (onClick != null) btn.Click += (s, e) => onClick();
            ToolTip.SetTip(btn, tooltip);
            return btn;
        }
    }
}
