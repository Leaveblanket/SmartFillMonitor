using System.IO.Ports;

namespace SmartFillMonitor.Services.Plc
{
    public sealed class SerialPortService : ISerialPortService
    {
        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }
    }
}
