using System;

namespace SmartFillMonitor.Services.Threading
{
    /// <summary>
    /// 封装 UI 线程调度能力，避免上层直接依赖 WPF Dispatcher。
    /// </summary>
    public interface IUiThreadService
    {
        /// <summary>
        /// 判断当前线程是否可直接访问 UI。
        /// </summary>
        bool CheckAccess();

        /// <summary>
        /// 同步切换到 UI 线程执行操作。
        /// </summary>
        void Invoke(Action action);

        /// <summary>
        /// 异步投递操作到 UI 线程执行。
        /// </summary>
        void BeginInvoke(Action action);
    }
}
