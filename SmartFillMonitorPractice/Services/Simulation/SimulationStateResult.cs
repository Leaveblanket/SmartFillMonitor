using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;

namespace SmartFillMonitor.Services.Simulation
{
    public sealed class SimulationStateResult
    {
        public SimulationStateResult(
            bool isConnectedReadable,
            SimulationRunState runState,
            string plcConnectionText,
            string runStateText,
            string currentPhaseText,
            string syncStatusText,
            LightState indicatorState,
            DeviceState? deviceState,
            string? userMessage = null)
        {
            IsConnectedReadable = isConnectedReadable;
            RunState = runState;
            PlcConnectionText = plcConnectionText;
            RunStateText = runStateText;
            CurrentPhaseText = currentPhaseText;
            SyncStatusText = syncStatusText;
            IndicatorState = indicatorState;
            DeviceState = deviceState;
            UserMessage = userMessage;
        }

        public bool IsConnectedReadable { get; }

        public SimulationRunState RunState { get; }

        public string PlcConnectionText { get; }

        public string RunStateText { get; }

        public string CurrentPhaseText { get; }

        public string SyncStatusText { get; }

        public LightState IndicatorState { get; }

        public DeviceState? DeviceState { get; }

        public string? UserMessage { get; }
    }
}
