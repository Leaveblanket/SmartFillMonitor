using SmartFillMonitor.Models.Enum;

namespace SmartFillMonitor.Services.Production
{
    public sealed class ProductionStatusView
    {
        public ProductionStatusView(string deviceStatus, LightState indicatorState, bool shouldClearRealtimeValues)
        {
            DeviceStatus = deviceStatus;
            IndicatorState = indicatorState;
            ShouldClearRealtimeValues = shouldClearRealtimeValues;
        }

        public string DeviceStatus { get; }

        public LightState IndicatorState { get; }

        public bool ShouldClearRealtimeValues { get; }

        public static ProductionStatusView FromState(ProductionRunState state, bool shouldClearRealtimeValues = false)
        {
            return state switch
            {
                ProductionRunState.Running => new ProductionStatusView("运行中", LightState.Green, shouldClearRealtimeValues),
                ProductionRunState.Stopped => new ProductionStatusView("已停止", LightState.Yellow, shouldClearRealtimeValues),
                ProductionRunState.Ready => new ProductionStatusView("已连接 / 已就绪", LightState.Yellow, shouldClearRealtimeValues),
                _ => new ProductionStatusView("未连接", LightState.Red, shouldClearRealtimeValues)
            };
        }

        public static ProductionStatusView Starting()
        {
            return new ProductionStatusView("启动中...", LightState.Green, false);
        }

        public static ProductionStatusView Stopping()
        {
            return new ProductionStatusView("停止中...", LightState.Yellow, false);
        }

        public static ProductionStatusView Resetting()
        {
            return new ProductionStatusView("复位中...", LightState.Yellow, false);
        }
    }
}
