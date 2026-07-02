using System.Collections.Generic;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services.Session;
using Xunit;

namespace SmartFillMonitor.Tests;

public class UserSessionServiceTests
{
    [Fact]
    public void SetCurrentUser_StoresRoleAndPermissionChecks()
    {
        var session = new SessionService();
        var user = new User
        {
            Id = 1,
            UserName = "admin",
            Role = Role.Admin
        };

        session.SetCurrentUser(user);

        Assert.Same(user, session.CurrentUser);
        Assert.Equal(Role.Admin, session.CurrentRole);
        Assert.True(session.HasPermission(Permission.ManageUsers));
        Assert.True(session.HasPermission(Permission.ControlPlc));
        Assert.True(session.HasPermission(Permission.ManageSettings));
        Assert.True(session.HasPermission(Permission.ManageAlarms));
        Assert.True(session.HasPermission(Permission.ExportLogs));
    }

    [Fact]
    public void Clear_RemovesUserRoleAndPermissions()
    {
        var session = new SessionService();
        var user = new User
        {
            Id = 2,
            UserName = "eng1",
            Role = Role.Engineer
        };

        session.SetCurrentUser(user, new HashSet<Permission> { Permission.ControlPlc });
        session.Clear();

        Assert.Null(session.CurrentUser);
        Assert.Null(session.CurrentRole);
        Assert.False(session.HasPermission(Permission.ControlPlc));
    }
}
