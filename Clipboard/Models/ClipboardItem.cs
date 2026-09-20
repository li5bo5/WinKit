using System;
using System.ComponentModel;
using System.IO;

namespace WinKit.Clipboard.Models
{
    /// <summary>
    /// 剪贴板项目类型
    /// </summary>
    public enum ClipboardItemType
    {
        Text,
        Image
    }

    /// <summary>
    /// 剪贴板项目实体（支持文本与本地持久化图片、支持置顶）
    /// </summary>
    public class ClipboardItem : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ClipboardItemType Type { get; set; } = ClipboardItemType.Text;
        public string ContentType { get; set; } = "Text";
        public string? Content { get; set; }
        public string? SourceApp { get; set; }
        
        /// <summary>
        /// 原图本地相对/绝对路径
        /// </summary>
        public string? ImagePath { get; set; }

        /// <summary>
        /// 54px 高度缩略图本地相对/绝对路径
        /// </summary>
        public string? ThumbnailPath { get; set; }

        /// <summary>
        /// 原图 SHA256 哈希（用于防抖去重）
        /// </summary>
        public string? ImageHash { get; set; }

        /// <summary>
        /// 图片文件字节大小
        /// </summary>
        public long ImageSize { get; set; }

        /// <summary>
        /// 图片原始分辨率描述（如 "1920×1080"）
        /// </summary>
        public string? ImageResolution { get; set; }

        /// <summary>
        /// 创建时间戳
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

        public DateTime Timestamp
        {
            get => CreatedAt.LocalDateTime;
            set => CreatedAt = new DateTimeOffset(value);
        }

        private bool _isPinned = false;
        /// <summary>
        /// 是否置顶锁死
        /// </summary>
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    OnPropertyChanged(nameof(IsPinned));
                    OnPropertyChanged(nameof(PinIcon));
                }
            }
        }

        /// <summary>
        /// 置顶时间戳
        /// </summary>
        public DateTimeOffset? PinnedAt { get; set; }

        public string PinIcon => _isPinned ? "📍" : "📌";

        public string FormattedTime => CreatedAt.LocalDateTime.ToString("MM-dd HH:mm");

        public bool IsImage => Type == ClipboardItemType.Image;
        public bool IsText => Type == ClipboardItemType.Text;

        private string? _displayTextCached;
        /// <summary>
        /// 格式化显示文本（带只读缓存，优化长文本列表滚动性能）
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (_displayTextCached != null) return _displayTextCached;
                if (IsImage)
                {
                    string sizeStr = ImageSize > 1024 * 1024
                        ? $"{ImageSize / 1024.0 / 1024.0:F1} MB"
                        : $"{ImageSize / 1024.0:F0} KB";
                    _displayTextCached = string.IsNullOrEmpty(ImageResolution)
                        ? $"[图片] {sizeStr}"
                        : $"[图片] {ImageResolution} · {sizeStr}";
                }
                else
                {
                    var text = Content ?? string.Empty;
                    text = text.Trim();
                    if (text.Length > 100)
                    {
                        text = text.Substring(0, 100) + "...";
                    }
                    _displayTextCached = text.Replace("\r", " ").Replace("\n", " ");
                }
                return _displayTextCached;
            }
        }

        /// <summary>
        /// 获取估计内存/占用大小
        /// </summary>
        public long EstimatedSize
        {
            get
            {
                if (IsImage) return ImageSize;
                return Content?.Length * 2 ?? 0;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
