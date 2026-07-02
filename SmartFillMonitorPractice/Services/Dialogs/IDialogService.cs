namespace SmartFillMonitor.Services.Dialogs
{
    /// <summary>
    /// 封装应用中的对话框展示能力，统一消息提示的调用入口。
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// 显示消息对话框并返回用户选择结果。
        /// </summary>
        DialogResult ShowMessage(
            string message,
            string caption,
            PromptSeverity severity,
            DialogButtons buttons = DialogButtons.Ok);
    }
}
