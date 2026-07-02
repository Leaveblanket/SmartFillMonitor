using System.ComponentModel;

namespace SmartFillMonitor.Models.Enum
{
    public enum AlarmCode
    {
        [Description("无")]
        None = 0,

        [Description("原料箱液位过低")]
        LowLiquidLevel = 1,

        [Description("压缩空气压力偏低")]
        LowAirPressure = 2001,

        [Description("加热温度过高")]
        HighTemperature = 3001,

        [Description("PLC 通信故障")]
        CommunicationError = 4001,

        [Description("系统内部错误")]
        SystemError = 5001,

        [Description("测试报警")]
        TestAlarm = 9001,
    }
}
