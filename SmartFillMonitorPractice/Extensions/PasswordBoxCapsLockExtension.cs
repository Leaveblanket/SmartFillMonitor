using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SmartFillMonitor.Extensions
{
    public static class PasswordBoxCapsLockExtension
    {
        public static readonly DependencyProperty HintTargetProperty =
            DependencyProperty.RegisterAttached(
                "HintTarget",
                typeof(UIElement),
                typeof(PasswordBoxCapsLockExtension),
                new PropertyMetadata(null, OnHintTargetChanged));

        private static readonly DependencyProperty IsCapsLockHandlerAttachedProperty =
            DependencyProperty.RegisterAttached(
                "IsCapsLockHandlerAttached",
                typeof(bool),
                typeof(PasswordBoxCapsLockExtension),
                new PropertyMetadata(false));

        public static UIElement? GetHintTarget(DependencyObject obj)
        {
            return (UIElement?)obj.GetValue(HintTargetProperty);
        }

        public static void SetHintTarget(DependencyObject obj, UIElement? value)
        {
            obj.SetValue(HintTargetProperty, value);
        }

        private static bool GetIsCapsLockHandlerAttached(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsCapsLockHandlerAttachedProperty);
        }

        private static void SetIsCapsLockHandlerAttached(DependencyObject obj, bool value)
        {
            obj.SetValue(IsCapsLockHandlerAttachedProperty, value);
        }

        private static void OnHintTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox passwordBox)
            {
                return;
            }

            AttachCapsLockHandlers(passwordBox);
            UpdateHintVisibility(passwordBox);
        }

        private static void AttachCapsLockHandlers(PasswordBox passwordBox)
        {
            if (GetIsCapsLockHandlerAttached(passwordBox))
            {
                return;
            }

            passwordBox.GotKeyboardFocus += OnCapsLockStateMayHaveChanged;
            passwordBox.PreviewKeyDown += OnCapsLockStateMayHaveChanged;
            passwordBox.PreviewKeyUp += OnCapsLockStateMayHaveChanged;
            passwordBox.Unloaded += OnPasswordBoxUnloaded;

            SetIsCapsLockHandlerAttached(passwordBox, true);
        }

        private static void OnPasswordBoxUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox passwordBox)
            {
                return;
            }

            passwordBox.GotKeyboardFocus -= OnCapsLockStateMayHaveChanged;
            passwordBox.PreviewKeyDown -= OnCapsLockStateMayHaveChanged;
            passwordBox.PreviewKeyUp -= OnCapsLockStateMayHaveChanged;
            passwordBox.Unloaded -= OnPasswordBoxUnloaded;

            SetIsCapsLockHandlerAttached(passwordBox, false);
        }

        private static void OnCapsLockStateMayHaveChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                UpdateHintVisibility(passwordBox);
            }
        }

        private static void UpdateHintVisibility(PasswordBox passwordBox)
        {
            if (GetHintTarget(passwordBox) is not UIElement hintTarget)
            {
                return;
            }

            hintTarget.Visibility = Keyboard.IsKeyToggled(Key.CapsLock)
                ? Visibility.Visible
                : Visibility.Hidden;
        }
    }
}
