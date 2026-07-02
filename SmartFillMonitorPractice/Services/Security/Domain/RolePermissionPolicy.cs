using System.Collections.Generic;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;

namespace SmartFillMonitor.Services.Security
{
    internal static class RolePermissionPolicy
    {
        private static readonly IReadOnlySet<Permission> EmptyPermissions = new HashSet<Permission>();

        private static readonly IReadOnlyDictionary<Role, IReadOnlySet<Permission>> PermissionMap =
            new Dictionary<Role, IReadOnlySet<Permission>>
            {
                {
                    Role.Admin,
                    new HashSet<Permission>
                    {
                        Permission.ManageSettings,
                        Permission.ManageUsers,
                        Permission.ControlPlc,
                        Permission.ManageAlarms,
                        Permission.ExportLogs,
                    }
                },
                {
                    Role.Engineer,
                    new HashSet<Permission>
                    {
                        Permission.ControlPlc,
                        Permission.ManageAlarms,
                        Permission.ExportLogs,
                    }
                }
            };

        public static IReadOnlySet<Permission> BuildPermissions(Role? role)
        {
            return role.HasValue && PermissionMap.TryGetValue(role.Value, out var permissions)
                ? permissions
                : EmptyPermissions;
        }
    }
}
