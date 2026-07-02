using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FreeSql;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Plc;
using SmartFillMonitor.Services.Session;
using SmartFillMonitor.Services.Threading;

namespace SmartFillMonitor.Tests;

internal sealed class FakePlcService : IPlcService
{
    public event EventHandler<DeviceState>? DataReceived;
    public event EventHandler<bool>? ConnectionChanged;

    public bool IsConnected { get; set; }
    public PlcReadSnapshot Snapshot { get; set; } = new PlcReadSnapshot();

    public int InitializeCallCount { get; private set; }
    public int ConnectCallCount { get; private set; }
    public int DisconnectCallCount { get; private set; }

    public Task InitializeAsync(DeviceSettings settings)
    {
        InitializeCallCount++;
        return Task.CompletedTask;
    }

    public Task ConnectAsync()
    {
        ConnectCallCount++;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        DisconnectCallCount++;
        return Task.CompletedTask;
    }

    public Task<bool> PulseCommandAsync(string command, int delayMs = 120)
    {
        return Task.FromResult(true);
    }

    public void RaiseConnectionChanged(bool connected)
    {
        IsConnected = connected;
        ConnectionChanged?.Invoke(this, connected);
    }

    public void RaiseDataReceived(DeviceState state)
    {
        Snapshot = new PlcReadSnapshot
        {
            HasSuccessfulRead = true,
            LastReadSuccessTime = DateTime.Now,
            LastDeviceState = state
        };
        DataReceived?.Invoke(this, state);
    }
}

internal sealed class AsyncDisposalAwarePlcService : IPlcService, IDisposable, IAsyncDisposable
{
    public static List<AsyncDisposalAwarePlcService> Instances { get; } = new();

    public AsyncDisposalAwarePlcService()
    {
        Instances.Add(this);
    }

    public event EventHandler<DeviceState>? DataReceived;

    public event EventHandler<bool>? ConnectionChanged;

    public bool IsConnected { get; private set; }

    public PlcReadSnapshot Snapshot { get; private set; } = new PlcReadSnapshot();

    public int InitializeCallCount { get; private set; }

    public int AsyncDisposeCallCount { get; private set; }

