using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SmartFillMonitor.ViewModels.Auth;

namespace SmartFillMonitor.Views.Auth
{
    public partial class AuthShellView : Window
    {
        private AuthShellViewModel? _authShellViewModel;
        private LoginViewModel? _loginViewModel;

        public AuthShellView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnShellCloseButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            DragMove();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DetachFromAuthShellViewModel();

            if (DataContext is AuthShellViewModel viewModel)
            {
                _authShellViewModel = viewModel;
                _authShellViewModel.PropertyChanged += OnAuthShellViewModelPropertyChanged;
                AttachLoginClose(_authShellViewModel.CurrentContent);
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            DetachFromAuthShellViewModel();
            _authShellViewModel?.Dispose();
            _authShellViewModel = null;

            base.OnClosed(e);
        }

        private void OnAuthShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AuthShellViewModel.CurrentContent))
            {
                AttachLoginClose(_authShellViewModel?.CurrentContent);
            }
        }

        private void AttachLoginClose(object? content)
        {
            DetachLoginClose();

            if (content is LoginViewModel loginViewModel)
            {
                _loginViewModel = loginViewModel;
                _loginViewModel.CloseRequested += OnLoginCloseRequested;
            }
        }

        private void DetachLoginClose()
        {
            if (_loginViewModel is not null)
            {
                _loginViewModel.CloseRequested -= OnLoginCloseRequested;
                _loginViewModel = null;
            }
        }

        private void DetachFromAuthShellViewModel()
        {
            DetachLoginClose();

            if (_authShellViewModel is not null)
            {
                _authShellViewModel.PropertyChanged -= OnAuthShellViewModelPropertyChanged;
            }
        }

        private void OnLoginCloseRequested(bool accepted)
        {
            DialogResult = accepted;
        }
    }
}
