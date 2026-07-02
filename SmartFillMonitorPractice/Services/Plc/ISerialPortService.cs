namespace SmartFillMonitor.Services.Plc
{
    /// <summary>
    /// 提供本机串口枚举能力。
    /// </summary>
    public interface ISerialPortService
    {
        /// <summary>
        /// 获取当前系统可用串口名称列表。
        /// </summary>
        string[] GetAvailablePorts();
    }
}
