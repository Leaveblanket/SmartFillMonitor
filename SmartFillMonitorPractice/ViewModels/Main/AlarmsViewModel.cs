using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Dialogs;
using SmartFillMonitor.Services.Alarms;
using SmartFillMonitor.Services.Threading;

namespace SmartFillMonitor.ViewModels.Main
{
        public partial class AlarmsViewModel : ObservableObject, IDisposable
        {
            private static readonly AlarmViewMapper AlarmViewMapper = new();
            private readonly IAlarmService _alarmService;
            private readonly IDialogService _dialogService;
            private readonly IUiThreadService _uiThreadService;
            private bool _isDisposed;

        public ObservableCollection<AlarmUiModel> ActiveAlarms { get; } = new();

        public ObservableCollection<AlarmUiModel> HistoryAlarms { get; } = new();

        public ObservableCollection<KeyValuePair<AlarmSeverity, string>> HistorySeverityOptions { get; } = new();

        [ObservableProperty]
        private int activeAlarmCount;

        [ObservableProperty]
        private DateTime historyStartDate = DateTime.Today.AddDays(-1);

        [ObservableProperty]
        private DateTime historyEndDate = DateTime.Today;

        [ObservableProperty]
        private AlarmSeverity selectedHistorySeverity = AlarmSeverity.All;

        [ObservableProperty]
        private int historyPageIndex = 1;

        [ObservableProperty]
        private int historyPageSize = 20;

        [ObservableProperty]
        private long historyTotalCount;

        public string HistoryPageText => $"第 {HistoryPageIndex} / {HistoryTotalPageCount} 页";

        public long HistoryTotalPageCount => HistoryTotalCount <= 0 ? 1 : (HistoryTotalCount + HistoryPageSize - 1) / HistoryPageSize;

        public bool CanPrevHistoryPage => HistoryPageIndex > 1;

        public bool CanNextHistoryPage => HistoryPageIndex < HistoryTotalPageCount;

        public AlarmsViewModel(
            IAlarmService alarmService,
            IDialogService dialogService,
            IUiThreadService uiThreadService)
        {
            _alarmService = alarmService;
            _dialogService = dialogService;
            _uiThreadService = uiThreadService;

            InitializeHistorySeverityOptions();
            SubscribeToAlarmEvents();

            _ = LoadActiveAlarmsAsync();
            _ = LoadHistoryPageAsync(1);
        }

        private void InitializeHistorySeverityOptions()
        {
            HistorySeverityOptions.Add(new KeyValuePair<AlarmSeverity, string>(AlarmSeverity.All, AlarmSeverity.All.GetDescription()));
            HistorySeverityOptions.Add(new KeyValuePair<AlarmSeverity, string>(AlarmSeverity.Info, AlarmSeverity.Info.GetDescription()));
            HistorySeverityOptions.Add(new KeyValuePair<AlarmSeverity, string>(AlarmSeverity.Warning, AlarmSeverity.Warning.GetDescription()));
            HistorySeverityOptions.Add(new KeyValuePair<AlarmSeverity, string>(AlarmSeverity.Error, AlarmSeverity.Error.GetDescription()));
            HistorySeverityOptions.Add(new KeyValuePair<AlarmSeverity, string>(AlarmSeverity.Critical, AlarmSeverity.Critical.GetDescription()));
        }

        private void SubscribeToAlarmEvents()
        {
            _alarmService.AlarmTriggered += OnAlarmTriggered;
            _alarmService.AlarmAcknowledged += OnAlarmAcknowledged;
            _alarmService.AlarmRecovered += OnAlarmRecovered;
        }

        private void UnsubscribeFromAlarmEvents()
        {
            _alarmService.AlarmTriggered -= OnAlarmTriggered;
            _alarmService.AlarmAcknowledged -= OnAlarmAcknowledged;
            _alarmService.AlarmRecovered -= OnAlarmRecovered;
        }

        partial void OnHistoryPageIndexChanged(int value)
        {
            RaiseHistoryPageState();
        }

        partial void OnHistoryPageSizeChanged(int value)
        {
            RaiseHistoryPageState();
        }

        partial void OnHistoryTotalCountChanged(long value)
        {
            RaiseHistoryPageState();
        }

