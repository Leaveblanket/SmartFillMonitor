using SmartFillMonitor.Models;
using SmartFillMonitor.Services.Session;

namespace SmartFillMonitor.Services.Security
{
    public class AuthorizationService : IAuthorizationService
    {
        private readonly ISessionService _sessionService;
        private readonly IAuditService _auditService;

        public AuthorizationService(ISessionService sessionService, IAuditService auditService)
        {
            _sessionService = sessionService;
            _auditService = auditService;
        }

        public void EnsurePermission(Permission permission, string action)
        {
            if (_sessionService.HasPermission(permission))
            {
                return;
            }

            var actor = _sessionService.CurrentUser?.UserName ?? string.Empty;
            _auditService.Security("PermissionDenied", "Denied", $"{actor} 尝试执行“{action}”，但缺少权限 {permission}", actor);
            throw new AuthorizationException($"当前用户无权执行：{action}");
        }
    }
}
