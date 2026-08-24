using System;

namespace WinKit.Todo.Models
{
    /// <summary>
    /// 待办回收站数据实体
    /// </summary>
    public class RecycleBinItem
    {
        /// <summary>原始待办的唯一标识（恢复时保持不变）</summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>待办文本内容</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>待办最初创建时间</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>移入回收站时间</summary>
        public DateTime DeletedAt { get; set; } = DateTime.Now;

        /// <summary>格式化展示的删除时间</summary>
        public string FormattedTime => DeletedAt.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>别名兼容：格式化删除时间</summary>
        public string FormattedDeletedAt => FormattedTime;
    }
}
