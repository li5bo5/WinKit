using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WinKit.Common;
using WinKit.Clipboard.Models;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 剪切板数据管理器 - 使用 SQLite 数据库存储并使用 Deflate 压缩文本
    /// </summary>
    public class ClipboardManager : IDisposable
    {
        private readonly string _dbPath;
        private readonly ObservableCollection<ClipboardItem> _items;
        private readonly SettingsManager _settingsManager;
        private bool _isFullyLoaded = false;
        private int _insertsSinceCleanup = 0;

        private const int InitialLoadCount = 200;
        private const int BatchLoadCount = 500;
        private const int CleanupThrottle = 10; // 每 N 次插入执行一次清理

        private static readonly SHA256 Sha256 = SHA256.Create();

        public ReadOnlyObservableCollection<ClipboardItem> Items { get; }

        public event NotifyCollectionChangedEventHandler? ItemsChanged;

        public ClipboardManager(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;

            AppPaths.EnsureDirectories();
            _dbPath = AppPaths.Database;

            _items = new ObservableCollection<ClipboardItem>();
            _items.CollectionChanged += (s, e) => ItemsChanged?.Invoke(s, e);
            Items = new ReadOnlyObservableCollection<ClipboardItem>(_items);

            InitializeDatabase();
            LoadInitialData();

            // 后台分批加载剩余历史数据，避免阻塞 UI
            _ = LoadRemainingDataAsync();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS _meta (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS clipboard_items (
                    id TEXT PRIMARY KEY,
                    content BLOB NOT NULL,
                    timestamp TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_timestamp ON clipboard_items(timestamp DESC);
            ";
            cmd.ExecuteNonQuery();
        }

        public void AddTextItem(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            text = text.Trim();
            if (text.Length == 0) return;

            var settings = _settingsManager.Settings;

            // 哈希去重逻辑
            if (settings.PasteEnableTextDeduplication)
            {
                var hash = ComputeTextHash(text);
                var existingItem = _items.FirstOrDefault(item => item.Type == ClipboardItemType.Text && item.ContentHash == hash);
                
                if (existingItem != null)
                {
                    _items.Remove(existingItem);
                    var newItem = new ClipboardItem
                    {
                        Type = ClipboardItemType.Text,
                        Content = text,
                        ContentHash = hash,
                        Timestamp = DateTime.Now
                    };
                    ReplaceItemInDb(existingItem.Id, newItem);
                    _items.Insert(0, newItem);
                    
                    if (++_insertsSinceCleanup >= CleanupThrottle)
                    {
                        CleanupOldData();
                        _insertsSinceCleanup = 0;
                    }
                    return;
                }
            }
            else
            {
                // 简单去重：检查是否与最新项相同
                if (_items.Count > 0 && _items[0].Type == ClipboardItemType.Text && _items[0].Content == text)
                    return;
            }

            var item = new ClipboardItem
            {
                Type = ClipboardItemType.Text,
                Content = text,
                ContentHash = ComputeTextHash(text),
                Timestamp = DateTime.Now
            };

            InsertItemToDb(item);
            _items.Insert(0, item);

            if (++_insertsSinceCleanup >= CleanupThrottle)
            {
                CleanupOldData();
                _insertsSinceCleanup = 0;
            }
        }

        private static string ComputeTextHash(string text)
        {
            var bytes = Sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
            return Convert.ToBase64String(bytes);
        }

        private void InsertItemToDb(ClipboardItem item)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var compressed = CompressContent(item.Content ?? string.Empty);
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO clipboard_items (id, content, timestamp) VALUES (@id, @content, @timestamp)";
            command.Parameters.AddWithValue("@id", item.Id.ToString());
            command.Parameters.AddWithValue("@content", compressed);
            command.Parameters.AddWithValue("@timestamp", item.Timestamp.ToString("O"));
            command.ExecuteNonQuery();
        }

        public void RemoveItem(Guid id)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                _items.Remove(item);
                DeleteItemFromDb(id);
            }
        }

        private void DeleteItemFromDb(Guid id)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM clipboard_items WHERE id = @id";
            command.Parameters.AddWithValue("@id", id.ToString());
            command.ExecuteNonQuery();
        }

        private void ReplaceItemInDb(Guid oldId, ClipboardItem newItem)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM clipboard_items WHERE id = @id";
                deleteCmd.Parameters.AddWithValue("@id", oldId.ToString());
                deleteCmd.ExecuteNonQuery();

                var compressed = CompressContent(newItem.Content ?? string.Empty);
                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = "INSERT INTO clipboard_items (id, content, timestamp) VALUES (@id, @content, @timestamp)";
                insertCmd.Parameters.AddWithValue("@id", newItem.Id.ToString());
                insertCmd.Parameters.AddWithValue("@content", compressed);
                insertCmd.Parameters.AddWithValue("@timestamp", newItem.Timestamp.ToString("O"));
                insertCmd.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void ClearAll()
        {
            _items.Clear();
            ClearAllFromDb();
        }

        private void ClearAllFromDb()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM clipboard_items";
            command.ExecuteNonQuery();
        }

        public IEnumerable<ClipboardItem> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return _items;

            query = query.ToLowerInvariant();
            return _items.Where(item => item.Type == ClipboardItemType.Text && item.Content?.ToLowerInvariant().Contains(query) == true);
        }

        public void CleanupOldData()
        {
            var settings = _settingsManager.Settings;
            int maxItems = settings.PasteMaxItems;

            var itemsToDelete = new List<ClipboardItem>();

            // 限制条目数
            while (_items.Count > maxItems)
            {
                var oldest = _items.LastOrDefault();
                if (oldest != null)
                {
                    _items.Remove(oldest);
                    itemsToDelete.Add(oldest);
                }
                else break;
            }

            if (itemsToDelete.Count > 0)
            {
                DeleteItemsBatchFromDb(itemsToDelete);
            }
        }

        private void DeleteItemsBatchFromDb(List<ClipboardItem> items)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var item in items)
                {
                    var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM clipboard_items WHERE id = @id";
                    command.Parameters.AddWithValue("@id", item.Id.ToString());
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private void LoadInitialData()
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = $"SELECT id, content, timestamp FROM clipboard_items ORDER BY timestamp DESC LIMIT {InitialLoadCount}";

                using var reader = command.ExecuteReader();
                var loadedList = new List<ClipboardItem>();

                while (reader.Read())
                {
                    var id = Guid.Parse(reader.GetString(0));
                    var content = DecompressContent((byte[])reader["content"]);
                    var timestamp = DateTime.Parse(reader.GetString(2));

                    loadedList.Add(new ClipboardItem
                    {
                        Id = id,
                        Type = ClipboardItemType.Text,
                        Content = content,
                        ContentHash = ComputeTextHash(content),
                        Timestamp = timestamp
                    });
                }

                _items.Clear();
                foreach (var item in loadedList.OrderByDescending(i => i.Timestamp))
                {
                    _items.Add(item);
                }

                if (loadedList.Count < InitialLoadCount)
                {
                    _isFullyLoaded = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClipboardManager: 初始数据加载失败 - {ex.Message}");
            }
        }

        private async Task LoadRemainingDataAsync()
        {
            if (_isFullyLoaded) return;

            try
            {
                int offset = InitialLoadCount;
                int totalLoaded = 0;

                while (true)
                {
                    var batch = await Task.Run(() =>
                    {
                        var batchItems = new List<(Guid id, string content, DateTime timestamp)>();
                        using var connection = new SqliteConnection($"Data Source={_dbPath}");
                        connection.Open();

                        var command = connection.CreateCommand();
                        command.CommandText = $"SELECT id, content, timestamp FROM clipboard_items ORDER BY timestamp DESC LIMIT {BatchLoadCount} OFFSET {offset}";

                        using var reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            var id = Guid.Parse(reader.GetString(0));
                            var content = DecompressContent((byte[])reader["content"]);
                            var timestamp = DateTime.Parse(reader.GetString(2));
                            batchItems.Add((id, content, timestamp));
                        }
                        return batchItems;
                    });

                    if (batch.Count == 0) break;

                    foreach (var (id, content, timestamp) in batch)
                    {
                        _items.Add(new ClipboardItem
                        {
                            Id = id,
                            Type = ClipboardItemType.Text,
                            Content = content,
                            ContentHash = ComputeTextHash(content),
                            Timestamp = timestamp
                        });
                    }

                    totalLoaded += batch.Count;
                    offset += BatchLoadCount;

                    if (batch.Count < BatchLoadCount) break;
                }

                _isFullyLoaded = true;
                System.Diagnostics.Debug.WriteLine($"ClipboardManager: 历史记录后台读取完成，共加载 {totalLoaded} 项");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClipboardManager: 历史记录后台读取失败 - {ex.Message}");
            }
        }

        private static byte[] CompressContent(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            using var outputStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(outputStream, CompressionLevel.Fastest))
            {
                deflateStream.Write(bytes, 0, bytes.Length);
            }
            return outputStream.ToArray();
        }

        private static string DecompressContent(byte[] compressed)
        {
            using var inputStream = new MemoryStream(compressed);
            using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();
            deflateStream.CopyTo(outputStream);
            return Encoding.UTF8.GetString(outputStream.ToArray());
        }

        public void Dispose()
        {
        }
    }
}
