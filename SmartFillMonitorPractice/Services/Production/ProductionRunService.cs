using System;
using System.Threading.Tasks;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services.Plc;
using SmartFillMonitor.Services.Session;

namespace SmartFillMonitor.Services.Production
{
    /// <summary>
    /// 管理生产运行状态和操作（包括启动、停止、重置和捕获生产记录）的服务.
    /// </summary>
    public sealed class ProductionRunService : IProductionRunService
    {
        private readonly IPlcService _plcService;
        private readonly IProductionRecordService _productionRecordService;
        private readonly ISessionService _sessionService;
        private string _lastBarCode = string.Empty;

        public ProductionRunService(
            IPlcService plcService,
            IProductionRecordService productionRecordService,
            ISessionService sessionService)
        {
            _plcService = plcService;
            _productionRecordService = productionRecordService;
            _sessionService = sessionService;
            CurrentState = _plcService.Snapshot.HasSuccessfulRead
                ? ProductionRunState.Ready
                : ProductionRunState.Disconnected;
        }

        public ProductionRunState CurrentState { get; private set; }

        public ProductionStatusView GetStatusView(bool shouldClearRealtimeValues = false)
        {
            return ProductionStatusView.FromState(CurrentState, shouldClearRealtimeValues);
        }

        public ProductionStatusView GetStatusViewForConnectionChanged(bool connected)
        {
            ApplyConnectionChanged(connected);
            return GetStatusView(shouldClearRealtimeValues: !connected);
        }

        public ProductionRealtimeSnapshot CreateRealtimeSnapshot(DeviceState state)
        {
            return new ProductionRealtimeSnapshot(
                state.ActualCount,
                state.TargetCount,
                state.CurrentTemp,
                state.SettingTemp,
                state.RunningTime,
                state.CurrentCycleTime,
                state.StandardCycleTime,
                state.LiquidLevel,
                state.ValveOpen,
                CurrentState == ProductionRunState.Running);
        }

        public ProductionCommandResult ApplyConnectionChanged(bool connected)
        {
            if (!connected)
            {
                _lastBarCode = string.Empty;
                CurrentState = ProductionRunState.Disconnected;
                return ProductionCommandResult.Skipped(CurrentState, "PLC 已断开。");
            }

            CurrentState = _plcService.Snapshot.HasSuccessfulRead
                ? ProductionRunState.Ready
                : ProductionRunState.Disconnected;

            return ProductionCommandResult.Success(CurrentState, "PLC 连接状态已同步。");
        }

        public async Task<ProductionCommandResult> StartAsync()
        {
            if (CurrentState == ProductionRunState.Disconnected)
            {
                RefreshStateFromSnapshot();
                return ProductionCommandResult.Skipped(CurrentState, "PLC 未连接，不能启动生产。");
            }

            var success = await _plcService.PulseCommandAsync("Start");
            if (!success)
            {
                RefreshStateFromSnapshot();
                return ProductionCommandResult.Failed(CurrentState, "PLC 启动命令发送失败。");
            }

            CurrentState = ProductionRunState.Running;
            return ProductionCommandResult.Success(CurrentState, "PLC 启动命令已发送。");
        }

        public async Task<ProductionCommandResult> StopAsync()
        {
            if (CurrentState == ProductionRunState.Disconnected)
            {
                RefreshStateFromSnapshot();
                return ProductionCommandResult.Skipped(CurrentState, "PLC 未连接，不能停止生产。");
            }

            var success = await _plcService.PulseCommandAsync("Stop");
            if (!success)
            {
                RefreshStateFromSnapshot();
                return ProductionCommandResult.Failed(CurrentState, "PLC 停止命令发送失败。");
            }

            CurrentState = ProductionRunState.Stopped;
            return ProductionCommandResult.Success(CurrentState, "PLC 停止命令已发送。");
        }

        public async Task<ProductionCommandResult> ResetAsync()
        {
            var wasReadable = _plcService.Snapshot.HasSuccessfulRead;
            if (wasReadable)
            {
                await _plcService.PulseCommandAsync("Stop");
                await Task.Delay(100);
                await _plcService.PulseCommandAsync("Reset");
            }

            _lastBarCode = string.Empty;
            RefreshStateFromSnapshot();
            return ProductionCommandResult.Success(CurrentState, "PLC 复位流程已执行。");
        }

        public async Task<ProductionCaptureResult> CaptureIfNeededAsync(DeviceState state)
        {
            if (CurrentState != ProductionRunState.Running)
            {
                return ProductionCaptureResult.Skipped("生产未运行，不保存记录。");
            }

            if (_sessionService.CurrentUser == null)
            {
                return ProductionCaptureResult.Skipped("当前无登录用户，不保存记录。");
            }

            var barcode = state.BarCode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return ProductionCaptureResult.Skipped("条码为空，不保存记录。");
            }

            if (barcode == _lastBarCode)
            {
                return ProductionCaptureResult.Skipped("条码未变化，不重复保存记录。");
            }

            _lastBarCode = barcode;

            var record = new ProductionRecord
            {
                Time = DateTime.Now,
                BatchNo = barcode,
                SettingTemp = state.SettingTemp,
                ActualTemp = state.CurrentTemp,
                ActualCount = state.ActualCount,
                TargetCount = state.TargetCount,
                IsNG = false,
                CycleTime = state.CurrentCycleTime,
                Operator = _sessionService.CurrentUser?.UserName ?? string.Empty
            };

            await _productionRecordService.SaveAsync(record);
            return ProductionCaptureResult.SavedRecord(barcode);
        }

        private void RefreshStateFromSnapshot()
        {
            CurrentState = _plcService.Snapshot.HasSuccessfulRead
                ? ProductionRunState.Ready
                : ProductionRunState.Disconnected;
        }
    }
}
