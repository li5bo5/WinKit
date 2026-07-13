using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WinKit.Common;
using WinKit.Todo.Models;

namespace WinKit.Todo.Services
{
    public class MarkdownStorage
    {
        private readonly string _filePath;

        public MarkdownStorage()
        {
            AppPaths.EnsureDirectories();
            _filePath = AppPaths.Todos;

            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, string.Empty, Encoding.UTF8);
            }
        }

        public List<TodoItem> LoadTodos()
        {
            var todos = new List<TodoItem>();
            if (!File.Exists(_filePath)) return todos;

            var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                // 格式："- Title" (markdown 列表项)
                var title = trimmed.StartsWith("- ") ? trimmed.Substring(2) : trimmed;
                todos.Add(new TodoItem { Title = title });
            }
            return todos;
        }

        public void SaveTodos(IEnumerable<TodoItem> items)
        {
            var sb = new StringBuilder();
            foreach (var item in items)
            {
                sb.AppendLine($"- {item.Title}");
            }
            File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
