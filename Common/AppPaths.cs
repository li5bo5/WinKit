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
        /// 应用程序数据目录（应用程序所在目录下的 Data 文件夹，绿色便携）
        /// </summary>
        public static readonly string AppData = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Data");

        /// <summary>
        /// 待办事项明文保存路径 (todos.jsonl)
        /// </summary>
        public static readonly string Todos = Path.Combine(AppData, "todos.jsonl");

        /// <summary>
        /// 待办回收站明文保存路径 (recycle_bin.jsonl)
        /// </summary>
        public static readonly string RecycleBin = Path.Combine(AppData, "recycle_bin.jsonl");

        /// <summary>
        /// 剪贴板历史明文保存路径 (clipboard.jsonl)
        /// </summary>
        public static readonly string Clipboard = Path.Combine(AppData, "clipboard.jsonl");

        /// <summary>
        /// 常用短语明文保存路径 (phrases.jsonl)
        /// </summary>
        public static readonly string Phrases = Path.Combine(AppData, "phrases.jsonl");

        /// <summary>
        /// 剪贴板原图保存目录 (Data/clipboard/images)
        /// </summary>
        public static readonly string ClipboardImagesDir = Path.Combine(AppData, "clipboard", "images");

        /// <summary>
        /// 剪贴板缩略图保存目录 (Data/clipboard/thumbnails)
        /// </summary>
        public static readonly string ClipboardThumbnailsDir = Path.Combine(AppData, "clipboard", "thumbnails");

        /// <summary>
        /// 统一配置文件路径 (settings.jsonl)
        /// </summary>
        public static readonly string Settings = Path.Combine(AppData, "settings.jsonl");

        /// <summary>
        /// 确保数据存放目录存在
        /// </summary>
        public static void EnsureDirectories()
        {
            if (!Directory.Exists(AppData))
            {
                Directory.CreateDirectory(AppData);
            }
            if (!Directory.Exists(ClipboardImagesDir))
            {
                Directory.CreateDirectory(ClipboardImagesDir);
            }
            if (!Directory.Exists(ClipboardThumbnailsDir))
            {
                Directory.CreateDirectory(ClipboardThumbnailsDir);
            }
        }
    }
}
