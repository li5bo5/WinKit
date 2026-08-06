using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WinKit.Common;
using WinKit.Todo.Models;

namespace WinKit.Todo.Services
{
    public class HistoryStorage
    {
        private readonly string _filePath;

        public HistoryStorage()
        {
            AppPaths.EnsureDirectories();
            _filePath = AppPaths.History;

            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, string.Empty, Encoding.UTF8);
            }
        }

        public List<HistoryItem> LoadHistory()
        {
            var list = new List<HistoryItem>();
            if (!File.Exists(_filePath)) return list;

            var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // 格式：- [2026-08-06 12:00:00] Title
                if (trimmed.StartsWith("- ["))
                {
                    int closeBracketIndex = trimmed.IndexOf("] ");
                    if (closeBracketIndex > 3)
                    {
                        string timeStr = trimmed.Substring(3, closeBracketIndex - 3);
                        string titleStr = trimmed.Substring(closeBracketIndex + 2);
                        titleStr = titleStr.Replace("\\n", "\n").Replace("\\r", "\r");

                        if (DateTime.TryParse(timeStr, out DateTime deletedAt))
                        {
                            list.Add(new HistoryItem
                            {
                                Title = titleStr,
                                DeletedAt = deletedAt
                            });
                            continue;
                        }
                    }
                }

                // 兼容没有时间戳的普通行
                var title = trimmed.StartsWith("- ") ? trimmed.Substring(2) : trimmed;
                title = title.Replace("\\n", "\n").Replace("\\r", "\r");
                list.Add(new HistoryItem
                {
                    Title = title,
                    DeletedAt = DateTime.Now
                });
            }

            // 最新的在前
            list.Reverse();
            return list;
        }

        public void AddHistory(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return;

            var escaped = title.Replace("\r", "\\r").Replace("\n", "\\n");
            string line = $"- [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {escaped}{Environment.NewLine}";
            File.AppendAllText(_filePath, line, Encoding.UTF8);
        }

        public void SaveHistory(IEnumerable<HistoryItem> items)
        {
            var sb = new StringBuilder();
            // 文件保存时按时间顺序正序保存
            var list = new List<HistoryItem>(items);
            list.Reverse();

            foreach (var item in list)
            {
                var escaped = item.Title.Replace("\r", "\\r").Replace("\n", "\\n");
                sb.AppendLine($"- [{item.DeletedAt:yyyy-MM-dd HH:mm:ss}] {escaped}");
            }
            File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ClearHistory()
        {
            File.WriteAllText(_filePath, string.Empty, Encoding.UTF8);
        }
    }
}
