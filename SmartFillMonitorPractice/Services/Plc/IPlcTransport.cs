using System;
using System.Threading;
using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Plc
{
    /// <summary>
    /// 抽象底层 PLC 通信传输层，负责连接建立以及读写寄存器和线圈。
    /// </summary>
    public interface IPlcTransport : IDisposable
    {
        /// <summary>
        /// 当前传输层是否已连接。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 使用指定设备配置建立通信连接。
        /// </summary>
        Task ConnectAsync(DeviceSettings settings, CancellationToken cancellationToken = default);

        /// <summary>
        /// 断开当前通信连接。
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 读取指定范围的保持寄存器。
        /// </summary>
        Task<ushort[]> ReadHoldingRegistersAsync(
            byte slaveId,
            ushort startAddress,
            ushort numberOfPoints,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 写入单个线圈值。
        /// </summary>
        Task WriteSingleCoilAsync(byte slaveId, ushort address, bool value, CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量写入保持寄存器值。
        /// </summary>
        Task WriteMultipleRegistersAsync(
            byte slaveId,
            ushort startAddress,
            ushort[] values,
            CancellationToken cancellationToken = default);
    }
}
