using System;
using System.IO;
using System.Windows;

namespace KhangNghi.DesktopAdmin
{
    public partial class App : Application
    {
        private static void WriteCrashLog(string message)
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KhangNghi", "Logs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "app_crash.log"), $"{DateTimeOffset.Now:O} {message}\n");
            }
            catch (Exception) { /* Logging must not hide the original exception. */ }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                string inner = ex?.InnerException != null ? $"\nInner: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}" : "";
                string msg = $"[AppDomain Unhandled] {ex?.Message}\n{ex?.StackTrace}{inner}";
                WriteCrashLog(msg);
                MessageBox.Show(msg, "Lỗi Khởi Động", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                var ex = args.Exception;
                string inner = ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}" : "";
                string msg = $"[Dispatcher Unhandled] {ex.Message}\n{ex.StackTrace}{inner}";
                WriteCrashLog(msg);
                MessageBox.Show(msg, "Lỗi Khởi Động", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
