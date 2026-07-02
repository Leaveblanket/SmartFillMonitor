using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services.Session;
using Xunit;

namespace SmartFillMonitor.Tests;

public class SessionServiceTests
{
    [Fact]
    public void SetCurrentUser_Populates_User_Role_And_Permissions()
    {
        var session = new SessionService();
        var user = new User { Id = 7, UserName = "admin", Role = Role.Admin };

        session.SetCurrentUser(user);

        Assert.True(session.IsLoggedIn);
        Assert.Same(user, session.CurrentUser);
        Assert.Equal(Role.Admin, session.CurrentRole);
        Assert.True(session.HasPermission(Permission.ManageSettings));
    }

    [Fact]
    public void Clear_Resets_CurrentUser_And_Permissions()
    {
        var session = new SessionService();
        session.SetCurrentUser(new User { UserName = "eng1", Role = Role.Engineer });

        session.Clear();

        Assert.False(session.IsLoggedIn);
        Assert.Null(session.CurrentUser);
        Assert.Null(session.CurrentRole);
        Assert.False(session.HasPermission(Permission.ManageSettings));
    }

    [Fact]
    public void SetCurrentUser_Raises_SessionChanged()
    {
        var session = new SessionService();
        User? published = null;
        session.SessionChanged += user => published = user;
        var user = new User { UserName = "operator", Role = Role.Engineer };

        session.SetCurrentUser(user);

        Assert.Same(user, published);
    }
}
