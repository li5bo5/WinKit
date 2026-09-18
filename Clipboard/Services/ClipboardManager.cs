using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WinKit.Clipboard.Models;
using WinKit.Common;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 剪贴板历史数据管理服务，基于 JSON Lines 明文存储，支持文本与图片双模态、上限折半清理与磁盘文件协同
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
                foreach (var item in loaded.OrderByDescending(i => i.IsPinned).ThenByDescending(i => i.PinnedAt ?? i.CreatedAt))
                {
                    _items.Add(item);
                }

                EnforceMaxCapacity();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClipboardManager: 数据加载失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 切换条目的置顶锁定状态；若超过最大置顶上限则返回 false
        /// </summary>
        public bool TogglePin(ClipboardItem item)
        {
            if (item == null) return false;

            if (!item.IsPinned)
            {
                int maxPinned = _settingsManager.Settings.ClipboardMaxPinnedItems;
                if (maxPinned < 1) maxPinned = 1;
                if (maxPinned > 20) maxPinned = 20;

                int currentPinned = _items.Count(i => i.IsPinned);
                if (currentPinned >= maxPinned)
                {
                    return false;
                }

                item.IsPinned = true;
                item.PinnedAt = DateTimeOffset.Now;
            }
            else
            {
                item.IsPinned = false;
                item.PinnedAt = null;
            }

            ReorderItems();
            _storage.QueueSave(_items, 500);
            return true;
        }

        private void ReorderItems()
        {
            var sorted = _items
                .OrderByDescending(i => i.IsPinned)
                .ThenByDescending(i => i.PinnedAt ?? i.CreatedAt)
                .ToList();

            for (int targetIdx = 0; targetIdx < sorted.Count; targetIdx++)
            {
                var targetItem = sorted[targetIdx];
                int currentIdx = _items.IndexOf(targetItem);
                if (currentIdx != targetIdx && currentIdx >= 0)
                {
                    _items.Move(currentIdx, targetIdx);
                }
            }
        }

        /// <summary>
        /// 添加任意剪贴板条目（文本或图片），支持精准去重、置顶避让与折半容量控制
        /// </summary>
        public void AddItem(ClipboardItem newItem)
        {
            if (newItem == null) return;

            var settings = _settingsManager.Settings;

            if (newItem.IsText && !string.IsNullOrEmpty(newItem.Content))
            {
                string text = newItem.Content.Trim();
                if (settings.PasteEnableTextDeduplication)
                {
                    var duplicates = _items.Where(i => i.IsText && i.Content == text).ToList();
                    foreach (var dup in duplicates)
                    {
                        // 若原已有项处于置顶状态，新条目继承置顶标记
                        if (dup.IsPinned)
                        {
                            newItem.IsPinned = true;
                            newItem.PinnedAt = dup.PinnedAt;
                        }
                        _items.Remove(dup);
                    }
                }
                else
                {
                    if (_items.Count > 0 && _items[0].IsText && _items[0].Content == text)
                        return;
                }
            }
            else if (newItem.IsImage && !string.IsNullOrEmpty(newItem.ImageHash))
            {
                var existing = _items.FirstOrDefault(i => i.IsImage && i.ImageHash == newItem.ImageHash);
                if (existing != null)
                {
                    if (existing.IsPinned)
                    {
                        newItem.IsPinned = true;
                        newItem.PinnedAt = existing.PinnedAt;
                    }
                    _items.Remove(existing);
                    ImageProcessingService.SafeDeleteFiles(existing);
                }
            }

            // 新条目插入在所有置顶条目下方，置顶项永远稳居前排
            int insertIndex = newItem.IsPinned ? 0 : _items.Count(i => i.IsPinned);
            _items.Insert(insertIndex, newItem);

            EnforceMaxCapacity();
            _storage.QueueSave(_items, 500);
        }

        public void AddTextItem(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            AddItem(new ClipboardItem
            {
                Id = Guid.NewGuid(),
                Type = ClipboardItemType.Text,
                ContentType = "Text",
                Content = text.Trim(),
                CreatedAt = DateTimeOffset.Now
            });
        }

        /// <summary>
        /// 容量控制：超过上限 N 时，一次性截断清理至 N / 2 条，置顶条目永久豁免清理
        /// </summary>
        private void EnforceMaxCapacity()
        {
            int maxItems = _settingsManager.Settings.PasteMaxItems;
            if (maxItems < 100) maxItems = 100;
            if (maxItems > 500) maxItems = 500;

            var unpinnedItems = _items.Where(i => !i.IsPinned).ToList();
            if (unpinnedItems.Count > maxItems)
            {
                int targetCount = Math.Max(50, maxItems / 2);
                int toRemoveCount = unpinnedItems.Count - targetCount;
                for (int i = _items.Count - 1; i >= 0 && toRemoveCount > 0; i--)
                {
                    if (!_items[i].IsPinned)
                    {
                        var evicted = _items[i];
                        _items.RemoveAt(i);
                        toRemoveCount--;
                        if (evicted.IsImage)
                        {
                            ImageProcessingService.SafeDeleteFiles(evicted);
                        }
                    }
                }
            }
        }

        public void MoveToTop(ClipboardItem item)
        {
            if (item == null) return;
            var index = _items.IndexOf(item);
            if (index > 0)
            {
                _items.RemoveAt(index);
                if (item.IsPinned)
                {
                    item.PinnedAt = DateTimeOffset.Now;
                    _items.Insert(0, item);
                }
                else
                {
                    item.CreatedAt = DateTimeOffset.Now;
                    int insertIdx = _items.Count(i => i.IsPinned);
                    _items.Insert(insertIdx, item);
                }
                _storage.QueueSave(_items, 500);
            }
        }

        public void RemoveItem(Guid id)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                _items.Remove(item);
                if (item.IsImage)
                {
                    ImageProcessingService.SafeDeleteFiles(item);
                }
                _storage.QueueSave(_items, 500);
            }
        }

        public void RemoveItems(IEnumerable<ClipboardItem> items)
        {
            if (items == null) return;
            bool changed = false;
            foreach (var item in items)
            {
                if (_items.Remove(item))
                {
                    changed = true;
                    if (item.IsImage)
                    {
                        ImageProcessingService.SafeDeleteFiles(item);
                    }
                }
            }
            if (changed)
            {
                _storage.QueueSave(_items, 500);
            }
        }

        /// <summary>
        /// 当前是否存在置顶条目
        /// </summary>
        public bool HasPinnedItems => _items.Any(i => i.IsPinned);

        /// <summary>
        /// 一键取消所有置顶条目
        /// </summary>
        public void UnpinAll()
        {
            bool changed = false;
            foreach (var item in _items)
            {
                if (item.IsPinned)
                {
                    item.IsPinned = false;
                    item.PinnedAt = null;
                    changed = true;
                }
            }

            if (changed)
            {
                ReorderItems();
                _storage.QueueSave(_items, 500);
            }
        }

        public void ClearAll()
        {
            // 清理所有非置顶条目，置顶条目受保护不被误清空
            var toRemove = _items.Where(i => !i.IsPinned).ToList();
            foreach (var item in toRemove)
            {
                _items.Remove(item);
                if (item.IsImage)
                {
                    ImageProcessingService.SafeDeleteFiles(item);
                }
            }
            _storage.QueueSave(_items, 200);
        }

        public void Dispose()
        {
            try
            {
                _storage.FlushAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // 静默保底
            }
        }
    }
}
