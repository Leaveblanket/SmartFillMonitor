using System.Collections.Generic;
using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Logging
{
    /// <summary>
    /// 负责系统日志的持久化查询与导出。
    /// </summary>
    public interface ISystemLogService
    {
        /// <summary>
        /// 按筛选条件分页查询系统日志。
        /// </summary>
        Task<(List<SystemLog> Items, long Total)> QueryAsync(SystemLogQueryFilter filter, int pageIndex, int pageSize);

        /// <summary>
        /// 将筛选后的系统日志导出到指定文件。
        /// 权限校验由调用方负责。
        /// </summary>
        Task<string> ExportAsync(SystemLogQueryFilter filter, string filePath);
    }
}
