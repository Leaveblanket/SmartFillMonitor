using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Threading;

namespace SmartFillMonitor.ViewModels.Main
{
    public partial class HeaderViewModel : ObservableObject, IDisposable
    {
        // 内联 HeaderContentSnapshot 类型
        private sealed record HeaderContentSnapshot(
            bool IsDashboard,
            bool IsSimulation,
            LightState? IndicatorState,
            string? CurrentBatchNo);

        [ObservableProperty]
        private string currentBatchNo = string.Empty;

        [ObservableProperty]
        private string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [ObservableProperty]
        private bool isPlcConnected;

        [ObservableProperty]
        private LightState indicatorState = LightState.Red;

        private readonly IPlcService _plcService;
        private readonly IUiThreadService _uiThreadService;
        private readonly DispatcherTimer _timer;
        private object? _activeContent;
        private INotifyPropertyChanged? _activeContentNotifier;
        private bool _stopped;

        public HeaderViewModel(IPlcService plcService, IUiThreadService uiThreadService)
        {
            _plcService = plcService;
            _uiThreadService = uiThreadService;
            _plcService.ConnectionChanged += OnConnectionChanged;
            _plcService.DataReceived += OnDataReceived;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            IsPlcConnected = _plcService.Snapshot.HasSuccessfulRead;
            SyncFromContent();
        }

        public void Activate(object? mainContent)
        {
            if (_stopped)
            {
                return;
            }

            if (ReferenceEquals(_activeContent, mainContent))
            {
                return;
            }

            if (_activeContentNotifier != null)
            {
                _activeContentNotifier.PropertyChanged -= ActiveContent_PropertyChanged;
            }

            _activeContent = mainContent;
            _activeContentNotifier = mainContent as INotifyPropertyChanged;
            if (_activeContentNotifier != null)
            {
                _activeContentNotifier.PropertyChanged += ActiveContent_PropertyChanged;
            }

            _uiThreadService.BeginInvoke(SyncFromContent);
        }

        public void Stop()
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            LogHelper.Info("Header 状态服务收到停止请求。");

            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _plcService.ConnectionChanged -= OnConnectionChanged;
            _plcService.DataReceived -= OnDataReceived;

            if (_activeContentNotifier != null)
            {
                _activeContentNotifier.PropertyChanged -= ActiveContent_PropertyChanged;
                _activeContentNotifier = null;
            }

            _activeContent = null;
        }

        public void Dispose()
        {
            Stop();
        }

        private void OnConnectionChanged(object? sender, bool connected)
        {
            if (_stopped)
            {
                return;
            }

            _uiThreadService.BeginInvoke(() =>
            {
                SyncFromContent();
            });
        }

        private void OnDataReceived(object? sender, DeviceState state)
        {
            if (_stopped || state == null)
            {
                return;
            }

            _uiThreadService.BeginInvoke(SyncFromContent);
        }

        private void ActiveContent_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_stopped)
            {
                return;
            }

            if (e.PropertyName == nameof(DashBoardViewModel.IndicatorState) ||
                e.PropertyName == nameof(SimulationViewModel.IndicatorState) ||
                e.PropertyName == nameof(SimulationViewModel.CurrentBatchNo))
            {
                _uiThreadService.BeginInvoke(SyncFromContent);
            }
        }

        private void SyncFromContent()
        {
            if (_stopped)
            {
                return;
            }

            // 内联 HeaderStatusService.Build 逻辑
            var snapshot = _plcService.Snapshot;
            var isConnected = snapshot.HasSuccessfulRead;
            var barcode = snapshot.LastDeviceState?.BarCode ?? string.Empty;
            var contentSnapshot = BuildContentSnapshot();

            if (contentSnapshot == null)
            {
                IndicatorState = isConnected ? LightState.Yellow : LightState.Red;
                CurrentBatchNo = barcode;
                IsPlcConnected = isConnected;
                return;
            }

            if (contentSnapshot.IsSimulation)
            {
                IndicatorState = contentSnapshot.IndicatorState ?? (isConnected ? LightState.Yellow : LightState.Red);
                CurrentBatchNo = contentSnapshot.CurrentBatchNo;
                IsPlcConnected = isConnected;
                return;
            }

            if (contentSnapshot.IsDashboard)
            {
                IndicatorState = contentSnapshot.IndicatorState ?? (isConnected ? LightState.Yellow : LightState.Red);
                CurrentBatchNo = barcode;
                IsPlcConnected = isConnected;
                return;
            }

            IndicatorState = isConnected ? LightState.Yellow : LightState.Red;
            CurrentBatchNo = barcode;
            IsPlcConnected = isConnected;
        }

        private HeaderContentSnapshot? BuildContentSnapshot()
        {
            return _activeContent switch
            {
                SimulationViewModel simulation => new HeaderContentSnapshot(
                    IsDashboard: false,
                    IsSimulation: true,
                    IndicatorState: simulation.IndicatorState,
                    CurrentBatchNo: simulation.CurrentBatchNo),
                DashBoardViewModel dashboard => new HeaderContentSnapshot(
                    IsDashboard: true,
                    IsSimulation: false,
                    IndicatorState: dashboard.IndicatorState,
                    CurrentBatchNo: string.Empty),
                _ => null
            };
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_stopped)
            {
                return;
            }

            CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

    }
}
