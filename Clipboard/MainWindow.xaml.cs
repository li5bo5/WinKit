using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
using WinPoint = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Button = System.Windows.Controls.Button;
using DrawingImage = System.Drawing.Image;

namespace WinKit.Clipboard
{
    public partial class MainWindow : Window
    {
        private readonly ClipboardManager _clipboardManager;
        private readonly ClipboardService _clipboardService;
        private readonly SettingsManager  _settingsManager;

        // ── 拖拽调整大小 ───────────────────────────────
        private bool     _isResizing = false;
        private WinPoint _resizeStart;
        private double   _resizeStartW, _resizeStartH;

        // ── 前台目标窗口句柄（呼出剪贴板前的活动窗口） ──
        private IntPtr _lastTargetHwnd = IntPtr.Zero;

        // ── 全局鼠标点击监听（用于精准外部点击隐藏）────
        private readonly GlobalMouseClickMonitor _mouseMonitor = new();

        // ── Pin（固定）状态 ────────────────────────────
        private bool _isPinned = false;
        public  bool IsPinned  => _isPinned;

        // ── Win32 P/Invoke ─────────────────────────────
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int  GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int  SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                                                           int x, int y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }

        // ══════════════════════════════════════════════
        // 构造函数
        // ══════════════════════════════════════════════
        public MainWindow(ClipboardManager clipboardManager, ClipboardService clipboardService, SettingsManager settingsManager)
        {
            InitializeComponent();
            _clipboardManager = clipboardManager;
            _clipboardService = clipboardService;
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

            // 窗口可见性变化：隐藏时停止鼠标监听
            this.IsVisibleChanged += (s, e) =>
            {
                if (!(bool)e.NewValue)
                {
                    _mouseMonitor.Stop();
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
            int exStyle = GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
        }

        // ══════════════════════════════════════════════
        // 标题栏按钮悬浮显隐控制
        // ══════════════════════════════════════════════
        private void TitleBar_MouseEnter(object sender, MouseEventArgs e) => SetTitleButtonsOpacity(1);
        private void TitleBar_MouseLeave(object sender, MouseEventArgs e) => SetTitleButtonsOpacity(0);

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
            _isPinned    = false;
            this.Topmost = false;
            UpdatePinButton();
        }

        private void UpdatePinButton()
        {
            PinBtn.Content = "📌";
            if (TopAccentLine != null)
                TopAccentLine.Visibility = _isPinned ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TogglePinState()
        {
            _isPinned    = !_isPinned;
            this.Topmost = _isPinned;
            UpdatePinButton();

            if (IsVisible) RefreshMouseMonitor();
        }

        private void PinBtn_Click(object sender, RoutedEventArgs e) => TogglePinState();

        // ══════════════════════════════════════════════
        // Esc 退出
        // ══════════════════════════════════════════════
        private void Window_KeyDown(object sender, KeyEventArgs e)
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
            // ① 呼出前立即记录当前前台窗口
            IntPtr fgWnd = GetForegroundWindow();
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

            // ③ 显示窗口并强制置于最顶层（默认未固定，单次呼出临时置顶生效）
            _isPinned = false;
            UpdatePinButton();
            SetTitleButtonsOpacity(0);

            Show();
            WindowState = WindowState.Normal;

            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            // ④ 未开启记忆滚动时，复位到第一条
            if (!_settingsManager.Settings.PasteRememberScrollPosition && _clipboardManager.Items.Count > 0)
                ClipboardList.ScrollIntoView(_clipboardManager.Items.FirstOrDefault());

            // ⑤ 启动外部鼠标点击监听
            RefreshMouseMonitor();
        }

        private void RefreshMouseMonitor()
        {
            if (!IsVisible)
            {
                _mouseMonitor.Stop();
                return;
            }

            if (_isPinned)
            {
                _mouseMonitor.Stop();
            }
            else
            {
                var selfHwnd = new WindowInteropHelper(this).Handle;
                _mouseMonitor.Start(new[] { selfHwnd });
            }
        }

        private void OnClickedOutsideWindow()
        {
            Dispatcher.Invoke(() =>
            {
                if (!_isPinned && IsVisible) Hide();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            _mouseMonitor.Dispose();
            base.OnClosed(e);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        public new void Hide()
        {
            _isPinned = false;
            UpdatePinButton();
            SetTitleButtonsOpacity(0);
            base.Hide();
            RefreshMouseMonitor();
        }

        public void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

        // ══════════════════════════════════════════════
        // 列表项单击 — 零延迟直接回填
        // ══════════════════════════════════════════════
        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 排除子按钮点击（预览、删除等）
            if (IsClickOnActionButton(e.OriginalSource as DependencyObject)) return;

            if (sender is ListBoxItem item && item.Content is ClipboardItem clipItem)
            {
                UseSelectedItem(clipItem);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 回填选中的文本或图片到前台窗口（未固定时隐藏，固定时保持置顶连续粘贴）
        /// </summary>
        private async void UseSelectedItem(ClipboardItem item)
        {
            if (item == null) return;

            try
            {
                // 1. 设置自回填忽略通知并写入系统剪贴板
                if (item.IsImage && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                {
                    _clipboardService.NotifyUpcomingSelfPaste(null, item.ImageHash);

                    bool setOk = false;
                    for (int i = 0; i < 3 && !setOk; i++)
                    {
                        try
                        {
                            using var img = DrawingImage.FromFile(item.ImagePath);
                            System.Windows.Forms.Clipboard.SetImage(img);
                            setOk = true;
                        }
                        catch
                        {
                            await Task.Delay(30);
                        }
                    }
                    if (!setOk) return;

                    uint seq = NativeMethods.GetClipboardSequenceNumber();
                    _clipboardService.RegisterSelfPasteSequence(seq, null, item.ImageHash);
                }
                else if (item.IsText && !string.IsNullOrEmpty(item.Content))
                {
                    _clipboardService.NotifyUpcomingSelfPaste(item.Content);

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

                    uint seq = NativeMethods.GetClipboardSequenceNumber();
                    _clipboardService.RegisterSelfPasteSequence(seq, item.Content);
                }
                else
                {
                    return;
                }

                // 2. 若未固定，先隐藏窗口
                if (!_isPinned)
                {
                    Hide();
                    await Task.Delay(50);
                }

                // 4. 精准归还前台焦点至目标输入窗口
                if (_lastTargetHwnd != IntPtr.Zero && IsWindow(_lastTargetHwnd))
                {
                    NativeMethods.ForceSetForegroundWindow(_lastTargetHwnd);
                    for (int wait = 0; wait < 6 && GetForegroundWindow() != _lastTargetHwnd; wait++)
                    {
                        await Task.Delay(30);
                    }
                    await Task.Delay(30);
                }

                // 5. 模拟 Ctrl+V 粘贴
                NativeMethods.SimulateCtrlV();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard: 回填失败 ({ex.Message})");
            }
        }

        // ══════════════════════════════════════════════
        // 键盘 Enter 确认回填
        // ══════════════════════════════════════════════
        private void ClipboardList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ClipboardList.SelectedItem is ClipboardItem item)
                {
                    UseSelectedItem(item);
                    e.Handled = true;
                }
            }
        }

        // ══════════════════════════════════════════════
        // “小眼睛”图片系统预览按钮
        // ══════════════════════════════════════════════
        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClipboardItem item && !string.IsNullOrEmpty(item.ImagePath))
            {
                try
                {
                    if (File.Exists(item.ImagePath))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = item.ImagePath,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"预览图片失败: {ex.Message}");
                }
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════
        // 删除单项
        // ══════════════════════════════════════════════
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClipboardItem item)
            {
                _clipboardManager.RemoveItem(item.Id);
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
            {
                _clipboardManager.ClearAll();
            }
        }

        private bool IsClickOnActionButton(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj is Button btn && (btn.Name == "DeleteBtn" || btn.Name == "PreviewBtn")) return true;
                obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
            }
            return false;
        }

        // ══════════════════════════════════════════════
        // 右下角 ResizeGrip 调整窗口大小
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

        private void ResizeGrip_MouseMove(object sender, MouseEventArgs e)
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
