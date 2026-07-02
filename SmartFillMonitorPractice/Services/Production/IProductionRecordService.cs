using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Production
{
    /// <summary>
    /// 负责生产记录的准备、保存、查询和导出。
    /// </summary>
    public interface IProductionRecordService
    {
        /// <summary>
        /// 保存一条生产记录。
        /// </summary>
        Task<bool> SaveAsync(ProductionRecord record);

        /// <summary>
        /// 按时间范围查询生产记录。
        /// </summary>
        Task<List<ProductionRecord>> QueryAsync(DateTime start, DateTime end);

        /// <summary>
        /// 将生产记录导出到指定文件。
        /// 权限校验由调用方负责。
        /// </summary>
        Task ExportAsync(List<ProductionRecord> records, string filePath);
    }
}
