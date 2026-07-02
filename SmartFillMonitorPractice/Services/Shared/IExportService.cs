using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartFillMonitor.Services.Shared
{
    /// <summary>
    /// 提供通用的数据导出能力。
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// 将指定记录集合导出到目标文件。
        /// </summary>
        Task<string> ExportAsync<T>(IEnumerable<T> records, string filePath);
    }
}
