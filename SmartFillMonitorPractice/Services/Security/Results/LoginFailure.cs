namespace SmartFillMonitor.Services.Security
{
    public enum LoginFailure
    {
        None = 0,
        CredentialsMissing = 1,
        UserNotFoundOrPasswordInvalid = 2,
        UserDisabled = 3,
        UserLocked = 4
    }
}
