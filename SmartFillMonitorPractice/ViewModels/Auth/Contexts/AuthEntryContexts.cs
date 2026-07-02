namespace SmartFillMonitor.ViewModels.Auth
{
    public sealed class LoginEntryContext
    {
        public string PreferredUserName { get; init; } = string.Empty;
    }

    public sealed class RegisterEntryContext
    {
        public string SuggestedUserName { get; init; } = string.Empty;
    }
}
