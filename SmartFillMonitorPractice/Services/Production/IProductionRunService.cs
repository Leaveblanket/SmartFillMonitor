using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Production
{
    /// <summary>
    /// 封装生产运行状态机、界面状态映射和与 PLC 交互的核心业务逻辑。
    /// </summary>
    public interface IProductionRunService
    {
        /// <summary>
        /// 当前生产运行状态。
        /// </summary>
        ProductionRunState CurrentState { get; }

        /// <summary>
        /// 获取当前生产状态对应的展示模型。
        /// </summary>
        ProductionStatusView GetStatusView(bool shouldClearRealtimeValues = false);

        /// <summary>
        /// 根据 PLC 连接变化生成新的展示状态。
        /// </summary>
        ProductionStatusView GetStatusViewForConnectionChanged(bool connected);

        /// <summary>
        /// 从设备状态创建实时生产快照。
        /// </summary>
        ProductionRealtimeSnapshot CreateRealtimeSnapshot(DeviceState state);

        /// <summary>
        /// 应用连接状态变化并更新内部状态机。
        /// </summary>
        ProductionCommandResult ApplyConnectionChanged(bool connected);

        /// <summary>
        /// 发送启动生产命令。
        /// </summary>
        Task<ProductionCommandResult> StartAsync();

        /// <summary>
        /// 发送停止生产命令。
        /// </summary>
        Task<ProductionCommandResult> StopAsync();

        /// <summary>
        /// 执行生产复位流程。
        /// </summary>
        Task<ProductionCommandResult> ResetAsync();

        /// <summary>
        /// 在需要时保存当前生产快照为生产记录。
        /// </summary>
        Task<ProductionCaptureResult> CaptureIfNeededAsync(DeviceState state);
    }
}
