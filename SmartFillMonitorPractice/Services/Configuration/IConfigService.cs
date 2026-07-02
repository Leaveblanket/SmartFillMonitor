using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Configuration
{
    /// <summary>
    /// 负责设备配置文件的定位、读取、校验与保存。
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// 获取配置文件的物理路径。
        /// </summary>
        string GetSettingFilePath();

        /// <summary>
        /// 读取设备配置。
        /// </summary>
        Task<DeviceSettings> LoadSettingsAsync();

        /// <summary>
        /// 保存设备配置。
        /// 权限校验由调用方负责。
        /// </summary>
        Task<bool> SaveDeviceSettingsAsync(DeviceSettings settings);

        /// <summary>
        /// 备份损坏的配置文件，避免原文件被直接覆盖。
        /// </summary>
        void BackCorruptFile(string originalPath);

        /// <summary>
        /// 校验设备配置是否合法。
        /// </summary>
        void Validate(DeviceSettings settings);
    }
}
