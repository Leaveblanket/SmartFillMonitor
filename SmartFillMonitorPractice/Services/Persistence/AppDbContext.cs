using System;
using FreeSql;

namespace SmartFillMonitor.Services.Persistence
{
    public class AppDbContext : IAppDbContext
    {
        private readonly object _lock = new();
        private bool _disposed;

        public IFreeSql Fsql { get; private set; } = null!;

        public void Initialize(string connectionString, DataType dataType = DataType.Sqlite)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (Fsql != null)
            {
                return;
            }

            lock (_lock)
            {
                if (Fsql != null)
                {
                    return;
                }

                Fsql = new FreeSqlBuilder()
                    .UseConnectionString(dataType, connectionString)
                    .UseAdoConnectionPool(true)
                    .UseMonitorCommand(
                        _ => { },
                        (cmd, traceLog) =>
                        {
                            Console.WriteLine($"[SQL]: {cmd.CommandText}\r\n->{traceLog}");
                        })
                    .UseAutoSyncStructure(true)
                    .UseLazyLoading(true)
                    .Build();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Fsql?.Dispose();
            _disposed = true;
        }
    }
}