        private void RaiseHistoryPageState()
        {
            OnPropertyChanged(nameof(HistoryPageText));
            OnPropertyChanged(nameof(HistoryTotalPageCount));
            OnPropertyChanged(nameof(CanPrevHistoryPage));
            OnPropertyChanged(nameof(CanNextHistoryPage));
        }

        private void OnAlarmTriggered(object? sender, AlarmRecord record)
        {
            if (_isDisposed)
            {
                return;
            }

            _uiThreadService.BeginInvoke(() =>
            {
                if (_isDisposed)
                {
                    return;
                }

                if (ActiveAlarms.Any(a => a.Id == record.Id))
                {
                    return;
                }

                ActiveAlarms.Insert(0, CreateAlarmUiModel(record));
                ActiveAlarmCount = ActiveAlarms.Count;
            });
        }

        private void OnAlarmAcknowledged(object? sender, AlarmRecord record)
        {
            if (_isDisposed)
            {
                return;
            }

            _ = LoadActiveAlarmsAsync();
            _ = LoadHistoryPageAsync(HistoryPageIndex);
        }

        private void OnAlarmRecovered(object? sender, AlarmRecord record)
        {
            if (_isDisposed)
            {
                return;
            }

            _uiThreadService.BeginInvoke(() =>
            {
                if (_isDisposed)
                {
                    return;
                }

                RemoveActiveAlarm(record.Id);
            });

            _ = LoadHistoryPageAsync(1);
        }

        private void RemoveActiveAlarm(long alarmId)
        {
            var item = ActiveAlarms.FirstOrDefault(a => a.Id == alarmId);
            if (item != null)
            {
                ActiveAlarms.Remove(item);
            }

            ActiveAlarmCount = ActiveAlarms.Count;
        }


        private async Task LoadActiveAlarmsAsync()
        {
            try
            {
                var records = await _alarmService.GetActiveAlarmsAsync();
                if (_isDisposed)
                {
                    return;
                }

                _uiThreadService.BeginInvoke(() =>
                {
                    if (_isDisposed)
                    {
                        return;
                    }

                    ReplaceActiveAlarms(records);
                });
            }
            catch (Exception ex)
            {
                LogHelper.Error("加载活动报警失败", ex);
            }
        }

        private async Task LoadHistoryPageAsync(int pageIndex)
        {
            try
            {
                if (pageIndex <= 0)
                {
                    pageIndex = 1;
                }

                var endTime = HistoryEndDate.Date.AddDays(1);
                var records = await _alarmService.GetAlarmHistoryAsync(pageIndex, HistoryPageSize, HistoryStartDate, endTime, SelectedHistorySeverity);
                if (_isDisposed)
                {
                    return;
                }

                _uiThreadService.BeginInvoke(() =>
                {
                    if (_isDisposed)
                    {
                        return;
                    }

                    HistoryPageIndex = pageIndex;
                    HistoryTotalCount = records.Total;
                    ReplaceHistoryAlarms(records.Item);
                });
            }
            catch (InfrastructureException ex)
            {
                LogHelper.Error("加载历史报警失败", ex);
                _dialogService.ShowMessage(ex.Message, "历史报警加载失败", PromptSeverity.Error, DialogButtons.Ok);
            }
            catch (Exception ex)
            {
                LogHelper.Error("加载历史报警失败", ex);
            }
        }

        private void ReplaceActiveAlarms(IEnumerable<AlarmRecord> records)
        {
            ActiveAlarms.Clear();
            foreach (var item in records)
            {
                ActiveAlarms.Add(CreateAlarmUiModel(item));
            }

            ActiveAlarmCount = ActiveAlarms.Count;
        }

        private void ReplaceHistoryAlarms(IEnumerable<AlarmRecord> records)
        {
            HistoryAlarms.Clear();
            foreach (var item in records)
            {
                HistoryAlarms.Add(CreateAlarmUiModel(item));
            }
        }

        private AlarmUiModel CreateAlarmUiModel(AlarmRecord record)
        {
            return AlarmUiModel.FromViewItem(AlarmViewMapper.MapAlarm(record));
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadActiveAlarmsAsync();
            await LoadHistoryPageAsync(HistoryPageIndex);
        }

        [RelayCommand]
        private async Task LoadHistoryAlarmsAsync()
        {
            await LoadHistoryPageAsync(1);
        }

        [RelayCommand]
        private async Task PrevHistoryPageAsync()
        {
            if (!CanPrevHistoryPage)
            {
                return;
            }

            await LoadHistoryPageAsync(HistoryPageIndex - 1);
        }

