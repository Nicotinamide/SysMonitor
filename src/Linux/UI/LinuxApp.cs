using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace SysMonitor.Linux.UI
{
    public class LinuxApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            UpdateTheme(LinuxSettings.IsDark);

            LinuxSettings.SettingsChanged += () =>
            {
                Dispatcher.UIThread.Post(() => UpdateTheme(LinuxSettings.IsDark));
            };
            LinuxTheme.ThemeChanged += () =>
            {
                Dispatcher.UIThread.Post(() => UpdateTheme(LinuxSettings.IsDark));
            };
        }

        private void UpdateTheme(bool isDark)
        {
            RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var floatWin = new LinuxFloatingWindow();
                desktop.MainWindow = floatWin;
                floatWin.Show();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
