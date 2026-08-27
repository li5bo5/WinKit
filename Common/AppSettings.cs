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
        /// <summary>待办事项展开显示（true: 完全展开自动换行; false: 默认3行折叠）</summary>
        public bool TodoExpandedDisplay { get; set; } = false;
        /// <summary>双击托盘图标唤出/切换 TodoList</summary>
        public bool TrayDoubleClickTodoEnabled { get; set; } = true;
        /// <summary>待办历史记录保留天数，默认 60 天，超过自动清理</summary>
        public int RecycleBinRetentionDays { get; set; } = 60;

        // ── Quick Phrases 常用短语设置 ──────────────────
        /// <summary>是否启用连续输入 vv 呼出常用短语</summary>
        public bool QuickPhraseEnabled { get; set; } = true;

        // ── 常规与外观设置 ──────────────────────────────
        /// <summary>颜色主题模式：System(跟随系统，默认), Light(浅色), Dark(深色)</summary>
        public string ThemeMode { get; set; } = "System";
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
        /// <summary>剪贴板图片历史保留天数，默认 15 天 (1~365)</summary>
        public int ClipboardImageRetentionDays { get; set; } = 15;
        /// <summary>剪贴板图片最大缓存占用空间，默认 100MB (50~2048)</summary>
        public int ClipboardImageMaxStorageMB { get; set; } = 100;

        // ── 自定义快捷键设置 ────────────────────────────
        /// <summary>置顶显示快捷键（置顶时取消置顶，未置顶时置顶并取消穿透）</summary>
        public string HotkeyTodoTopToggle { get; set; } = "Ctrl+D";

        /// <summary>唤出 / 隐藏剪贴板窗口（默认 Win+V）</summary>
        public string HotkeyClipboardToggle { get; set; } = "Win+V";

        /// <summary>在 Todo 编辑状态下保存并退出</summary>
        public string HotkeyTodoSaveAndExit { get; set; } = "Ctrl+S";
    }
}
