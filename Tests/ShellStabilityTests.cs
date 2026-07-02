using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Dialogs;
using SmartFillMonitor.Services.Session;
using SmartFillMonitor.Services.Threading;
using SmartFillMonitor.ViewModels.Main;
using SmartFillMonitor.Views.Main;
using Xunit;

namespace SmartFillMonitor.Tests;

public class ShellStabilityTests
{
    [Fact]
    public void SessionScopeContracts_AreAvailableForCoordinatorTests()
    {
        var coordinator = new FakeSessionCoordinator();

        Assert.False(coordinator.IsSwitchingUser);
    }

    [Fact]
    public async Task MainView_UsesInjectedMainViewModel_AsDataContext()
    {
        await StaTestHelper.RunAsync(() =>
        {
            WpfTestResources.EnsureMainShellResources();

            var navigationService = new FakeNavigationService();
            var plcService = new FakePlcService();
            var coordinator = new FakeSessionCoordinator();
            var session = new FakeSessionService();
            var dialog = new FakeDialogService();
            var uiThread = new ImmediateUiThreadService();
            var dashboard = new DashBoardViewModel(
                plcService,
                new FakeAlarmService(),
                new FakeProductionRunService(),
                dialog,
                uiThread);
            var simulation = new SimulationViewModel(
                plcService,
                dialog,
                uiThread);
            using var header = new HeaderViewModel(plcService, uiThread);
            var viewModel = new MainViewModel(
                navigationService,
                header,
                dashboard,
                simulation,
                session,
                coordinator,
                dialog,
                uiThread);

            var view = new MainView(viewModel);

            Assert.Same(viewModel, view.DataContext);

            view.Close();
            viewModel.Dispose();
            dashboard.Dispose();
            simulation.Dispose();
        });
    }

    [Fact]
    public async Task MainViewModel_AcceptsSessionCoordinator()
    {
        await StaTestHelper.RunAsync(() =>
        {
            WpfTestResources.EnsureMainShellResources();

            var navigationService = new FakeNavigationService();
            var plcService = new FakePlcService();
            var coordinator = new FakeSessionCoordinator();
            var session = new FakeSessionService();
            var dialog = new FakeDialogService();
            var uiThread = new ImmediateUiThreadService();
            var dashboard = new DashBoardViewModel(
                plcService,
                new FakeAlarmService(),
                new FakeProductionRunService(),
                dialog,
                uiThread);
            var simulation = new SimulationViewModel(
                plcService,
                dialog,
                uiThread);
            using var header = new HeaderViewModel(plcService, uiThread);
            var vm = new MainViewModel(
                navigationService,
                header,
                dashboard,
                simulation,
                session,
                coordinator,
                dialog,
                uiThread);

            Assert.Equal("切换用户", vm.SwitchUserButtonText);
            Assert.Equal(0, coordinator.ExitCallCount);
            vm.Dispose();
            dashboard.Dispose();
            simulation.Dispose();
        });
    }

    [Fact]
    public async Task NavigationService_RejectsSetting_ForNonAdministrator()
    {
        await StaTestHelper.RunAsync(() =>
        {
            var services = BuildShellServices(new User
            {
                UserName = "eng1",
                Role = Role.Engineer
            });

            var navigationService = services.GetRequiredService<IMainNavigationService>();
            Assert.Throws<AuthorizationException>(() => navigationService.NavigateTo<SettingsViewModel>());
        });
    }

    [Fact]
    public async Task MainViewModel_UpdatesUserState_AndActivatesHeader_OnNavigation()
    {
        await StaTestHelper.RunAsync(() =>
        {
            var navigationService = new FakeNavigationService();
            var plcService = new FakePlcService();
            var coordinator = new FakeSessionCoordinator();
            var session = new FakeSessionService();
            var dialog = new FakeDialogService();
            var uiThread = new ImmediateUiThreadService();
            var dashboard = new DashBoardViewModel(
                plcService,
                new FakeAlarmService(),
                new FakeProductionRunService(),
                dialog,
                uiThread);
            var simulation = new SimulationViewModel(
                plcService,
                dialog,
                uiThread);
            using var header = new HeaderViewModel(plcService, uiThread);
            var admin = new User { UserName = "admin", Role = Role.Admin };
            session.SetCurrentUser(admin);

            var vm = new MainViewModel(
                navigationService,
                header,
                dashboard,
                simulation,
                session,
                coordinator,
                dialog,
                uiThread);

            Assert.True(vm.IsUserLoggedIn);
            Assert.True(vm.IsAdmin);
            Assert.Equal("admin", vm.CurrentUserName);

            vm.NavigateToSimulationCommand.Execute(null);
            Assert.Equal(typeof(SimulationViewModel), navigationService.LastViewModel);
            Assert.NotNull(vm.Header);

            vm.StopForExit();
            vm.StopForExit();

            dashboard.Dispose();
            simulation.Dispose();
            vm.Dispose();
        });
    }

    [Fact]
    public void SettingsViewModel_Uses_Direct_Service_Dependencies()
    {
        var vm = new SettingsViewModel(
            new FakeConfigService(),
            new FakePlcService(),
            new FakeSerialPortService(),
            new FakeAuthorizationService(),
            new FakeDialogService());

        Assert.NotNull(vm.PortNames);
    }

