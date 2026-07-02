using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Dialogs;
using SmartFillMonitor.Services.Logging;
using SmartFillMonitor.Services.Security;
using SmartFillMonitor.Services.Threading;

namespace SmartFillMonitor.ViewModels.Main
{
    public partial class LogsViewModel : ObservableObject, IDisposable
    {
        private const int PageSize = 50;
        private readonly ILogLiveFeed _logLiveFeed;
        private readonly ISystemLogService _systemLogService;
        private readonly IAuthorizationService _authorizationService;
        private readonly IDialogService _dialogService;
        private readonly IUiThreadService _uiThreadService;
        private bool _isDisposed;

        [ObservableProperty]
        private DateTime startDate = new(2026, 2, 1);

        [ObservableProperty]
        private DateTime endDate = DateTime.Today.AddDays(1).AddSeconds(-1);

        [ObservableProperty]
        private string selectedLevel = "All";

        public ObservableCollection<string> LogLevels { get; } = new()
        {
            "All",
            "Debug",
            "Information",
            "Warning",
            "Error",
            "Fatal"
        };

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private int totalCount;

        [ObservableProperty]
        private int pageIndex = 1;

        [ObservableProperty]
        private ObservableCollection<SystemLog> logs = new();

        public ObservableCollection<string> LiveLogs { get; } = new();

        public LogsViewModel(
            ILogLiveFeed logLiveFeed,
            ISystemLogService systemLogService,
            IAuthorizationService authorizationService,
            IDialogService dialogService,
            IUiThreadService uiThreadService)
        {
            _logLiveFeed = logLiveFeed;
            _systemLogService = systemLogService;
            _authorizationService = authorizationService;
            _dialogService = dialogService;
            _uiThreadService = uiThreadService;

            foreach (var entry in _logLiveFeed.GetSnapshot())
            {
                LiveLogs.Add(entry);
            }

            _logLiveFeed.LogAppended += OnLogAppended;
            _logLiveFeed.ResetRequested += OnResetRequested;
            _ = LoadLogsAsync();
        }

        public void Dispose()
        {
            _isDisposed = true;
            _logLiveFeed.LogAppended -= OnLogAppended;
            _logLiveFeed.ResetRequested -= OnResetRequested;
        }

        [RelayCommand]
        private async Task PreviousPageAsync()
        {
            if (PageIndex <= 1)
            {
                return;
            }

            PageIndex--;
            await LoadLogsAsync();
        }

        [RelayCommand]
        private async Task NextPageAsync()
        {
            if (Logs.Count < PageSize)
            {
                return;
            }

            PageIndex++;
            await LoadLogsAsync();
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            PageIndex = 1;
            await LoadLogsAsync();
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            try
            {
                var path = $"Logs_Export_{DateTime.Now:yyyyMMddHHmmss}.csv";
                var (filter, _, _) = BuildQueryRequest();
                _authorizationService.EnsurePermission(Permission.ExportLogs, "导出日志");
                var fullPath = await _systemLogService.ExportAsync(filter, path);
                _dialogService.ShowMessage(
                    $"日志已导出到文件：{System.IO.Path.GetFullPath(fullPath)}",
                    "提示",
                    PromptSeverity.Information,
                    DialogButtons.Ok);
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
                LogHelper.Error("导出日志失败", ex);
                _dialogService.ShowMessage("日志导出失败，请稍后重试。", "错误", PromptSeverity.Error, DialogButtons.Ok);
            }
        }

        private async Task LoadLogsAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var (filter, normalizedPageIndex, pageSize) = BuildQueryRequest();
                var result = await _systemLogService.QueryAsync(filter, normalizedPageIndex, pageSize);

                StartDate = filter.StartTime;
                EndDate = filter.EndTime;
                PageIndex = normalizedPageIndex;
                TotalCount = (int)result.Total;
                Logs = new ObservableCollection<SystemLog>(result.Items);
            }
            catch (Exception ex)
            {
                LogHelper.Error("加载日志失败", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnLogAppended(object? sender, string message)
        {
            AppendLiveLog(message);
        }

        private void OnResetRequested(object? sender, EventArgs e)
        {
            ClearLiveLogs();
        }

        private void AppendLiveLog(string message)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _uiThreadService.BeginInvoke(() =>
            {
                if (_isDisposed)
                {
                    return;
                }

                LiveLogs.Add(message);
                TrimLiveLogs();
            });
        }

        private void ClearLiveLogs()
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

                LiveLogs.Clear();
            });
        }

        private void TrimLiveLogs()
        {
            while (LiveLogs.Count > _logLiveFeed.Capacity)
            {
                LiveLogs.RemoveAt(0);
            }
        }

        private (SystemLogQueryFilter Filter, int PageIndex, int PageSize) BuildQueryRequest()
        {
            var normalizedStartDate = StartDate.Date;
            var normalizedEndDate = EndDate < StartDate ? StartDate : EndDate;
            var normalizedPageIndex = PageIndex <= 0 ? 1 : PageIndex;

            return (
                new SystemLogQueryFilter
                {
                    StartTime = normalizedStartDate,
                    EndTime = normalizedEndDate,
                    Level = SelectedLevel,
                    SearchText = SearchText
                },
                normalizedPageIndex,
                PageSize);
        }
    }
}
