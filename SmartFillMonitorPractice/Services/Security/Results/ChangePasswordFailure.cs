namespace SmartFillMonitor.Services.Security
{
    public enum ChangePasswordFailure
    {
        None = 0,
        CurrentPasswordMissing = 1,
        NewPasswordMissing = 2,
        NewPasswordMismatch = 3,
        CurrentPasswordInvalid = 4,
        NewPasswordTooWeak = 5,
        NewPasswordSameAsCurrent = 6,
        CurrentUserUnavailable = 7,
        Unknown = 8
    }
}
