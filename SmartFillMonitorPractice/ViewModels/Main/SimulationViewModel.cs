using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Dialogs;
using SmartFillMonitor.Services.Simulation;
using SmartFillMonitor.Services.Threading;

namespace SmartFillMonitor.ViewModels.Main
{
    public partial class SimulationViewModel : ObservableObject, IDisposable
    {
        private readonly IPlcService _plcService;
        private readonly IDialogService _dialogService;
        private readonly IUiThreadService _uiThreadService;
        private bool _disposed;
        private bool _isConnectedReadable;
        private int _maxActualCount;
        private const double CountZeroDisplayHeight = 0.3;

        [ObservableProperty]
        private string plcConnectionText = "未连接";

        [ObservableProperty]
        private string runStateText = "未连接";

        [ObservableProperty]
        private string currentPhaseText = "未连接";

        [ObservableProperty]
        private string syncStatusText = "未检测到有效 Modbus 数据";

        [ObservableProperty]
        private string currentBatchNo = string.Empty;

        [ObservableProperty]
        private int actualCount;

        [ObservableProperty]
        private int targetCount;

        [ObservableProperty]
        private double currentTemp;

        [ObservableProperty]
        private double settingTemp;

        [ObservableProperty]
        private double runningTime;

        [ObservableProperty]
        private double currentCycleTime;

        [ObservableProperty]
        private double standardCycleTime;

        [ObservableProperty]
        private double liquidLevel;

        [ObservableProperty]
        private bool valveOpen;

        [ObservableProperty]
        private string valveStatusText = "关闭";

        [ObservableProperty]
        private int currentScriptIndex;

        [ObservableProperty]
        private LightState indicatorState = LightState.Red;

        [ObservableProperty]
        private ObservableCollection<ISeries>? tempCharts;

        [ObservableProperty]
        private ObservableCollection<ISeries>? countCharts;

        [ObservableProperty]
        private double countAxisMax = 1;

        [ObservableProperty]
        private SimulationRunState currentRunState = SimulationRunState.Ready;

        [ObservableProperty]
        private Axis[]? tempXAxes;

        [ObservableProperty]
        private Axis[]? tempYAxes;

        [ObservableProperty]
        private Axis[]? countXAxes;

        [ObservableProperty]
        private Axis[]? countYAxes;

        public Func<double, string> CountAxisFormatter { get; } = value => value.ToString("F0");

        public SimulationViewModel(
            IPlcService plcService,
            IDialogService dialogService,
            IUiThreadService uiThreadService)
        {
            _plcService = plcService;
            _dialogService = dialogService;
            _uiThreadService = uiThreadService;

            TempCharts = new ObservableCollection<ISeries>
            {
                new ColumnSeries<double>
                {
                    Name = "温度采样",
                    Values = new ObservableCollection<double>(),
                    Fill = new SolidColorPaint(new SKColor(30, 144, 255)),
                    Stroke = new SolidColorPaint(SKColors.Cyan) { StrokeThickness = 1 },
                    MaxBarWidth = 22
                }
            };

            TempXAxes = new[]
            {
                new Axis
                {
                    IsVisible = false,
                    SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128)) { StrokeThickness = 0 }
                }
            };

