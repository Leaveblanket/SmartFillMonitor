using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services.Session;
using SmartFillMonitor.ViewModels.Main;

namespace SmartFillMonitor.Services.Navigation
{
    public sealed class NavigationService : IMainNavigationService, IAuthNavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISessionService? _sessionService;
        private readonly SimulationViewModel? _simulationViewModel;

        public NavigationService(
            IServiceProvider serviceProvider,
            ISessionService? sessionService = null,
            SimulationViewModel? simulationViewModel = null)
        {
            _serviceProvider = serviceProvider;
            _sessionService = sessionService;
            _simulationViewModel = simulationViewModel;
        }

        public event Action<object?>? CurrentViewModelChanged;

        private object? CurrentViewModel { get; set; }

        public void NavigateTo<T>() where T : class
        {
            NavigateTo<T>(null);
        }

        public void NavigateTo<T>(object? parameter) where T : class
        {
            var targetType = typeof(T);
            if (targetType != typeof(SimulationViewModel) &&
                _simulationViewModel != null &&
                ReferenceEquals(CurrentViewModel, _simulationViewModel))
            {
                _simulationViewModel.PauseSimulation();
            }

            if (targetType == typeof(SettingsViewModel) &&
                _sessionService?.CurrentRole != Role.Admin)
            {
                throw new AuthorizationException("当前用户无权进入系统设置页面。");
            }

            var viewModel = _serviceProvider.GetRequiredService<T>();
            if (ReferenceEquals(CurrentViewModel, viewModel))
            {
                return;
            }

            _ = NavigateInternalAsync(viewModel, parameter);
        }

        private async Task NavigateInternalAsync(object viewModel, object? parameter)
        {
            if (CurrentViewModel is INavigationAware currentNavigationAware)
            {
                await currentNavigationAware.OnNavigatedFromAsync();
            }

            CurrentViewModel = viewModel;
            if (CurrentViewModel is INavigationAware navigationAware)
            {
                await navigationAware.OnNavigatedToAsync(parameter);
            }

            CurrentViewModelChanged?.Invoke(CurrentViewModel);
        }
    }
}
