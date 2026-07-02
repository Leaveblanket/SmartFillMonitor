namespace SmartFillMonitor.Services.Security
{
    public enum RegisterFailure
    {
        None = 0,
        CredentialsMissing = 1,
        UserAlreadyExists = 2,
        AdminRegistrationNotAllowed = 3
    }
}
