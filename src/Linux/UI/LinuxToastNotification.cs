using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace SysMonitor.Linux.UI
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class LinuxToastNotification : Window
    {
        private static LinuxToastNotification _activeToast;
        private DispatcherTimer _dismissTimer;
        private readonly Action _onClick;

        public static void Show(string title, string message, string iconEmoji, ToastType type, Action onClick = null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_activeToast != null)
                    {
                        try { _activeToast.Close(); } catch { }
                        _activeToast = null;
                    }

                    var toast = new LinuxToastNotification(title, message, iconEmoji, type, onClick);
                    _activeToast = toast;
                    toast.Show();
                }
                catch { }
            });
        }

        public LinuxToastNotification(string title, string message, string iconEmoji, ToastType type, Action onClick)
        {
            _onClick = onClick;
            Title = "SysMonitorToast";
            RequestedThemeVariant = LinuxSettings.IsDark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
            SystemDecorations = SystemDecorations.None;
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Topmost = true;
            ShowInTaskbar = false;
            CanResize = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            Cursor = new Cursor(StandardCursorType.Hand);

            var theme = LinuxTheme.Current;

            // Pick accent color
            Color c = (theme.AccentBlue as ISolidColorBrush)?.Color ?? Color.FromRgb(88, 166, 255);
            if (type == ToastType.Success)
            {
                c = (theme.AccentEmerald as ISolidColorBrush)?.Color ?? Color.FromRgb(16, 185, 129);
            }
            else if (type == ToastType.Warning)
            {
                c = (theme.AccentAmber as ISolidColorBrush)?.Color ?? Color.FromRgb(245, 158, 11);
            }
            else if (type == ToastType.Error)
            {
                c = (theme.AccentRed as ISolidColorBrush)?.Color ?? Color.FromRgb(248, 81, 73);
            }

            var borderHighlight = new SolidColorBrush(Color.FromArgb(
                LinuxSettings.IsDark ? (byte)95 : (byte)130, c.R, c.G, c.B));

            var root = new Border
            {
                Background = theme.CardBg,
                BorderBrush = borderHighlight,
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 9, 14, 9),
                Margin = new Thickness(8),
                MinWidth = 200,
                MaxWidth = 360,
                BoxShadow = BoxShadows.Parse(theme.IsDark ? "0 4 16 #60000000" : "0 4 16 #30000000")
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            // Icon Badge
            var iconBorder = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(Color.FromArgb(32, c.R, c.G, c.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, c.R, c.G, c.B)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var tbIcon = new TextBlock
            {
                Text = iconEmoji,
                FontSize = 13,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            iconBorder.Child = tbIcon;
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            // Text Stack
            var textStack = new StackPanel
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var tbTitle = new TextBlock
            {
                Text = title,
                FontSize = 11.5,
                FontWeight = FontWeight.Bold,
                Foreground = theme.TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textStack.Children.Add(tbTitle);

            if (!string.IsNullOrEmpty(message))
            {
                var tbMsg = new TextBlock
                {
                    Text = message,
                    FontSize = 10,
                    Foreground = theme.TextSecondary,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 14
                };
                textStack.Children.Add(tbMsg);
            }

            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);
            root.Child = grid;
            Content = root;

            PointerReleased += (s, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Left)
                {
                    try { _onClick?.Invoke(); } catch { }
                    Close();
                }
            };

            Opened += (s, e) =>
            {
                PositionAtBottomRight();

                _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.8) };
                _dismissTimer.Tick += (ts, te) =>
                {
                    _dismissTimer.Stop();
                    Close();
                };
                _dismissTimer.Start();
            };
        }

        private void PositionAtBottomRight()
        {
            var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (screen == null) return;

            int toastW = (int)Bounds.Width;
            int toastH = (int)Bounds.Height;
            if (toastW <= 0) toastW = 240;
            if (toastH <= 0) toastH = 55;

            int targetX = screen.WorkingArea.X + screen.WorkingArea.Width - toastW - 16;
            int targetY = screen.WorkingArea.Y + screen.WorkingArea.Height - toastH - 24;

            Position = new PixelPoint(targetX, targetY);
        }
    }
}
