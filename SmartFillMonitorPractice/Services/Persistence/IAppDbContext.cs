using FreeSql;

namespace SmartFillMonitor.Services.Persistence
{
    /// <summary>
    /// 封装应用数据库上下文，统一暴露 FreeSql 实例及初始化入口。
    /// </summary>
    public interface IAppDbContext : IDisposable
    {
        /// <summary>
        /// 当前应用使用的 FreeSql 实例。
        /// </summary>
        IFreeSql Fsql { get; }

        /// <summary>
        /// 根据连接字符串和数据库类型初始化数据库上下文。
        /// </summary>
        void Initialize(string connectionString, DataType dataType = DataType.Sqlite);
    }
}
