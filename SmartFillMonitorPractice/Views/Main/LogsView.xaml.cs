using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using SmartFillMonitor.ViewModels.Main;

namespace SmartFillMonitor.Views.Main
{
    /// <summary>
    /// LogsView.xaml 的交互逻辑
    /// </summary>
    public partial class LogsView : UserControl
    {
        public LogsView()
        {
            InitializeComponent();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LogsViewModel viewModel)
            {
                return;
            }

            viewModel.LiveLogs.CollectionChanged -= OnLiveLogsCollectionChanged;
            viewModel.LiveLogs.CollectionChanged += OnLiveLogsCollectionChanged;
        }

        /// <summary>
        /// 滚动到最新日志
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnLiveLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || LiveLogListBox.Items.Count == 0)
            {
                return;
            }

            var lastItem = LiveLogListBox.Items[LiveLogListBox.Items.Count - 1];
            LiveLogListBox.ScrollIntoView(lastItem);
        }
    }
}
