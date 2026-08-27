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
                foreach (var item in loaded.OrderByDescending(i => i.CreatedAt))
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
        /// 添加任意剪贴板条目（文本或图片），支持精准去重与折半容量控制
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
                    // 命中已有图片去重：彻底删除旧项数据及其磁盘文件，采用新捕获的全新图片项
                    _items.Remove(existing);
                    ImageProcessingService.SafeDeleteFiles(existing);
                }
            }

            _items.Insert(0, newItem);
            EnforceMaxCapacity();
            _storage.Save(_items);
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
        /// 容量控制：超过上限 N 时，一次性截断清理至 N / 2 条，并同步清理淘汰条目的图片文件
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
                    var evicted = _items[_items.Count - 1];
                    _items.RemoveAt(_items.Count - 1);
                    if (evicted.IsImage)
                    {
                        ImageProcessingService.SafeDeleteFiles(evicted);
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
                item.CreatedAt = DateTimeOffset.Now;
                _items.Insert(0, item);
                _storage.Save(_items);
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
                _storage.Save(_items);
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
                _storage.Save(_items);
            }
        }

        public void ClearAll()
        {
            foreach (var item in _items.Where(i => i.IsImage))
            {
                ImageProcessingService.SafeDeleteFiles(item);
            }
            _items.Clear();
            _storage.Save(_items);
        }

        public void Dispose()
        {
            _storage.Flush();
        }
    }
}
