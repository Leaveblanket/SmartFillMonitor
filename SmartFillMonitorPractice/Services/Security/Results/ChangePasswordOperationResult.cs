namespace SmartFillMonitor.Services.Security
{
    public sealed record ChangePasswordOperationResult(
        bool Succeeded,
        ChangePasswordFailure Failure,
        string Message)
    {
        public static ChangePasswordOperationResult Success()
        {
            return new ChangePasswordOperationResult(true, ChangePasswordFailure.None, string.Empty);
        }

        public static ChangePasswordOperationResult Fail(ChangePasswordFailure failure, string message)
        {
            return new ChangePasswordOperationResult(false, failure, message);
        }
    }
}
