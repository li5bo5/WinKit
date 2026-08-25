using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace WinKit.Common
{
    /// <summary>
    /// 全局主题管理器：负责提取 Windows 任务栏/系统色彩、感知亮度自适应与 DynamicResource 注入
    /// </summary>
    public static class ThemeManager
    {
        private static SettingsManager? _settingsManager;
        private static bool _isHooked = false;

        /// <summary>
        /// 初始化主题管理器，挂载配置并开始监听系统事件
        /// </summary>
        public static void Initialize(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            if (!_isHooked)
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                _isHooked = true;
            }
            ApplyTheme();
        }

        /// <summary>
        /// 释放系统事件监听
        /// </summary>
        public static void Dispose()
        {
            if (_isHooked)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                _isHooked = false;
            }
        }

        /// <summary>
        /// 响应系统偏好设置或主题变化
        /// </summary>
        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // 当系统偏好或主题色彩变更且当前为跟随系统模式时，实时刷新主题
            if (_settingsManager?.Settings.ThemeMode == "System" || string.IsNullOrEmpty(_settingsManager?.Settings.ThemeMode))
            {
                Application.Current?.Dispatcher.Invoke(() => ApplyTheme());
            }
        }

        /// <summary>
        /// 应用主题样式并派发全局 DynamicResource 资源
        /// </summary>
        /// <param name="overrideMode">临时覆盖模式（用于设置界面下拉即时预览）</param>
        /// <param name="overrideOpacity">临时覆盖不透明度（用于设置界面调节预览）</param>
        public static void ApplyTheme(string? overrideMode = null, int? overrideOpacity = null)
        {
            string mode = overrideMode ?? _settingsManager?.Settings.ThemeMode ?? "System";
            int opacityPercent = overrideOpacity ?? _settingsManager?.Settings.WindowOpacity ?? 100;
            opacityPercent = Math.Clamp(opacityPercent, 40, 100);

            Color baseBgColor;
            bool isDark;

            if (mode == "Light")
            {
                baseBgColor = Color.FromRgb(245, 245, 245);
                isDark = false;
            }
            else if (mode == "Dark")
            {
                baseBgColor = Color.FromRgb(32, 32, 32);
                isDark = true;
            }
            else // "System" (跟随系统)
            {
                (baseBgColor, isDark) = GetSystemTaskbarColorAndBrightness();
            }

            // 计算带透明度的背景色（基础 Alpha 240 乘以用户设定的不透明度比例）
            byte finalAlpha = (byte)Math.Clamp((int)(240 * (opacityPercent / 100.0)), 30, 255);
            Color windowBg = Color.FromArgb(finalAlpha, baseBgColor.R, baseBgColor.G, baseBgColor.B);

            UpdateResources(windowBg, isDark);
        }

        /// <summary>
        /// 获取 Windows 系统的任务栏合成色及感知亮度
        /// </summary>
        private static (Color Color, bool IsDark) GetSystemTaskbarColorAndBrightness()
        {
            try
            {
                // 1. 读取 Windows 个性化主题设置
                using var personalizeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                bool systemUsesLightTheme = false;
                bool colorPrevalence = false;

                if (personalizeKey != null)
                {
                    var sysLightVal = personalizeKey.GetValue("SystemUsesLightTheme");
                    if (sysLightVal is int sysLightInt) systemUsesLightTheme = (sysLightInt == 1);

                    var prevVal = personalizeKey.GetValue("ColorPrevalence");
                    if (prevVal is int prevInt) colorPrevalence = (prevInt == 1);
                }

                // 2. 如果开启了在任务栏显示强调色
                if (colorPrevalence)
                {
                    using var dwmKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                    if (dwmKey != null)
                    {
                        var colorVal = dwmKey.GetValue("ColorizationColor");
                        if (colorVal is int dwmColorInt)
                        {
                            byte r = (byte)((dwmColorInt >> 16) & 0xFF);
                            byte g = (byte)((dwmColorInt >> 8) & 0xFF);
                            byte b = (byte)(dwmColorInt & 0xFF);
                            Color accent = Color.FromRgb(r, g, b);
                            // 感知亮度计算公式
                            bool dark = ((r * 299 + g * 587 + b * 114) / 1000) < 130;
                            return (accent, dark);
                        }
                    }
                }

                // 3. 未开启强调色或提取失败，根据系统深浅色判断
                if (systemUsesLightTheme)
                {
                    // Windows 11 浅色任务栏
                    return (Color.FromRgb(243, 243, 243), false);
                }
                else
                {
                    // Windows 10 默认任务栏或 Windows 11 深色任务栏
                    return (Color.FromRgb(32, 32, 32), true);
                }
            }
            catch
            {
                // 降级兜底：浅色
                return (Color.FromRgb(245, 245, 245), false);
            }
        }

        /// <summary>
        /// 更新全局 Application Resources 中的 DynamicResource 主题画刷
        /// </summary>
        private static void UpdateResources(Color windowBg, bool isDark)
        {
            var res = Application.Current?.Resources;
            if (res == null) return;

            // 窗体背景与边框
            res["ThemeWindowBackgroundBrush"] = new SolidColorBrush(windowBg);
            res["ThemeWindowBorderBrush"]     = new SolidColorBrush(isDark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(26, 0, 0, 0));

            // 文本颜色
            res["ThemeTextPrimaryBrush"]     = new SolidColorBrush(isDark ? Color.FromRgb(245, 245, 245) : Color.FromRgb(34, 34, 34));
            res["ThemeTextSecondaryBrush"]   = new SolidColorBrush(isDark ? Color.FromRgb(168, 168, 168) : Color.FromRgb(102, 102, 102));
            res["ThemeTextTertiaryBrush"]    = new SolidColorBrush(isDark ? Color.FromRgb(130, 130, 130) : Color.FromRgb(153, 153, 153));
            res["ThemeTextPlaceholderBrush"] = new SolidColorBrush(isDark ? Color.FromArgb(102, 255, 255, 255) : Color.FromArgb(136, 0, 0, 0));

            // 列表项悬停与选中
            res["ThemeItemHoverBrush"]       = new SolidColorBrush(isDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0));
            res["ThemeItemSelectedBrush"]    = new SolidColorBrush(isDark ? Color.FromArgb(46, 255, 255, 255) : Color.FromArgb(26, 0, 0, 0));

            // 输入框背景与边框
            res["ThemeInputBackgroundBrush"] = new SolidColorBrush(isDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromRgb(255, 255, 255));
            res["ThemeInputBorderBrush"]     = new SolidColorBrush(isDark ? Color.FromArgb(64, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0));

            // 卡片容器背景与边框 (偏好设置等)
            res["ThemeCardBackgroundBrush"]  = new SolidColorBrush(isDark ? Color.FromArgb(26, 255, 255, 255) : Color.FromArgb(153, 255, 255, 255));
            res["ThemeCardBorderBrush"]      = new SolidColorBrush(isDark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(21, 0, 0, 0));

            // 分隔线
            res["ThemeSeparatorBrush"]       = new SolidColorBrush(isDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0));

            // 按钮悬停/按压
            res["ThemeButtonHoverBrush"]     = new SolidColorBrush(isDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0));
            res["ThemeButtonPressedBrush"]   = new SolidColorBrush(isDark ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(35, 0, 0, 0));

            // 滚动条滑块
            res["ThemeScrollBarThumbBrush"]  = new SolidColorBrush(isDark ? Color.FromArgb(77, 255, 255, 255) : Color.FromArgb(77, 0, 0, 0));

            // 窗体阴影效果
            res["ThemeShadowColor"]          = isDark ? Colors.Black : Colors.Black;
            res["ThemeShadowOpacity"]        = isDark ? 0.35 : 0.12;
        }
    }
}
