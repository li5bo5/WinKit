using System;

namespace WinKit.Todo.Models
{
    public class HistoryItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; } = DateTime.Now;

        public string FormattedTime => DeletedAt.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
