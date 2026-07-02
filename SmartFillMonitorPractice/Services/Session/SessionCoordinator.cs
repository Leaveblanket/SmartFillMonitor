using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.Services.Logging;
using SmartFillMonitor.ViewModels.Auth;
using SmartFillMonitor.ViewModels.Main;
using SmartFillMonitor.Views.Auth;
using SmartFillMonitor.Views.Main;

namespace SmartFillMonitor.Services.Session
{
    /// <summary>
    /// 协调整个已认证会话生命周期，包括认证壳显示、主壳作用域创建、切换用户和退出流程。
    /// </summary>
    public sealed class SessionCoordinator : ISessionCoordinator
    {
        private readonly IServiceProvider _rootProvider;
        private readonly ISessionService _sessionService;
        private readonly ILogLiveFeed _logLiveFeed;
        private IServiceScope? _sessionScope;

        public SessionCoordinator(
            IServiceProvider rootProvider,
            ISessionService sessionService,
            ILogLiveFeed logLiveFeed)
        {
            _rootProvider = rootProvider;
            _sessionService = sessionService;
            _logLiveFeed = logLiveFeed;
        }

        public bool IsSwitchingUser { get; private set; }

        public bool IsExiting { get; private set; }

        public async Task<bool> StartAsync()
        {
            var authenticated = await ShowAuthenticationAsync();
            if (!authenticated || !_sessionService.IsLoggedIn)
            {
                Application.Current?.Shutdown();
                return false;
            }

            await CreateAndShowSessionAsync();
            return true;
        }

        public async Task<bool> SwitchUserAsync()
        {
            if (IsSwitchingUser || IsExiting)
            {
                return false;
            }

            IsSwitchingUser = true;
            try
            {
                await CloseAndDisposeSessionAsync();
                _sessionService.Clear();
                _logLiveFeed.Reset();

                var authenticated = await ShowAuthenticationAsync();
                if (!authenticated || !_sessionService.IsLoggedIn)
                {
                    Application.Current?.Shutdown();
                    return false;
                }

                await CreateAndShowSessionAsync();
                return true;
            }
            finally
            {
                IsSwitchingUser = false;
            }
        }

        public async Task ExitAsync()
        {
            if (IsExiting)
            {
                return;
            }

            IsExiting = true;
            try
            {
                await CloseAndDisposeSessionAsync();
                _sessionService.Clear();
                Application.Current?.Shutdown();
            }
            finally
            {
                IsExiting = false;
            }
        }

        private async Task<bool> ShowAuthenticationAsync()
        {
            var application = Application.Current ?? throw new InvalidOperationException("WPF Application 尚未初始化。");
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var authShellWindow = _rootProvider.GetRequiredService<AuthShellView>();
            var authShellViewModel = _rootProvider.GetRequiredService<AuthShellViewModel>();
            authShellWindow.DataContext = authShellViewModel;

            var result = authShellWindow.ShowDialog();
            return await Task.FromResult(result == true);
        }

        private async Task CreateAndShowSessionAsync()
        {
            _sessionScope = _rootProvider.CreateScope();

            var application = Application.Current ?? throw new InvalidOperationException("WPF Application 尚未初始化。");
            var mainWindow = _sessionScope.ServiceProvider.GetRequiredService<MainView>();
            application.MainWindow = mainWindow;
            application.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            await Task.CompletedTask;
        }

        private async Task CloseAndDisposeSessionAsync()
        {
            var application = Application.Current;
            var mainWindow = application?.MainWindow;

            if (mainWindow is MainView view && view.DataContext is MainViewModel viewModel)
            {
                viewModel.StopForExit();
            }

            if (application != null && mainWindow != null)
            {
                application.MainWindow = null;
                mainWindow.Close();
            }

            if (_sessionScope is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                _sessionScope?.Dispose();
            }

            _sessionScope = null;
        }
    }
}
