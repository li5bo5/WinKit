using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace WinKit.Common
{
    /// <summary>
    /// 通用 JSON Lines (.jsonl) 存储引擎，支持明文读写、原子写入与损坏自愈备份
    /// </summary>
    /// <typeparam name="T">存储的数据实体类型</typeparam>
    public class JsonLinesStorage<T> where T : class
    {
        private readonly string _filePath;
        private readonly string _bakFilePath;
        private readonly string _tmpFilePath;
        private readonly object _fileLock = new();

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
            _bakFilePath = filePath + ".bak";
            _tmpFilePath = filePath + ".tmp";

            AppPaths.EnsureDirectories();
        }

        /// <summary>
        /// 从文件加载数据列表；若主文件损坏或解析失败，自动回退并从 .bak 备份恢复
        /// </summary>
        /// <returns>反序列化后的实体列表</returns>
        public List<T> Load()
        {
            lock (_fileLock)
            {
                // 1. 若主文件存在，尝试逐行加载
                if (File.Exists(_filePath))
                {
                    try
                    {
                        var items = ReadLinesFromFile(_filePath);
                        return items;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 读取主文件 {_filePath} 失败 ({ex.Message})，尝试从备份恢复");
                    }
                }

                // 2. 主文件不存在或已损坏时，尝试从备份文件加载自愈
                if (File.Exists(_bakFilePath))
                {
                    try
                    {
                        var items = ReadLinesFromFile(_bakFilePath);
                        // 自动用备份恢复重建主文件
                        Save(items);
                        return items;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 读取备份文件 {_bakFilePath} 失败 ({ex.Message})");
                    }
                }

                // 3. 主文件和备份均不存在时，创建空白文件
                try
                {
                    if (!File.Exists(_filePath))
                    {
                        File.WriteAllText(_filePath, string.Empty, Encoding.UTF8);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 创建空白文件失败 ({ex.Message})");
                }

                return new List<T>();
            }
        }

        /// <summary>
        /// 原子持久化保存数据（写入临时文件 + 制作备份 + 原子替换）
        /// </summary>
        /// <param name="items">待保存的数据列表</param>
        public void Save(IEnumerable<T> items)
        {
            if (items == null) return;

            lock (_fileLock)
            {
                try
                {
                    var sb = new StringBuilder();
                    foreach (var item in items)
                    {
                        if (item == null) continue;
                        var line = JsonSerializer.Serialize(item, JsonOptions);
                        sb.AppendLine(line);
                    }

                    // 1. 写入同目录下的 .tmp 临时文件
                    File.WriteAllText(_tmpFilePath, sb.ToString(), Encoding.UTF8);

                    // 2. 如果主文件存在，备份为 .bak 文件
                    if (File.Exists(_filePath))
                    {
                        try
                        {
                            File.Copy(_filePath, _bakFilePath, overwrite: true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 备份文件失败 ({ex.Message})");
                        }
                    }

                    // 3. 原子替换覆盖主文件
                    File.Move(_tmpFilePath, _filePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"JsonLinesStorage: 原子写入失败 ({ex.Message})");
                    throw;
                }
                finally
                {
                    // 4. 清理残留临时文件
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
        /// 内部辅助：从指定文件逐行反序列化 JSON 实体
        /// </summary>
        private static List<T> ReadLinesFromFile(string path)
        {
            var list = new List<T>();
            var lines = File.ReadAllLines(path, Encoding.UTF8);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                var item = JsonSerializer.Deserialize<T>(trimmed, JsonOptions);
                if (item != null)
                {
                    list.Add(item);
                }
            }

            return list;
        }
    }
}
