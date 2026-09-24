using System;
using System.Collections.Generic;
using System.Linq;
using WinKit.Common;
using WinKit.Todo.Models;

namespace WinKit.Todo.Services
{
    /// <summary>
    /// 待办事项业务服务，基于 JsonLinesStorage 明文存储引擎
    /// </summary>
    public class TodoService
    {
        private readonly JsonLinesStorage<TodoItem> _storage;

        public TodoService()
        {
            _storage = new JsonLinesStorage<TodoItem>(AppPaths.Todos);
        }

        public TodoService(string filePath)
        {
            _storage = new JsonLinesStorage<TodoItem>(filePath);
        }

        /// <summary>
        /// 从存储文件加载全部待办事项
        /// </summary>
        public List<TodoItem> LoadTodos()
        {
            var list = _storage.Load();
            // 若包含序号则按序号排序，否则保持原有顺序
            return list.OrderBy(t => t.Order).ToList();
        }

        /// <summary>
        /// 持久化保存待办事项列表（采用 300ms 后台防抖异步写入，彻底避免 UI 线程磁盘阻塞）
        /// </summary>
        public void SaveTodos(IEnumerable<TodoItem> items)
        {
            if (items == null) return;
            int order = 0;
            foreach (var item in items)
            {
                item.Order = order++;
            }
            _storage.QueueSave(items, 300);
        }

        /// <summary>
        /// 强制立即刷盘（退出或关键操作时调用）
        /// </summary>
        public void Flush()
        {
            _storage.Flush();
        }
    }
}
