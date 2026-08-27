using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;

namespace WinKit.Common
{
    /// <summary>
    /// 通用 JSON Lines (.jsonl) 存储引擎，支持明文逐行容错读写、原子替换与异步后台防抖刷盘
    /// </summary>
    /// <typeparam name="T">存储的数据实体类型</typeparam>
    public class JsonLinesStorage<T> : IDisposable where T : class
    {
        private readonly string _filePath;
        private readonly string _tmpFilePath;
        private readonly object _fileLock = new();

        // 异步队列与防抖支持
        private readonly object _queueLock = new();
        private List<T>? _pendingItems;
        private CancellationTokenSource? _debounceCts;
        private Task _currentWriteTask = Task.CompletedTask;
        private bool _isDisposed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = false
        };

        /// <summary>
        /// 初始化 JSON Lines 存储引擎
        /// </summary>
        /// <param name="filePath">目标 .jsonl 文件绝对路径</param>
        public JsonLinesStorage(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            _filePath = filePath;
            _tmpFilePath = filePath + ".tmp";

            AppPaths.EnsureDirectories();
        }

        /// <summary>
        /// 从文件加载数据列表；采用逐行容错机制，损坏行自动跳过，不影响有效行
        /// </summary>
        /// <returns>反序列化后的实体列表</returns>
        public List<T> Load()
        {
            lock (_fileLock)
            {
                if (!File.Exists(_filePath))
                {
                    try
                    {
                        File.WriteAllText(_filePath, string.Empty, Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 创建空白文件 {_filePath} 失败 ({ex.Message})");
                    }
                    return new List<T>();
                }

                try
                {
                    return ReadLinesFromFile(_filePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 读取主文件 {_filePath} 异常 ({ex.Message})");
                    return new List<T>();
                }
            }
        }

        /// <summary>
        /// 异步逐行容错加载数据列表
        /// </summary>
        public async Task<List<T>> LoadAsync()
        {
            if (!File.Exists(_filePath))
            {
                try
                {
                    await File.WriteAllTextAsync(_filePath, string.Empty, Encoding.UTF8).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 创建空白文件 {_filePath} 失败 ({ex.Message})");
                }
                return new List<T>();
            }

            var list = new List<T>();
            try
            {
                using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    try
                    {
                        var item = JsonSerializer.Deserialize<T>(trimmed, JsonOptions);
                        if (item != null)
                        {
                            list.Add(item);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 跳过损坏行 ({jsonEx.Message}) -> {trimmed}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 异步读取 {_filePath} 失败 ({ex.Message})");
            }

            return list;
        }

        /// <summary>
        /// 同步原子持久化保存数据（写入 .tmp 临时文件 + 原子替换覆盖）
        /// </summary>
        /// <param name="items">待保存的数据列表</param>
        public void Save(IEnumerable<T> items)
        {
            if (items == null) return;
            var snapshot = items.Where(i => i != null).ToList();

            lock (_fileLock)
            {
                try
                {
                    var sb = new StringBuilder();
                    foreach (var item in snapshot)
                    {
                        var line = JsonSerializer.Serialize(item, JsonOptions);
                        sb.AppendLine(line);
                    }

                    // 1. 写入同目录下的 .tmp 临时文件
                    File.WriteAllText(_tmpFilePath, sb.ToString(), Encoding.UTF8);

                    // 2. 原子替换覆盖主文件
                    File.Move(_tmpFilePath, _filePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 同步原子写入 {_filePath} 失败 ({ex.Message})");
                    throw;
                }
                finally
                {
                    // 3. 清理残留临时文件
                    try
                    {
                        if (File.Exists(_tmpFilePath))
                        {
                            File.Delete(_tmpFilePath);
                        }
                    }
                    catch
                    {
                        // 忽略临时文件清理异常
                    }
                }
            }
        }

        /// <summary>
        /// 原生异步原子持久化保存数据
        /// </summary>
        public async Task SaveAsync(IEnumerable<T> items)
        {
            if (items == null) return;
            var snapshot = items.Where(i => i != null).ToList();

            var sb = new StringBuilder();
            foreach (var item in snapshot)
            {
                var line = JsonSerializer.Serialize(item, JsonOptions);
                sb.AppendLine(line);
            }

            try
            {
                await File.WriteAllTextAsync(_tmpFilePath, sb.ToString(), Encoding.UTF8).ConfigureAwait(false);

                lock (_fileLock)
                {
                    File.Move(_tmpFilePath, _filePath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 异步原子写入 {_filePath} 失败 ({ex.Message})");
                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(_tmpFilePath))
                    {
                        File.Delete(_tmpFilePath);
                    }
                }
                catch
                {
                    // 忽略清理异常
                }
            }
        }

        /// <summary>
        /// 队列防抖保存（默认 300ms 防抖合并，适合高频连续修改）
        /// </summary>
        public void QueueSave(IEnumerable<T> items, int debounceMs = 300)
        {
            if (_isDisposed || items == null) return;

            var snapshot = items.Where(i => i != null).ToList();

            lock (_queueLock)
            {
                _pendingItems = snapshot;
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                _currentWriteTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(debounceMs, token).ConfigureAwait(false);
                        List<T>? toWrite;
                        lock (_queueLock)
                        {
                            toWrite = _pendingItems;
                            _pendingItems = null;
                        }

                        if (toWrite != null && !token.IsCancellationRequested)
                        {
                            await SaveAsync(toWrite).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常防抖取消
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 防抖队列写入失败 ({ex.Message})");
                    }
                }, token);
            }
        }

        /// <summary>
        /// 强制立即刷盘（退出或关键操作时调用）
        /// </summary>
        public async Task FlushAsync()
        {
            List<T>? toWrite = null;
            lock (_queueLock)
            {
                _debounceCts?.Cancel();
                toWrite = _pendingItems;
                _pendingItems = null;
            }

            if (toWrite != null)
            {
                await SaveAsync(toWrite).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await _currentWriteTask.ConfigureAwait(false);
                }
                catch
                {
                    // 忽略任务异常
                }
            }
        }

        /// <summary>
        /// 同步立即刷盘
        /// </summary>
        public void Flush()
        {
            List<T>? toWrite = null;
            lock (_queueLock)
            {
                _debounceCts?.Cancel();
                toWrite = _pendingItems;
                _pendingItems = null;
            }

            if (toWrite != null)
            {
                Save(toWrite);
            }
        }

        /// <summary>
        /// 内部辅助：从指定文件逐行反序列化 JSON 实体，自动跳过损坏行
        /// </summary>
        private static List<T> ReadLinesFromFile(string path)
        {
            var list = new List<T>();
            var lines = File.ReadAllLines(path, Encoding.UTF8);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                try
                {
                    var item = JsonSerializer.Deserialize<T>(trimmed, JsonOptions);
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 跳过损坏行 ({ex.Message}) -> {trimmed}");
                }
            }

            return list;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                Flush();
            }
            catch
            {
                // 忽略释放异常
            }

            _debounceCts?.Dispose();
        }
    }
}
