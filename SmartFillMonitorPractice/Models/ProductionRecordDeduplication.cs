using System;
using FreeSql.DataAnnotations;

namespace SmartFillMonitor.Models
{
    [Table(Name = "ProductionRecordDeduplications")]
    [Index("idx_unique_production_request_key", "RequestKey", true)]
    public class ProductionRecordDeduplication
    {
        [Column(IsPrimary = true, IsIdentity = true)]
        public long Id { get; set; }

        [Column(StringLength = 120, IsNullable = false)]
        public string RequestKey { get; set; } = string.Empty;

        public long? RecordId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
