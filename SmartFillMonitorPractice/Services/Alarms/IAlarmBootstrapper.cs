using System.Threading.Tasks;

namespace SmartFillMonitor.Services.Alarms
{
    /// <summary>
    /// 负责报警存储初始化，例如索引准备和活动报警规范化。
    /// </summary>
    public interface IAlarmBootstrapper
    {
        /// <summary>
        /// 执行报警存储初始化。
        /// </summary>
        Task InitializeAsync();
    }
}
