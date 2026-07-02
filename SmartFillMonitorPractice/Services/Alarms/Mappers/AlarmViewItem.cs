namespace SmartFillMonitor.Services.Alarms
{
    public sealed class AlarmViewItem
    {
        public long Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string TimeStr { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string SeverityText { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string AckUser { get; init; } = string.Empty;
        public string AckTimeStr { get; init; } = string.Empty;
        public string RecoverUser { get; init; } = string.Empty;
        public string RecoverTimeStr { get; init; } = string.Empty;
        public string TriggeredBy { get; init; } = string.Empty;
        public string TriggeredByTypeText { get; init; } = string.Empty;
        public string ProcessSuggestion { get; init; } = string.Empty;
        public string DurationText { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public bool IsAcknowledged { get; init; }
        public bool CanAcknowledge { get; init; }
        public string AcknowledgeButtonText { get; init; } = string.Empty;
    }
}
