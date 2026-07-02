using System;
using System.Reflection;
using Microsoft.Data.Sqlite;
using SmartFillMonitor.Models;
using Xunit;

namespace SmartFillMonitor.Tests;

public class ProductionRecordWriteServiceTests
{
    [Fact]
    public void PrepareAsync_CreatesUniqueIndex_ForRequestKey()
    {
        using var scope = new TestAppScope();

        var indexes = scope.DbContext.Fsql.Ado.Query<SqliteIndexInfo>(
            """
            SELECT name AS Name, sql AS Sql
            FROM sqlite_master
            WHERE type = 'index'
              AND tbl_name = 'ProductionRecordDeduplications';
            """);

        Assert.Contains(indexes, index =>
            index.Name == "idx_unique_production_request_key" &&
            index.Sql.Contains("UNIQUE INDEX", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SaveAsync_IsIdempotent_ForSameKey()
    {
        using var scope = new TestAppScope();
        var record = new ProductionRecord
        {
            Time = new DateTime(2026, 3, 26, 8, 30, 15),
            BatchNo = "BATCH-001",
            ActualCount = 10,
            TargetCount = 100,
            CycleTime = 1.25,
            Operator = "admin"
        };

        var firstResult = await scope.ProductionRecordService.SaveAsync(record);
        var secondResult = await scope.ProductionRecordService.SaveAsync(new ProductionRecord
        {
            Time = record.Time,
            BatchNo = record.BatchNo,
            ActualCount = record.ActualCount,
            TargetCount = record.TargetCount,
            CycleTime = record.CycleTime,
            Operator = record.Operator
        });

        Assert.True(firstResult);
        Assert.True(secondResult);

        var count = scope.DbContext.Fsql.Select<ProductionRecord>().Count();
        var dedupCount = scope.DbContext.Fsql.Select<ProductionRecordDeduplication>().Count();

        Assert.Equal(1, count);
        Assert.Equal(1, dedupCount);
    }

    [Fact]
    public void IsTransientDbException_ReturnsTrue_ForWrappedSqliteBusyException()
    {
        var exception = new Exception(
            "outer",
            new SqliteException("sqlite failure", 5));

        var result = InvokeExceptionClassifier("IsTransientDbException", exception);

        Assert.True(result);
    }

    [Fact]
    public void IsUniqueViolation_ReturnsTrue_ForWrappedSqliteUniqueConstraintException()
    {
        var exception = new Exception(
            "outer",
            new SqliteException("constraint failed", 19, 2067));

        var result = InvokeExceptionClassifier("IsUniqueViolation", exception);

        Assert.True(result);
    }

    private static bool InvokeExceptionClassifier(string methodName, Exception exception)
    {
        var method = typeof(ProductionRecordService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { exception });
        Assert.IsType<bool>(result);
        return (bool)result!;
    }

    private sealed class SqliteIndexInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Sql { get; set; } = string.Empty;
    }
}
