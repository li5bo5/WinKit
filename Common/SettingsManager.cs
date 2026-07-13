using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace WinKit.Common
{
    /// <summary>
    /// 统一配置管理器
    /// </summary>
    public class SettingsManager : IDisposable
    {
        private readonly string _settingsFile;
        private AppSettings _settings;

        public AppSettings Settings => _settings;

        public event EventHandler<AppSettings>? SettingsChanged;

        public SettingsManager()
        {
            AppPaths.EnsureDirectories();
            _settingsFile = AppPaths.Settings;
            _settings = LoadSettings();
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        public void SaveSettings(AppSettings newSettings)
        {
            _settings = newSettings;

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(_settingsFile, json, Encoding.UTF8);

                SettingsChanged?.Invoke(this, _settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsManager: 保存设置失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    var json = File.ReadAllText(_settingsFile, Encoding.UTF8);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsManager: 加载设置失败 - {ex.Message}");
            }

            return new AppSettings();
        }

        public void Dispose()
        {
        }
    }
}
