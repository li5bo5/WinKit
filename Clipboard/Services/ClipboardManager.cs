using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WinKit.Clipboard.Models;
using WinKit.Common;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 剪贴板历史数据管理服务，基于 JSON Lines 明文存储，支持上限折半清理
    /// </summary>
    public class ClipboardManager : IDisposable
    {
        private readonly JsonLinesStorage<ClipboardItem> _storage;
        private readonly SettingsManager _settingsManager;
        private readonly ObservableCollection<ClipboardItem> _items = new();

        public ObservableCollection<ClipboardItem> Items => _items;
        public int TotalCount => _items.Count;

        public ClipboardManager(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _storage = new JsonLinesStorage<ClipboardItem>(AppPaths.Clipboard);

            _settingsManager.SettingsChanged += OnSettingsChanged;
            LoadInitialData();
        }

        public ClipboardManager(SettingsManager settingsManager, string filePath)
        {
            _settingsManager = settingsManager;
            _storage = new JsonLinesStorage<ClipboardItem>(filePath);

            _settingsManager.SettingsChanged += OnSettingsChanged;
            LoadInitialData();
        }

        private void OnSettingsChanged(object? sender, AppSettings newSettings)
        {
            int beforeCount = _items.Count;
            EnforceMaxCapacity();
            if (_items.Count != beforeCount)
            {
                _storage.Save(_items);
            }
        }

        private void LoadInitialData()
        {
            try
            {
                var loaded = _storage.Load();
                _items.Clear();
                foreach (var item in loaded.OrderByDescending(i => i.Timestamp))
                {
                    _items.Add(item);
                }

                // 启动时若超过上限，执行一次折半清理
                EnforceMaxCapacity();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClipboardManager: 数据加载失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 添加文本条目（支持去重与容量超限折半清理）
        /// </summary>
        public void AddTextItem(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            text = text.Trim();
            if (text.Length == 0) return;

            var settings = _settingsManager.Settings;

            // 1. 去重逻辑
            if (settings.PasteEnableTextDeduplication)
            {
                var existingItem = _items.FirstOrDefault(item => item.Type == ClipboardItemType.Text && item.Content == text);
                if (existingItem != null)
                {
                    existingItem.Timestamp = DateTime.Now;
                    if (_items.IndexOf(existingItem) != 0)
                    {
                        _items.Remove(existingItem);
                        _items.Insert(0, existingItem);
                    }
                    _storage.Save(_items);
                    return;
                }
            }
            else
            {
                // 简单去重：如果与最新项完全相同则忽略
                if (_items.Count > 0 && _items[0].Type == ClipboardItemType.Text && _items[0].Content == text)
                    return;
            }

            // 2. 插入新项到顶部
            var newItem = new ClipboardItem
            {
                Type = ClipboardItemType.Text,
                ContentType = "Text",
                Content = text,
                Timestamp = DateTime.Now
            };

            _items.Insert(0, newItem);

            // 3. 检查容量上限，超限时折半清理
            EnforceMaxCapacity();

            // 4. 持久化保存
            _storage.Save(_items);
        }

        /// <summary>
        /// 容量控制：超过上限 N 时，一次性截断清理至 N / 2 条
        /// </summary>
        private void EnforceMaxCapacity()
        {
            int maxItems = _settingsManager.Settings.PasteMaxItems;
            if (maxItems < 100) maxItems = 100;
            if (maxItems > 500) maxItems = 500;

            if (_items.Count > maxItems)
            {
                int targetCount = Math.Max(50, maxItems / 2);
                while (_items.Count > targetCount)
                {
                    _items.RemoveAt(_items.Count - 1);
                }
            }
        }

        /// <summary>
        /// 将指定条目移动到列表顶部
        /// </summary>
        public void MoveToTop(ClipboardItem item)
        {
            if (item == null) return;
            var index = _items.IndexOf(item);
            if (index > 0)
            {
                _items.RemoveAt(index);
                item.Timestamp = DateTime.Now;
                _items.Insert(0, item);
                _storage.Save(_items);
            }
        }

        /// <summary>
        /// 删除指定条目
        /// </summary>
        public void RemoveItem(Guid id)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                _items.Remove(item);
                _storage.Save(_items);
            }
        }

        /// <summary>
        /// 清空全部剪贴板历史
        /// </summary>
        public void ClearAll()
        {
            _items.Clear();
            _storage.Save(_items);
        }

        public void Dispose()
        {
            // 纯托管数据，无非托管资源需要释放
        }
    }
}
