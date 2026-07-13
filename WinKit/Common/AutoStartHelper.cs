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

                var value = key.GetValue(AppName);
                return value != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoStartHelper: 注册表读取失败 - {ex.Message}");
                return false;
            }
        }
    }
}
