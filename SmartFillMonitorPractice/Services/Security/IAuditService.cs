namespace SmartFillMonitor.Services.Security
{
    /// <summary>
    /// 记录安全操作和业务操作审计日志。
    /// </summary>
    public interface IAuditService
    {
        /// <summary>
        /// 记录一条安全相关审计日志。
        /// </summary>
        void Security(string action, string result, string detail, string? userName = null);

        /// <summary>
        /// 记录一条业务操作审计日志。
        /// </summary>
        void Operation(string action, string result, string detail, string? userName = null);
    }
}
