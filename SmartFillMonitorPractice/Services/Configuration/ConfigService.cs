using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Configuration
{
    public class ConfigService : IConfigService
    {
        private const string SettingFileName = "device-settings.json";
        private readonly SemaphoreSlim _ioLock = new(1, 1);
        private readonly IAuditService _auditService;

        public ConfigService(IAuditService auditService)
        {
            _auditService = auditService;
        }

        public string GetSettingFilePath()
        {
            return Path.Combine(AppContext.BaseDirectory, SettingFileName);
        }

        public async Task<DeviceSettings> LoadSettingsAsync()
        {
            var path = GetSettingFilePath();
            DeviceSettings? settings = null;
            var shouldRecoverWithDefaults = false;

            await _ioLock.WaitAsync();
            try
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(path);
                        settings = JsonSerializer.Deserialize<DeviceSettings>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (settings != null)
                        {
                            Validate(settings);
                            LogHelper.Info($"配置文件加载成功：{path}");
                            return settings;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        LogHelper.Error($"配置文件格式错误，将回退到默认配置：{jsonEx.Message}");
                        BackCorruptFile(path);
                        shouldRecoverWithDefaults = true;
                    }
                    catch (BusinessException)
                    {
                        LogHelper.Warn("检测到非法配置值，已回退到默认配置。");
                        BackCorruptFile(path);
                        shouldRecoverWithDefaults = true;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"读取配置文件失败：{ex.Message}");
                        throw new InfrastructureException("读取配置文件失败，请检查配置文件或磁盘状态。", ex);
                    }
                }
                else
                {
                    LogHelper.Warn($"配置文件不存在：{path}，将创建默认配置。");
                    shouldRecoverWithDefaults = true;
                }
            }
            finally
            {
                _ioLock.Release();
            }

            if (!shouldRecoverWithDefaults)
            {
                throw new InfrastructureException("配置加载失败，未能生成默认配置。");
            }

            settings = new DeviceSettings();
            await SaveDeviceSettingsAsync(settings);
            return settings;
        }

        public async Task<bool> SaveDeviceSettingsAsync(DeviceSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            Validate(settings);

            var path = GetSettingFilePath();
            var tempPath = path + ".tmp";

            await _ioLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, path, true);

                LogHelper.Info("配置文件保存成功。");
                _auditService.Operation("SaveSettings", "Success", $"Port={settings.PortName}, BaudRate={settings.BaudRate}");

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"配置文件保存失败：{ex.Message}");
                _auditService.Operation("SaveSettings", "Failed", ex.Message);

                return false;
            }
            finally
            {
                _ioLock.Release();

                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public void BackCorruptFile(string originalPath)
        {
            try
            {
                var backupPath = originalPath + ".corrupt" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(originalPath, backupPath, true);
                LogHelper.Warn($"已备份损坏的配置文件：{backupPath}");
            }
            catch (Exception ex)
            {
                LogHelper.Error("备份损坏配置文件失败", ex);
            }
        }

        public void Validate(DeviceSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.PortName))
            {
                throw new BusinessException("串口号不能为空。");
            }

            if (settings.BaudRate is < 1200 or > 921600)
            {
                throw new BusinessException("波特率超出允许范围。");
            }

            if (settings.DataBits is < 5 or > 8)
            {
                throw new BusinessException("数据位必须在 5 到 8 之间。");
            }

            if (!Enum.TryParse(typeof(System.IO.Ports.Parity), settings.Parity, true, out _))
            {
                throw new BusinessException("校验位配置无效。");
            }

            if (!Enum.TryParse(typeof(System.IO.Ports.StopBits), settings.StopBits, true, out _))
            {
                throw new BusinessException("停止位配置无效。");
            }
        }
    }
}
