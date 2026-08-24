using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WinKit.Common;
using WinKit.Clipboard.Models;
using WinKit.Clipboard.Services;
using WinPoint = System.Windows.Point;

namespace WinKit.Clipboard
{
    public partial class MainWindow : Window
    {
        private readonly ClipboardManager _clipboardManager;
        private readonly SettingsManager  _settingsManager;

        // ── 拖拽调整大小 ───────────────────────────────
        private bool     _isResizing = false;
        private WinPoint _resizeStart;
        private double   _resizeStartW, _resizeStartH;

        // ── Toast 计数 ─────────────────────────────────
        private int           _toastActiveCount = 0;

        private Guid?          _copiedItemId = null;

        // ── 前台目标窗口句柄（呼出剪贴板前的活动窗口） ──
        private IntPtr _lastTargetHwnd = IntPtr.Zero;

        // ── 全局鼠标点击监听（用于精准外部点击隐藏）────
        private readonly GlobalMouseClickMonitor _mouseMonitor = new();

        // ── Pin（固定）状态 ────────────────────────────
        private bool _isPinned = false;
        public  bool IsPinned  => _isPinned;

        // ── Win32 P/Invoke ─────────────────────────────
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")] private static extern int  GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int  SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                                                           int x, int y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }

        private const int  GWL_EXSTYLE      = -20;
        private const int  WS_EX_NOACTIVATE = 0x08000000;
        private const int  WS_EX_TOOLWINDOW = 0x00000080;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE       = 0x0001;
        private const uint SWP_NOMOVE       = 0x0002;
        private const uint SWP_NOACTIVATE   = 0x0010;
        private const uint SWP_SHOWWINDOW   = 0x0040;

        private const byte VK_CONTROL     = 0x11;
        private const byte VK_V           = 0x56;
        private const byte KEYEVENTF_KEYUP = 0x0002;

        // ══════════════════════════════════════════════
        // 构造函数
        // ══════════════════════════════════════════════
        public MainWindow(ClipboardManager clipboardManager, SettingsManager settingsManager)
        {
            InitializeComponent();
            _clipboardManager = clipboardManager;
            _settingsManager  = settingsManager;

            ClipboardList.ItemsSource = _clipboardManager.Items;

            ((System.Collections.Specialized.INotifyCollectionChanged)_clipboardManager.Items)
                .CollectionChanged += (s, e) => UpdateUIStates();

            Loaded += (s, e) =>
            {
                UpdateUIStates();
                LoadSettings();
                SetTitleButtonsOpacity(_isPinned ? 1 : 0);
            };

            this.KeyDown += Window_KeyDown;

            // 窗口可见性变化：隐藏时停止鼠标监听并清空未决点击
            this.IsVisibleChanged += (s, e) =>
            {
                if (!(bool)e.NewValue)
                {
                    _mouseMonitor.Stop();
                    ClickDispatcher.Default.Clear();
                }
            };

            // 配置鼠标点击监听 — 点击剪贴板窗口以外的区域时，若未固定则隐藏
            _mouseMonitor.ClickedOutside += OnClickedOutsideWindow;
        }

