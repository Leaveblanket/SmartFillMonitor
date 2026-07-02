using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Session;

namespace SmartFillMonitor.ViewModels.Main
{
    public partial class AccountSecurityViewModel : ObservableObject
    {
        private readonly IMainNavigationService _navigationService;
        private readonly ISessionCoordinator _sessionCoordinator;
        private readonly IDialogService _dialogService;
        private readonly IUserService _userService;
        private readonly ISessionService _sessionService;

        [ObservableProperty]
        private string currentUserName = string.Empty;

        [ObservableProperty]
        private string currentPassword = string.Empty;

        [ObservableProperty]
        private string newPassword = string.Empty;

        [ObservableProperty]
        private string confirmPassword = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        public AccountSecurityViewModel(
            IMainNavigationService navigationService,
            ISessionCoordinator sessionCoordinator,
            IDialogService dialogService,
            IUserService userService,
            ISessionService sessionService)
        {
            _navigationService = navigationService;
            _sessionCoordinator = sessionCoordinator;
            _dialogService = dialogService;
            _userService = userService;
            _sessionService = sessionService;

            CurrentUserName = _sessionService.CurrentUser?.UserName ?? string.Empty;
        }

        public string CurrentUserText => $"当前用户：{CurrentUserName}";

        [RelayCommand]
        private async Task SaveAsync()
        {
            IsBusy = true;
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentPassword))
                {
                    ShowPrompt("请输入当前密码。", "提示", PromptSeverity.Information);
                    return;
                }

                if (string.IsNullOrWhiteSpace(NewPassword))
                {
                    ShowPrompt("请输入新密码。", "提示", PromptSeverity.Information);
                    return;
                }

                if (NewPassword != ConfirmPassword)
                {
                    ConfirmPassword = string.Empty;
                    ShowPrompt("两次输入的新密码不一致。", "提示", PromptSeverity.Warning);
                    return;
                }

                var result = await _userService.ChangeCurrentUserPasswordAsync(CurrentPassword, NewPassword);

                if (result.Succeeded)
                {
                    ShowPrompt("密码修改成功。", "提示", PromptSeverity.Information);
                    await _sessionCoordinator.SwitchUserAsync();
                    return;
                }

                var message = string.IsNullOrWhiteSpace(result.Message) ? "修改密码失败。" : result.Message;
                switch (result.Failure)
                {
                    case ChangePasswordFailure.CurrentPasswordInvalid:
                        CurrentPassword = string.Empty;
                        break;
                    case ChangePasswordFailure.NewPasswordTooWeak:
                    case ChangePasswordFailure.NewPasswordSameAsCurrent:
                        NewPassword = string.Empty;
                        ConfirmPassword = string.Empty;
                        break;
                    case ChangePasswordFailure.CurrentUserUnavailable:
                        CurrentPassword = string.Empty;
                        NewPassword = string.Empty;
                        ConfirmPassword = string.Empty;
                        break;
                }

                ShowPrompt(message, "提示", PromptSeverity.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.Error("主界面修改密码失败", ex);
                ShowMessage("密码修改失败，请稍后重试。", "错误", PromptSeverity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _navigationService.NavigateTo<DashBoardViewModel>();
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
