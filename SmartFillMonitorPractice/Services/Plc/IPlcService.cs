using System;
using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Plc
{
    /// <summary>
    /// 提供面向上层业务的 PLC 连接、读写与状态发布能力。
    /// </summary>
    public interface IPlcService
    {
        /// <summary>
        /// 当收到新的设备状态数据时发布。
        /// </summary>
        event EventHandler<DeviceState>? DataReceived;

        /// <summary>
        /// 当 PLC 连接状态变化时发布。
        /// </summary>
        event EventHandler<bool>? ConnectionChanged;

        /// <summary>
        /// 当前 PLC 是否已连接。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 最近一次读取到的 PLC 快照状态。
        /// </summary>
        PlcReadSnapshot Snapshot { get; }

        /// <summary>
        /// 使用指定设备参数初始化 PLC 服务。
        /// </summary>
        Task InitializeAsync(DeviceSettings settings);

        /// <summary>
        /// 建立 PLC 连接。
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// 断开 PLC 连接。
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 向 PLC 发送一个瞬时脉冲命令。
        /// </summary>
        Task<bool> PulseCommandAsync(string command, int delayMs = 120);
    }
}
