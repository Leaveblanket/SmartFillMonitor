using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services.Session;

namespace SmartFillMonitor.Services.Alarms
{
    public class AlarmService : IAlarmService
    {
        private readonly IAppDbContext _dbContext;
        private readonly ISessionService _sessionService;
        private readonly IAuthorizationService _authorizationService;
        private readonly IAuditService _auditService;

        public AlarmService(IAppDbContext dbContext, ISessionService sessionService, IAuthorizationService authorizationService, IAuditService auditService)
        {
            _dbContext = dbContext;
            _sessionService = sessionService;
            _authorizationService = authorizationService;
            _auditService = auditService;
        }

        public event EventHandler<AlarmRecord>? AlarmTriggered;

        public event EventHandler<AlarmRecord>? AlarmAcknowledged;

        public event EventHandler<AlarmRecord>? AlarmRecovered;

        public async Task TriggerAlarmAsync(AlarmRecord alarmRecord)
        {
            if (alarmRecord == null || alarmRecord.AlarmCode == AlarmCode.None)
            {
                return;
            }

            try
            {
                var now = DateTime.Now;
                alarmRecord.StartTime = alarmRecord.StartTime == default ? now : alarmRecord.StartTime;
                alarmRecord.IsActive = true;
                alarmRecord.IsAcknowledged = false;
                alarmRecord.AckTime = null;
                alarmRecord.AckUser = null;
                alarmRecord.AckUserId = null;
                alarmRecord.AckUserName = null;
                alarmRecord.RecoverUserId = null;
                alarmRecord.RecoverUserName = null;
                alarmRecord.EndTime = default;
                alarmRecord.DurationSeconds = null;
                alarmRecord.ProcessSuggestion = null;
                alarmRecord.TriggeredBy = NormalizeTriggeredBy(alarmRecord.TriggeredBy, alarmRecord.TriggeredByType);

                if (string.IsNullOrWhiteSpace(alarmRecord.Description))
                {
                    alarmRecord.Description = alarmRecord.AlarmCode.GetDescription();
                }

                await _dbContext.Fsql.Insert(alarmRecord).ExecuteAffrowsAsync();

                var latestRecord = await _dbContext.Fsql.Select<AlarmRecord>()
                    .Where(a => a.AlarmCode == alarmRecord.AlarmCode && a.IsActive)
                    .OrderByDescending(a => a.Id)
                    .FirstAsync();

                if (latestRecord != null)
                {
                    alarmRecord = latestRecord;
                }

                LogHelper.Warn($"[报警触发] {alarmRecord.AlarmCode}: {alarmRecord.Message ?? alarmRecord.Description}");
                AlarmTriggered?.Invoke(this, alarmRecord);
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                LogHelper.Debug($"报警 {alarmRecord.AlarmCode} 已存在活动记录，忽略重复触发。");
            }
            catch (Exception ex)
            {
                LogHelper.Error($"触发报警异常：{alarmRecord.AlarmCode}", ex);
                throw new InfrastructureException("触发报警失败。", ex);
            }
        }

        public Task TriggerTestAlarmAsync()
        {
            var currentUser = _sessionService.CurrentUser;
            var triggeredBy = currentUser?.UserName ?? "system";
            var sourceType = currentUser == null ? AlarmTriggeredByType.System : AlarmTriggeredByType.User;

            return TriggerAlarmAsync(new AlarmRecord
            {
                AlarmCode = AlarmCode.TestAlarm,
                AlarmSeverity = AlarmSeverity.Warning,
                StartTime = DateTime.Now,
                Description = "用于测试报警链路",
                Message = "测试报警已触发",
                TriggeredBy = triggeredBy,
                TriggeredByType = sourceType
            });
        }

        public async Task<bool> AcknowledgeAlarmAsync(long alarmId, string processSuggestion = "")
        {
            _authorizationService.EnsurePermission(Permission.ManageAlarms, "确认报警");
            var (userId, actor) = ResolveCurrentUserForAlarmAction();
            var activeAlarm = await _dbContext.Fsql.Select<AlarmRecord>()
                .Where(a => a.Id == alarmId && a.IsActive)
                .FirstAsync();

            if (activeAlarm == null)
            {
                return false;
            }

            if (activeAlarm.IsAcknowledged)
            {
                return true;
            }

            var now = DateTime.Now;
            activeAlarm.IsAcknowledged = true;
            activeAlarm.AckTime = now;
            activeAlarm.AckUser = actor;
            activeAlarm.AckUserId = userId;
            activeAlarm.AckUserName = actor;
            activeAlarm.ProcessSuggestion = string.IsNullOrWhiteSpace(processSuggestion) ? null : processSuggestion.Trim();

            await _dbContext.Fsql.Update<AlarmRecord>()
                .Where(a => a.Id == activeAlarm.Id)
                .Set(a => a.IsAcknowledged, true)
                .Set(a => a.AckTime, now)
                .Set(a => a.AckUser, actor)
                .Set(a => a.AckUserId, userId)
                .Set(a => a.AckUserName, actor)
                .Set(a => a.ProcessSuggestion, activeAlarm.ProcessSuggestion)
                .ExecuteAffrowsAsync();

            _auditService.Operation("AcknowledgeAlarm", "Success", $"报警已确认：ID={activeAlarm.Id}；编码={activeAlarm.AlarmCode}", actor);
            AlarmAcknowledged?.Invoke(this, activeAlarm);
            return true;
        }

