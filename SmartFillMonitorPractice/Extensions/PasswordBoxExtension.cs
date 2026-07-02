using System.Windows;
using System.Windows.Controls;

namespace SmartFillMonitor.Extensions
{
    public static class PasswordBoxExtension
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxExtension),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordPropertyChanged));

        private static readonly DependencyProperty IsPasswordSyncingProperty =
            DependencyProperty.RegisterAttached(
                "IsPasswordSyncing",
                typeof(bool),
                typeof(PasswordBoxExtension),
                new PropertyMetadata(false));

        private static readonly DependencyProperty IsPasswordChangedHandlerAttachedProperty =
            DependencyProperty.RegisterAttached(
                "IsPasswordChangedHandlerAttached",
                typeof(bool),
                typeof(PasswordBoxExtension),
                new PropertyMetadata(false));

        public static string GetBoundPassword(DependencyObject obj)
        {
            return (string)obj.GetValue(BoundPasswordProperty);
        }

        public static void SetBoundPassword(DependencyObject obj, string value)
        {
            obj.SetValue(BoundPasswordProperty, value);
        }

        private static bool GetIsPasswordSyncing(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsPasswordSyncingProperty);
        }

        private static void SetIsPasswordSyncing(DependencyObject obj, bool value)
        {
            obj.SetValue(IsPasswordSyncingProperty, value);
        }

        private static bool GetIsPasswordChangedHandlerAttached(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsPasswordChangedHandlerAttachedProperty);
        }

        private static void SetIsPasswordChangedHandlerAttached(DependencyObject obj, bool value)
        {
            obj.SetValue(IsPasswordChangedHandlerAttachedProperty, value);
        }

        private static void OnBoundPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox passwordBox)
            {
                return;
            }

            AttachPasswordChangedHandler(passwordBox);

            if (GetIsPasswordSyncing(passwordBox))
            {
                return;
            }

            var newPassword = e.NewValue as string ?? string.Empty;
            if (passwordBox.Password == newPassword)
            {
                return;
            }

            passwordBox.Password = newPassword;
        }

        private static void AttachPasswordChangedHandler(PasswordBox passwordBox)
        {
            if (GetIsPasswordChangedHandlerAttached(passwordBox))
            {
                return;
            }

            passwordBox.PasswordChanged += OnPasswordBoxPasswordChanged;
            SetIsPasswordChangedHandlerAttached(passwordBox, true);
        }

        private static void OnPasswordBoxPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox passwordBox)
            {
                return;
            }

            SetIsPasswordSyncing(passwordBox, true);
            SetBoundPassword(passwordBox, passwordBox.Password);
            SetIsPasswordSyncing(passwordBox, false);
        }
    }
}
