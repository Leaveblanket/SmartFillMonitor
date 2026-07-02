namespace SmartFillMonitor.Services.Production
{
    public sealed class ProductionRealtimeSnapshot
    {
        public ProductionRealtimeSnapshot(
            int actualCount,
            int targetCount,
            double currentTemp,
            double settingTemp,
            double runningTime,
            double currentCycleTime,
            double standardCycleTime,
            double liquidLevel,
            bool valveOpen,
            bool shouldAppendTemperaturePoint)
        {
            ActualCount = actualCount;
            TargetCount = targetCount;
            CurrentTemp = currentTemp;
            SettingTemp = settingTemp;
            RunningTime = runningTime;
            CurrentCycleTime = currentCycleTime;
            StandardCycleTime = standardCycleTime;
            LiquidLevel = liquidLevel;
            ValveOpen = valveOpen;
            ShouldAppendTemperaturePoint = shouldAppendTemperaturePoint;
        }

        public int ActualCount { get; }

        public int TargetCount { get; }

        public double CurrentTemp { get; }

        public double SettingTemp { get; }

        public double RunningTime { get; }

        public double CurrentCycleTime { get; }

        public double StandardCycleTime { get; }

        public double LiquidLevel { get; }

        public bool ValveOpen { get; }

        public bool ShouldAppendTemperaturePoint { get; }
    }
}
