using System;
using System.Collections.Generic;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Session
{
    public interface ISessionService
    {
        event Action<User?>? SessionChanged;

        User? CurrentUser { get; }

        Role? CurrentRole { get; }

        bool IsLoggedIn { get; }

        bool HasPermission(Permission permission);

        void SetCurrentUser(User user, IReadOnlySet<Permission>? permissions = null);

        void Clear();
    }
}
