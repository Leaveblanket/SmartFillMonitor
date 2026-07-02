using System;
using System.Windows;

namespace SmartFillMonitor.Services.Threading
{
    /// <summary>
    /// WPF 实现的 UI 线程服务，提供在 WPF 应用程序的主线程上执行操作的方法。
    /// </summary>
    public sealed class WpfUiThreadService : IUiThreadService
    {
        public bool CheckAccess()
        {
            var dispatcher = Application.Current?.Dispatcher;
            return dispatcher == null || dispatcher.CheckAccess();
        }

        public void Invoke(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        public void BeginInvoke(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                action();
                return;
            }

            _ = dispatcher.BeginInvoke(action);
        }
    }
}
