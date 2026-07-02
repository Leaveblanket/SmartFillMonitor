using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Production
{
    /// <summary>
    /// 管理生产记录的服务，包括保存、查询和导出生产记录，同时处理幂等性和数据库异常的逻辑。
    /// </summary>
    public sealed class ProductionRecordService : IProductionRecordService
    {
        private const int SqliteBusyErrorCode = 5;
        private const int SqliteConstraintErrorCode = 19;
        private const int SqliteUniqueConstraintExtendedErrorCode = 2067;

        private readonly IAppDbContext _dbContext;
        private readonly IAuditService _auditService;
        private readonly IExportService _exportService;

        public ProductionRecordService(
            IAppDbContext dbContext,
            IAuditService auditService,
            IExportService exportService)
        {
            _dbContext = dbContext;
            _auditService = auditService;
            _exportService = exportService;
        }

        #region Write

        public async Task<bool> SaveAsync(ProductionRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.BatchNo))
            {
                return false;
            }

            record.BatchNo = record.BatchNo.Trim();
            record.Time = record.Time == default ? DateTime.Now : record.Time;
            var requestKey = BuildRequestKey(record);
            if (string.IsNullOrWhiteSpace(requestKey))
            {
                return false;
            }

            for (var retry = 1; retry <= 3; retry++)
            {
                try
                {
                    _dbContext.Fsql.Transaction(() =>
                    {
                        _dbContext.Fsql.Insert(new ProductionRecordDeduplication
                        {
                            RequestKey = requestKey,
                            CreatedAt = record.Time == default ? DateTime.Now : record.Time
                        }).ExecuteAffrows();

                        record.Id = _dbContext.Fsql.Insert(record).ExecuteIdentity();
                        _dbContext.Fsql.Update<ProductionRecordDeduplication>()
                            .Where(d => d.RequestKey == requestKey)
                            .Set(d => d.RecordId, record.Id)
                            .ExecuteAffrows();
                    });
                    return true;
                }
                catch (Exception ex) when (IsUniqueViolation(ex))
                {
                    LogHelper.Info($"生产记录命中 RequestKey 唯一约束，按幂等成功处理：{record.BatchNo}");
                    return true;
                }
                catch (Exception ex) when (IsTransientDbException(ex) && retry < 3)
                {
                    LogHelper.Warn($"保存生产记录失败，准备重试：{record.BatchNo}，第 {retry} 次，{ex.Message}");
                    await Task.Delay(200 * retry);
                }
                catch (Exception ex)
                {
                    LogHelper.Error("保存生产记录失败", ex);
                    return false;
                }
            }

            return false;
        }

        #endregion

        #region Query

        public async Task<List<ProductionRecord>> QueryAsync(DateTime start, DateTime end)
        {
            return await _dbContext.Fsql.Select<ProductionRecord>()
                .Where(r => r.Time >= start && r.Time <= end)
                .OrderByDescending(r => r.Time)
                .ToListAsync();
        }

        #endregion

        #region Export

        public async Task ExportAsync(List<ProductionRecord> records, string filePath)
        {
            var fullPath = await _exportService.ExportAsync(records ?? new List<ProductionRecord>(), filePath);
            _auditService.Operation("ExportProductionRecords", "Success", $"导出的生产记录文件路径：{fullPath}");
        }

        #endregion

        #region Private Helpers

        private static bool IsTransientDbException(Exception ex)
        {
            if (TryGetSqliteException(ex, out var sqliteException) && sqliteException != null)
            {
                return sqliteException.SqliteErrorCode == SqliteBusyErrorCode;
            }

            var message = ex.ToString();
            return message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("SQLITE_BUSY", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("busy", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUniqueViolation(Exception ex)
        {
            if (TryGetSqliteException(ex, out var sqliteException) && sqliteException != null)
            {
                return sqliteException.SqliteExtendedErrorCode == SqliteUniqueConstraintExtendedErrorCode
                       || (sqliteException.SqliteErrorCode == SqliteConstraintErrorCode
                           && sqliteException.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase));
            }

            var message = ex.ToString();
            return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("SQLITE_CONSTRAINT", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetSqliteException(Exception ex, out SqliteException? sqliteException)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is SqliteException candidate)
                {
                    sqliteException = candidate;
                    return true;
                }
            }

            sqliteException = null;
            return false;
        }

        private static string BuildRequestKey(ProductionRecord? record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            var batchNo = (record.BatchNo ?? string.Empty).Trim().ToUpperInvariant();
            var operatorName = (record.Operator ?? string.Empty).Trim().ToUpperInvariant();
            var cycleTime = Math.Round(record.CycleTime, 2).ToString("F2", CultureInfo.InvariantCulture);
            var eventTime = record.Time == default ? DateTime.UnixEpoch : record.Time;
            var secondBucket = eventTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

            var signature = $"{batchNo}|{record.ActualCount}|{record.TargetCount}|{cycleTime}|{operatorName}|{secondBucket}";
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(signature));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        #endregion
    }
}