        // ══════════════════════════════════════════════
        // 初始化 — 设置 WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW
        // ══════════════════════════════════════════════
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd    = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        // ══════════════════════════════════════════════
        // 标题栏按钮悬浮显隐控制（对齐 TodoList）
        // ══════════════════════════════════════════════
        private void TitleBar_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => SetTitleButtonsOpacity(1);
        private void TitleBar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isPinned) SetTitleButtonsOpacity(0);
        }

        private void SetTitleButtonsOpacity(double opacity)
        {
            PinBtn.Opacity   = opacity;
            CloseBtn.Opacity = opacity;
        }

        // ══════════════════════════════════════════════
        // 设置加载与保存
        // ══════════════════════════════════════════════
        private void LoadSettings()
        {
            _isPinned      = _settingsManager.Settings.PasteIsPinned;
            this.Topmost   = _isPinned;
            UpdatePinButton();
        }

        private void UpdatePinButton()
        {
            PinBtn.Content  = _isPinned ? "📍" : "📌";
            PinBtn.ToolTip  = _isPinned ? "取消固定" : "固定";
        }

        private void TogglePinState()
        {
            _isPinned      = !_isPinned;
            this.Topmost   = _isPinned;
            UpdatePinButton();
            SetTitleButtonsOpacity(_isPinned ? 1 : 0);

            var settings = _settingsManager.Settings;
            settings.PasteIsPinned = _isPinned;
            _settingsManager.SaveSettings(settings);

            // 固定状态变更：重新挂接或停止鼠标监听
            if (IsVisible) RefreshMouseMonitor();
        }

        private void PinBtn_Click(object sender, RoutedEventArgs e) => TogglePinState();

        // ══════════════════════════════════════════════
        // Esc 退出
        // ══════════════════════════════════════════════
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════
        // UI 状态更新
        // ══════════════════════════════════════════════
        private void UpdateUIStates()
        {
            int count       = _clipboardManager.Items.Count;
            CountText.Text  = $"{count} 项";
            EmptyPlaceholder.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════
        // ShowAtMouse — 在鼠标附近弹出并强制置于最顶层
        // ══════════════════════════════════════════════
        public void ShowAtMouse()
        {
            // ① 呼出前立即记录当前前台窗口（用户正在打字的窗口）
            IntPtr fgWnd = GetForegroundWindow();
            // 排除自身窗口与无效句柄
            var selfHwnd = new WindowInteropHelper(this).Handle;
            if (fgWnd != IntPtr.Zero && fgWnd != selfHwnd && IsWindow(fgWnd))
                _lastTargetHwnd = fgWnd;

            // ② 计算弹出位置
            GetCursorPos(out POINT mousePos);
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(mousePos.X, mousePos.Y));
            var area   = screen.WorkingArea;

            var dpi        = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            double dpiX    = dpi.DpiScaleX;
            double dpiY    = dpi.DpiScaleY;

            double mouseX  = mousePos.X / dpiX;
            double mouseY  = mousePos.Y / dpiY;
            double workL   = area.Left   / dpiX;
            double workT   = area.Top    / dpiY;
            double workR   = area.Right  / dpiX;
            double workB   = area.Bottom / dpiY;

            double left = mouseX + 10;
            double top  = mouseY + 10;

            if (left + Width  > workR) left = mouseX - Width  - 10;
            if (left          < workL) left = workL;
            if (top  + Height > workB) top  = mouseY - Height - 10;
            if (top           < workT) top  = workT;

            Left = left;
            Top  = top;

            // ③ 显示窗口并强制置于 Z-Order 最顶层（HWND_TOPMOST + SWP_NOACTIVATE 不抢夺焦点）
            Show();
            WindowState = WindowState.Normal;

            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            // ④ 未开启记忆滚动时，复位到第一条
            if (!_settingsManager.Settings.PasteRememberScrollPosition && _clipboardManager.Items.Count > 0)
                ClipboardList.ScrollIntoView(_clipboardManager.Items.FirstOrDefault());

            // ⑤ 启动外部鼠标点击监听（未固定时才监听，固定时不自动隐藏）
            RefreshMouseMonitor();
        }

        // ══════════════════════════════════════════════
        // 全局鼠标监听控制
        // ══════════════════════════════════════════════
        private void RefreshMouseMonitor()
        {
            if (!IsVisible)
            {
                _mouseMonitor.Stop();
                return;
            }

            if (_isPinned)
            {
                // 固定模式：不自动隐藏，停止监听
                _mouseMonitor.Stop();
            }
            else
            {
                // 非固定模式：监听外部点击，立即隐藏
                var selfHwnd = new WindowInteropHelper(this).Handle;
                _mouseMonitor.Start(new[] { selfHwnd });
            }
        }

        private void OnClickedOutsideWindow()
        {
            // 在 UI 线程隐藏（此回调来自鼠标钩子线程）
            Dispatcher.Invoke(() =>
            {
                if (!_isPinned && IsVisible) Hide();
            });
        }

        // ══════════════════════════════════════════════
        // 窗口隐藏时停止监听
        // ══════════════════════════════════════════════
        protected override void OnClosed(EventArgs e)
        {
            _mouseMonitor.Dispose();
            base.OnClosed(e);
        }

        // ══════════════════════════════════════════════
        // 标题栏拖动
        // ══════════════════════════════════════════════
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        // ══════════════════════════════════════════════
        // 关闭按钮
        // ══════════════════════════════════════════════
        public void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

        // ══════════════════════════════════════════════
        // 列表项点击 — 统一接入 ClickDispatcher 调度单击/双击
        // ══════════════════════════════════════════════
        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 若点击的是列表项内的子按钮（如删除按钮），直接跳过交由按钮 Click 处理
            if (IsClickOnDeleteButton(e.OriginalSource as DependencyObject)) return;

            if (sender is ListBoxItem item && item.Content is ClipboardItem clipItem)
            {
                ClickDispatcher.Default.HandleClick(
                    clipItem,
                    () => ClickItem(clipItem),
                    () => UseSelectedItem(clipItem)
                );
            }
        }

        /// <summary>
        /// 单击逻辑：复制文本到系统剪贴板，将条目移至首位，未固定时自动隐藏
        /// </summary>
        private void ClickItem(ClipboardItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Content)) return;
            try
            {
                System.Windows.Clipboard.SetText(item.Content);
                _copiedItemId = item.Id;
                _clipboardManager.MoveToTop(item);
                ClipboardList.UpdateLayout();

                if (!_isPinned)
                {
                    Hide();
                }
                else
                {
                    // 固定模式：显示 Toast 提示
                    ShowToast("已复制到剪贴板");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"单击复制失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 双击逻辑：将文本注入目标窗口（自动填充）
        /// 未固定时隐藏窗口；固定时保持显示在最上面，支持连续粘贴
        /// </summary>
        private async void UseSelectedItem(ClipboardItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Content)) return;
            try
            {
                // ① 安全设置剪贴板（带最多 3 次重试，防止 COM 冲突）
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

                _copiedItemId = null;

                // ② 若未固定，先隐藏窗口（防止 Ctrl+V 被发送到剪贴板窗口本身）
                if (!_isPinned)
                {
                    Hide();
                    await Task.Delay(60);
                }

                // ③ 穿透 Windows 权限限制，精准将前台焦点归还给目标输入窗口
                if (_lastTargetHwnd != IntPtr.Zero && IsWindow(_lastTargetHwnd))
                {
                    NativeMethods.ForceSetForegroundWindow(_lastTargetHwnd);
                    for (int wait = 0; wait < 6 && GetForegroundWindow() != _lastTargetHwnd; wait++)
                    {
                        await Task.Delay(30);
                    }
                    await Task.Delay(30);
                }

                // ④ 模拟 Ctrl+V 粘贴
                NativeMethods.SimulateCtrlV();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"双击粘贴失败: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════
        // 键盘 Enter 键确认
        // ══════════════════════════════════════════════
        private void ClipboardList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ClipboardList.SelectedItem is ClipboardItem item)
                {
                    if (_copiedItemId == item.Id)
                        UseSelectedItem(item);
                    else
                        CopyItemToClipboard(item);
                    e.Handled = true;
                }
            }
        }

        private void CopyItemToClipboard(ClipboardItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Content)) return;
            try
            {
                System.Windows.Clipboard.SetText(item.Content);
                _copiedItemId = item.Id;
                ShowToast("已复制到系统剪贴板");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"复制失败: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════
        // Toast 提示
        // ══════════════════════════════════════════════
        private async void ShowToast(string message)
        {
            CountText.Text = message;
            _toastActiveCount++;
            await Task.Delay(1200);
            _toastActiveCount--;
            if (_toastActiveCount == 0) UpdateUIStates();
        }

        // ══════════════════════════════════════════════
        // 删除按钮
        // ══════════════════════════════════════════════
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is Guid id)
            {
                _clipboardManager.RemoveItem(id);
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════
        // 清空全部
        // ══════════════════════════════════════════════
        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("确定清空全部剪贴板历史吗？此操作不可撤销。", "提示",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                _clipboardManager.ClearAll();
        }

        // ══════════════════════════════════════════════
        // 辅助：判断点击是否在删除按钮上
        // ══════════════════════════════════════════════
        private bool IsClickOnDeleteButton(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj is System.Windows.Controls.Button btn && btn.Name == "DeleteBtn") return true;
                obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
            }
            return false;
        }

        // ══════════════════════════════════════════════
        // Ctrl+V 模拟粘贴
        // ══════════════════════════════════════════════
        private static void SimulateCtrlV()
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V,       0, 0, UIntPtr.Zero);
            keybd_event(VK_V,       0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // ══════════════════════════════════════════════
        // 右下角 ResizeGrip
        // ══════════════════════════════════════════════
        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isResizing    = true;
            _resizeStart   = e.GetPosition(null);
            _resizeStartW  = Width;
            _resizeStartH  = Height;
            ((UIElement)sender).CaptureMouse();
            ((UIElement)sender).MouseMove        += ResizeGrip_MouseMove;
            ((UIElement)sender).MouseLeftButtonUp += ResizeGrip_MouseLeftButtonUp;
            e.Handled = true;
        }

        private void ResizeGrip_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isResizing) return;
            var    pos   = e.GetPosition(null);
            var    delta = pos - _resizeStart;
            Width  = Math.Max(MinWidth,  _resizeStartW + delta.X);
            Height = Math.Max(MinHeight, _resizeStartH + delta.Y);
        }

        private void ResizeGrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isResizing = false;
            ((UIElement)sender).ReleaseMouseCapture();
            ((UIElement)sender).MouseMove        -= ResizeGrip_MouseMove;
            ((UIElement)sender).MouseLeftButtonUp -= ResizeGrip_MouseLeftButtonUp;
        }
    }
}
