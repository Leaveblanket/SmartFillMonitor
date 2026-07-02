namespace SmartFillMonitor.Services.Security
{
    public sealed record LoginOperationResult(
        bool Succeeded,
        LoginFailure Failure,
        string Message)
    {
        public static LoginOperationResult Success()
        {
            return new LoginOperationResult(true, LoginFailure.None, string.Empty);
        }

        public static LoginOperationResult Fail(LoginFailure failure, string message)
        {
            return new LoginOperationResult(false, failure, message);
        }
    }
}
