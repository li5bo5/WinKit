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

        /// <summary>置顶显示快捷键（置顶时取消置顶，未置顶时置顶并取消穿透）</summary>
        public string HotkeyTodoTopToggle { get; set; } = "Ctrl+D";

        /// <summary>唤出 / 隐藏剪贴板窗口（原 Win+V）</summary>
        public string HotkeyClipboardToggle { get; set; } = "Win+V";

        /// <summary>在 Todo 编辑状态下保存并退出</summary>
        public string HotkeyTodoSaveAndExit { get; set; } = "Ctrl+S";

        /// <summary>Esc 键退出各浮窗与编辑状态（总开关）</summary>
        public bool HotkeyEscExitEnabled { get; set; } = true;
    }
}
