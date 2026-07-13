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

        // ── Clipboard 剪贴板设置 ────────────────────────
        public int PasteMaxItems { get; set; } = 100;
        public bool PasteEnableTextDeduplication { get; set; } = true;
        public bool PasteEnableMonitoring { get; set; } = true;
    }
}
