using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SmartFillMonitor.ViewModels.Main;

namespace SmartFillMonitor.Views.Main
{
    public partial class MainView : Window
    {
        public MainView(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            LogHelper.Info("主窗口已创建。");
        }

        private void OnChromeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (Application.Current?.MainWindow != this)
            {
                base.OnClosing(e);
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                e.Cancel = true;
                _ = Dispatcher.BeginInvoke(new Action(() => _ = viewModel.RequestExitFromShellAsync()));
                return;
            }

            base.OnClosing(e);
        }
    }
}