    [Fact]
    public async Task LoginAsync_Uses_DialogService_For_Switch_User_Confirmation()
    {
        await StaTestHelper.RunAsync(async () =>
        {
            var navigationService = new FakeNavigationService();
            var plcService = new FakePlcService();
            var dialog = new FakeDialogService { Result = DialogResult.Yes };
            var uiThread = new ImmediateUiThreadService();
            var coordinator = new FakeSessionCoordinator();
            var session = new FakeSessionService();
            var dashboard = new DashBoardViewModel(
                plcService,
                new FakeAlarmService(),
                new FakeProductionRunService(),
                dialog,
                uiThread);
            var simulation = new SimulationViewModel(
                plcService,
                dialog,
                uiThread);
            using var header = new HeaderViewModel(plcService, uiThread);
            var vm = new MainViewModel(
                navigationService,
                header,
                dashboard,
                simulation,
                session,
                coordinator,
                dialog,
                uiThread);

            await vm.SwitchUserCommand.ExecuteAsync(null);

            Assert.Equal(1, dialog.ShowMessageCallCount);
            Assert.Equal("切换用户确认", dialog.LastCaption);
            Assert.Equal(1, coordinator.SwitchUserCallCount);
        });
    }

    [Fact]
    public void StartSimulation_Uses_DialogService_When_StartIsRejected()
    {
        var plcService = new FakePlcService();
        var dialog = new FakeDialogService();
        var uiThread = new ImmediateUiThreadService();
        plcService.Snapshot = new PlcReadSnapshot
        {
            HasSuccessfulRead = false,
            LastDeviceState = null
        };

        var vm = new SimulationViewModel(plcService, dialog, uiThread);

        vm.StartSimulationCommand.Execute(null);

        Assert.Equal(1, dialog.ShowMessageCallCount);
        Assert.Equal("未检测到有效 Modbus 数据，当前不能启动仿真联调。", dialog.LastMessage);
        Assert.Equal("提示", dialog.LastCaption);
        Assert.Equal(PromptSeverity.Information, dialog.LastSeverity);
        Assert.Equal(DialogButtons.Ok, dialog.LastButtons);
    }

    [Fact]
    public void ImmediateUiThreadService_Matches_Production_Interface_Contract()
    {
        SmartFillMonitor.Services.Threading.IUiThreadService service = new ImmediateUiThreadService();
        var called = false;

        Assert.True(service.CheckAccess());

        service.Invoke(() => called = true);

        Assert.True(called);
    }

    [Fact]
    public async Task MainViewModel_Uses_CurrentUser_To_Initialize_UserState()
    {
        await StaTestHelper.RunAsync(() =>
        {
            var navigationService = new FakeNavigationService();
            var plcService = new FakePlcService();
            var dialog = new FakeDialogService();
            var uiThread = new ImmediateUiThreadService();
            var coordinator = new FakeSessionCoordinator();
            var session = new FakeSessionService();
            var dashboard = new DashBoardViewModel(
                plcService,
                new FakeAlarmService(),
                new FakeProductionRunService(),
                dialog,
                uiThread);
            var simulation = new SimulationViewModel(
                plcService,
                dialog,
                uiThread);
            using var header = new HeaderViewModel(plcService, uiThread);
            var vm = new MainViewModel(
                navigationService,
                header,
                dashboard,
                simulation,
                session,
                coordinator,
                dialog,
                uiThread);

            session.SetCurrentUser(new User { UserName = "admin", Role = Role.Admin });

            Assert.Equal("admin", vm.CurrentUserName);
            Assert.True(vm.IsUserLoggedIn);
            Assert.True(vm.IsAdmin);
        });
    }

    [Fact]
    public async Task WpfUiThreadService_BeginInvoke_Posts_Asynchronously_OnUiThread()
    {
        await StaTestHelper.RunAsync(() =>
        {
            WpfTestResources.EnsureMainShellResources();

            IUiThreadService service = new WpfUiThreadService();
            var steps = new List<string>();
            var invoked = false;
            var frame = new DispatcherFrame();

            steps.Add("before-begininvoke");
            service.BeginInvoke(() =>
            {
                steps.Add("callback");
                invoked = true;
                frame.Continue = false;
            });
            steps.Add("after-begininvoke");

            Assert.False(invoked);
            Assert.Equal(new[] { "before-begininvoke", "after-begininvoke" }, steps);

            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                if (!invoked)
                {
                    frame.Continue = false;
                }
            }), DispatcherPriority.ApplicationIdle);

            Dispatcher.PushFrame(frame);

            Assert.True(invoked);
            Assert.Equal(new[] { "before-begininvoke", "after-begininvoke", "callback" }, steps);
        });
    }

    private static ServiceProvider BuildShellServices(User currentUser)
    {
        var services = new ServiceCollection();
        var sessionService = new SessionService();
        var fakeProductionRecordServices = new FakeProductionRecordServices();
        sessionService.SetCurrentUser(currentUser);

        services.AddSingleton<IUserService>(new FakeUserService());
        services.AddSingleton<ISessionService>(sessionService);
        services.AddSingleton<IPlcService, FakePlcService>();
        services.AddSingleton<FakeAlarmService>();
        services.AddSingleton<IAlarmService>(sp => sp.GetRequiredService<FakeAlarmService>());
        services.AddSingleton<IDialogService, FakeDialogService>();
        services.AddSingleton<SmartFillMonitor.Services.Threading.IUiThreadService, ImmediateUiThreadService>();
        services.AddSingleton<IProductionRecordService>(fakeProductionRecordServices);
        services.AddSingleton<IProductionRunService, FakeProductionRunService>();
        services.AddSingleton<ISystemLogService, FakeSystemLogService>();
        services.AddSingleton<IConfigService, FakeConfigService>();
        services.AddSingleton<IAuthorizationService, FakeAuthorizationService>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<DashBoardViewModel>();
        services.AddSingleton<SimulationViewModel>();
        services.AddSingleton<DashQueryViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<AlarmsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<IMainNavigationService>(sp => new NavigationService(
            sp,
            sp.GetRequiredService<ISessionService>(),
            sp.GetRequiredService<SimulationViewModel>()));
        return services.BuildServiceProvider();
    }
}
