using System.Threading.Tasks;

namespace SmartFillMonitor.Services.Navigation
{
    /// <summary>
    /// 表示页面或视图模型支持接收导航进入与离开通知。
    /// </summary>
    public interface INavigationAware
    {
        /// <summary>
        /// 在导航进入当前对象时调用，可接收导航参数。
        /// </summary>
        Task OnNavigatedToAsync(object? parameter);

        /// <summary>
        /// 在从当前对象导航离开时调用。
        /// </summary>
        Task OnNavigatedFromAsync();
    }
}
