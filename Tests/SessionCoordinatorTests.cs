using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Session;
using SmartFillMonitor.Services.Threading;
using SmartFillMonitor.ViewModels.Main;
using SmartFillMonitor.ViewModels.Auth;
using SmartFillMonitor.Views.Auth;
using SmartFillMonitor.Views.Main;
using Xunit;

namespace SmartFillMonitor.Tests;

public class SessionCoordinatorTests
{
    [Fact]
    public void App_Registers_Global_Session_And_Main_Scope_Lifetimes()
    {
        var services = new ServiceCollection();
        var registerAppServices = typeof(App).GetMethod("RegisterAppServices", BindingFlags.Static | BindingFlags.NonPublic)!;
        var registerSessionServices = typeof(App).GetMethod("RegisterSessionServices", BindingFlags.Static | BindingFlags.NonPublic)!;
        var registerAuthenticationServices = typeof(App).GetMethod("RegisterAuthenticationServices", BindingFlags.Static | BindingFlags.NonPublic)!;

        registerAppServices.Invoke(null, new object[] { services });
        registerSessionServices.Invoke(null, new object[] { services });
        registerAuthenticationServices.Invoke(null, new object[] { services });

        AssertLifetime<ISessionService, SessionService>(services, ServiceLifetime.Singleton);
        AssertLifetime<IAuthorizationService, AuthorizationService>(services, ServiceLifetime.Singleton);
        AssertLifetime<IUserService, UserService>(services, ServiceLifetime.Singleton);
        AssertLifetime<IAlarmService, AlarmService>(services, ServiceLifetime.Singleton);
        AssertLifetime<IProductionRunService, ProductionRunService>(services, ServiceLifetime.Singleton);
        AssertLifetime<IPlcTransport, ModbusRtuTransport>(services, ServiceLifetime.Singleton);
        AssertLifetime<IPlcService, PlcService>(services, ServiceLifetime.Singleton);
        Assert.DoesNotContain(services, service => service.ServiceType.Name == "IUserSessionService");
        Assert.Contains(services, service => service.ServiceType == typeof(IMainNavigationService) && service.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, service => service.ServiceType == typeof(IAuthNavigationService) && service.Lifetime == ServiceLifetime.Singleton);
        AssertLifetime<HeaderViewModel, HeaderViewModel>(services, ServiceLifetime.Scoped);
        AssertLifetime<AlarmsViewModel, AlarmsViewModel>(services, ServiceLifetime.Scoped);
        AssertLifetime<DashBoardViewModel, DashBoardViewModel>(services, ServiceLifetime.Scoped);
        AssertLifetime<SimulationViewModel, SimulationViewModel>(services, ServiceLifetime.Scoped);
        AssertLifetime<DashQueryViewModel, DashQueryViewModel>(services, ServiceLifetime.Scoped);
        AssertLifetime<LogsViewModel, LogsViewModel>(services, ServiceLifetime.Scoped);
        AssertLifetime<SettingsViewModel, SettingsViewModel>(services, ServiceLifetime.Scoped);
        AssertLifetime<MainViewModel, MainViewModel>(services, ServiceLifetime.Scoped);
        AssertLifetime<MainView, MainView>(services, ServiceLifetime.Scoped);
        AssertLifetime<AccountSecurityViewModel, AccountSecurityViewModel>(services, ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task SessionCoordinator_StartAsync_Shows_MainWindow_When_Authentication_Succeeds()
    {
        await StaTestHelper.RunAsync(async () =>
        {
            WpfTestResources.EnsureMainShellResources();
            WpfTestResources.EnsureAuthShellResources();
            var application = StaTestHelper.CurrentApplication;

            var rootProvider = BuildRootProvider();
            var coordinator = rootProvider.GetRequiredService<ISessionCoordinator>();
            var session = (FakeSessionService)rootProvider.GetRequiredService<ISessionService>();
            using var authShellAutoAccepter = new AuthShellAutoAccepter(() =>
                session.SetCurrentUser(new User { UserName = "admin", Role = Role.Admin }));

            var started = await coordinator.StartAsync();

            Assert.True(started);
            Assert.Equal("admin", session.CurrentUser?.UserName);
            Assert.NotNull(application.MainWindow);
            Assert.IsType<MainView>(application.MainWindow);
            Assert.Equal(ShutdownMode.OnMainWindowClose, application.ShutdownMode);

            CloseMainWindowForTest(application.MainWindow);
        });
    }

    [Fact]
    public async Task SessionCoordinator_StartAsync_Shows_MainWindow_Without_SessionShellHost()
    {
        await StaTestHelper.RunAsync(async () =>
        {
            WpfTestResources.EnsureMainShellResources();
            WpfTestResources.EnsureAuthShellResources();
            _ = StaTestHelper.CurrentApplication;

            var rootProvider = BuildRootProvider();
            var coordinator = rootProvider.GetRequiredService<ISessionCoordinator>();
            var session = (FakeSessionService)rootProvider.GetRequiredService<ISessionService>();
            using var authShellAutoAccepter = new AuthShellAutoAccepter(() =>
                session.SetCurrentUser(new User { UserName = "admin", Role = Role.Admin }));

            var started = await coordinator.StartAsync();

            Assert.True(started);
            Assert.IsType<MainView>(StaTestHelper.CurrentApplication.MainWindow);

            CloseMainWindowForTest(StaTestHelper.CurrentApplication.MainWindow);
        });
    }

    [Fact]
    public async Task SessionCoordinator_StartAsync_Shuts_Down_When_Authentication_Is_Cancelled()
    {
        await StaTestHelper.RunAsync(async () =>
        {
            WpfTestResources.EnsureAuthShellResources();
            using var authShellAutoRejector = new AuthShellAutoRejector();

            var rootProvider = BuildRootProvider();
            var coordinator = rootProvider.GetRequiredService<ISessionCoordinator>();

            var started = await coordinator.StartAsync();

            Assert.False(started);
        });
    }

    [Fact]
    public async Task SessionCoordinator_SwitchUserAsync_Resets_LiveFeed_And_Recreates_MainWindow()
    {
        await StaTestHelper.RunAsync(async () =>
        {
            WpfTestResources.EnsureMainShellResources();
            WpfTestResources.EnsureAuthShellResources();
            var application = StaTestHelper.CurrentApplication;

            var rootProvider = BuildRootProvider();
            var coordinator = rootProvider.GetRequiredService<ISessionCoordinator>();
            var session = (FakeSessionService)rootProvider.GetRequiredService<ISessionService>();
            var liveFeed = (FakeLogLiveFeed)rootProvider.GetRequiredService<ILogLiveFeed>();
            var users = new Queue<User>(new[]
            {
                new User { UserName = "first", Role = Role.Admin },
                new User { UserName = "second", Role = Role.Engineer }
            });
            using var authShellAutoAccepter = new AuthShellAutoAccepter(() => session.SetCurrentUser(users.Dequeue()));

            Assert.True(await coordinator.StartAsync());
            var firstMainWindow = application.MainWindow;

            var switched = await coordinator.SwitchUserAsync();
            var secondMainWindow = application.MainWindow;

            Assert.True(switched);
            Assert.Equal("second", session.CurrentUser?.UserName);
            Assert.Equal(1, liveFeed.ResetCallCount);
            Assert.NotNull(firstMainWindow);
            Assert.NotNull(secondMainWindow);
            Assert.NotSame(firstMainWindow, secondMainWindow);
            Assert.False(coordinator.IsSwitchingUser);

            CloseMainWindowForTest(application.MainWindow);
        });
    }

    [Fact]
    public async Task SessionCoordinator_SwitchUserAsync_Reuses_Application_Plc_Runtime()
    {
        await StaTestHelper.RunAsync(async () =>
        {
            WpfTestResources.EnsureMainShellResources();
            WpfTestResources.EnsureAuthShellResources();
            var application = StaTestHelper.CurrentApplication;

            var rootProvider = BuildRootProvider();
            var coordinator = rootProvider.GetRequiredService<ISessionCoordinator>();
            var session = (FakeSessionService)rootProvider.GetRequiredService<ISessionService>();
            var plcService = (FakePlcService)rootProvider.GetRequiredService<IPlcService>();
            var users = new Queue<User>(new[]
            {
                new User { UserName = "first", Role = Role.Admin },
                new User { UserName = "second", Role = Role.Engineer }
            });
            using var authShellAutoAccepter = new AuthShellAutoAccepter(() => session.SetCurrentUser(users.Dequeue()));

            await plcService.InitializeAsync(new DeviceSettings());

            Assert.True(await coordinator.StartAsync());

            Assert.True(await coordinator.SwitchUserAsync());

            Assert.Equal(1, plcService.InitializeCallCount);
            Assert.Same(plcService, rootProvider.GetRequiredService<IPlcService>());

            CloseMainWindowForTest(application.MainWindow);
        });
    }

    [Fact]
    public async Task SessionCoordinator_StartAsync_Uses_SessionScope_UserContext()
    {
        await StaTestHelper.RunAsync(async () =>
        {
            WpfTestResources.EnsureMainShellResources();
            WpfTestResources.EnsureAuthShellResources();

            var rootProvider = BuildRootProvider();
            var coordinator = rootProvider.GetRequiredService<ISessionCoordinator>();
            var session = (FakeSessionService)rootProvider.GetRequiredService<ISessionService>();
            using var authShellAutoAccepter = new AuthShellAutoAccepter(() =>
                session.SetCurrentUser(new User { UserName = "session-user", Role = Role.Engineer }));

            Assert.True(await coordinator.StartAsync());

            Assert.Equal("session-user", session.CurrentUser?.UserName);

            if (StaTestHelper.CurrentApplication.MainWindow is MainView mainView &&
                mainView.DataContext is MainViewModel mainViewModel)
            {
                Assert.Equal("session-user", mainViewModel.CurrentUserName);
            }

            CloseMainWindowForTest(StaTestHelper.CurrentApplication.MainWindow);
        });
    }

    [Fact]
    public void AuthShellViewModel_Ctor_Navigates_To_LoginView()
    {
        var rootProvider = BuildRootProvider();
        var authShellViewModel = rootProvider.GetRequiredService<AuthShellViewModel>();

        Assert.NotNull(authShellViewModel.CurrentContent);
    }

    private static ServiceProvider BuildRootProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ISessionService, FakeSessionService>();
        services.AddSingleton<IUserService, FakeUserService>();
        services.AddSingleton<ILogLiveFeed, FakeLogLiveFeed>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IAuthorizationService, FakeAuthorizationService>();
        services.AddSingleton<FakeAlarmService>();
        services.AddSingleton<IAlarmService>(sp => sp.GetRequiredService<FakeAlarmService>());
        services.AddSingleton<FakeProductionRecordServices>();
        services.AddSingleton<IProductionRecordService>(sp => sp.GetRequiredService<FakeProductionRecordServices>());
        services.AddSingleton<IProductionRunService, FakeProductionRunService>();
        services.AddSingleton<ISystemLogService, FakeSystemLogService>();
        services.AddSingleton<IDialogService, FakeDialogService>();
        services.AddSingleton<IUiThreadService, ImmediateUiThreadService>();
        services.AddSingleton<ISessionCoordinator, SessionCoordinator>();
        services.AddSingleton<IConfigService, FakeConfigService>();
        services.AddSingleton<IAuthNavigationService, FakeNavigationService>();
        services.AddTransient<AuthShellView>();
        services.AddTransient<AuthShellViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();

        services.AddSingleton<IPlcService, FakePlcService>();

        services.AddScoped<IMainNavigationService, FakeNavigationService>();
        services.AddScoped<HeaderViewModel>();
        services.AddScoped<DashBoardViewModel>();
        services.AddScoped<SimulationViewModel>();
        services.AddScoped<DashQueryViewModel>();
        services.AddScoped<LogsViewModel>();
        services.AddScoped<AlarmsViewModel>();
        services.AddScoped<SettingsViewModel>();
        services.AddScoped<MainViewModel>();
        services.AddScoped<MainView>();
        services.AddScoped<AccountSecurityViewModel>();

        return services.BuildServiceProvider();
    }

    private static void AssertLifetime<TService, TImplementation>(IServiceCollection services, ServiceLifetime expected)
    {
        var descriptor = Assert.Single(services, service =>
            service.ServiceType == typeof(TService) &&
            service.ImplementationType == typeof(TImplementation));

        Assert.Equal(expected, descriptor.Lifetime);
    }

    private static void CloseMainWindowForTest(Window? window)
    {
        if (Application.Current != null)
        {
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (window != null && ReferenceEquals(Application.Current.MainWindow, window))
            {
                Application.Current.MainWindow = null;
            }
        }

        window?.Close();
    }
}
