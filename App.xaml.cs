using System;
using System.IO;
using System.Windows;

namespace MidiAutoPlayer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"未处理的异常: {ex?.Message}\n\n{ex?.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"UI异常: {args.Exception.Message}\n\n{args.Exception.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                MessageBox.Show($"Task异常: {args.Exception.Message}\n\n{args.Exception.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                args.SetObserved();
            };

            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
                File.WriteAllText(logPath, $"程序启动: {DateTime.Now}\n");
            }
            catch { }
        }
    }
}
