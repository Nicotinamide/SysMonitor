using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace SysMonitor
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private static void Log(string msg)
        {
            try
            {
                string logFile = @"C:\Users\4955\.gemini\antigravity\scratch\SysMonitor_Native\debug.log";
                System.IO.File.AppendAllText(logFile, string.Format("[{0:HH:mm:ss.fff}] [PID:{1}] {2}\r\n", DateTime.Now, Process.GetCurrentProcess().Id, msg));
            }
            catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            // Initialize AppSettings (theme & language) before creating any UI or window
            try
            {
                AppSettings.Load();
            }
            catch { }

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Log("CRITICAL UnhandledException: " + (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "null"));
            };

            Log("Process started. Args: " + string.Join(" ", args));

            try
            {
                SetProcessDPIAware();
            }
            catch (Exception ex)
            {
                Log("SetProcessDPIAware failed: " + ex.Message);
            }

            // Ensure single instance: smoothly replace any stale or background instance
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                foreach (Process p in Process.GetProcessesByName("SysMonitor"))
                {
                    if (p.Id != currentPid)
                    {
                        try
                        {
                            Log("Killing previous SysMonitor PID: " + p.Id);
                            p.Kill();
                            p.WaitForExit(1500);
                        }
                        catch (Exception ex)
                        {
                            Log("Failed killing PID " + p.Id + ": " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Process sweep failed: " + ex.Message);
            }

            try
            {
                Log("Creating Application instance...");
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;

                app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                {
                    Log("DispatcherUnhandledException: " + e.Exception.ToString());
                };

                app.Exit += delegate(object sender, ExitEventArgs e)
                {
                    Log("app.Exit event fired! ExitCode: " + e.ApplicationExitCode);
                };

                Log("Instantiating MainWindow...");
                MainWindow mainWindow = new MainWindow();
                mainWindow.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
                {
                    Log("mainWindow.Closing fired! Cancel: " + e.Cancel);
                };
                mainWindow.Closed += delegate(object sender, EventArgs e)
                {
                    Log("mainWindow.Closed fired!");
                };

                Log("Setting app.MainWindow...");
                app.MainWindow = mainWindow;

                Log("Calling mainWindow.Show()...");
                mainWindow.Show();
                mainWindow.Activate();

                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
                Log(string.Format("mainWindow HWND: 0x{0:X}, Left: {1}, Top: {2}, Width: {3}, Height: {4}, Vis: {5}, Opacity: {6}",
                    hwnd.ToInt64(), mainWindow.Left, mainWindow.Top, mainWindow.Width, mainWindow.Height, mainWindow.Visibility, mainWindow.Opacity));

                Log("Calling app.Run(mainWindow)...");
                int exitCode = app.Run(mainWindow);
                Log("app.Run(mainWindow) completed with code: " + exitCode);
            }
            catch (Exception ex)
            {
                Log("Main catch exception: " + ex.ToString());
                try
                {
                    string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SysMonitor_error.log");
                    System.IO.File.WriteAllText(log, ex.ToString());
                }
                catch { }
            }
            Log("Process terminating.");
        }
    }
}
