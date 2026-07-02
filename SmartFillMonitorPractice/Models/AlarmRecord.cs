using System;
using FreeSql.DataAnnotations;

namespace SmartFillMonitor.Models
{
    [Table(Name = "AlarmRecord")]
    public class AlarmRecord
    {
        [Column(IsPrimary = true, IsIdentity = true)]
        public long Id { get; set; }

        public AlarmCode AlarmCode { get; set; }

        [Column(Name = "AlarmServerity")]
        public AlarmSeverity AlarmSeverity { get; set; }

        public DateTime StartTime { get; set; } = DateTime.Now;

        public DateTime EndTime { get; set; }

        public double? DurationSeconds { get; set; }

        public bool IsActive { get; set; }

        [Column(Name = "IsAcKnowledged")]
        public bool IsAcknowledged { get; set; }

        public DateTime? AckTime { get; set; }

        [Column(Name = "Ackuser", StringLength = 50)]
        public string? AckUser { get; set; }

        public long? AckUserId { get; set; }

        [Column(StringLength = 50)]
        public string? AckUserName { get; set; }

        public long? RecoverUserId { get; set; }

        [Column(StringLength = 50)]
        public string? RecoverUserName { get; set; }

        [Column(StringLength = 100)]
        public string? TriggeredBy { get; set; }

        public AlarmTriggeredByType TriggeredByType { get; set; } = AlarmTriggeredByType.System;

        [Column(StringLength = 500)]
        public string? Description { get; set; }

        [Column(StringLength = 500)]
        public string? Message { get; set; }

        [Column(StringLength = 500)]
        public string? ProcessSuggestion { get; set; }
    }
}
