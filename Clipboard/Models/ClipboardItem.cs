using System;

namespace WinKit.Clipboard.Models
{
    /// <summary>
    /// 剪贴板项目类型
    /// </summary>
    public enum ClipboardItemType
    {
        Text
    }

    /// <summary>
    /// 剪贴板项目实体
    /// </summary>
    public class ClipboardItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ClipboardItemType Type { get; set; }
        public string? Content { get; set; }
        public string? ContentHash { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 获取显示文本（用于列表展示）
        /// </summary>
        public string DisplayText
        {
            get
            {
                var text = Content ?? string.Empty;
                // 去除首尾多余空白字符
                text = text.Trim();
                if (text.Length > 100)
                {
                    text = text.Substring(0, 100) + "...";
                }
                return text.Replace("\r", " ").Replace("\n", " ");
            }
        }

        /// <summary>
        /// 获取估计内存大小
        /// </summary>
        public long EstimatedSize
        {
            get
            {
                return Content?.Length * 2 ?? 0; // UTF-16 characters
            }
        }
    }
}
