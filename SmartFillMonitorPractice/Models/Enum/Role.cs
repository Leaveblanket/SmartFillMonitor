using System.ComponentModel;

namespace SmartFillMonitor.Models.Enum
{
    public enum Role
    {
        [Description("管理员")]
        Admin = 0,

        [Description("工程师")]
        Engineer = 1,
    }
}
