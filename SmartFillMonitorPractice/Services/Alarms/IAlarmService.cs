using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Alarms
{
    /// <summary>
    /// 定义报警领域的核心服务，负责报警生命周期管理、事件发布和历史查询。
    /// </summary>
    public interface IAlarmService
    {
        /// <summary>
        /// 当新报警被触发时发布。
        /// </summary>
        event EventHandler<AlarmRecord>? AlarmTriggered;

        /// <summary>
        /// 当报警被确认时发布。
        /// </summary>
        event EventHandler<AlarmRecord>? AlarmAcknowledged;

        /// <summary>
        /// 当报警被恢复时发布。
        /// </summary>
        event EventHandler<AlarmRecord>? AlarmRecovered;

        /// <summary>
        /// 触发一条报警记录。
        /// </summary>
        Task TriggerAlarmAsync(AlarmRecord alarmRecord);

        /// <summary>
        /// 触发一条用于测试的报警。
        /// </summary>
        Task TriggerTestAlarmAsync();

        /// <summary>
        /// 确认指定报警，并可附带处理建议。
        /// </summary>
        Task<bool> AcknowledgeAlarmAsync(long alarmId, string processSuggestion = "");

        /// <summary>
        /// 恢复指定报警。
        /// </summary>
        Task<bool> RecoverAlarmAsync(long alarmId);

        /// <summary>
        /// 按报警编码恢复当前活动报警。
        /// </summary>
        Task<bool> RecoverAlarmAsync(AlarmCode alarmCode);

        /// <summary>
        /// 根据报警当前状态决定执行确认还是恢复操作。
        /// </summary>
        Task<bool> HandleAlarmActionAsync(long alarmId, bool isAcknowledged, string processSuggestion = "");

        /// <summary>
        /// 恢复测试报警。
        /// </summary>
        Task<bool> RecoverTestAlarmAsync();

        /// <summary>
        /// 获取当前所有活动报警。
        /// </summary>
        Task<List<AlarmRecord>> GetActiveAlarmsAsync();

        /// <summary>
        /// 按时间和级别分页查询报警历史。
        /// </summary>
        Task<(List<AlarmRecord> Item, long Total)> GetAlarmHistoryAsync(
            int pageIndex,
            int pageSize,
            DateTime? startTime = null,
            DateTime? endTime = null,
            AlarmSeverity alarmSeverity = AlarmSeverity.All);
    }
}
