namespace SmartFillMonitor.Services.Security
{
    public class AuditService : IAuditService
    {
        public AuditService()
        {
        }

        /// <summary>
        /// 记录安全审计日志
        /// </summary>
        /// <param name="action"></param>
        /// <param name="result"></param>
        /// <param name="detail"></param>
        /// <param name="userName"></param>
        public void Security(string action, string result, string detail, string? userName = null)
        {
            LogHelper.Info($"[审计][安全][{action}][{result}] 用户={userName}；详情={detail}");
        }

        /// <summary>
        /// 记录操作审计日志
        /// </summary>
        /// <param name="action"></param>
        /// <param name="result"></param>
        /// <param name="detail"></param>
        /// <param name="userName"></param>
        public void Operation(string action, string result, string detail, string? userName = null)
        {
            LogHelper.Info($"[审计][操作][{action}][{result}] 用户={userName}；详情={detail}");
        }
    }
}


