using System;
using System.IO;

namespace WinKit.Common
{
    /// <summary>
    /// 统一的数据与文件路径管理器
    /// </summary>
    public static class AppPaths
    {
        /// <summary>
        /// 应用程序数据目录（%APPDATA%/WinKit）
        /// </summary>
        public static readonly string AppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinKit");

        /// <summary>
        /// 待办事项保存路径 (todos.md)
        /// </summary>
        public static readonly string Todos = Path.Combine(AppData, "todos.md");

        /// <summary>
        /// SQLite 剪贴板数据库路径 (clipboard.db)
        /// </summary>
        public static readonly string Database = Path.Combine(AppData, "clipboard.db");

        /// <summary>
        /// 统一配置文件路径 (settings.json)
        /// </summary>
        public static readonly string Settings = Path.Combine(AppData, "settings.json");

        /// <summary>
        /// 键盘钩子与热键日志路径
        /// </summary>
        public static readonly string HotKeyLog = Path.Combine(AppData, "hotkey.log");

        /// <summary>
        /// 确保数据存放目录存在
        /// </summary>
        public static void EnsureDirectories()
        {
            if (!Directory.Exists(AppData))
            {
                Directory.CreateDirectory(AppData);
            }
        }
    }
}
