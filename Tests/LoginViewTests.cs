using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Navigation;
using SmartFillMonitor.ViewModels.Auth;
using SmartFillMonitor.Views.Auth;
using Xunit;

namespace SmartFillMonitor.Tests;

public class LoginViewTests
{
    [Fact]
    public async Task LoginView_Cancel_Does_Not_Try_To_Set_DialogResult_On_Modeless_Host()
    {
        await StaTestHelper.RunAsync(() =>
        {
            WpfTestResources.EnsureAuthShellResources();

            var viewModel = new LoginViewModel(
                new FakeUserService(),
                new FakeNavigationService(),
                new FakeDialogService());

            var view = new LoginView
            {
                DataContext = viewModel
            };

            var hostWindow = new Window
            {
                Content = view,
                Width = 420,
                Height = 320,
                ShowInTaskbar = false
            };

            hostWindow.Show();

            var exception = Record.Exception(() => viewModel.CancelCommand.Execute(null));

            hostWindow.Close();

            Assert.Null(exception);
        });
    }

    [Fact]
    public async Task LoginView_CloseRequested_Does_Not_Conflict_With_AuthShell_Dialog_Handler()
    {
        await StaTestHelper.RunAsync(() =>
        {
            WpfTestResources.EnsureAuthShellResources();

            var services = new ServiceCollection();
            services.AddSingleton<IUserService, FakeUserService>();
            services.AddSingleton<IDialogService, FakeDialogService>();
            services.AddSingleton<IAuthNavigationService>(sp => new NavigationService(sp));
            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();

            using var provider = services.BuildServiceProvider();
            var shellViewModel = new AuthShellViewModel(provider.GetRequiredService<IAuthNavigationService>());
            var viewModel = Assert.IsType<LoginViewModel>(shellViewModel.CurrentContent);
            viewModel.UserNameText = "admin";
            viewModel.Password = "StrongPass123";

            var dialogWindow = new AuthShellView
            {
                DataContext = shellViewModel
            };

            viewModel.CloseRequested += accepted => dialogWindow.DialogResult = accepted;
            dialogWindow.Loaded += async (_, _) => await viewModel.LoginAsync();

            var result = dialogWindow.ShowDialog();

            Assert.True(result);
            shellViewModel.Dispose();
        });
    }
}
