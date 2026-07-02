using System.ComponentModel;

namespace SmartFillMonitor.Models.Enum
{
    public enum AlarmTriggeredByType
    {
        [Description("系统")]
        System = 0,

        [Description("PLC")]
        Plc = 1,

        [Description("用户")]
        User = 2,
    }
}
