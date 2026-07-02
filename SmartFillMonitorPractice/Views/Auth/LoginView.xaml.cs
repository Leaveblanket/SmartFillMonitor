using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SmartFillMonitor.Views.Auth
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
            KeyDown += LoginView_KeyDown;
        }

        private void LoginView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && CancelButton.Command?.CanExecute(null) == true)
            {
                CancelButton.Command.Execute(null);
                e.Handled = true;
            }
        }
    }
}
