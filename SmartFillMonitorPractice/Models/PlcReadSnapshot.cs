using System;

namespace SmartFillMonitor.Models
{
    public sealed class PlcReadSnapshot
    {
        public bool HasSuccessfulRead { get; init; }

        public DateTime? LastReadSuccessTime { get; init; }

        public DeviceState? LastDeviceState { get; init; }
    }
}
