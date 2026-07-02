using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.Services;

namespace SmartFillMonitor.ViewModels.Auth
{
    public partial class AuthShellViewModel : ObservableObject, IDisposable
    {
        private readonly IAuthNavigationService _authNavigationService;

        [ObservableProperty]
        private object? currentContent;

        public AuthShellViewModel(
            IAuthNavigationService authNavigationService)
        {
            _authNavigationService = authNavigationService;

            _authNavigationService.CurrentViewModelChanged += AuthNavigationService_CurrentViewModelChanged;
            _authNavigationService.NavigateTo<LoginViewModel>();
        }


        private void AuthNavigationService_CurrentViewModelChanged(object? viewModel)
        {
            CurrentContent = viewModel;
        }

        public void Dispose()
        {
            _authNavigationService.CurrentViewModelChanged -= AuthNavigationService_CurrentViewModelChanged;
        }
    }
}
