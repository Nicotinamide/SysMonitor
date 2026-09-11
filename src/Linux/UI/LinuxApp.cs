using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace SysMonitor.Linux.UI
{
    public class LinuxApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
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