            TempYAxes = new[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 120,
                    IsVisible = true,
                    LabelsPaint = new SolidColorPaint(SKColors.White),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128)) { StrokeThickness = 0.5f }
                }
            };

            CountCharts = new ObservableCollection<ISeries>
            {
                new ColumnSeries<double>
                {
                    Name = "产量采样",
                    Values = new ObservableCollection<double>(),
                    Fill = new SolidColorPaint(new SKColor(30, 144, 255)),
                    Stroke = new SolidColorPaint(SKColors.Cyan) { StrokeThickness = 1 },
                    MaxBarWidth = 22,
                    DataLabelsPaint = null
                }
            };

            CountXAxes = new[]
            {
                new Axis
                {
                    IsVisible = false,
                    SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128)) { StrokeThickness = 0 }
                }
            };

            CountYAxes = new[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = countAxisMax,
                    IsVisible = true,
                    LabelsPaint = new SolidColorPaint(SKColors.White),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128)) { StrokeThickness = 0.5f },
                    Labeler = CountAxisFormatter
                }
            };

            _plcService.ConnectionChanged += OnConnectionChanged;
            _plcService.DataReceived += OnDataReceived;

            ResetDisplayValues();
            ApplySimulationState(InitializeSimulation());
        }

        private void OnConnectionChanged(object? sender, bool connected)
        {
            _uiThreadService.BeginInvoke(() =>
            {
                ApplySimulationState(ApplyConnectionChanged(connected));
            });
        }

        private void OnDataReceived(object? sender, DeviceState state)
        {
            if (state == null)
            {
                return;
            }

            _uiThreadService.BeginInvoke(() =>
            {
                var result = ApplyDataReceived(state);
                ApplySimulationState(result);

                if (result.RunState == SimulationRunState.Running)
                {
                    CurrentScriptIndex++;
                    AppendChartPoint(TempCharts, CurrentTemp, 48);
                    AppendChartPoint(CountCharts, GetCountDisplayValue(ActualCount), 24);
                    CurrentPhaseText = result.CurrentPhaseText;
                }
            });
        }

        private void ApplySimulationState(SimulationStateResult result)
        {
            PlcConnectionText = result.PlcConnectionText;
            RunStateText = result.RunStateText;
            CurrentPhaseText = result.CurrentPhaseText;
            SyncStatusText = result.SyncStatusText;
            IndicatorState = result.IndicatorState;

            if (result.DeviceState != null)
            {
                UpdateRealtimeValues(result.DeviceState);
            }
        }

        private void UpdateRealtimeValues(DeviceState state)
        {
            ActualCount = state.ActualCount;
            TargetCount = state.TargetCount;
            CurrentTemp = state.CurrentTemp;
            SettingTemp = state.SettingTemp;
            RunningTime = state.RunningTime;
            CurrentCycleTime = state.CurrentCycleTime;
            StandardCycleTime = state.StandardCycleTime;
            LiquidLevel = state.LiquidLevel;
            ValveOpen = state.ValveOpen;
            CurrentBatchNo = state.BarCode ?? string.Empty;
            UpdateCountAxisRange(state.ActualCount);
        }

        private void ResetDisplayValues()
        {
            ActualCount = 0;
            TargetCount = 0;
            CurrentTemp = 0;
            SettingTemp = 0;
            RunningTime = 0;
            CurrentCycleTime = 0;
            StandardCycleTime = 0;
            LiquidLevel = 0;
            ValveOpen = false;
            CurrentBatchNo = string.Empty;
            CurrentScriptIndex = 0;
            _maxActualCount = 0;
            CountAxisMax = 1;
        }

        private void AppendChartPoint(ObservableCollection<ISeries>? collection, double value, int maxCount)
        {
            if (collection == null || collection.Count == 0)
            {
                return;
            }

            var series = (ColumnSeries<double>)collection[0];
            var values = (ObservableCollection<double>?)series.Values ?? new ObservableCollection<double>();
            if (series.Values == null)
            {
                series.Values = values;
            }

            values.Add(value);
            if (values.Count > maxCount)
            {
                values.RemoveAt(0);
            }
        }

        private double GetCountDisplayValue(int actualCount)
        {
            return actualCount <= 0 ? CountZeroDisplayHeight : actualCount;
        }

        private void UpdateCountAxisRange(int actualCount)
        {
            if (actualCount > _maxActualCount)
            {
                _maxActualCount = actualCount;
            }

            var newMax = _maxActualCount <= 0
                ? 1
                : Math.Max(1, Math.Ceiling(_maxActualCount * 1.2));

            CountAxisMax = newMax;

            if (CountYAxes?.Length > 0)
            {
                CountYAxes[0].MaxLimit = newMax;
            }
        }

        private void ClearCharts()
        {
            if (TempCharts?.Count > 0 && TempCharts[0] is ColumnSeries<double> tempSeries)
            {
                (tempSeries.Values as ObservableCollection<double>)?.Clear();
            }

            if (CountCharts?.Count > 0 && CountCharts[0] is ColumnSeries<double> countSeries)
            {
                (countSeries.Values as ObservableCollection<double>)?.Clear();
            }
        }

        partial void OnValveOpenChanged(bool value)
        {
            ValveStatusText = value ? "打开" : "关闭";
        }

        public void PauseSimulation()
        {
            _uiThreadService.BeginInvoke(() =>
            {
                ApplySimulationState(Pause());
            });
        }

        public void StopAndDispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            LogHelper.Info("仿真联调视图模型正在解除 PLC 事件订阅。");
            _plcService.ConnectionChanged -= OnConnectionChanged;
            _plcService.DataReceived -= OnDataReceived;
        }

        public void Dispose()
        {
            StopAndDispose();
        }

        [RelayCommand]
        private void StartSimulation()
        {
            var result = Start();
            ApplySimulationState(result);

            if (!string.IsNullOrWhiteSpace(result.UserMessage))
            {
                _dialogService.ShowMessage(result.UserMessage, "提示", PromptSeverity.Information, DialogButtons.Ok);
            }
        }

        [RelayCommand]
        private void StopSimulation()
        {
            ApplySimulationState(Stop());
        }

        [RelayCommand]
        private void ResetSimulation()
        {
            ClearCharts();
            CurrentScriptIndex = 0;
            CurrentBatchNo = string.Empty;

            var result = Reset();
            if (result.DeviceState == null)
            {
                ResetDisplayValues();
            }

            ApplySimulationState(result);
        }

        private SimulationStateResult InitializeSimulation()
        {
            if (_plcService.Snapshot.HasSuccessfulRead && _plcService.Snapshot.LastDeviceState != null)
            {
                return ApplyReadableState(_plcService.Snapshot.LastDeviceState);
            }

            return ApplyDisconnectedState();
        }

        private SimulationStateResult ApplyConnectionChanged(bool connected)
        {
            if (!connected)
            {
                return ApplyDisconnectedState();
            }

            return _plcService.Snapshot.LastDeviceState != null
                ? ApplyReadableState(_plcService.Snapshot.LastDeviceState)
                : BuildCurrentResult(null);
        }

        private SimulationStateResult ApplyDataReceived(DeviceState state)
        {
            return ApplyReadableState(state);
        }

        private SimulationStateResult Start()
        {
            if (!_plcService.Snapshot.HasSuccessfulRead || _plcService.Snapshot.LastDeviceState == null)
            {
                var disconnected = ApplyDisconnectedState();
                return new SimulationStateResult(
                    disconnected.IsConnectedReadable,
                    disconnected.RunState,
                    disconnected.PlcConnectionText,
                    disconnected.RunStateText,
                    disconnected.CurrentPhaseText,
                    disconnected.SyncStatusText,
                    disconnected.IndicatorState,
                    disconnected.DeviceState,
                    "未检测到有效 Modbus 数据，当前不能启动仿真联调。");
            }

            _isConnectedReadable = true;
            CurrentRunState = SimulationRunState.Running;
            return new SimulationStateResult(
                true,
                CurrentRunState,
                "已连接",
                "运行中",
                "采样中",
                BuildSyncStatusText(),
                LightState.Green,
                _plcService.Snapshot.LastDeviceState);
        }

        private SimulationStateResult Stop()
        {
            if (!_isConnectedReadable || CurrentRunState != SimulationRunState.Running)
            {
                return BuildCurrentResult(_plcService.Snapshot.LastDeviceState);
            }

            CurrentRunState = SimulationRunState.Stopped;
            return BuildCurrentResult(_plcService.Snapshot.LastDeviceState);
        }

        private SimulationStateResult Reset()
        {
            if (!_plcService.Snapshot.HasSuccessfulRead || _plcService.Snapshot.LastDeviceState == null)
            {
                return ApplyDisconnectedState();
            }

            _isConnectedReadable = true;
            CurrentRunState = SimulationRunState.Ready;
            return BuildCurrentResult(_plcService.Snapshot.LastDeviceState);
        }

        private SimulationStateResult Pause()
        {
            return Stop();
        }

        private SimulationStateResult ApplyReadableState(DeviceState state)
        {
            _isConnectedReadable = true;

            if (CurrentRunState != SimulationRunState.Running && CurrentRunState != SimulationRunState.Stopped)
            {
                CurrentRunState = SimulationRunState.Ready;
            }

            return BuildCurrentResult(state);
        }

        private SimulationStateResult ApplyDisconnectedState()
        {
            _isConnectedReadable = false;
            CurrentRunState = SimulationRunState.Ready;
            return new SimulationStateResult(
                false,
                CurrentRunState,
                "未连接",
                "未连接",
                "未连接",
                "未检测到有效 Modbus 数据",
                LightState.Red,
                null);
        }

        private SimulationStateResult BuildCurrentResult(DeviceState? state)
        {
            if (!_isConnectedReadable)
            {
                return ApplyDisconnectedState();
            }

            return CurrentRunState switch
            {
                SimulationRunState.Running => new SimulationStateResult(true, CurrentRunState, "已连接", "运行中", "采样中", BuildSyncStatusText(), LightState.Green, state),
                SimulationRunState.Stopped => new SimulationStateResult(true, CurrentRunState, "已连接", "已停止", "采样暂停", BuildSyncStatusText(), LightState.Yellow, state),
                _ => new SimulationStateResult(true, CurrentRunState, "已连接", "已连接 / 已就绪", "待启动", BuildSyncStatusText(), LightState.Yellow, state)
            };
        }

        private string BuildSyncStatusText()
        {
            return _plcService.Snapshot.LastReadSuccessTime.HasValue
                ? $"最近采样：{_plcService.Snapshot.LastReadSuccessTime.Value:HH:mm:ss}"
                : "已读到有效 Modbus 数据";
        }
    }
}
