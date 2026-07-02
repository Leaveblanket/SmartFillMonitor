using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Dialogs;
using SmartFillMonitor.Services.Session;
using SmartFillMonitor.Services.Threading;

namespace SmartFillMonitor.ViewModels.Main
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IMainNavigationService _navigationService;
        private readonly ISessionService _sessionService;
        private readonly ISessionCoordinator _sessionCoordinator;
        private readonly IDialogService _dialogService;
        private readonly IUiThreadService _uiThreadService;
        private readonly DashBoardViewModel _dashBoardViewModel;
        private readonly SimulationViewModel _simulationViewModel;
        private bool _isExitCleanedUp;

        [ObservableProperty]
        private object mainContent = null!;

        [ObservableProperty]
        private bool isAdmin;

        [ObservableProperty]
        private bool isUserLoggedIn;

        [ObservableProperty]
        private string currentUserName = "未登录";

        [ObservableProperty]
        private string switchUserButtonText = "切换用户";

        [ObservableProperty]
        private string exitSystemButtonText = "退出系统";

        public HeaderViewModel Header { get; }

        public MainViewModel(
            IMainNavigationService navigationService,
            HeaderViewModel header,
            DashBoardViewModel dashBoardViewModel,
            SimulationViewModel simulationViewModel,
            ISessionService sessionService,
            ISessionCoordinator sessionCoordinator,
            IDialogService dialogService,
            IUiThreadService uiThreadService)
        {
            _navigationService = navigationService;
            Header = header;
            _dashBoardViewModel = dashBoardViewModel;
            _simulationViewModel = simulationViewModel;
            _sessionService = sessionService;
            _sessionCoordinator = sessionCoordinator;
            _dialogService = dialogService;
            _uiThreadService = uiThreadService;

            _navigationService.CurrentViewModelChanged += NavigationService_CurrentViewModelChanged;
            _sessionService.SessionChanged += SessionService_SessionChanged;

            UpdateUser(_sessionService.CurrentUser);

            _navigationService.NavigateTo<DashBoardViewModel>();
            LogHelper.Info("主窗口视图模型已初始化。");
        }

        private void SessionService_SessionChanged(User? user)
        {
            _uiThreadService.BeginInvoke(() => UpdateUser(user));
        }

        private void NavigationService_CurrentViewModelChanged(object? viewModel)
        {
            _uiThreadService.BeginInvoke(() =>
            {
                MainContent = viewModel ?? _dashBoardViewModel;
                Header.Activate(MainContent);
            });
        }

        private void UpdateUser(User? user)
        {
            var isUserLoggedIn = user != null;
            var isAdmin = user?.Role == Role.Admin;

            CurrentUserName = user?.UserName ?? "未登录";
            IsUserLoggedIn = isUserLoggedIn;
            IsAdmin = isAdmin;
            SwitchUserButtonText = "切换用户";
            ExitSystemButtonText = "退出系统";

            if (!isAdmin && MainContent is SettingsViewModel)
            {
                _navigationService.NavigateTo<DashBoardViewModel>();
            }
        }

        [RelayCommand]
        private void NavigateToDashBoard()
        {
            NavigateTo<DashBoardViewModel>();
        }

        [RelayCommand]
        private void NavigateToSimulation()
        {
            NavigateTo<SimulationViewModel>();
        }

        [RelayCommand]
        private void NavigateToDashQuery()
        {
            NavigateTo<DashQueryViewModel>();
        }

        [RelayCommand]
        private void NavigateToLogs()
        {
            NavigateTo<LogsViewModel>();
        }

        [RelayCommand]
        private void NavigateToAlarms()
        {
            NavigateTo<AlarmsViewModel>();
        }

        [RelayCommand]
        private void NavigateToSetting()
        {
            NavigateTo<SettingsViewModel>();
        }

        [RelayCommand]
        private void OpenAccountSecurity()
        {
            NavigateTo<AccountSecurityViewModel>();
        }

        private void NavigateTo<T>() where T : class
        {
            if (!IsUserLoggedIn)
            {
                return;
            }

            try
            {
                _navigationService.NavigateTo<T>();
            }
            catch (AuthorizationException ex)
            {
                _dialogService.ShowMessage(ex.Message, "权限不足", PromptSeverity.Warning, DialogButtons.Ok);
            }
        }

        [RelayCommand]
        private async Task SwitchUserAsync()
        {
            var result = _dialogService.ShowMessage("确定要切换当前用户吗？", "切换用户确认", PromptSeverity.Information, DialogButtons.YesNo);
            if (result != DialogResult.Yes)
            {
                return;
            }

            LogHelper.Info("主窗口已确认切换用户命令。");
            _simulationViewModel.PauseSimulation();
            await _sessionCoordinator.SwitchUserAsync();
        }

        [RelayCommand]
        private async Task ExitSystemAsync()
        {
            var result = _dialogService.ShowMessage("确定要退出系统吗？", "退出确认", PromptSeverity.Information, DialogButtons.YesNo);
            if (result != DialogResult.Yes)
            {
                return;
            }

            LogHelper.Info("主窗口已确认退出系统命令。");
            await _sessionCoordinator.ExitAsync();
        }

        public async Task<bool> RequestExitFromShellAsync()
        {
            if (_sessionCoordinator.IsSwitchingUser || _sessionCoordinator.IsExiting)
            {
                return false;
            }

            LogHelper.Info("主窗口关闭请求已转交视图模型处理。");
            await _sessionCoordinator.ExitAsync();
            return true;
        }

        public void StopForExit()
        {
            if (_isExitCleanedUp)
            {
                LogHelper.Debug("主窗口视图模型退出清理已执行过，本次请求忽略。");
                return;
            }

            _isExitCleanedUp = true;
            LogHelper.Info("主窗口视图模型退出清理开始。");

            _sessionService.SessionChanged -= SessionService_SessionChanged;
            _navigationService.CurrentViewModelChanged -= NavigationService_CurrentViewModelChanged;
            DisposeCurrentContentIfNeeded();
            Header.Stop();

            _dashBoardViewModel.StopAndDispose();
            _simulationViewModel.StopAndDispose();

            LogHelper.Info("主窗口视图模型退出清理完成。");
        }

        public void Dispose()
        {
            StopForExit();
        }

        private void DisposeCurrentContentIfNeeded()
        {
            if (ReferenceEquals(MainContent, _dashBoardViewModel) || ReferenceEquals(MainContent, _simulationViewModel))
            {
                return;
            }

            if (MainContent is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

    }
}
