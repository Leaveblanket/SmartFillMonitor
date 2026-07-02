using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Alarms
{
    /// <summary>
    /// 基于 PLC 实时状态自动维护高温和低液位报警。
    /// </summary>
    public sealed class PlcAlarmMonitorService : IPlcAlarmMonitorService
    {
        private const double HighTemperatureDelta = 5.0;
        private const double LowLiquidLevelThreshold = 20.0;

        private readonly IPlcService _plcService;
        private readonly IServiceScopeFactory _scopeFactory;
        private bool _started;
        private bool _highTemperatureActive;
        private bool _lowLiquidLevelActive;

        public PlcAlarmMonitorService(IPlcService plcService, IServiceScopeFactory scopeFactory)
        {
            _plcService = plcService;
            _scopeFactory = scopeFactory;
        }

        public Task StartAsync()
        {
            if (_started)
            {
                return Task.CompletedTask;
            }

            _plcService.DataReceived += OnDataReceived;
            _started = true;
            LogHelper.Info("PLC 自动报警监控已启动。");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (!_started)
            {
                return Task.CompletedTask;
            }

            _plcService.DataReceived -= OnDataReceived;
            _started = false;
            _highTemperatureActive = false;
            _lowLiquidLevelActive = false;
            LogHelper.Info("PLC 自动报警监控已停止。");
            return Task.CompletedTask;
        }

        private void OnDataReceived(object? sender, DeviceState state)
        {
            if (state == null)
            {
                return;
            }

            _ = EvaluateHighTemperatureAsync(state);
            _ = EvaluateLowLiquidLevelAsync(state);
        }

        private async Task EvaluateHighTemperatureAsync(DeviceState state)
        {
            using var scope = _scopeFactory.CreateScope();
            var alarmService = scope.ServiceProvider.GetRequiredService<IAlarmService>();
            var shouldTrigger = state.CurrentTemp > state.SettingTemp + HighTemperatureDelta;
            if (shouldTrigger)
            {
                if (_highTemperatureActive)
                {
                    return;
                }

                await alarmService.TriggerAlarmAsync(new AlarmRecord
                {
                    AlarmCode = AlarmCode.HighTemperature,
                    AlarmSeverity = AlarmSeverity.Error,
                    StartTime = DateTime.Now,
                    Description = "设备当前温度超过设定温度 5°C。",
                    Message = $"当前温度 {state.CurrentTemp:F1}°C，设定温度 {state.SettingTemp:F1}°C。",
                    TriggeredBy = "PLC",
                    TriggeredByType = AlarmTriggeredByType.Plc
                });

                _highTemperatureActive = true;
                return;
            }

            if (!_highTemperatureActive)
            {
                return;
            }

            await alarmService.RecoverAlarmAsync(AlarmCode.HighTemperature);
            _highTemperatureActive = false;
        }

        private async Task EvaluateLowLiquidLevelAsync(DeviceState state)
        {
            using var scope = _scopeFactory.CreateScope();
            var alarmService = scope.ServiceProvider.GetRequiredService<IAlarmService>();
            var shouldTrigger = state.LiquidLevel < LowLiquidLevelThreshold;
            if (shouldTrigger)
            {
                if (_lowLiquidLevelActive)
                {
                    return;
                }

                await alarmService.TriggerAlarmAsync(new AlarmRecord
                {
                    AlarmCode = AlarmCode.LowLiquidLevel,
                    AlarmSeverity = AlarmSeverity.Warning,
                    StartTime = DateTime.Now,
                    Description = "原料箱液位低于 20%。",
                    Message = $"当前液位 {state.LiquidLevel:F1}%。",
                    TriggeredBy = "PLC",
                    TriggeredByType = AlarmTriggeredByType.Plc
                });

                _lowLiquidLevelActive = true;
                return;
            }

            if (!_lowLiquidLevelActive)
            {
                return;
            }

            await alarmService.RecoverAlarmAsync(AlarmCode.LowLiquidLevel);
            _lowLiquidLevelActive = false;
        }
    }
}
