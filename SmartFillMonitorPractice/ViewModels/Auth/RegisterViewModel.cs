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
    public partial class RegisterViewModel : ObservableObject, INavigationAware
    {
        private readonly IUserService _userService;
        private readonly IAuthNavigationService _authNavigationService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<RegisterRoleOption> roleOptions = new();

        [ObservableProperty]
        private RegisterRoleOption? selectedRole;

        [ObservableProperty]
        private string userName = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string hintText = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private string confirmPassword = string.Empty;

        public string CreatedUserName { get; private set; } = string.Empty;

        public RegisterViewModel(
            IUserService userService,
            IAuthNavigationService authNavigationService,
            IDialogService dialogService)
        {
            _userService = userService;
            _authNavigationService = authNavigationService;
            _dialogService = dialogService;
        }

        public async Task OnNavigatedToAsync(object? parameter)
        {
            var registerEntryContext = parameter as RegisterEntryContext;
            var loginUsers = await _userService.GetLoginUsersAsync();
            var hasUsers = loginUsers.Count > 0;
            var availableRoles = BuildAvailableRoles();

            RoleOptions = new ObservableCollection<RegisterRoleOption>(availableRoles);
            SelectedRole = hasUsers
                ? availableRoles.FirstOrDefault(x => x.Role == Role.Engineer)
                : availableRoles.FirstOrDefault(x => x.Role == Role.Admin);
            HintText = hasUsers
                ? "请选择角色后完成注册。"
                : "当前是首个账户注册，建议先创建管理员账户。";
            UserName = registerEntryContext?.SuggestedUserName?.Trim() ?? string.Empty;
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            CreatedUserName = string.Empty;
        }

        public Task OnNavigatedFromAsync()
        {
            return Task.CompletedTask;
        }

        [RelayCommand]
        public async Task RegisterAsync()
        {
            IsBusy = true;
            try
            {
                var trimmedUserName = (UserName ?? string.Empty).Trim();
                if (SelectedRole == null)
                {
                    ShowPrompt("请选择用户类型。", "提示", PromptSeverity.Information);
                    return;
                }

                if (Password != ConfirmPassword)
                {
                    ShowPrompt("两次输入的密码不一致。", "提示", PromptSeverity.Warning);
                    ConfirmPassword = string.Empty;
                    return;
                }

                var registerResult = await _userService.RegisterAsync(trimmedUserName, Password, SelectedRole.Role);
                if (registerResult.Succeeded)
                {
                    ShowPrompt("注册成功。", "提示", PromptSeverity.Information);
                    CreatedUserName = trimmedUserName;
                    _authNavigationService.NavigateTo<LoginViewModel>(new LoginEntryContext
                    {
                        PreferredUserName = CreatedUserName
                    });
                    return;
                }

                var message = string.IsNullOrWhiteSpace(registerResult.Message)
                    ? "注册失败。"
                    : registerResult.Message;
                switch (registerResult.Failure)
                {
                    case RegisterFailure.CredentialsMissing:
                        ShowPrompt(message, "提示", PromptSeverity.Information);
                        break;
                    case RegisterFailure.UserAlreadyExists:
                        ShowPrompt(message, "提示", PromptSeverity.Warning);
                        break;
                    case RegisterFailure.AdminRegistrationNotAllowed:
                        Password = string.Empty;
                        ConfirmPassword = string.Empty;
                        ShowPrompt(message, "提示", PromptSeverity.Warning);
                        break;
                    default:
                        Password = string.Empty;
                        ConfirmPassword = string.Empty;
                        ShowPrompt(message, "提示", PromptSeverity.Warning);
                        break;
                }
            }
            catch (BusinessException ex)
            {
                ShowMessage(ex.Message, "提示", PromptSeverity.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.Error("公开注册失败", ex);
                ShowMessage("注册失败，请稍后重试。", "错误", PromptSeverity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            _authNavigationService.NavigateTo<LoginViewModel>();
        }

        private void ShowMessage(string message, string caption, PromptSeverity severity)
        {
            _dialogService.ShowMessage(message, caption, severity);
        }

        private void ShowPrompt(string message, string caption, PromptSeverity severity)
        {
            ShowMessage(message, caption, severity);
        }

        private static RegisterRoleOption[] BuildAvailableRoles()
        {
            return
            [
                new(Role.Admin, "管理员"),
                new(Role.Engineer, "工程师")
            ];
        }
    }
}
