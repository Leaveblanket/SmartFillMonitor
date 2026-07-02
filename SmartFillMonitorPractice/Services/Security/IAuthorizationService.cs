using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Security
{
    /// <summary>
    /// 提供权限校验能力，用于拦截无权限的业务操作。
    /// </summary>
    public interface IAuthorizationService
    {
        /// <summary>
        /// 确保当前用户具备指定权限，否则抛出异常。
        /// </summary>
        void EnsurePermission(Permission permission, string action);
    }
}
