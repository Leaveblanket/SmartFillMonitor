using System;
using System.Linq;
using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Alarms
{
    /// <summary>
    /// 在应用启动期间初始化报警存储结构。
    /// </summary>
    public sealed class AlarmBootstrapper : IAlarmBootstrapper
    {
        private readonly IAppDbContext _dbContext;

        public AlarmBootstrapper(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task InitializeAsync()
        {
            await NormalizeActiveAlarmAsync();
            const string sql = "CREATE UNIQUE INDEX IF NOT EXISTS idx_alarm_active_unique ON AlarmRecord(AlarmCode) WHERE IsActive = 1;";
            await _dbContext.Fsql.Ado.ExecuteNonQueryAsync(sql);
        }

        private async Task NormalizeActiveAlarmAsync()
        {
            var activeAlarms = await _dbContext.Fsql.Select<AlarmRecord>()
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            foreach (var group in activeAlarms.GroupBy(a => a.AlarmCode))
            {
                var keep = group.OrderByDescending(a => a.StartTime).ThenByDescending(a => a.Id).First();
                foreach (var duplicate in group.Where(a => a.Id != keep.Id))
                {
                    var endTime = duplicate.EndTime == default ? (duplicate.AckTime ?? duplicate.StartTime) : duplicate.EndTime;
                    var duration = Math.Max(0, (endTime - duplicate.StartTime).TotalSeconds);

                    await _dbContext.Fsql.Update<AlarmRecord>()
                        .Where(a => a.Id == duplicate.Id)
                        .Set(a => a.IsActive, false)
                        .Set(a => a.EndTime, endTime)
                        .Set(a => a.DurationSeconds, duration)
                        .Set(a => a.RecoverUserName, string.IsNullOrWhiteSpace(duplicate.RecoverUserName) ? "system" : duplicate.RecoverUserName)
                        .ExecuteAffrowsAsync();
                }
            }
        }
    }
}
