using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;

namespace SmartFillMonitor.ViewModels.Auth
{
    public partial class LoginViewModel : ObservableObject, INavigationAware
    {
        private readonly IUserService _userService;
        private readonly IAuthNavigationService _authNavigationService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<User> users = new();

        [ObservableProperty]
        private string userNameText = string.Empty;

        [ObservableProperty]
        private string hintText = string.Empty;

        [ObservableProperty]
        private bool noUsersVisible;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string password = string.Empty;

        public event Action<bool>? CloseRequested;

        public LoginViewModel(
            IUserService userService,
            IAuthNavigationService authNavigationService,
            IDialogService dialogService)
        {
            _userService = userService;
            _authNavigationService = authNavigationService;
            _dialogService = dialogService;
        }

        public async Task LoadUsersAsync(string? preferredUserName = null)
        {
            try
            {
                var loginUsers = await _userService.GetLoginUsersAsync();
                var hasUsers = loginUsers.Count > 0;
                var selectedUserName = string.Empty;

                if (hasUsers)
                {
                    var matched = !string.IsNullOrWhiteSpace(preferredUserName)
                        ? loginUsers.FirstOrDefault(x => string.Equals(x.UserName, preferredUserName, StringComparison.OrdinalIgnoreCase))
                        : null;

                    selectedUserName = matched?.UserName ?? loginUsers.FirstOrDefault()?.UserName ?? string.Empty;
                }
                else
                {
                    selectedUserName = preferredUserName ?? string.Empty;
                }

                Users = new ObservableCollection<User>(loginUsers);
                NoUsersVisible = !hasUsers;
                HintText = hasUsers
                    ? "请选择用户后输入密码登录。"
                    : "当前系统中还没有可登录用户，请先点击“注册”创建首个账户。";
                UserNameText = selectedUserName;
            }
            catch (InfrastructureException ex)
            {
                LogHelper.Error("加载登录用户列表失败", ex);
                HintText = "加载用户列表失败，请检查数据库连接后重试。";
                ShowMessage(ex.Message, "错误", PromptSeverity.Error);
            }
            catch (Exception ex)
            {
                LogHelper.Error("加载登录用户列表异常", ex);
                HintText = "加载用户列表失败，请稍后重试。";
                ShowMessage("加载用户列表失败，请稍后重试。", "错误", PromptSeverity.Error);
            }
        }

        public async Task OnNavigatedToAsync(object? parameter)
        {
            var loginEntryContext = parameter as LoginEntryContext;
            var preferredUserName = loginEntryContext?.PreferredUserName;

            await LoadUsersAsync(preferredUserName);
        }

        public Task OnNavigatedFromAsync()
        {
            return Task.CompletedTask;
        }

        [RelayCommand]
        public async Task LoginAsync()
        {
            IsBusy = true;
            try
            {
                var resolvedUserName = (UserNameText ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(resolvedUserName))
                {
                    ShowPrompt("请选择或输入用户名。", "提示", PromptSeverity.Information);
                    return;
                }

                LoginOperationResult? loginResult = await _userService.AuthenticateAsync(resolvedUserName, Password);
                if (loginResult.Succeeded)
                {
                    CloseRequested?.Invoke(true);
                    return;
                }

                var message = string.IsNullOrWhiteSpace(loginResult.Message)
                    ? "用户名或密码错误。"
                    : loginResult.Message;
                var severity = loginResult.Failure == LoginFailure.CredentialsMissing
                    ? PromptSeverity.Information
                    : PromptSeverity.Warning;
                var shouldClearPassword = loginResult.Failure != LoginFailure.CredentialsMissing;

                if (shouldClearPassword)
                {
                    Password = string.Empty;
                }

                ShowPrompt(message, "登录失败", severity);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void RequestRegister()
        {
            _authNavigationService.NavigateTo<RegisterViewModel>();
        }

        [RelayCommand]
        public void Cancel()
        {
            CloseRequested?.Invoke(false);
        }

        private void ShowMessage(string message, string caption, PromptSeverity severity)
        {
            _dialogService.ShowMessage(message, caption, severity);
        }

        private void ShowPrompt(string message, string caption, PromptSeverity severity)
        {
            ShowMessage(message, caption, severity);
        }
    }
}
