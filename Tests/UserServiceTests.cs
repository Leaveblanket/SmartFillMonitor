using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;
using Xunit;

namespace SmartFillMonitor.Tests;

public class UserServiceTests
{
    [Fact]
    public async Task RegisterAsync_RejectsAdminRegistration_WhenAdminAlreadyExists()
    {
        using var scope = new TestAppScope();
        await scope.UserService.RegisterAsync("admin", "StrongPass123", Role.Admin);

        var result = await scope.UserService.RegisterAsync("admin2", "StrongPass123", Role.Admin);

        Assert.False(result.Succeeded);
        Assert.Equal("系统已有管理员时，公开注册只允许创建工程师账号。", result.Message);
    }

    [Fact]
    public async Task AuthenticateAsync_LocksUserAfterTooManyFailures()
    {
        using var scope = new TestAppScope();
        await scope.UserService.RegisterAsync("admin", "StrongPass123", Role.Admin);

        for (var i = 0; i < 5; i++)
        {
            await scope.UserService.AuthenticateAsync("admin", "WrongPass123");
        }

        var user = scope.GetUser("admin");
        Assert.NotNull(user.LockedUntil);
        Assert.True(user.LockedUntil > DateTime.Now);
    }

    [Fact]
    public async Task AuthenticateAsync_WritesRoleAndPermissionsIntoSession()
    {
        using var scope = new TestAppScope();
        await scope.UserService.RegisterAsync("admin", "StrongPass123", Role.Admin);

        var result = await scope.UserService.AuthenticateAsync("admin", "StrongPass123");

        Assert.True(result.Succeeded);
        Assert.Equal(Role.Admin, scope.Session.CurrentRole);
        Assert.True(scope.Session.HasPermission(Permission.ManageUsers));
        Assert.True(scope.Session.HasPermission(Permission.ManageSettings));
    }

    [Fact]
    public async Task LogoutAsync_ClearsSessionPermissions()
    {
        using var scope = new TestAppScope();
        await scope.UserService.RegisterAsync("admin", "StrongPass123", Role.Admin);
        await scope.UserService.AuthenticateAsync("admin", "StrongPass123");

        await scope.UserService.LogoutAsync();

        Assert.Null(scope.Session.CurrentUser);
        Assert.Null(scope.Session.CurrentRole);
        Assert.False(scope.Session.HasPermission(Permission.ManageUsers));
    }
}
