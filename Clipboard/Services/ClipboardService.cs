using System;
using System.Windows;
using System.Windows.Threading;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 剪切板监控服务 — 仅监控文本，后台低占用
    /// </summary>
    public class ClipboardService : IDisposable
    {
        private DispatcherTimer? _timer;
        private string? _lastTextContent;
        private bool _isMonitoring;
        private int _pollInterval = 800; // ms

        public event EventHandler<string>? TextChanged;

        public void StartMonitoring()
        {
            if (_isMonitoring) return;
            _isMonitoring = true;

            _timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(_pollInterval),
                DispatcherPriority.Background, // 低优先级，不阻塞 UI 线程
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

        private void OnTimerTick(object? sender, EventArgs e)
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsText())
                    return;

                var text = System.Windows.Clipboard.GetText();
                if (!string.IsNullOrEmpty(text) && text != _lastTextContent)
                {
                    _lastTextContent = text;
                    TextChanged?.Invoke(this, text);
                }
            }
            catch
            {
                // 剪切板可能被独占，静默忽略
            }
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
