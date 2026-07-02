namespace SmartFillMonitor.Services.Alarms
{
    public sealed class RecentAlarmViewItem
    {
        public long Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string TimeStr { get; init; } = string.Empty;
    }
}
