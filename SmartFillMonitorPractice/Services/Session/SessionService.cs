using System;
using System.Collections.Generic;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services.Security;

namespace SmartFillMonitor.Services.Session
{
    public sealed class SessionService : ISessionService
    {
        private static readonly IReadOnlySet<Permission> EmptyPermissions = new HashSet<Permission>();
        private IReadOnlySet<Permission> _grantedPermissions = EmptyPermissions;

        public event Action<User?>? SessionChanged;

        public User? CurrentUser { get; private set; }

        public Role? CurrentRole { get; private set; }

        public bool IsLoggedIn => CurrentUser != null;

        public bool HasPermission(Permission permission)
        {
            return _grantedPermissions.Contains(permission);
        }

        public void SetCurrentUser(User user, IReadOnlySet<Permission>? permissions = null)
        {
            CurrentUser = user;
            CurrentRole = user.Role;
            _grantedPermissions = permissions ?? RolePermissionPolicy.BuildPermissions(user.Role);
            SessionChanged?.Invoke(CurrentUser);
        }

        public void Clear()
        {
            CurrentUser = null;
            CurrentRole = null;
            _grantedPermissions = EmptyPermissions;
            SessionChanged?.Invoke(null);
        }
    }
}
