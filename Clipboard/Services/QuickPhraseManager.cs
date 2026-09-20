using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using WinKit.Clipboard.Models;
using WinKit.Common;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 常用短语管理服务，基于 phrases.jsonl 明文存储与内存缓存
    /// </summary>
    public class QuickPhraseManager
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = false
        };

        private readonly JsonLinesStorage<QuickPhraseItem> _storage;
        private readonly ObservableCollection<QuickPhraseItem> _items = new();
        private List<QuickPhraseItem> _lastValidCache = new();
        private bool _hasFormatError = false;

        public ObservableCollection<QuickPhraseItem> Items => _items;
        public bool HasFormatError => _hasFormatError;

        public QuickPhraseManager()
        {
            AppPaths.EnsureDirectories();
            _storage = new JsonLinesStorage<QuickPhraseItem>(AppPaths.Phrases);

            if (!File.Exists(AppPaths.Phrases))
            {
                ResetToDefaultPhrases();
            }

            Reload();
        }

        /// <summary>
        /// 从文件重新加载短语，支持格式损坏容错并保留最近一次有效缓存
        /// </summary>
        public void Reload()
        {
            try
            {
                _hasFormatError = false;
                if (!File.Exists(AppPaths.Phrases))
                {
                    ResetToDefaultPhrases();
                }

                var loaded = _storage.Load();
                if (loaded.Count > 0)
                {
                    _lastValidCache = loaded;
                    _items.Clear();
                    foreach (var item in loaded)
                    {
                        _items.Add(item);
                    }
                }
                else
                {
                    // 若文件内容为空但不是首次，检查是否有格式错误
                    var raw = File.ReadAllText(AppPaths.Phrases, Encoding.UTF8).Trim();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        _hasFormatError = true;
                        // 使用内存有效缓存
                        if (_lastValidCache.Count > 0 && _items.Count == 0)
                        {
                            foreach (var item in _lastValidCache) _items.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"QuickPhraseManager: 加载短语文件失败 ({ex.Message})");
                _hasFormatError = true;
                if (_lastValidCache.Count > 0 && _items.Count == 0)
                {
                    foreach (var item in _lastValidCache) _items.Add(item);
                }
            }
        }

        /// <summary>
        /// 重置为初始默认常用短语
        /// </summary>
        public static void ResetToDefaultPhrases()
        {
            AppPaths.EnsureDirectories();
            var defaults = new List<QuickPhraseItem>
            {
                new() { Content = "收到，我会尽快处理。" },
                new() { Content = "您好，我现在正在处理其他事项，稍后回复您。" },
                new() { Content = "感谢您的反馈与支持。" }
            };

            var storage = new JsonLinesStorage<QuickPhraseItem>(AppPaths.Phrases);
            storage.Save(defaults);
        }
    }
}
