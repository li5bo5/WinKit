using System;
using System.Collections.Generic;
using System.Linq;
using WinKit.Common;
using WinKit.Todo.Models;

namespace WinKit.Todo.Services
{
    /// <summary>
    /// 待办回收站业务服务，支持保留天数自动清理与原 ID 恢复
    /// </summary>
    public class RecycleBinService
    {
        private readonly JsonLinesStorage<RecycleBinItem> _storage;
        private readonly SettingsManager _settingsManager;

        public RecycleBinService(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _storage = new JsonLinesStorage<RecycleBinItem>(AppPaths.RecycleBin);
        }

        public RecycleBinService(SettingsManager settingsManager, string filePath)
        {
            _settingsManager = settingsManager;
            _storage = new JsonLinesStorage<RecycleBinItem>(filePath);
        }

        /// <summary>
        /// 加载回收站条目，并自动清理超过配置天数的过期数据
        /// </summary>
        public List<RecycleBinItem> LoadItems()
        {
            var rawList = _storage.Load();
            int retentionDays = _settingsManager.Settings.RecycleBinRetentionDays;
            if (retentionDays <= 0) retentionDays = 60; // 兜底默认 60 天

            var expireThreshold = DateTime.Now.AddDays(-retentionDays);

            // 过滤过期条目
            var validItems = rawList.Where(item => item.DeletedAt >= expireThreshold).ToList();

            // 若有过期条目被剔除，触发保存清理
            if (validItems.Count < rawList.Count)
            {
                _storage.Save(validItems);
            }

            // 按删除时间倒序排列（最新的在最前）
            return validItems.OrderByDescending(i => i.DeletedAt).ToList();
        }

        /// <summary>
        /// 将待办事项移入回收站（完整保留原始 ID 与创建时间）
        /// </summary>
        public void AddToRecycleBin(TodoItem todo)
        {
            if (todo == null || string.IsNullOrWhiteSpace(todo.Title)) return;

            var currentItems = LoadItems();
            var recycleItem = new RecycleBinItem
            {
                Id = todo.Id,
                Title = todo.Title,
                CreatedAt = todo.CreatedAt,
                DeletedAt = DateTime.Now
            };

            currentItems.Insert(0, recycleItem);
            _storage.Save(currentItems);
        }

        /// <summary>
        /// 保存回收站条目
        /// </summary>
        public void SaveItems(IEnumerable<RecycleBinItem> items)
        {
            if (items == null) return;
            _storage.Save(items);
        }

        /// <summary>
        /// 清空回收站
        /// </summary>
        public void Clear()
        {
            _storage.Save(new List<RecycleBinItem>());
        }
    }
}
