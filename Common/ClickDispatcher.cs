using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace WinKit.Common
{
    /// <summary>
    /// 全局统一鼠标点击调度器，用于精确区分并互斥派发单击（Single Click）与双击（Double Click）动作
    /// </summary>
    public sealed class ClickDispatcher
    {
        private static readonly Lazy<ClickDispatcher> _lazyInstance = new(() => new ClickDispatcher());
        public static ClickDispatcher Default => _lazyInstance.Value;

        private readonly TimeSpan _doubleClickTime;
        private readonly Dictionary<object, PendingClick> _pending = new();
        private readonly object _lock = new();

        private sealed class PendingClick
        {
            public required object Key { get; init; }
            public required Action SingleAction { get; init; }
            public required DispatcherTimer Timer { get; init; }
        }

        public ClickDispatcher()
        {
            int intervalMs = System.Windows.Forms.SystemInformation.DoubleClickTime;
            if (intervalMs <= 0) intervalMs = 500;
            _doubleClickTime = TimeSpan.FromMilliseconds(intervalMs);
        }

        /// <summary>
        /// 接收点击事件并进行单击/双击互斥调度
        /// </summary>
        /// <param name="key">用于区分不同点击目标的唯一标识（如实体对象、UI 控件或特定 ID）</param>
        /// <param name="singleAction">单击动作（延迟在双击时限后触发）</param>
        /// <param name="doubleAction">双击动作（立即触发并取消未决单击）</param>
        public void HandleClick(object key, Action singleAction, Action doubleAction)
        {
            if (key == null || singleAction == null || doubleAction == null) return;

            var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            lock (_lock)
            {
                // 若已存在未决单击，说明在时限内收到了第二次点击 → 判定为双击
                if (_pending.TryGetValue(key, out var pending))
                {
                    pending.Timer.Stop();
                    _pending.Remove(key);

                    // 在 UI 线程立即执行双击动作
                    dispatcher.BeginInvoke(DispatcherPriority.Input, doubleAction);
                    return;
                }

                // 第一次点击到达 → 启动定时器等待潜在的第二次点击
                var timer = new DispatcherTimer(DispatcherPriority.Input, dispatcher)
                {
                    Interval = _doubleClickTime
                };

                timer.Tick += (s, e) =>
                {
                    lock (_lock)
                    {
                        timer.Stop();
                        _pending.Remove(key);
                    }

                    // 超时未收到第二次点击 → 在 UI 线程执行单击动作
                    dispatcher.BeginInvoke(DispatcherPriority.Input, singleAction);
                };

                _pending[key] = new PendingClick
                {
                    Key = key,
                    SingleAction = singleAction,
                    Timer = timer
                };

                timer.Start();
            }
        }

        /// <summary>
        /// 取消指定目标的未决单击任务
        /// </summary>
        /// <param name="key">目标标识</param>
        public void CancelPending(object key)
        {
            if (key == null) return;

            lock (_lock)
            {
                if (_pending.TryGetValue(key, out var pending))
                {
                    pending.Timer.Stop();
                    _pending.Remove(key);
                }
            }
        }

        /// <summary>
        /// 清空所有未决任务（窗口隐藏或数据重置时调用）
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                foreach (var pending in _pending.Values)
                {
                    pending.Timer.Stop();
                }
                _pending.Clear();
            }
        }
    }
}
