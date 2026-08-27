using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace WinKit.Common
{
    /// <summary>
    /// 统一配置管理器，基于 settings.jsonl 单行明文存储与原子替换
    /// </summary>
    public class SettingsManager
    {
        private readonly string _settingsFile;
        private readonly string _tmpSettingsFile;
        private AppSettings _settings;
        private readonly object _lock = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = false // 单行 JSON
        };

        public AppSettings Settings => _settings;

        public event EventHandler<AppSettings>? SettingsChanged;

        public SettingsManager()
        {
            AppPaths.EnsureDirectories();
            _settingsFile = AppPaths.Settings;
            _tmpSettingsFile = _settingsFile + ".tmp";
            _settings = LoadSettings();
        }

        /// <summary>
        /// 保存设置并触发变更广播（原子写入 settings.jsonl）
        /// </summary>
        public void SaveSettings(AppSettings newSettings)
        {
            if (newSettings == null) return;
            _settings = newSettings;

            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_settings, JsonOptions);
                    File.WriteAllText(_tmpSettingsFile, json + Environment.NewLine, Encoding.UTF8);
                    File.Move(_tmpSettingsFile, _settingsFile, overwrite: true);

                    SettingsChanged?.Invoke(this, _settings);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SettingsManager: 保存设置失败 ({ex.Message})");
                }
                finally
                {
                    try
                    {
                        if (File.Exists(_tmpSettingsFile))
                        {
                            File.Delete(_tmpSettingsFile);
                        }
                    }
                    catch
                    {
                        // 忽略临时文件清理异常
                    }
                }
            }
        }

        /// <summary>
        /// 加载设置（支持旧版 settings.json 平滑无缝迁移）
        /// </summary>
        private AppSettings LoadSettings()
        {
            lock (_lock)
            {
                try
                {
                    // 1. 优先从 settings.jsonl 读取
                    if (File.Exists(_settingsFile))
                    {
                        var lines = File.ReadAllLines(_settingsFile, Encoding.UTF8);
                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if (string.IsNullOrWhiteSpace(trimmed)) continue;

                            var parsed = JsonSerializer.Deserialize<AppSettings>(trimmed, JsonOptions);
                            if (parsed != null) return parsed;
                        }
                    }

                    // 2. 兼容检查旧版 settings.json 并平滑迁移
                    var oldJsonPath = Path.Combine(AppPaths.AppData, "settings.json");
                    if (File.Exists(oldJsonPath))
                    {
                        try
                        {
                            var oldContent = File.ReadAllText(oldJsonPath, Encoding.UTF8);
                            var oldParsed = JsonSerializer.Deserialize<AppSettings>(oldContent, JsonOptions);
                            if (oldParsed != null)
                            {
                                // 迁移保存为 settings.jsonl
                                SaveSettings(oldParsed);
                                return oldParsed;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"SettingsManager: 旧配置迁移失败 ({ex.Message})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SettingsManager: 加载设置失败 ({ex.Message})");
                }

                var defaultSettings = new AppSettings();
                SaveSettings(defaultSettings);
                return defaultSettings;
            }
        }
    }
}
