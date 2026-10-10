using System;
using Microsoft.Win32;

namespace WinKit.Common
{
    /// <summary>
    /// 开机启动项配置助手
    /// </summary>
    public static class AutoStartHelper
    {
        private const string AppName = "WinKit";
        private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// 开启或关闭开机自启
        /// </summary>
        public static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoStartHelper: 注册表写入失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否已设置开机自启
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
                if (key == null) return false;

                var value = key.GetValue(AppName) as string;
                if (string.IsNullOrWhiteSpace(value)) return false;

                string currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(currentExe)) return false;

                string registeredPath = value.Trim().Trim('"');
                return string.Equals(registeredPath, currentExe, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoStartHelper: 注册表读取失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查并校准自启路径（如果已开启自启但路径与当前 EXE 不一致，则自动更新）
        /// </summary>
        public static void CheckAndSyncPath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
                if (key == null) return;

                var value = key.GetValue(AppName) as string;
                if (string.IsNullOrWhiteSpace(value)) return;

                string currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(currentExe)) return;

                string registeredPath = value.Trim().Trim('"');
                if (!string.Equals(registeredPath, currentExe, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(AppName, $"\"{currentExe}\"");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoStartHelper: 校准自启路径失败 - {ex.Message}");
            }
        }
    }
}
