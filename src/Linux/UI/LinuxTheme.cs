using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SysMonitor.Linux.UI
{
    public class LinuxThemePalette
    {
        public bool IsDark { get; set; }

        public IBrush CardBg { get; set; }
        public IBrush BorderBrush { get; set; }
        public IBrush BorderMuted { get; set; }
        public IBrush InnerTileBg { get; set; }
        public IBrush PillBg { get; set; }

        public IBrush TextPrimary { get; set; }
        public IBrush TextSecondary { get; set; }
        public IBrush TextMuted { get; set; }

        public IBrush AccentBlue { get; set; }
        public IBrush AccentEmerald { get; set; }
        public IBrush AccentAmber { get; set; }
        public IBrush AccentRed { get; set; }

        public static LinuxThemePalette CreateLight()
        {
            return new LinuxThemePalette
            {
                IsDark = false,
                CardBg = new SolidColorBrush(Color.FromArgb(242, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 226, 232, 240)),
                BorderMuted = new SolidColorBrush(Color.FromArgb(100, 241, 245, 249)),
                InnerTileBg = new SolidColorBrush(Color.FromArgb(160, 248, 250, 252)),
                PillBg = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),

                TextPrimary = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                TextSecondary = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextMuted = new SolidColorBrush(Color.FromRgb(148, 163, 184)),

                AccentBlue = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                AccentEmerald = new SolidColorBrush(Color.FromRgb(5, 150, 105)),
                AccentAmber = new SolidColorBrush(Color.FromRgb(217, 119, 6)),
                AccentRed = new SolidColorBrush(Color.FromRgb(220, 38, 38))
            };
        }

        public static LinuxThemePalette CreateDark()
        {
            return new LinuxThemePalette
            {
                IsDark = true,
                CardBg = new SolidColorBrush(Color.FromArgb(240, 30, 31, 34)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(130, 63, 66, 72)),
                BorderMuted = new SolidColorBrush(Color.FromArgb(90, 53, 55, 60)),
                InnerTileBg = new SolidColorBrush(Color.FromArgb(150, 35, 37, 41)),
                PillBg = new SolidColorBrush(Color.FromArgb(220, 24, 25, 28)),

                TextPrimary = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                TextSecondary = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                TextMuted = new SolidColorBrush(Color.FromRgb(100, 116, 139)),

                AccentBlue = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                AccentEmerald = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                AccentAmber = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                AccentRed = new SolidColorBrush(Color.FromRgb(239, 68, 68))
            };
        }
    }

    public static class LinuxTheme
    {
        public static event Action ThemeChanged;
        public static LinuxThemePalette Current { get; private set; } = LinuxThemePalette.CreateLight();

        public static void SetDark(bool isDark)
        {
            Current = isDark ? LinuxThemePalette.CreateDark() : LinuxThemePalette.CreateLight();
            ThemeChanged?.Invoke();
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
