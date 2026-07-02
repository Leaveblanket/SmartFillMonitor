using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
using SmartFillMonitor.Services.Alarms;
using SmartFillMonitor.Services.Production;
using SmartFillMonitor.Services.Threading;

namespace SmartFillMonitor.ViewModels.Main
{
    public partial class DashBoardViewModel : ObservableObject, IDisposable
    {
        private static readonly AlarmViewMapper AlarmViewMapper = new();
        private readonly IPlcService _plcService;
        private readonly IAlarmService _alarmService;
        private readonly IProductionRunService _productionRunService;
        private readonly IDialogService _dialogService;
        private readonly IUiThreadService _uiThreadService;
        private readonly ObservableCollection<RecentAlarmItem> _recentAlarmMirror = new();
        private bool _disposed;

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
        private string deviceStatus = "未连接";

        [ObservableProperty]
        private double currentCycleTime;

        [ObservableProperty]
        private double standardCycleTime;

        [ObservableProperty]
        private bool valveOpen;

        [ObservableProperty]
        private double liquidLevel;

        [ObservableProperty]
        private ObservableCollection<ISeries>? tempLiveCharts;

        [ObservableProperty]
        private Axis[]? xAxes;

        [ObservableProperty]
        private Axis[]? yAxes;

        [ObservableProperty]
        private LightState indicatorState = LightState.Red;

        public ObservableCollection<RecentAlarmItem> RecentAlarms => _recentAlarmMirror;

        public DashBoardViewModel(
            IPlcService plcService,
            IAlarmService alarmService,
            IProductionRunService productionRunService,
            IDialogService dialogService,
            IUiThreadService uiThreadService)
        {
            _plcService = plcService;
            _alarmService = alarmService;
            _productionRunService = productionRunService;
            _dialogService = dialogService;
            _uiThreadService = uiThreadService;

            _plcService.DataReceived += OnDataReceived;
            _plcService.ConnectionChanged += OnConnectionChanged;
            _alarmService.AlarmTriggered += OnAlarmTriggered;
            _alarmService.AlarmRecovered += OnAlarmRecovered;

            TempLiveCharts = new ObservableCollection<ISeries>
            {
                new ColumnSeries<double>
                {
                    Name = "温度(°C)",
                    Values = new ObservableCollection<double>(),
                    Fill = new SolidColorPaint(new SKColor(70, 130, 180)),
                    Stroke = new SolidColorPaint(SKColors.Cyan) { StrokeThickness = 1 },
                    MaxBarWidth = 22
                }
            };

            XAxes = new[]
            {
                new Axis
                {
                    IsVisible = false,
                    SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128)) { StrokeThickness = 0 }
                }
            };

