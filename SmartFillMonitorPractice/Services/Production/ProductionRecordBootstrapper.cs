using System.Threading.Tasks;

namespace SmartFillMonitor.Services.Production
{
    /// <summary>
    /// 在应用启动期间初始化生产记录存储结构。
    /// </summary>
    public sealed class ProductionRecordBootstrapper : IProductionRecordBootstrapper
    {
        private readonly IAppDbContext _dbContext;

        public ProductionRecordBootstrapper(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task InitializeAsync()
        {
            await EnsureDeduplicationStoreAsync();
            await EnsureRequestKeyUniquenessAsync();
        }

        private async Task EnsureDeduplicationStoreAsync()
        {
            const string sql = """
                               CREATE TABLE IF NOT EXISTS ProductionRecordDeduplications (
                                   Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                   RequestKey TEXT NOT NULL,
                                   RecordId INTEGER NULL,
                                   CreatedAt TEXT NOT NULL
                               );
                               """;
            await _dbContext.Fsql.Ado.ExecuteNonQueryAsync(sql);
        }

        private async Task EnsureRequestKeyUniquenessAsync()
        {
            const string sql = "CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_production_request_key ON ProductionRecordDeduplications(RequestKey);";
            await _dbContext.Fsql.Ado.ExecuteNonQueryAsync(sql);
        }
    }
}
