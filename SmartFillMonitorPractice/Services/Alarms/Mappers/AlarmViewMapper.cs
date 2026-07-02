using System;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Alarms
{
    public sealed class AlarmViewMapper
    {
        public AlarmViewItem MapAlarm(AlarmRecord record)
        {
            var description = string.IsNullOrWhiteSpace(record.Message) ? record.Description ?? string.Empty : record.Message;
            var ackUser = string.IsNullOrWhiteSpace(record.AckUserName) ? record.AckUser ?? string.Empty : record.AckUserName;
            var recoverUser = record.RecoverUserName ?? string.Empty;
            var statusText = record.IsActive
                ? (record.IsAcknowledged ? "已确认" : "活动中")
                : "已恢复";

            return new AlarmViewItem
            {
                Id = record.Id,
                Code = $"E{(int)record.AlarmCode}",
                Title = record.AlarmCode.GetDescription(),
                Description = description,
                SeverityText = record.AlarmSeverity.GetDescription(),
                TimeStr = record.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                StatusText = statusText,
                AckUser = ackUser,
                AckTimeStr = record.AckTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                RecoverUser = recoverUser,
                RecoverTimeStr = record.EndTime == default ? string.Empty : record.EndTime.ToString("yyyy-MM-dd HH:mm:ss"),
                TriggeredBy = record.TriggeredBy ?? string.Empty,
                TriggeredByTypeText = record.TriggeredByType.GetDescription(),
                ProcessSuggestion = record.ProcessSuggestion ?? string.Empty,
                DurationText = FormatDuration(record.DurationSeconds),
                IsActive = record.IsActive,
                IsAcknowledged = record.IsAcknowledged,
                CanAcknowledge = record.IsActive,
                AcknowledgeButtonText = record.IsActive
                    ? (record.IsAcknowledged ? "恢复报警" : "确认报警")
                    : "已处理",
            };
        }

        public RecentAlarmViewItem MapRecentAlarm(AlarmRecord record)
        {
            return new RecentAlarmViewItem
            {
                Id = record.Id,
                Title = record.AlarmCode.GetDescription(),
                TimeStr = record.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        private static string FormatDuration(double? durationSeconds)
        {
            if (!durationSeconds.HasValue || durationSeconds.Value <= 0)
            {
                return string.Empty;
            }

            var span = TimeSpan.FromSeconds(Math.Round(durationSeconds.Value));
            var result = string.Empty;

            if (span.Days > 0)
            {
                result += $"{span.Days}天";
            }

            if (span.Hours > 0)
            {
                result += $"{span.Hours}小时";
            }

            if (span.Minutes > 0)
            {
                result += $"{span.Minutes}分";
            }

            if (span.Seconds > 0 || string.IsNullOrWhiteSpace(result))
            {
                result += $"{span.Seconds}秒";
            }

            return result;
        }
    }
}
