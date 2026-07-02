using System.Threading.Tasks;

namespace SmartFillMonitor.Services.Session
{
    /// <summary>
    /// 协调整个已认证会话生命周期，包括启动、切换用户和退出应用。
    /// </summary>
    public interface ISessionCoordinator
    {
        /// <summary>
        /// 当前是否处于切换用户流程中。
        /// </summary>
        bool IsSwitchingUser { get; }

        /// <summary>
        /// 当前是否处于退出流程中。
        /// </summary>
        bool IsExiting { get; }

        /// <summary>
        /// 启动认证流程并在成功后创建主会话。
        /// </summary>
        Task<bool> StartAsync();

        /// <summary>
        /// 切换到其他用户并重建会话。
        /// </summary>
        Task<bool> SwitchUserAsync();

        /// <summary>
        /// 结束当前会话并退出应用。
        /// </summary>
        Task ExitAsync();
    }
}
