using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;
using WinKit.Clipboard.Models;
using WinKit.Common;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 剪贴板监听与自回填过滤服务（支持文本与图片监控、精准序列号自回填过滤）
    /// </summary>
    public class ClipboardService : IDisposable
    {
        private DispatcherTimer? _timer;
        private bool _isMonitoring;
        private int _pollInterval = 400; // ms

        private string? _lastTextContent;
        private string? _lastImageHash;
        private uint _lastHandledSequence = 0;

        // 自回填注册序列号与时间戳 (SequenceNumber -> TimestampTicks)
        private readonly ConcurrentDictionary<uint, long> _selfPasteSequences = new();
        // 即将发生的自回填标记 (内容哈希/文本 -> 截止时间)
        private readonly ConcurrentDictionary<string, long> _upcomingSelfPastes = new();

        public event EventHandler<ClipboardItem>? ItemDetected;

        public void StartMonitoring()
        {
            if (_isMonitoring) return;
            _isMonitoring = true;

            _timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(_pollInterval),
                DispatcherPriority.Background,
                OnTimerTick,
                Dispatcher.CurrentDispatcher);
            _timer.Start();
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _timer?.Stop();
            _timer = null;
        }

        /// <summary>
        /// 预先通知即将发生 WinKit 内部主动回填写入
        /// </summary>
        public void NotifyUpcomingSelfPaste(string? text, string? imageHash = null)
        {
            long expireAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3000;
            if (!string.IsNullOrEmpty(text))
            {
                _upcomingSelfPastes[text] = expireAt;
            }
            if (!string.IsNullOrEmpty(imageHash))
            {
                _upcomingSelfPastes[imageHash] = expireAt;
            }
        }

        /// <summary>
        /// 注册 WinKit 自身写入剪贴板后获取到的 Win32 真实序列号
        /// </summary>
        public void RegisterSelfPasteSequence(uint seqNumber, string? text, string? imageHash = null)
        {
            long expireAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 5000;
            _selfPasteSequences[seqNumber] = expireAt;
            _lastHandledSequence = seqNumber;

            if (!string.IsNullOrEmpty(text))
            {
                _lastTextContent = text;
            }
            if (!string.IsNullOrEmpty(imageHash))
            {
                _lastImageHash = imageHash;
            }
        }

        private volatile bool _isInternalPasting;

        /// <summary>
        /// 开启内部主动回填锁，在此期间绝对不将剪贴板的变化当作用户外部新复制
        /// </summary>
        public void BeginInternalPaste()
        {
            _isInternalPasting = true;
        }

        /// <summary>
        /// 结束内部主动回填，给予微弱静默期确保 Win32 消息循环平稳跳过
        /// </summary>
        public void EndInternalPaste()
        {
            Task.Delay(350).ContinueWith(_ => _isInternalPasting = false);
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            try
            {
                if (_isInternalPasting) return;

                uint currentSeq = NativeMethods.GetClipboardSequenceNumber();
                if (currentSeq == _lastHandledSequence && currentSeq != 0)
                {
                    return; // 序列号未变，剪贴板未更新
                }

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // 检查并清理过期的自回填注册项
                foreach (var kvp in _selfPasteSequences)
                {
                    if (now > kvp.Value) _selfPasteSequences.TryRemove(kvp.Key, out _);
                }
                foreach (var kvp in _upcomingSelfPastes)
                {
                    if (now > kvp.Value) _upcomingSelfPastes.TryRemove(kvp.Key, out _);
                }

                // 1. 序列号命中自回填注册列表 -> 忽略跳过
                if (_selfPasteSequences.ContainsKey(currentSeq))
                {
                    _lastHandledSequence = currentSeq;
                    return;
                }

                // 2. 检查图片内容
                if (System.Windows.Forms.Clipboard.ContainsImage())
                {
                    Bitmap? bmpCopy = null;
                    try
                    {
                        using var rawImg = System.Windows.Forms.Clipboard.GetImage();
                        if (rawImg != null)
                        {
                            bmpCopy = new Bitmap(rawImg);
                        }
                    }
                    catch { }

                    if (bmpCopy != null)
                    {
                        _lastHandledSequence = currentSeq;

                        // 彻底剥离 UI 线程：在后台工作线程池执行 PNG 编码、哈希、缩略图缩放及双重写盘
                        Task.Run(() =>
                        {
                            try
                            {
                                using (bmpCopy)
                                {
                                    var processedItem = ImageProcessingService.ProcessAndSaveImage(bmpCopy);
                                    if (processedItem != null)
                                    {
                                        if (!string.IsNullOrEmpty(processedItem.ImageHash))
                                        {
                                            if (_upcomingSelfPastes.ContainsKey(processedItem.ImageHash) ||
                                                processedItem.ImageHash == _lastImageHash)
                                            {
                                                ImageProcessingService.SafeDeleteFiles(processedItem);
                                                return;
                                            }
                                        }

                                        _lastImageHash = processedItem.ImageHash;
                                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            ItemDetected?.Invoke(this, processedItem);
                                        }));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"ClipboardService: 异步处理大图失败 ({ex.Message})");
                            }
                        });
                        return;
                    }
                }

                // 3. 检查文本内容
                if (System.Windows.Forms.Clipboard.ContainsText())
                {
                    string text = string.Empty;
                    try
                    {
                        text = System.Windows.Forms.Clipboard.GetText();
                    }
                    catch { }

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        string normalizedText = text.Replace("\r\n", "\n").Trim();
                        // 检查是否为即将发生的自回填文本（兼顾原始文本与换行归一化比对）
                        if (_upcomingSelfPastes.ContainsKey(text) ||
                            _upcomingSelfPastes.ContainsKey(normalizedText) ||
                            text == _lastTextContent ||
                            normalizedText == _lastTextContent?.Replace("\r\n", "\n").Trim())
                        {
                            _lastHandledSequence = currentSeq;
                            return;
                        }

                        _lastTextContent = text;
                        _lastHandledSequence = currentSeq;

                        var textItem = new ClipboardItem
                        {
                            Id = Guid.NewGuid(),
                            Type = ClipboardItemType.Text,
                            ContentType = "Text",
                            Content = text,
                            CreatedAt = DateTimeOffset.Now
                        };
                        ItemDetected?.Invoke(this, textItem);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClipboardService: 监控检查异常 ({ex.Message})");
            }
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
