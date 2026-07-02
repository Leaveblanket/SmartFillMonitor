using System.ComponentModel;

namespace SmartFillMonitor.Models.Enum
{
    public enum AlarmSeverity
    {
        [Description("全部")]
        All = 0,

        [Description("提示")]
        Info = 1,

        [Description("警告")]
        Warning = 2,

        [Description("错误")]
        Error = 3,

        [Description("致命")]
        Critical = 4,
    }
}