        public Task<bool> RecoverAlarmAsync(AlarmCode alarmCode)
        {
            return RecoverAlarmInternalAsync(
                () => _dbContext.Fsql.Select<AlarmRecord>()
                    .Where(a => a.AlarmCode == alarmCode && a.IsActive)
                    .OrderByDescending(a => a.Id)
                    .FirstAsync(),
                null,
                "system",
                false);
        }

        public Task<bool> RecoverAlarmAsync(long alarmId)
        {
            var (userId, actor) = ResolveCurrentUserForAlarmAction();
            return RecoverAlarmInternalAsync(
                () => _dbContext.Fsql.Select<AlarmRecord>()
                    .Where(a => a.Id == alarmId && a.IsActive)
                    .FirstAsync(),
                userId,
                actor,
                true);
        }

        public Task<bool> HandleAlarmActionAsync(long alarmId, bool isAcknowledged, string processSuggestion = "")
        {
            return isAcknowledged
                ? RecoverAlarmAsync(alarmId)
                : AcknowledgeAlarmAsync(alarmId, processSuggestion);
        }

        public Task<bool> RecoverTestAlarmAsync()
        {
            return RecoverAlarmAsync(AlarmCode.TestAlarm);
        }

        public async Task<List<AlarmRecord>> GetActiveAlarmsAsync()
        {
            return await _dbContext.Fsql.Select<AlarmRecord>()
                .Where(a => a.IsActive)
                .OrderBy(a => a.IsAcknowledged)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();
        }

        public async Task<(List<AlarmRecord> Item, long Total)> GetAlarmHistoryAsync(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null, AlarmSeverity alarmSeverity = AlarmSeverity.All)
        {
            try
            {
                var query = _dbContext.Fsql.Select<AlarmRecord>()
                    .Where(w => !w.IsActive);

                if (startTime.HasValue)
                {
                    query = query.Where(w => w.StartTime >= startTime.Value);
                }

                if (endTime.HasValue)
                {
                    query = query.Where(w => w.StartTime < endTime.Value);
                }

                if (alarmSeverity != AlarmSeverity.All)
                {
                    query = query.Where(w => w.AlarmSeverity == alarmSeverity);
                }

                var total = await query.CountAsync();
                var list = await query
                    .OrderByDescending(a => a.EndTime)
                    .OrderByDescending(a => a.StartTime)
                    .Page(pageIndex, pageSize)
                    .ToListAsync();

                return (list, total);
            }
            catch (Exception ex)
            {
                LogHelper.Error("查询历史报警失败", ex);
                throw new InfrastructureException("查询历史报警失败。", ex);
            }
        }

        private async Task<bool> RecoverAlarmInternalAsync(Func<Task<AlarmRecord>> finder, long? recoverUserId, string actor, bool requireAuthorization)
        {
            if (requireAuthorization)
            {
                _authorizationService.EnsurePermission(Permission.ManageAlarms, "恢复报警");
            }

            var activeAlarm = await finder();
            if (activeAlarm == null)
            {
                return false;
            }

            var now = DateTime.Now;
            activeAlarm.IsActive = false;
            activeAlarm.EndTime = now;
            activeAlarm.DurationSeconds = Math.Max(0, (now - activeAlarm.StartTime).TotalSeconds);
            activeAlarm.RecoverUserId = recoverUserId;
            activeAlarm.RecoverUserName = actor;

            await _dbContext.Fsql.Update<AlarmRecord>()
                .Where(a => a.Id == activeAlarm.Id)
                .Set(a => a.IsActive, false)
                .Set(a => a.EndTime, now)
                .Set(a => a.DurationSeconds, activeAlarm.DurationSeconds)
                .Set(a => a.RecoverUserId, recoverUserId)
                .Set(a => a.RecoverUserName, actor)
                .ExecuteAffrowsAsync();

            if (requireAuthorization)
            {
                _auditService.Operation("RecoverAlarm", "Success", $"报警已恢复：ID={activeAlarm.Id}；编码={activeAlarm.AlarmCode}", actor);
            }
            else
            {
                LogHelper.Info($"报警已由系统恢复：ID={activeAlarm.Id}；编码={activeAlarm.AlarmCode}；执行者={actor}");
            }

            AlarmRecovered?.Invoke(this, activeAlarm);
            return true;
        }

        private (long? UserId, string Actor) ResolveCurrentUserForAlarmAction()
        {
            var currentUser = _sessionService.CurrentUser;
            if (currentUser == null)
            {
                throw new AuthorizationException("当前未登录，无法执行报警操作。");
            }

            var actor = currentUser.UserName;
            if (!string.IsNullOrWhiteSpace(actor))
            {
                return (currentUser.Id, actor);
            }

            throw new AuthorizationException("当前用户信息无效，无法执行报警操作。");
        }

        private string NormalizeTriggeredBy(string? triggeredBy, AlarmTriggeredByType sourceType)
        {
            if (!string.IsNullOrWhiteSpace(triggeredBy))
            {
                return triggeredBy.Trim();
            }

            return sourceType switch
            {
                AlarmTriggeredByType.Plc => "PLC",
                AlarmTriggeredByType.User => string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.UserName) ? "user" : _sessionService.CurrentUser.UserName,
                _ => "system",
            };
        }

        private static bool IsUniqueViolation(Exception ex)
        {
            var message = ex.ToString();
            return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("SQLITE_CONSTRAINT", StringComparison.OrdinalIgnoreCase);
        }
    }
}