    public Task InitializeAsync(DeviceSettings settings)
    {
        InitializeCallCount++;
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task ConnectAsync()
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task<bool> PulseCommandAsync(string command, int delayMs = 120)
    {
        return Task.FromResult(true);
    }

    public void Dispose()
    {
        throw new InvalidOperationException("Coordinator must dispose session scope asynchronously.");
    }

    public ValueTask DisposeAsync()
    {
        AsyncDisposeCallCount++;
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeAlarmService : IAlarmService
{
#pragma warning disable CS0067
    public event EventHandler<AlarmRecord>? AlarmTriggered;
    public event EventHandler<AlarmRecord>? AlarmAcknowledged;
    public event EventHandler<AlarmRecord>? AlarmRecovered;
#pragma warning restore CS0067

    public List<AlarmRecord> ActiveAlarms { get; } = new();

    public Task TriggerAlarmAsync(AlarmRecord alarmRecord)
    {
        ActiveAlarms.Add(alarmRecord);
        AlarmTriggered?.Invoke(this, alarmRecord);
        return Task.CompletedTask;
    }

    public Task TriggerTestAlarmAsync() => Task.CompletedTask;

    public Task<bool> AcknowledgeAlarmAsync(long alarmId, string processSuggestion = "") => Task.FromResult(true);

    public Task<bool> RecoverAlarmAsync(AlarmCode alarmCode) => Task.FromResult(true);

    public Task<bool> RecoverAlarmAsync(long alarmId) => Task.FromResult(true);

    public Task<bool> HandleAlarmActionAsync(long alarmId, bool isAcknowledged, string processSuggestion = "")
    {
        return Task.FromResult(true);
    }

    public Task<bool> RecoverTestAlarmAsync() => Task.FromResult(true);

    public Task<List<AlarmRecord>> GetActiveAlarmsAsync() => Task.FromResult(ActiveAlarms.ToList());

    public Task<(List<AlarmRecord> Item, long Total)> GetAlarmHistoryAsync(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null, AlarmSeverity alarmSeverity = AlarmSeverity.All)
    {
        return Task.FromResult((new List<AlarmRecord>(), 0L));
    }
}

internal sealed class FakeProductionRecordServices : IProductionRecordService
{
    public Task<bool> SaveAsync(ProductionRecord record) => Task.FromResult(true);

    public Task<List<ProductionRecord>> QueryAsync(DateTime start, DateTime end) => Task.FromResult(new List<ProductionRecord>());

    public Task ExportAsync(List<ProductionRecord> records, string filePath) => Task.CompletedTask;
}

internal sealed class FakeProductionRunService : IProductionRunService
{
    public ProductionRunState CurrentState { get; set; } = ProductionRunState.Ready;

    public ProductionStatusView StatusView { get; set; } = ProductionStatusView.FromState(ProductionRunState.Ready);

    public ProductionRealtimeSnapshot RealtimeSnapshot { get; set; } = new ProductionRealtimeSnapshot(0, 0, 0, 0, 0, 0, 0, 0, false, false);

    public ProductionCommandResult ConnectionChangedResult { get; set; } = ProductionCommandResult.Success(ProductionRunState.Ready, string.Empty);

    public ProductionCommandResult StartResult { get; set; } = ProductionCommandResult.Success(ProductionRunState.Running, string.Empty);

    public ProductionCommandResult StopResult { get; set; } = ProductionCommandResult.Success(ProductionRunState.Stopped, string.Empty);

    public ProductionCommandResult ResetResult { get; set; } = ProductionCommandResult.Success(ProductionRunState.Ready, string.Empty);

    public ProductionCaptureResult CaptureResult { get; set; } = ProductionCaptureResult.Skipped(string.Empty);

    public ProductionStatusView GetStatusView(bool shouldClearRealtimeValues = false)
    {
        return shouldClearRealtimeValues
            ? new ProductionStatusView(StatusView.DeviceStatus, StatusView.IndicatorState, true)
            : StatusView;
    }

    public ProductionStatusView GetStatusViewForConnectionChanged(bool connected)
    {
        CurrentState = ConnectionChangedResult.State;
        return !connected
            ? new ProductionStatusView(StatusView.DeviceStatus, StatusView.IndicatorState, true)
            : StatusView;
    }

    public ProductionRealtimeSnapshot CreateRealtimeSnapshot(DeviceState state)
    {
        return RealtimeSnapshot;
    }

    public ProductionCommandResult ApplyConnectionChanged(bool connected)
    {
        CurrentState = ConnectionChangedResult.State;
        return ConnectionChangedResult;
    }

    public Task<ProductionCommandResult> StartAsync()
    {
        CurrentState = StartResult.State;
        return Task.FromResult(StartResult);
    }

    public Task<ProductionCommandResult> StopAsync()
    {
        CurrentState = StopResult.State;
        return Task.FromResult(StopResult);
    }

    public Task<ProductionCommandResult> ResetAsync()
    {
        CurrentState = ResetResult.State;
        return Task.FromResult(ResetResult);
    }

    public Task<ProductionCaptureResult> CaptureIfNeededAsync(DeviceState state)
    {
        return Task.FromResult(CaptureResult);
    }
}

internal sealed class FakeUserService : IUserService
{
    public string LastErrorMessage { get; private set; } = string.Empty;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<RegisterOperationResult> RegisterAsync(string userName, string password, Role role)
    {
        return Task.FromResult(RegisterOperationResult.Success());
    }

    public Task<LoginOperationResult> AuthenticateAsync(string userName, string password)
    {
        return Task.FromResult(LoginOperationResult.Success());
    }

    public Task<ChangePasswordOperationResult> ChangeCurrentUserPasswordAsync(string currentPassword, string newPassword)
    {
        return Task.FromResult(ChangePasswordOperationResult.Success());
    }

    public Task LogoutAsync() => Task.CompletedTask;

    public Task<List<User>> GetAllUsersAsync() => Task.FromResult(new List<User>());

    public Task<List<User>> GetLoginUsersAsync() => Task.FromResult(new List<User>());
}

internal sealed class FakeNavigationService : IMainNavigationService, IAuthNavigationService
{
    public event Action<object?>? CurrentViewModelChanged;

    public object? LastViewModel { get; private set; }

    public object? LastParameter { get; private set; }

    public void NavigateTo<T>() where T : class
    {
        NavigateTo<T>(null);
    }

    public void NavigateTo<T>(object? parameter) where T : class
    {
        LastViewModel = typeof(T);
        LastParameter = parameter;
        CurrentViewModelChanged?.Invoke(LastViewModel);
    }
}

internal sealed class FakeSessionService : ISessionService
{
    private IReadOnlySet<Permission> _permissions = new HashSet<Permission>();

    public event Action<User?>? SessionChanged;

    public User? CurrentUser { get; private set; }

    public Role? CurrentRole { get; private set; }

    public bool IsLoggedIn => CurrentUser != null;

    public bool HasPermission(Permission permission)
    {
        return _permissions.Contains(permission);
    }

    public void SetCurrentUser(User user, IReadOnlySet<Permission>? permissions = null)
    {
        CurrentUser = user;
        CurrentRole = user.Role;
        _permissions = permissions ?? RolePermissionPolicy.BuildPermissions(user.Role);
        SessionChanged?.Invoke(CurrentUser);
    }

    public void Clear()
    {
        CurrentUser = null;
        CurrentRole = null;
        _permissions = new HashSet<Permission>();
        SessionChanged?.Invoke(null);
    }
}

internal sealed class FakeSessionCoordinator : ISessionCoordinator
{
    public bool IsSwitchingUser { get; set; }

    public bool IsExiting { get; set; }

    public int StartCallCount { get; private set; }

    public int SwitchUserCallCount { get; private set; }

    public int ExitCallCount { get; private set; }

    public Task<bool> StartAsync()
    {
        StartCallCount++;
        return Task.FromResult(true);
    }

    public Task<bool> SwitchUserAsync()
    {
        SwitchUserCallCount++;
        return Task.FromResult(true);
    }

    public Task ExitAsync()
    {
        ExitCallCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeLogLiveFeed : ILogLiveFeed
{
    private readonly List<string> _messages = new();

    public int Capacity => 100;

    public int ResetCallCount { get; private set; }

    public event EventHandler<string>? LogAppended;

    public event EventHandler? ResetRequested;

    public IReadOnlyList<string> GetSnapshot() => _messages;

    public void Publish(string message)
    {
        _messages.Add(message);
        LogAppended?.Invoke(this, message);
    }

    public void Reset()
    {
        _messages.Clear();
        ResetCallCount++;
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class FakeDialogService : IDialogService
{
    public DialogResult Result { get; set; } = DialogResult.Ok;

    public int ShowMessageCallCount { get; private set; }

    public string? LastMessage { get; private set; }

    public string? LastCaption { get; private set; }

    public PromptSeverity LastSeverity { get; private set; }

    public DialogButtons LastButtons { get; private set; }

    public DialogResult ShowMessage(string message, string caption, PromptSeverity severity, DialogButtons buttons = DialogButtons.Ok)
    {
        ShowMessageCallCount++;
        LastMessage = message;
        LastCaption = caption;
        LastSeverity = severity;
        LastButtons = buttons;
        return Result;
    }
}

// Phase 3 transition fake used only to align constructor signatures in non-threading tests.
// It executes inline and does not simulate real Dispatcher thread switching behavior.
internal sealed class ImmediateUiThreadService : IUiThreadService
{
    public int InvokeCallCount { get; private set; }

    public int BeginInvokeCallCount { get; private set; }

    public bool CheckAccess() => true;

    public void Invoke(Action action)
    {
        InvokeCallCount++;
        action();
    }

    public void BeginInvoke(Action action)
    {
        BeginInvokeCallCount++;
        action();
    }
}

internal sealed class FakeSystemLogService : ISystemLogService
{
    public ISelect<SystemLog> BuildQuery(SystemLogQueryFilter filter)
    {
        throw new NotSupportedException();
    }

    public Task<(List<SystemLog> Items, long Total)> QueryAsync(SystemLogQueryFilter filter, int pageIndex, int pageSize)
    {
        return Task.FromResult((new List<SystemLog>(), 0L));
    }

    public Task<string> ExportAsync(SystemLogQueryFilter filter, string filePath)
    {
        return Task.FromResult(filePath);
    }
}

internal sealed class FakeConfigService : IConfigService
{
    public string GetSettingFilePath() => string.Empty;
    public Task<DeviceSettings> LoadSettingsAsync() => Task.FromResult(new DeviceSettings { PortName = "COM1", BaudRate = 9600, DataBits = 8, Parity = "None", StopBits = "One", AutoConnect = false });
    public Task<bool> SaveDeviceSettingsAsync(DeviceSettings settings) => Task.FromResult(true);
    public void BackCorruptFile(string originalPath) { }
    public void Validate(DeviceSettings settings) { }
}

internal sealed class FakeSerialPortService : ISerialPortService
{
    public string[] Ports { get; set; } = new[] { "COM1" };

    public string[] GetAvailablePorts() => Ports;
}

internal sealed class FakeAuthorizationService : IAuthorizationService
{
    public void EnsurePermission(Permission permission, string action) { }
}

internal sealed class FakePlcTransport : IPlcTransport
{
    private readonly ushort[] _registers;
    private readonly ushort[] _barcodeRegisters;

    public FakePlcTransport(ushort[]? registers = null, string barcode = "BATCH-001")
    {
        _registers = registers ?? new ushort[] { 10, 100, 2500, 3000, 1, 12, 15, 500, 1, 2 };
        _barcodeRegisters = EncodeBarcode(barcode);
    }

    public bool IsConnected { get; private set; }

    public int ConnectCount { get; private set; }

    public int DisconnectCount { get; private set; }

    public int ReadCount { get; private set; }

    public Task ConnectAsync(DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        ConnectCount++;
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        DisconnectCount++;
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        if (!IsConnected)
        {
            throw new InvalidOperationException("Disconnected");
        }

        return Task.FromResult(startAddress == 0 ? _registers : _barcodeRegisters);
    }

    public Task WriteSingleCoilAsync(byte slaveId, ushort address, bool value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Disconnected");
        }

        return Task.CompletedTask;
    }

    public Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Disconnected");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IsConnected = false;
    }

    private static ushort[] EncodeBarcode(string barcode)
    {
        var text = barcode ?? string.Empty;
        var chars = text.ToCharArray();
        var values = new List<ushort>();
        for (var i = 0; i < chars.Length; i += 2)
        {
            var high = (byte)chars[i];
            var low = i + 1 < chars.Length ? (byte)chars[i + 1] : (byte)0;
            values.Add((ushort)((high << 8) | low));
        }

        while (values.Count < 10)
        {
            values.Add(0);
        }

        return values.ToArray();
    }
}
