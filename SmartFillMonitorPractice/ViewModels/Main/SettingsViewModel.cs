using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Dialogs;
using SmartFillMonitor.Services.Plc;
using SmartFillMonitor.Services.Security;

namespace SmartFillMonitor.ViewModels.Main
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly IPlcService _plcService;
        private readonly ISerialPortService _serialPortService;
        private readonly IAuthorizationService _authorizationService;
        private readonly IDialogService _dialogService;
        public ObservableCollection<string> PortNames { get; } = new();

        public ObservableCollection<int> BaudRates { get; } = new()
        {
            9600, 19200, 38400, 57600, 115200
        };

        public ObservableCollection<int> DataBitsOptions { get; } = new()
        {
            7, 8
        };

        public ObservableCollection<string> ParityOptions { get; } = new()
        {
            "None", "Odd", "Even"
        };

        public ObservableCollection<string> StopBitsOptions { get; } = new()
        {
            "None", "One", "Two"
        };

        [ObservableProperty]
        private string portName = "COM3";

        [ObservableProperty]
        private int selectedBaud = 115200;

        [ObservableProperty]
        private int selectedDataBits = 8;

        [ObservableProperty]
        private string selectedParity = "None";

        [ObservableProperty]
        private string selectedStopBits = "One";

        [ObservableProperty]
        private bool autoConnect = true;

        [ObservableProperty]
        private bool alarmSound = true;

        [ObservableProperty]
        private bool debugLogMode;

        public SettingsViewModel(
            IConfigService configService,
            IPlcService plcService,
            ISerialPortService serialPortService,
            IAuthorizationService authorizationService,
            IDialogService dialogService)
        {
            _configService = configService;
            _plcService = plcService;
            _serialPortService = serialPortService;
            _authorizationService = authorizationService;
            _dialogService = dialogService;

            RefreshPortList();
            _ = LoadSettingsAsync();
        }

        private void RefreshPortList()
        {
            var currentSelection = PortName;
            string[] ports;
            string selected;

            try
            {
                var portList = _serialPortService.GetAvailablePorts().ToList();
                selected = currentSelection ?? string.Empty;

                if (!string.IsNullOrEmpty(selected) && !portList.Contains(selected))
                {
                    portList.Add(selected);
                }

                ports = portList.ToArray();
            }
            catch (Exception ex)
            {
                LogHelper.Error($"获取串口列表失败：{ex.Message}");
                ports = ["COM1", "COM2"];
                selected = string.IsNullOrWhiteSpace(currentSelection) ? ports[0] : currentSelection;
            }

            PortName = string.Empty;
            PortNames.Clear();
            foreach (var item in ports)
            {
                PortNames.Add(item);
            }

            PortName = selected;
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                var settings = await _configService.LoadSettingsAsync();
                PortName = settings.PortName;
                SelectedBaud = settings.BaudRate;
                SelectedDataBits = settings.DataBits;
                SelectedParity = settings.Parity;
                SelectedStopBits = settings.StopBits;
                AutoConnect = settings.AutoConnect;
                AlarmSound = settings.AlarmSound;
                DebugLogMode = settings.DebugLogMode;
            }
            catch (InfrastructureException ex)
            {
                LogHelper.Error($"加载配置失败：{ex.Message}", ex);
                _dialogService.ShowMessage(ex.Message, "配置加载失败", PromptSeverity.Error, DialogButtons.Ok);
            }
            catch (Exception ex)
            {
                LogHelper.Error($"加载配置失败：{ex.Message}", ex);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                var model = new DeviceSettings
                {
                    PortName = PortName,
                    BaudRate = SelectedBaud,
                    DataBits = SelectedDataBits,
                    Parity = SelectedParity,
                    StopBits = SelectedStopBits,
                    AutoConnect = AutoConnect,
                    AlarmSound = AlarmSound,
                    DebugLogMode = DebugLogMode
                };

                _authorizationService.EnsurePermission(Permission.ManageSettings, "保存系统设置");

                var saved = await _configService.SaveDeviceSettingsAsync(model);
                if (!saved)
                {
                    _dialogService.ShowMessage("配置保存失败，请稍后重试。", "错误", PromptSeverity.Error, DialogButtons.Ok);
                    return;
                }

                try
                {
                    await _plcService.InitializeAsync(model);
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"PLC 重新初始化失败：{ex.Message}", ex);
                    _dialogService.ShowMessage("配置已保存，但 PLC 重新初始化失败，请检查设备连接。", "提示", PromptSeverity.Warning, DialogButtons.Ok);
                    return;
                }

                RefreshPortList();
                LogHelper.Info("配置保存成功，PLC 已按最新参数重新初始化。");
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
                LogHelper.Error($"保存配置失败：{ex.Message}", ex);
                _dialogService.ShowMessage("保存配置失败，请稍后重试。", "错误", PromptSeverity.Error, DialogButtons.Ok);
            }
        }
    }
}
