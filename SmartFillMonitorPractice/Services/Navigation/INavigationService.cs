using System;

namespace SmartFillMonitor.Services.Navigation
{
    /// <summary>
    /// 负责在视图模型之间切换导航，并通知当前显示对象变化。
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// 当前视图模型发生切换时发布。
        /// </summary>
        event Action<object?>? CurrentViewModelChanged;

        /// <summary>
        /// 导航到指定类型的目标视图模型。
        /// </summary>
        void NavigateTo<T>() where T : class;

        /// <summary>
        /// 携带参数导航到指定类型的目标视图模型。
        /// </summary>
        void NavigateTo<T>(object? parameter) where T : class;
    }
}