        [RelayCommand]
        private async Task NextHistoryPageAsync()
        {
            if (!CanNextHistoryPage)
            {
                return;
            }

            await LoadHistoryPageAsync(HistoryPageIndex + 1);
        }

        [RelayCommand]
        private async Task AcknowledgeAlarmAsync(AlarmUiModel? alarm)
        {
            if (alarm == null)
            {
                return;
            }

            try
            {
                var result = await _alarmService.HandleAlarmActionAsync(
                    alarm.Id,
                    alarm.IsAcknowledged,
                    alarm.ProcessSuggestion);

                if (!result)
                {
                    _dialogService.ShowMessage("操作失败", "提示", PromptSeverity.Warning, DialogButtons.Ok);
                    return;
                }

                await LoadActiveAlarmsAsync();
                await LoadHistoryPageAsync(1);
                LogHelper.Info($"操作成功：{alarm.Code}");
            }
            catch (AuthorizationException ex)
            {
                _dialogService.ShowMessage(ex.Message, "权限不足", PromptSeverity.Warning, DialogButtons.Ok);
            }
            catch (BusinessException ex)
            {
                _dialogService.ShowMessage(ex.Message, "提示", PromptSeverity.Warning, DialogButtons.Ok);
            }
            catch (Exception ex)
            {
                LogHelper.Error($"处理报警失败：{alarm.Code}", ex);
                _dialogService.ShowMessage("处理报警失败，请稍后重试。", "错误", PromptSeverity.Error, DialogButtons.Ok);
            }
        }

        [RelayCommand]
        private async Task TestAlarmAsync()
        {
            try
            {
                await _alarmService.TriggerTestAlarmAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error("触发测试报警失败", ex);
                _dialogService.ShowMessage("触发测试报警失败，请稍后重试。", "错误", PromptSeverity.Error, DialogButtons.Ok);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            UnsubscribeFromAlarmEvents();
        }

        public partial class AlarmUiModel : ObservableObject
        {
            [ObservableProperty]
            private long _id;

            [ObservableProperty]
            private string _code = string.Empty;

            [ObservableProperty]
            private string _title = string.Empty;

            [ObservableProperty]
            private string _timeStr = string.Empty;

            [ObservableProperty]
            private string _description = string.Empty;

            [ObservableProperty]
            private string _severityText = string.Empty;

            [ObservableProperty]
            private string _statusText = string.Empty;

            [ObservableProperty]
            private string _ackUser = string.Empty;

            [ObservableProperty]
            private string _ackTimeStr = string.Empty;

            [ObservableProperty]
            private string _recoverUser = string.Empty;

            [ObservableProperty]
            private string _recoverTimeStr = string.Empty;

            [ObservableProperty]
            private string _triggeredBy = string.Empty;

            [ObservableProperty]
            private string _triggeredByTypeText = string.Empty;

            [ObservableProperty]
            private string _processSuggestion = string.Empty;

            [ObservableProperty]
            private string _durationText = string.Empty;

            [ObservableProperty]
            private bool _isActive;

            [ObservableProperty]
            private bool _isAcknowledged;

            [ObservableProperty]
            private bool _canAcknowledge = true;

            [ObservableProperty]
            private string _acknowledgeButtonText = "确认报警";

            public static AlarmUiModel FromViewItem(AlarmViewItem item)
            {
                return new AlarmUiModel
                {
                    Id = item.Id,
                    Code = item.Code,
                    Title = item.Title,
                    Description = item.Description,
                    SeverityText = item.SeverityText,
                    TimeStr = item.TimeStr,
                    StatusText = item.StatusText,
                    AckUser = item.AckUser,
                    AckTimeStr = item.AckTimeStr,
                    RecoverUser = item.RecoverUser,
                    RecoverTimeStr = item.RecoverTimeStr,
                    TriggeredBy = item.TriggeredBy,
                    TriggeredByTypeText = item.TriggeredByTypeText,
                    ProcessSuggestion = item.ProcessSuggestion,
                    DurationText = item.DurationText,
                    IsActive = item.IsActive,
                    IsAcknowledged = item.IsAcknowledged,
                    CanAcknowledge = item.CanAcknowledge,
                    AcknowledgeButtonText = item.AcknowledgeButtonText,
                };
            }
        }
    }
}
