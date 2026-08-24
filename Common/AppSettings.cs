using System;

namespace WinKit.Common
{
    /// <summary>
    /// WinKit 统一配置模型类
    /// </summary>
    public class AppSettings
    {
        // ── TodoList 待办设置 ──────────────────────────
        public bool TodoIsPinned { get; set; } = false;
        public bool TodoIsPassThrough { get; set; } = false;
        /// <summary>双击托盘图标唤出/切换 TodoList</summary>
        public bool TrayDoubleClickTodoEnabled { get; set; } = true;
        /// <summary>待办历史记录保留天数，默认 60 天，超过自动清理</summary>
        public int RecycleBinRetentionDays { get; set; } = 60;

        // ── 常规与外观设置 ──────────────────────────────
        /// <summary>窗口不透明度（40 ~ 100）</summary>
        public int WindowOpacity { get; set; } = 100; // 默认 100%
        /// <summary>开机自动启动</summary>
        public bool AutoStart { get; set; } = true;

        // ── Clipboard 剪贴板设置 ────────────────────────
        public int PasteMaxItems { get; set; } = 300;
        public bool PasteEnableTextDeduplication { get; set; } = true;
        public bool PasteEnableMonitoring { get; set; } = true;
        public bool PasteRememberScrollPosition { get; set; } = false;
        public bool PasteIsPinned { get; set; } = false;

        // ── 自定义快捷键设置 ────────────────────────────
        /// <summary>置顶显示快捷键（置顶时取消置顶，未置顶时置顶并取消穿透）</summary>
        public string HotkeyTodoTopToggle { get; set; } = "Ctrl+D";

        /// <summary>唤出 / 隐藏剪贴板窗口（默认 Win+V）</summary>
        public string HotkeyClipboardToggle { get; set; } = "Win+V";

        /// <summary>在 Todo 编辑状态下保存并退出</summary>
        public string HotkeyTodoSaveAndExit { get; set; } = "Ctrl+S";
    }
}
