using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WinKit.Clipboard.Models;
using WinKit.Common;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 独立图片缓存清理服务：支持 4 重触发时机、保留天数清理、超限折半清理与孤立文件清理
    /// </summary>
    public class ImageCleanupService : IDisposable
    {
        private readonly SettingsManager _settingsManager;
        private readonly Func<IList<ClipboardItem>> _getItemsFunc;
        private readonly Action<List<ClipboardItem>> _removeItemsAction;
        private readonly System.Threading.Timer? _periodicTimer;
        private readonly CancellationTokenSource _cts = new();
        private int _isCleaning = 0;

        public ImageCleanupService(
            SettingsManager settingsManager,
            Func<IList<ClipboardItem>> getItemsFunc,
            Action<List<ClipboardItem>> removeItemsAction)
        {
            _settingsManager = settingsManager;
            _getItemsFunc = getItemsFunc;
            _removeItemsAction = removeItemsAction;

            // 1. 触发时机 1：应用启动 10 秒后执行初次清理
            Task.Delay(TimeSpan.FromSeconds(10), _cts.Token).ContinueWith(_ =>
            {
                if (!_cts.IsCancellationRequested) RunCleanupAsync();
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            // 2. 触发时机 2：每 12 小时定时轮询触发
            _periodicTimer = new System.Threading.Timer(_ =>
            {
                if (!_cts.IsCancellationRequested) RunCleanupAsync();
            }, null, TimeSpan.FromHours(12), TimeSpan.FromHours(12));
        }

        /// <summary>
        /// 触发时机 3 & 4：设置变更或新增图片后主动触发检查
        /// </summary>
        public void TriggerCleanup()
        {
            Task.Run(() => RunCleanupAsync());
        }

        /// <summary>
        /// 执行核心两阶段清理与孤立文件清理
        /// </summary>
        public Task RunCleanupAsync()
        {
            if (Interlocked.CompareExchange(ref _isCleaning, 1, 0) != 0)
            {
                return Task.CompletedTask; // 已有清理在运行，跳过
            }

            return Task.Run(() =>
            {
                try
                {
                    AppPaths.EnsureDirectories();
                    var settings = _settingsManager.Settings;
                    int retentionDays = settings.ClipboardImageRetentionDays > 0 ? settings.ClipboardImageRetentionDays : 15;
                    long maxStorageBytes = (settings.ClipboardImageMaxStorageMB > 0 ? settings.ClipboardImageMaxStorageMB : 100) * 1024L * 1024L;

                    var allItems = _getItemsFunc();
                    var imageItems = allItems.Where(i => i.IsImage).ToList();
                    var itemsToRemove = new List<ClipboardItem>();

                    // ── 阶段 1：保留天数清理 ──────────────────────────────────
                    var expireThreshold = DateTimeOffset.Now.AddDays(-retentionDays);
                    foreach (var item in imageItems)
                    {
                        if (_cts.IsCancellationRequested) return;
                        if (item.CreatedAt < expireThreshold)
                        {
                            itemsToRemove.Add(item);
                            ImageProcessingService.SafeDeleteFiles(item);
                        }
                    }

                    if (_cts.IsCancellationRequested) return;

                    // 从待检查列表中移除已清理项
                    var remainingImages = imageItems.Except(itemsToRemove).OrderBy(i => i.CreatedAt).ToList();

                    // ── 阶段 2：容量上限折半清理 ──────────────────────────────
                    long currentTotalSize = remainingImages.Sum(i => i.ImageSize);
                    if (currentTotalSize > maxStorageBytes && remainingImages.Count > 0)
                    {
                        int halfCount = (int)Math.Ceiling(remainingImages.Count / 2.0);
                        var toEvict = remainingImages.Take(halfCount).ToList();

                        foreach (var item in toEvict)
                        {
                            if (_cts.IsCancellationRequested) return;
                            itemsToRemove.Add(item);
                            ImageProcessingService.SafeDeleteFiles(item);
                        }
                    }

                    if (_cts.IsCancellationRequested) return;

                    // ── 阶段 3：通知 UI/存储移除已清理条目 ──────────────────────
                    if (itemsToRemove.Count > 0)
                    {
                        _removeItemsAction(itemsToRemove);
                    }

                    if (_cts.IsCancellationRequested) return;

                    // ── 阶段 4：孤立文件深度清理 ──────────────────────────────
                    CleanupOrphanFiles(allItems);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ImageCleanupService: 清理异常 ({ex.Message})");
                }
                finally
                {
                    Interlocked.Exchange(ref _isCleaning, 0);
                }
            });
        }

        /// <summary>
        /// 扫描磁盘目录，删除未被任何剪贴板项引用的孤立图片与缩略图
        /// </summary>
        private void CleanupOrphanFiles(IList<ClipboardItem> allItems)
        {
            try
            {
                var referencedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in allItems)
                {
                    if (!string.IsNullOrEmpty(item.ImagePath))
                        referencedFiles.Add(Path.GetFullPath(item.ImagePath));
                    if (!string.IsNullOrEmpty(item.ThumbnailPath))
                        referencedFiles.Add(Path.GetFullPath(item.ThumbnailPath));
                }

                CleanDirectoryOrphans(AppPaths.ClipboardImagesDir, referencedFiles);
                CleanDirectoryOrphans(AppPaths.ClipboardThumbnailsDir, referencedFiles);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageCleanupService: 孤立文件清理异常 ({ex.Message})");
            }
        }

        private void CleanDirectoryOrphans(string dirPath, HashSet<string> referencedFiles)
        {
            if (!Directory.Exists(dirPath)) return;

            var files = Directory.GetFiles(dirPath, "*.png");
            foreach (var file in files)
            {
                if (_cts.IsCancellationRequested) return;
                string fullPath = Path.GetFullPath(file);
                if (!referencedFiles.Contains(fullPath))
                {
                    try
                    {
                        File.Delete(fullPath);
                        System.Diagnostics.Debug.WriteLine($"ImageCleanupService: 已清理孤立文件 {fullPath}");
                    }
                    catch
                    {
                        // 忽略正在占用的文件
                    }
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
            }
            catch { }
            _periodicTimer?.Dispose();
            _cts.Dispose();
        }
    }
}
