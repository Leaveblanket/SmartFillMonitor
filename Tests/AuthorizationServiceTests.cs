using System.Collections.Generic;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Session;
using Xunit;

namespace SmartFillMonitor.Tests;

public class AuthorizationServiceTests
{
    [Fact]
    public void EnsurePermission_ThrowsWhenSessionLacksPermission()
    {
        var session = new SessionService();
        var audit = new AuditService();
        var service = new AuthorizationService(session, audit);
        var user = new User { Id = 1, UserName = "eng1", Role = Role.Engineer };

        session.SetCurrentUser(user, new HashSet<Permission> { Permission.ControlPlc });

        var ex = Assert.Throws<AuthorizationException>(() =>
            service.EnsurePermission(Permission.ManageUsers, "创建用户"));

        Assert.Equal("当前用户无权执行：创建用户", ex.Message);
    }
}
