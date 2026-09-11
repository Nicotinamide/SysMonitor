using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace SysMonitor
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class ToastNotification : Window
    {
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static ToastNotification _activeToast = null;
        private DispatcherTimer _dismissTimer;
        private bool _isClosing = false;
        private Action _onClick;

        public static void Show(string title, string message, string iconEmoji, ToastType type, Action onClick = null)
        {
            if (Application.Current == null) return;
            Application.Current.Dispatcher.BeginInvoke(new Action(delegate
            {
                try
                {
                    if (_activeToast != null)
                    {
                        _activeToast.FastClose();
                    }

                    ToastNotification toast = new ToastNotification(title, message, iconEmoji, type, onClick);
                    _activeToast = toast;
                    toast.Show();
                }
                catch { }
            }));
        }

        public ToastNotification(string title, string message, string iconEmoji, ToastType type, Action onClick)
        {
            _onClick = onClick;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            Cursor = Cursors.Hand;
            UseLayoutRounding = true;

            ThemePalette theme = AppTheme.Current;

            // Color accent according to type
            SolidColorBrush accentBrush = theme.AccentBlue;
            if (type == ToastType.Success) accentBrush = theme.AccentEmerald;
            else if (type == ToastType.Warning) accentBrush = theme.AccentAmber;
            else if (type == ToastType.Error) accentBrush = theme.AccentRed;

            SolidColorBrush borderHighlight = new SolidColorBrush(Color.FromArgb(
                theme.Mode == ThemeMode.Dark ? (byte)95 : (byte)130,
                accentBrush.Color.R,
                accentBrush.Color.G,
                accentBrush.Color.B));

            Border root = new Border
            {
                Background = theme.CardBg,
                BorderBrush = borderHighlight,
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 9, 14, 9),
                Margin = new Thickness(10), // margin for drop shadow
                Effect = new DropShadowEffect
                {
                    BlurRadius = 14,
                    ShadowDepth = 3,
                    Color = theme.ShadowColor,
                    Opacity = theme.ShadowOpacity
                },
                MinWidth = 200,
                MaxWidth = 360
            };

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Icon Badge
            Border iconBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(32, accentBrush.Color.R, accentBrush.Color.G, accentBrush.Color.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, accentBrush.Color.R, accentBrush.Color.G, accentBrush.Color.B)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock tbIcon = new TextBlock
            {
                Text = iconEmoji,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = tbIcon;
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            // Text Stack
            StackPanel textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock tbTitle = new TextBlock
            {
                Text = title,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = theme.TextPrimary,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textStack.Children.Add(tbTitle);

            if (!string.IsNullOrEmpty(message))
            {
                TextBlock tbMsg = new TextBlock
                {
                    Text = message,
                    FontSize = 10,
                    FontWeight = FontWeights.Normal,
                    Foreground = theme.TextSecondary,
                    FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
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

            // Mouse click handler -> trigger onClick (e.g. open detail) and dismiss
            MouseLeftButtonUp += delegate
            {
                if (_onClick != null)
                {
                    try { _onClick(); } catch { }
                }
                FadeOutAndClose();
            };

            SourceInitialized += delegate
            {
                try
                {
                    WindowInteropHelper helper = new WindowInteropHelper(this);
                    int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
                    SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
                }
                catch { }
            };

            Loaded += delegate
            {
                PositionAtBottomRight();
                AnimateIn();

                _dismissTimer = new DispatcherTimer();
                _dismissTimer.Interval = TimeSpan.FromSeconds(2.6);
                _dismissTimer.Tick += delegate
                {
                    _dismissTimer.Stop();
                    FadeOutAndClose();
                };
                _dismissTimer.Start();
            };
        }

        private void PositionAtBottomRight()
        {
            Rect wa = SystemParameters.WorkArea;
            Left = wa.Right - ActualWidth - 16;
            Top = wa.Bottom - ActualHeight - 16;
        }

        private void AnimateIn()
        {
            Opacity = 0;
            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, fade);
        }

        private void FadeOutAndClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            if (_dismissTimer != null) _dismissTimer.Stop();

            DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
            fade.Completed += delegate
            {
                if (_activeToast == this) _activeToast = null;
                Close();
            };
            BeginAnimation(OpacityProperty, fade);
        }

        private void FastClose()
        {
            _isClosing = true;
            if (_dismissTimer != null) _dismissTimer.Stop();
            Close();
        }
    }
}
