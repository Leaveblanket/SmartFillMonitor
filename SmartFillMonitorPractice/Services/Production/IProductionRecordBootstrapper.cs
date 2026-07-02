using System.Threading.Tasks;

namespace SmartFillMonitor.Services.Production
{
    /// <summary>
    /// 负责生产记录存储初始化，例如幂等辅助表和索引准备。
    /// </summary>
    public interface IProductionRecordBootstrapper
    {
        /// <summary>
        /// 执行生产记录存储初始化。
        /// </summary>
        Task InitializeAsync();
    }
}
