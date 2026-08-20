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

        // ── 不透明度设置 ────────────────────────────────
        public int WindowOpacity { get; set; } = 80; // 80% 不透明度（默认）

        // ── Clipboard 剪贴板设置 ────────────────────────
        public int PasteMaxItems { get; set; } = 100;
        public bool PasteEnableTextDeduplication { get; set; } = true;
        public bool PasteEnableMonitoring { get; set; } = true;
        public bool PasteRememberScrollPosition { get; set; } = false;
        public bool PasteIsPinned { get; set; } = false;

        // ── 自定义快捷键设置 ────────────────────────────
        // 格式："Modifiers+Key"，例如 "Win+V"、"Alt+V"、"Ctrl+Shift+V"
        // Modifiers 可组合：Win、Ctrl、Alt、Shift（用 + 分隔）

        /// <summary>临时显示 TodoList（10 秒无操作自动消失）</summary>
        public string HotkeyTodoTempShow { get; set; } = "Win+Alt+D";

        /// <summary>唤出 / 隐藏剪贴板窗口（原 Win+V）</summary>
        public string HotkeyClipboardToggle { get; set; } = "Win+V";

        /// <summary>映射截图 OCR 快捷键</summary>
        public string HotkeyScreenshotOcr { get; set; } = "Win+Shift+T";

        /// <summary>映射系统截图工具快捷键</summary>
        public string HotkeyScreenshotSnip { get; set; } = "Win+Shift+S";

        /// <summary>在 Todo 编辑状态下保存并退出</summary>
        public string HotkeyTodoSaveAndExit { get; set; } = "Win+S";

        /// <summary>Esc 键退出各浮窗与编辑状态（总开关）</summary>
        public bool HotkeyEscExitEnabled { get; set; } = true;
    }
}
