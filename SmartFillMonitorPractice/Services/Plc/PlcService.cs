using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Plc
{
    public class PlcService : IPlcService, IDisposable, IAsyncDisposable
    {
        private const byte SlaveId = 1;
        private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(3);
        private static readonly DeviceStateMapper DeviceStateMapper = new();
        private static readonly Dictionary<string, ushort> CommandAddressMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Start", 1 },
            { "Stop", 2 },
            { "Reset", 3 },
            { "Test", 4 }
        };

        private readonly IPlcTransport _transport;
        private readonly IAuthorizationService _authorizationService;
        private readonly IAuditService _auditService;
        private readonly object _pollSyncRoot = new();
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        private CancellationTokenSource? _pollCancellationTokenSource;
        private Task? _pollTask;
        private DeviceSettings? _currentSettings;
        private DateTime _lastReconnectTime = DateTime.MinValue;
        private bool _manualDisconnect = true;
        private bool _disposed;
        private bool _hasPublishedConnectionState;
        private bool _lastPublishedConnectionState;

        public PlcService(IPlcTransport transport, IAuthorizationService authorizationService, IAuditService auditService)
        {
            _transport = transport;
            _authorizationService = authorizationService;
            _auditService = auditService;
        }


        #region Events and State

        public event EventHandler<DeviceState>? DataReceived;

        public event EventHandler<bool>? ConnectionChanged;

        public bool IsConnected => _transport.IsConnected;

        public PlcReadSnapshot Snapshot { get; private set; } = new PlcReadSnapshot();

        #endregion

        #region Public Methods

        public async Task InitializeAsync(DeviceSettings settings)
        {
            ThrowIfDisposed();

            _currentSettings = settings;
            LogHelper.Info($"收到 PLC 服务初始化请求。AutoConnect={settings.AutoConnect}，Port={settings.PortName}");
            await DisconnectAsync();

            if (settings.AutoConnect)
            {
                await ConnectAsync();
            }
        }

        public async Task ConnectAsync()
        {
            ThrowIfDisposed();

            await _connectionLock.WaitAsync();
            try
            {
                _manualDisconnect = false;
                var connected = await ConnectTransportIfNeededAsync();
                if (!connected)
                {
                    return;
                }

                EnsurePollingLoopStarted();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            if (_disposed)
            {
                return;
            }

            _manualDisconnect = true;
            LogHelper.Info("收到 PLC 断开连接请求。");

            var pollTask = CancelPollingLoopAndGetTask();
            if (pollTask != null)
            {
                var completedTask = await Task.WhenAny(pollTask, Task.Delay(1500)); //等待当前轮询任务成功取消或超时
                if (completedTask != pollTask)
                {
                    LogHelper.Warn("PLC 轮询未能立即停止，当前关闭速度仍受串口读取超时限制。");
                }
            }

            await _connectionLock.WaitAsync();
            try
            {
                await _transport.DisconnectAsync();
                ResetReadState(clearLastReconnectTime: true);
                PublishConnectionChanged(false);
                LogHelper.Info("PLC 传输层已断开。");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<bool> PulseCommandAsync(string command, int delayMs = 120)
        {
            ThrowIfDisposed();

            _authorizationService.EnsurePermission(Permission.ControlPlc, $"执行 PLC 命令：{command}");

            var setHigh = await WriteCommandAsync(command, true);
            if (!setHigh)
            {
                _auditService.Operation("PlcWriteCommand", "Failed", $"PLC 命令执行失败：命令={command}；阶段=置位");
                return false;
            }

            await Task.Delay(delayMs);
            var setLow = await WriteCommandAsync(command, false);
            if (!setLow)
            {
                _auditService.Operation("PlcWriteCommand", "Failed", $"PLC 命令执行失败：命令={command}；阶段=复位");
                return false;
            }

            _auditService.Operation("PlcWriteCommand", "Success", $"PLC 命令执行成功：命令={command}");
            return true;
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                await DisconnectAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error("释放 PLC 服务时断开连接失败。", ex);
            }
            finally
            {
                _disposed = true;
            }

            _connectionLock.Dispose();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// 读取 PLC 的当前状态，包括保持寄存器和条码信息，并将其映射为 DeviceState 对象。
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task<DeviceState> ReadStateAsync(CancellationToken token)
        {
            ThrowIfDisposed();

            if (!IsConnected)
            {
                throw new InvalidOperationException("PLC 未连接。");
            }

            var registers = await _transport.ReadHoldingRegistersAsync(SlaveId, 0, 10, token);
            string barcode = string.Empty;

            try
            {
                var barcodeRegisters = await _transport.ReadHoldingRegistersAsync(SlaveId, 10, 10, token);
                barcode = ConvertRegistersToString(barcodeRegisters);
            }
            catch (Exception ex)
            {
                LogHelper.Warn($"读取 PLC 条码失败：{ex.Message}");
            }

            return DeviceStateMapper.Map(registers, barcode);
        }

        /// <summary>
        /// 写入 PLC 命令的值（true/false）到对应的线圈地址。
        /// </summary>
        /// <param name="command"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private async Task<bool> WriteCommandAsync(string command, bool value)
        {
            ThrowIfDisposed();

            if (!CommandAddressMap.TryGetValue(command, out var address))
            {
                LogHelper.Warn($"PLC 命令地址映射缺失。命令={command}");
                return false;
            }

            try
            {
                if (!IsConnected)
                {
                    LogHelper.Warn($"PLC 命令已忽略，因为传输层未连接。命令={command}，值={value}");
                    return false;
                }

                await _transport.WriteSingleCoilAsync(SlaveId, address, value);
                LogHelper.Info($"PLC 命令写入成功。命令={command}，值={value}，地址={address}");
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"PLC 命令写入失败。命令={command}，值={value}", ex);
                return false;
            }
        }

        /// <summary>
        /// 写入单个保持寄存器的值。
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private Task<bool> WriteHoldingRegisterAsync(ushort address, ushort value)
        {
            return WriteHoldingRegistersAsync(address, new[] { value });
        }

        /// <summary>
        /// 写入多个保持寄存器的值。
        /// </summary>
        /// <param name="startAddress"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        private async Task<bool> WriteHoldingRegistersAsync(ushort startAddress, ushort[] values)
        {
            ThrowIfDisposed();

            _authorizationService.EnsurePermission(Permission.ControlPlc, $"PLC 保持寄存器写入：{startAddress}");

            if (values == null || values.Length == 0)
            {
                return false;
            }

            try
            {
                if (!IsConnected)
                {
                    LogHelper.Warn($"PLC 保持寄存器写入已忽略，因为传输层未连接。起始地址={startAddress}，长度={values.Length}");
                    return false;
                }

                await _transport.WriteMultipleRegistersAsync(SlaveId, startAddress, values);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Warn($"PLC 保持寄存器写入失败。起始地址={startAddress}，长度={values.Length}，错误={ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 写入 ASCII 字符串到保持寄存器。
        /// </summary>
        /// <param name="startAddress"></param>
        /// <param name="text"></param>
        /// <param name="registerLength"></param>
        /// <returns></returns>
        private async Task<bool> WriteAsciiStringToHoldingRegistersAsync(ushort startAddress, string text, int registerLength)
        {
            ThrowIfDisposed();

            if (registerLength <= 0)
            {
                return false;
            }

            text ??= string.Empty;
            var maxCharCount = registerLength * 2;
            if (text.Length > maxCharCount)
            {
                text = text.Substring(0, maxCharCount);
            }

            var bytes = Encoding.ASCII.GetBytes(text);
            var values = new ushort[registerLength];
            var byteIndex = 0;

            for (var i = 0; i < registerLength; i++)
            {
                var high = byteIndex < bytes.Length ? bytes[byteIndex++] : (byte)0;
                var low = byteIndex < bytes.Length ? bytes[byteIndex++] : (byte)0;
                values[i] = (ushort)((high << 8) | low);
            }

            return await WriteHoldingRegistersAsync(startAddress, values);
        }

        /// <summary>
        /// 确保 PLC 传输层已连接；如果当前未连接，则尝试按当前配置建立连接。
        /// </summary>
        /// <returns>连接可用时返回 true，否则返回 false。</returns>
        private async Task<bool> ConnectTransportIfNeededAsync()
        {
            if (_currentSettings == null)
            {
                LogHelper.Warn("PLC 连接已跳过，因为尚未完成配置初始化。");
                return false;
            }

            if (IsConnected)
            {
                return true;
            }

            try
            {
                await _transport.ConnectAsync(_currentSettings);
                _lastReconnectTime = DateTime.Now;
                LogHelper.Info($"PLC 传输层已连接。端口={_currentSettings.PortName}");
                return true;
            }
            catch (Exception ex)
            {
                ResetReadState();
                PublishConnectionChanged(false);
                LogHelper.Error($"PLC 传输层连接失败。端口={_currentSettings.PortName}", ex);
                return false;
            }
        }

        /// <summary>
        /// 确保 PLC 轮询循环已启动，如果尚未启动，则创建新的轮询任务。
        /// </summary>
        private void EnsurePollingLoopStarted()
        {
            lock (_pollSyncRoot)
            {
                if (_pollCancellationTokenSource != null &&
                    !_pollCancellationTokenSource.IsCancellationRequested &&
                    _pollTask != null &&
                    !_pollTask.IsCompleted)
                {
                    return;
                }

                _pollCancellationTokenSource?.Dispose();
                _pollCancellationTokenSource = new CancellationTokenSource();
                _pollTask = Task.Run(() => PollDataLoop(_pollCancellationTokenSource.Token));
                LogHelper.Info("PLC 轮询已启动。");
            }
        }

        /// <summary>
        /// 取消 PLC 轮询循环，并返回正在运行的轮询任务（如果有）。
        /// </summary>
        /// <returns></returns>
        private Task? CancelPollingLoopAndGetTask()
        {
            lock (_pollSyncRoot)
            {
                if (_pollCancellationTokenSource == null && _pollTask == null)
                {
                    return null;
                }

                if (_pollCancellationTokenSource != null && !_pollCancellationTokenSource.IsCancellationRequested)
                {
                    _pollCancellationTokenSource.Cancel();
                }

                var task = _pollTask;
                _pollCancellationTokenSource?.Dispose();
                _pollCancellationTokenSource = null;
                _pollTask = null;
                return task;
            }
        }

        /// <summary>
        /// 更新读取状态为成功，并记录最后一次成功读取的时间和设备状态。
        /// </summary>
        /// <param name="state"></param>
        private void UpdateReadStateOnSuccess(DeviceState state)
        {
            Snapshot = new PlcReadSnapshot
            {
                HasSuccessfulRead = true,
                LastReadSuccessTime = DateTime.Now,
                LastDeviceState = state
            };
        }

        /// <summary>
        /// 重置读取状态为初始状态，并可选择清除最后一次重连时间。
        /// </summary>
        /// <param name="clearLastReconnectTime"></param>
        private void ResetReadState(bool clearLastReconnectTime = false)
        {
            Snapshot = new PlcReadSnapshot();

            if (clearLastReconnectTime)
            {
                _lastReconnectTime = DateTime.MinValue;
            }
        }

        /// <summary>
        /// 发布连接状态变化事件。
        /// </summary>
        /// <param name="connected"></param>
        private void PublishConnectionChanged(bool connected)
        {
            // 首次调用：仅记录状态，断开时静默不通知
            // 后续调用：仅状态发生变化时通知
            // 注意：首次断开静默仅指服务启动时尚未连接的初始状态，
            //       正常运行中从已连接变为断开仍会触发通知
            var shouldNotify = _hasPublishedConnectionState
                ? _lastPublishedConnectionState != connected
                : connected;

            _hasPublishedConnectionState = true;
            _lastPublishedConnectionState = connected;

            if (shouldNotify)
                ConnectionChanged?.Invoke(this, connected);
        }

        /// <summary>
        /// PLC 轮询循环，持续读取 PLC 数据并触发 DataReceived 事件。
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task PollDataLoop(CancellationToken token)
        {
            var errorCount = 0;
            LogHelper.Debug("PLC 轮询循环已进入。");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (!IsConnected) // 如果未连接，则尝试重新连接,500ms 后继续轮询
                        {
                            await TryReconnectAsync(token);
                            await Task.Delay(500, token);
                            continue;
                        }

                        var state = await ReadStateAsync(token);
                        if (token.IsCancellationRequested || _manualDisconnect || !IsConnected)
                        {
                            break;
                        }

                        UpdateReadStateOnSuccess(state);
                        PublishConnectionChanged(true);

                        errorCount = 0;
                        DataReceived?.Invoke(this, state);
                        await Task.Delay(200, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;

                        if (Snapshot.HasSuccessfulRead)
                        {
                            ResetReadState();
                            PublishConnectionChanged(false);
                        }

                        LogHelper.Warn($"PLC 轮询发生异常。次数={errorCount}，ManualDisconnect={_manualDisconnect}，消息={ex.Message}");

                        if (errorCount >= 3)
                        {
                            try
                            {
                                await _transport.DisconnectAsync();
                            }
                            catch (Exception disconnectEx)
                            {
                                LogHelper.Error("轮询异常次数达到阈值后断开 PLC 传输层失败。", disconnectEx);
                            }

                            errorCount = 0;
                        }

                        await Task.Delay(1000, token);
                    }
                }
            }
            finally
            {
                LogHelper.Debug("PLC 轮询循环已退出。");
            }
        }

        /// <summary>
        /// 尝试重新连接 PLC，如果满足条件则调用 ConnectTransportIfNeededAsync 方法。
        /// </summary>
        /// <remarks>
        /// 重新连接的条件包括：
        /// 1. 未手动断开连接 (_manualDisconnect 为 false)
        /// 2. 对象未被释放 (_disposed 为 false)
        /// 3. 当前设置允许自动连接 (_currentSettings.AutoConnect 为 true)
        /// 4. 距离上次尝试重新连接的时间间隔超过
        /// </remarks>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task TryReconnectAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested || _manualDisconnect || _disposed)
            {
                return;
            }

            if (_currentSettings is not { AutoConnect: true })
            {
                return;
            }

            if (DateTime.Now - _lastReconnectTime < ReconnectInterval)
            {
                return;
            }

            _lastReconnectTime = DateTime.Now;
            LogHelper.Warn("开始尝试重新连接 PLC。");

            await _connectionLock.WaitAsync(token);
            try
            {
                await ConnectTransportIfNeededAsync();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// 检查对象是否已被释放。若已释放，则抛出 ObjectDisposedException 异常。
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PlcService));
            }
        }

        /// <summary>
        /// 将 Modbus 寄存器数组转换为字符串，忽略值为 0 的寄存器。
        /// </summary>
        /// <param name="registers"></param>
        /// <returns></returns>
        private static string ConvertRegistersToString(ushort[] registers)
        {
            if (registers == null || registers.Length == 0)
            {
                return string.Empty;
            }

            var bytes = new List<byte>();
            foreach (var register in registers)
            {
                if (register == 0)
                {
                    break;
                }

                var high = (byte)(register >> 8);
                var low = (byte)(register & 0xFF);

                if (high != 0)
                {
                    bytes.Add(high);
                }

                if (low != 0)
                {
                    bytes.Add(low);
                }
            }

            return Encoding.ASCII.GetString(bytes.ToArray()).Trim();
        }

        #endregion
    }
}
