using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NewsAggregator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("UI线程异常", e.Exception);
            MessageBox.Show($"发生错误：{e.Exception.Message}\n\n程序将继续运行。", "错误",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            LogException("未处理异常", ex);
            MessageBox.Show($"发生严重错误，程序将退出。\n{ex?.Message}", "严重错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("未观察的任务异常", e.Exception);
            e.SetObserved();
        }

        private void LogException(string type, Exception ex)
        {
            try
            {
                string logFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "NewsAggregator");
                Directory.CreateDirectory(logFolder);

                string logFile = Path.Combine(logFolder, "error.log");
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}]\n" +
                                  $"消息：{ex?.Message}\n" +
                                  $"堆栈：{ex?.StackTrace}\n" +
                                  $"----------------------------------------\n";

                File.AppendAllText(logFile, logEntry);
            }
            catch { }
        }
    }
}