using System.Windows;

namespace SmartFillMonitor.Services.Dialogs
{
    public sealed class DialogService : IDialogService
    {
        public DialogResult ShowMessage(
            string message,
            string caption,
            PromptSeverity severity,
            DialogButtons buttons = DialogButtons.Ok)
        {
            var result = MessageBox.Show(message, caption, MapButtons(buttons), MapSeverity(severity));
            return MapResult(result);
        }

        private static MessageBoxImage MapSeverity(PromptSeverity severity)
        {
            return severity switch
            {
                PromptSeverity.Information => MessageBoxImage.Information,
                PromptSeverity.Warning => MessageBoxImage.Warning,
                PromptSeverity.Error => MessageBoxImage.Error,
                _ => MessageBoxImage.None
            };
        }

        private static MessageBoxButton MapButtons(DialogButtons buttons)
        {
            return buttons switch
            {
                DialogButtons.YesNo => MessageBoxButton.YesNo,
                _ => MessageBoxButton.OK
            };
        }


        private static DialogResult MapResult(MessageBoxResult result)
        {
            return result switch
            {
                MessageBoxResult.OK => DialogResult.Ok,
                MessageBoxResult.Yes => DialogResult.Yes,
                MessageBoxResult.No => DialogResult.No,
                _ => DialogResult.None
            };
        }
    }
}
