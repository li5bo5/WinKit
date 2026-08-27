using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WinKit.Clipboard.Models;
using WinKit.Clipboard.Services;
using WinKit.Common;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WinKit.Clipboard
{
    public partial class QuickPhraseWindow : Window
    {
        private readonly QuickPhraseManager _phraseManager;
        private readonly ClipboardService _clipboardService;
        private readonly GlobalMouseClickMonitor _mouseMonitor = new();

        private IntPtr _lastTargetHwnd = IntPtr.Zero;

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativeMethods.POINT lpPoint);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                                                         int x, int y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public QuickPhraseWindow(QuickPhraseManager phraseManager, ClipboardService clipboardService)
        {
            InitializeComponent();
            _phraseManager = phraseManager;
            _clipboardService = clipboardService;

            PhrasesList.ItemsSource = _phraseManager.Items;

            this.IsVisibleChanged += (s, e) =>
            {
                if (!(bool)e.NewValue)
                {
                    _mouseMonitor.Stop();
                }
            };

            _mouseMonitor.ClickedOutside += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (IsVisible) Hide();
                });
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            // 仅注入 WS_EX_TOOLWINDOW 防止任务栏污染，不注入 WS_EX_NOACTIVATE 确保可正常获取焦点接收键盘事件
            SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
        }

        /// <summary>
        /// 在前台输入光标处（或鼠标附近）弹出并激活键盘焦点
        /// </summary>
        public void ShowNearCaretOrMouse(IntPtr explicitTargetHwnd = default)
        {
            // 1. 重新加载短语并更新警告栏
            _phraseManager.Reload();
            WarningBar.Visibility = _phraseManager.HasFormatError ? Visibility.Visible : Visibility.Collapsed;

            // 2. 记录前台输入窗口
            if (explicitTargetHwnd != IntPtr.Zero && IsWindow(explicitTargetHwnd))
            {
                _lastTargetHwnd = explicitTargetHwnd;
            }
            else
            {
                IntPtr fgWnd = GetForegroundWindow();
                var selfHwnd = new WindowInteropHelper(this).Handle;
                if (fgWnd != IntPtr.Zero && fgWnd != selfHwnd && IsWindow(fgWnd))
                {
                    _lastTargetHwnd = fgWnd;
                }
            }

            // 3. 计算坐标：优先尝试获取 Caret 坐标
            double targetX = 0, targetY = 0;
            bool gotCaret = false;

            if (_lastTargetHwnd != IntPtr.Zero)
            {
                uint threadId = NativeMethods.GetWindowThreadProcessId(_lastTargetHwnd, out _);
                var guiInfo = new NativeMethods.GUITHREADINFO();
                guiInfo.cbSize = Marshal.SizeOf(guiInfo);

                if (NativeMethods.GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.hwndCaret != IntPtr.Zero)
                {
                    var pt = new NativeMethods.POINT { X = guiInfo.rcCaret.Left, Y = guiInfo.rcCaret.Bottom };
                    if (NativeMethods.ClientToScreen(guiInfo.hwndCaret, ref pt) && (pt.X > 0 || pt.Y > 0))
                    {
                        targetX = pt.X;
                        targetY = pt.Y + 4;
                        gotCaret = true;
                    }
                }
            }

            // 4. 若未获取到 Caret 坐标，回退至鼠标位置
            if (!gotCaret)
            {
                GetCursorPos(out var mousePos);
                targetX = mousePos.X + 10;
                targetY = mousePos.Y + 10;
            }

            // 5. DPI 转换与屏幕边界约束
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            double dpiX = dpi.DpiScaleX;
            double dpiY = dpi.DpiScaleY;

            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)targetX, (int)targetY));
            var area = screen.WorkingArea;

            double left = targetX / dpiX;
            double top = targetY / dpiY;
            double workL = area.Left / dpiX;
            double workT = area.Top / dpiY;
            double workR = area.Right / dpiX;
            double workB = area.Bottom / dpiY;

            double winW = Width > 0 ? Width : 300;
            double winH = ActualHeight > 0 ? ActualHeight : 200;

            if (left + winW > workR) left = workR - winW - 10;
            if (left < workL) left = workL + 10;
            if (top + winH > workB) top = targetY / dpiY - winH - 10;
            if (top < workT) top = workT + 10;

            Left = left;
            Top = top;

            // 6. 默认选中第一项
            if (_phraseManager.Items.Count > 0)
            {
                PhrasesList.SelectedIndex = 0;
            }

            // 7. 显示并正常激活焦点
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Focus();
            PhrasesList.Focus();

            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_SHOWWINDOW);

            // 8. 启动鼠标外部监听
            _mouseMonitor.Start(new[] { hwnd });
        }

        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.Content is QuickPhraseItem phrase)
            {
                UseSelectedPhrase(phrase);
                e.Handled = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                if (_lastTargetHwnd != IntPtr.Zero && IsWindow(_lastTargetHwnd))
                {
                    NativeMethods.ForceSetForegroundWindow(_lastTargetHwnd);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (PhrasesList.SelectedItem is QuickPhraseItem selected)
                {
                    UseSelectedPhrase(selected);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                int next = PhrasesList.SelectedIndex - 1;
                if (next >= 0)
                {
                    PhrasesList.SelectedIndex = next;
                    PhrasesList.ScrollIntoView(PhrasesList.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                int next = PhrasesList.SelectedIndex + 1;
                if (next < _phraseManager.Items.Count)
                {
                    PhrasesList.SelectedIndex = next;
                    PhrasesList.ScrollIntoView(PhrasesList.SelectedItem);
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// 回填选中的短语并保持在系统剪贴板中
        /// </summary>
        private async void UseSelectedPhrase(QuickPhraseItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Content)) return;

            try
            {
                // 1. 设置剪贴板监控自回填忽略标记
                _clipboardService.NotifyUpcomingSelfPaste(item.Content);

                // 2. 写入系统剪贴板（重试保护）
                bool setOk = false;
                for (int i = 0; i < 3 && !setOk; i++)
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(item.Content);
                        setOk = true;
                    }
                    catch
                    {
                        await Task.Delay(30);
                    }
                }
                if (!setOk) return;

                // 3. 记录本次写入后系统产生的最新序列号
                uint seq = NativeMethods.GetClipboardSequenceNumber();
                _clipboardService.RegisterSelfPasteSequence(seq, item.Content);

                // 4. 隐藏短语窗口
                Hide();
                await Task.Delay(50);

                // 5. 焦点归还给前台目标窗口
                if (_lastTargetHwnd != IntPtr.Zero && IsWindow(_lastTargetHwnd))
                {
                    NativeMethods.ForceSetForegroundWindow(_lastTargetHwnd);
                    for (int wait = 0; wait < 6 && GetForegroundWindow() != _lastTargetHwnd; wait++)
                    {
                        await Task.Delay(30);
                    }
                    await Task.Delay(30);
                }

                // 6. 模拟 Ctrl+V 粘贴
                NativeMethods.SimulateCtrlV();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"QuickPhrase: 回填短语失败 ({ex.Message})");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _mouseMonitor.Dispose();
            base.OnClosed(e);
        }
    }
}
