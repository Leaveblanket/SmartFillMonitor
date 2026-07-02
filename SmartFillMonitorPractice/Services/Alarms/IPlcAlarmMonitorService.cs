using System.Threading.Tasks;

namespace SmartFillMonitor.Services.Alarms
{
    /// <summary>
    /// 监控 PLC 实时数据并触发自动报警。
    /// </summary>
    public interface IPlcAlarmMonitorService
    {
        /// <summary>
        /// 启动 PLC 报警监控。重复调用应保持幂等。
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 停止 PLC 报警监控并解除事件订阅。重复调用应保持幂等。
        /// </summary>
        Task StopAsync();
    }
}
