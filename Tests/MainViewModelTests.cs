using System.Collections.Generic;
using System.Linq;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services.Session;
using SmartFillMonitor.ViewModels.Main;
using Xunit;

namespace SmartFillMonitor.Tests;

public class MainViewModelTests
{
    [Fact]
    public async Task NonAdministrator_LoginStateChange_On_SettingsPage_Redirects_To_Dashboard()
    {
        await StaTestHelper.RunAsync(() =>
        {
            WpfTestResources.EnsureMainShellResources();

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
            var navigationService = new InstanceNavigationService(
                dashboard,
                new SettingsViewModel(new FakeConfigService(), plcService, new FakeSerialPortService(), new FakeAuthorizationService(), dialog));
            using var header = new HeaderViewModel(plcService, uiThread);
            session.SetCurrentUser(new User { UserName = "admin", Role = Role.Admin });

            var viewModel = new MainViewModel(
                navigationService,
                header,
                dashboard,
                simulation,
                session,
                coordinator,
                dialog,
                uiThread);

            viewModel.NavigateToSettingCommand.Execute(null);
            Assert.Equal(typeof(SettingsViewModel), navigationService.LastRequestedType);

            session.SetCurrentUser(new User { UserName = "eng1", Role = Role.Engineer });

            Assert.Equal(typeof(DashBoardViewModel), navigationService.LastRequestedType);
            Assert.Equal("eng1", viewModel.CurrentUserName);
            Assert.False(viewModel.IsAdmin);
            Assert.True(viewModel.IsUserLoggedIn);

            viewModel.Dispose();
            dashboard.Dispose();
            simulation.Dispose();
        });
    }

    [Fact]
    public async Task SecondaryActionAsync_Does_Not_Exit_When_User_Cancels()
    {
        await StaTestHelper.RunAsync(async () =>
        {
            WpfTestResources.EnsureMainShellResources();

            var navigationService = new FakeNavigationService();
            var plcService = new FakePlcService();
            var dialog = new FakeDialogService { Result = DialogResult.No };
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
            var viewModel = new MainViewModel(
                navigationService,
                header,
                dashboard,
                simulation,
                session,
                coordinator,
                dialog,
                uiThread);

            await viewModel.ExitSystemCommand.ExecuteAsync(null);

            Assert.Equal(1, dialog.ShowMessageCallCount);
            Assert.Equal(0, coordinator.ExitCallCount);

            viewModel.Dispose();
            dashboard.Dispose();
            simulation.Dispose();
        });
    }

    private sealed class InstanceNavigationService : IMainNavigationService
    {
        private readonly Dictionary<Type, object> _instances;

        public InstanceNavigationService(params object[] instances)
        {
            _instances = instances.ToDictionary(x => x.GetType(), x => x);
        }

        public event Action<object?>? CurrentViewModelChanged;

        public Type? LastRequestedType { get; private set; }

        public void NavigateTo<T>() where T : class
        {
            NavigateTo<T>(null);
        }

        public void NavigateTo<T>(object? parameter) where T : class
        {
            LastRequestedType = typeof(T);
            var viewModel = parameter as T ?? ResolveViewModel<T>();
            CurrentViewModelChanged?.Invoke(viewModel);
        }

        private T ResolveViewModel<T>() where T : class
        {
            if (_instances.TryGetValue(typeof(T), out var existing))
            {
                return (T)existing;
            }

            throw new InvalidOperationException($"Cannot resolve test navigation target {typeof(T).Name}.");
        }
    }
}
