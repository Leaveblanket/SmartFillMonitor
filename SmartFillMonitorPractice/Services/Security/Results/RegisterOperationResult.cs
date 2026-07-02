namespace SmartFillMonitor.Services.Security
{
    public sealed record RegisterOperationResult(
        bool Succeeded,
        RegisterFailure Failure,
        string Message)
    {
        public static RegisterOperationResult Success()
        {
            return new RegisterOperationResult(true, RegisterFailure.None, string.Empty);
        }

        public static RegisterOperationResult Fail(RegisterFailure failure, string message)
        {
            return new RegisterOperationResult(false, failure, message);
        }
    }
}
