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

        private static bool _faultNotified = false;

        /// <summary>
        /// 从存储文件加载全部待办事项
        /// </summary>
        public List<TodoItem> LoadTodos()
        {
            var list = _storage.Load();
            if (_storage.HasReadFault && !_faultNotified)
            {
                _faultNotified = true;
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Windows.MessageBox.Show(
                        "待办数据文件读取遇到异常（可能被占用），已开启保护模式：本次会话不会自动保存覆盖，请检查文件占用并适时重启软件。",
                        "WinKit 数据保护",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }));
            }
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