            YAxes = new[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 150,
                    IsVisible = true,
                    LabelsPaint = new SolidColorPaint(SKColors.White),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128)) { StrokeThickness = 0.5f }
                }
            };

            _ = LoadRecentAlarmsAsync();
            ApplyStatusView(_productionRunService.GetStatusView());
        }

        private async Task LoadRecentAlarmsAsync()
        {
            try
            {
                var activeAlarms = await _alarmService.GetActiveAlarmsAsync();
                _uiThreadService.BeginInvoke(() =>
                {
                    _recentAlarmMirror.Clear();
                    foreach (var item in activeAlarms.Take(10))
                    {
                        _recentAlarmMirror.Add(RecentAlarmItem.FromViewItem(AlarmViewMapper.MapRecentAlarm(item)));
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.Error("加载首页最近报警失败", ex);
            }
        }

        private void OnConnectionChanged(object? sender, bool connected)
        {
            _uiThreadService.BeginInvoke(() =>
            {
                ApplyStatusView(_productionRunService.GetStatusViewForConnectionChanged(connected));
            });
        }

        private void OnAlarmTriggered(object? sender, AlarmRecord record)
        {
            _uiThreadService.BeginInvoke(() =>
            {
                if (_recentAlarmMirror.Any(a => a.Id == record.Id))
                {
                    return;
                }

                _recentAlarmMirror.Insert(0, RecentAlarmItem.FromViewItem(AlarmViewMapper.MapRecentAlarm(record)));
                while (_recentAlarmMirror.Count > 10)
                {
                    _recentAlarmMirror.RemoveAt(_recentAlarmMirror.Count - 1);
                }
            });
        }

        private void OnAlarmRecovered(object? sender, AlarmRecord record)
        {
            _uiThreadService.BeginInvoke(() =>
            {
                var item = _recentAlarmMirror.FirstOrDefault(a => a.Id == record.Id);
                if (item != null)
                {
                    _recentAlarmMirror.Remove(item);
                }
            });
        }

        private void OnDataReceived(object? sender, DeviceState state)
        {
            if (state == null)
            {
                return;
            }

            var snapshot = _productionRunService.CreateRealtimeSnapshot(state);

            _uiThreadService.BeginInvoke(() =>
            {
                ActualCount = snapshot.ActualCount;
                TargetCount = snapshot.TargetCount;
                CurrentTemp = snapshot.CurrentTemp;
                SettingTemp = snapshot.SettingTemp;
                RunningTime = snapshot.RunningTime;
                CurrentCycleTime = snapshot.CurrentCycleTime;
                StandardCycleTime = snapshot.StandardCycleTime;
                LiquidLevel = snapshot.LiquidLevel;
                ValveOpen = snapshot.ValveOpen;

                // 首页温度趋势需要反映实时采样，不应依赖生产运行状态。
                AppendChartPoint(snapshot.CurrentTemp);
            });

            _ = CaptureProductionRecordAsync(state);
        }

        private async Task CaptureProductionRecordAsync(DeviceState state)
        {
            try
            {
                var result = await _productionRunService.CaptureIfNeededAsync(state);
                if (result.Saved)
                {
                    LogHelper.Info($"保存生产记录成功：{result.BatchNo}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("保存生产记录失败", ex);
            }
        }

        private void AppendChartPoint(double value)
        {
            if (TempLiveCharts == null || TempLiveCharts.Count == 0)
            {
                return;
            }

            var series = (ColumnSeries<double>)TempLiveCharts[0];
            var values = (ObservableCollection<double>?)series.Values ?? new ObservableCollection<double>();
            if (series.Values == null)
            {
                series.Values = values;
            }

            values.Add(value);
            if (values.Count > 40)
            {
                values.RemoveAt(0);
            }
        }

        private void ClearChart()
        {
            if (TempLiveCharts == null || TempLiveCharts.Count == 0)
            {
                return;
            }

            var series = (ColumnSeries<double>)TempLiveCharts[0];
            if (series.Values is ObservableCollection<double> values)
            {
                values.Clear();
            }
        }

        private void ClearRealtimeValues()
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
        }

        private void ApplyStatusView(ProductionStatusView statusView)
        {
            DeviceStatus = statusView.DeviceStatus;
            IndicatorState = statusView.IndicatorState;

            if (statusView.ShouldClearRealtimeValues)
            {
                ClearRealtimeValues();
                ClearChart();
            }
        }

        public void StopAndDispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            LogHelper.Info("主页视图模型正在解除 PLC 和报警事件订阅。");
            _plcService.DataReceived -= OnDataReceived;
            _plcService.ConnectionChanged -= OnConnectionChanged;
            _alarmService.AlarmTriggered -= OnAlarmTriggered;
            _alarmService.AlarmRecovered -= OnAlarmRecovered;
        }

        public void Dispose()
        {
            StopAndDispose();
        }

        [RelayCommand]
        private async Task StartProductionAsync()
        {
            try
            {
                ApplyStatusView(ProductionStatusView.Starting());

                var result = await _productionRunService.StartAsync();
                ApplyStatusView(_productionRunService.GetStatusView());

                if (result.Succeeded)
                {
                    LogHelper.Info("发送启动命令到PLC");
                }
            }
            catch (AuthorizationException ex)
            {
                ApplyStatusView(_productionRunService.GetStatusView());
                _dialogService.ShowMessage(ex.Message, "权限不足", PromptSeverity.Warning, DialogButtons.Ok);
            }
            catch (Exception ex)
            {
                ApplyStatusView(_productionRunService.GetStatusView());
                LogHelper.Error("发送启动命令到PLC失败", ex);
            }
        }

        [RelayCommand]
        private async Task StopProductionAsync()
        {
            try
            {
                ApplyStatusView(ProductionStatusView.Stopping());

                var result = await _productionRunService.StopAsync();
                ApplyStatusView(_productionRunService.GetStatusView());

                if (result.Succeeded)
                {
                    LogHelper.Info("发送停止命令到PLC");
                }
            }
            catch (AuthorizationException ex)
            {
                ApplyStatusView(_productionRunService.GetStatusView());
                _dialogService.ShowMessage(ex.Message, "权限不足", PromptSeverity.Warning, DialogButtons.Ok);
            }
            catch (Exception ex)
            {
                ApplyStatusView(_productionRunService.GetStatusView());
                LogHelper.Error("发送停止命令到PLC失败", ex);
            }
        }

        [RelayCommand]
        private async Task ResetProductionAsync()
        {
            try
            {
                ApplyStatusView(ProductionStatusView.Resetting());

                var result = await _productionRunService.ResetAsync();
                ClearChart();
                ApplyStatusView(_productionRunService.GetStatusView());
                LogHelper.Info("发送复位命令到PLC");
            }
            catch (AuthorizationException ex)
            {
                ApplyStatusView(_productionRunService.GetStatusView());
                _dialogService.ShowMessage(ex.Message, "权限不足", PromptSeverity.Warning, DialogButtons.Ok);
            }
            catch (Exception ex)
            {
                ApplyStatusView(_productionRunService.GetStatusView());
                LogHelper.Error("发送复位命令到PLC失败", ex);
            }
        }

        public sealed class RecentAlarmItem
        {
            public long Id { get; set; }

            public string Title { get; set; } = string.Empty;

            public string TimeStr { get; set; } = string.Empty;

            public static RecentAlarmItem FromViewItem(RecentAlarmViewItem item)
            {
                return new RecentAlarmItem
                {
                    Id = item.Id,
                    Title = item.Title,
                    TimeStr = item.TimeStr
                };
            }
        }
    }
}
