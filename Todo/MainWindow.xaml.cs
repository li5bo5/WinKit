using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WinKit.Common;
using WinKit.Todo.Models;
using WinKit.Todo.Services;
using WinPoint       = System.Windows.Point;
using WinKey         = System.Windows.Input.KeyEventArgs;
using WinMouse       = System.Windows.Input.MouseEventArgs;
using WinDrag        = System.Windows.DragEventArgs;
using WinButton      = System.Windows.Controls.Button;
using WinPanel       = System.Windows.Controls.Panel;
using WinDropEffects = System.Windows.DragDropEffects;

namespace WinKit.Todo
{
    public partial class MainWindow : Window
    {
        // ══════════════════════════════════════════════
        // Win32 P/Invoke — 鼠标穿透（仅内容区）
        // ══════════════════════════════════════════════
        private const int WM_NCHITTEST  = 0x0084;
        private const int HTTRANSPARENT = -1;

        private readonly ObservableCollection<TodoItem> _items = new();
        private readonly TodoService _todoService;
        private readonly RecycleBinService _recycleBinService;
        private readonly SettingsManager _settingsManager;

        // 拖拽平滑排序动画状态
        private WinPoint _dragStartPos;
        private WinPoint _lastMousePos;
        private TodoItem? _dragItem;
        private System.Windows.Controls.ListViewItem? _dragContainer;
        private bool _isDragging = false;
        private int _dragStartIndex = -1;
        private int _targetDropIndex = -1;
        private double _dragGrabOffsetY = 0;
        private double _dragStartContainerTopInList = 0;
        private double _dragStartScrollOffset = 0;
        private ScrollViewer? _todoScrollViewer;
        private DispatcherTimer? _autoScrollTimer;
        private double _autoScrollVelocity = 0;
        private readonly List<(double Top, double Height)> _itemInitialBounds = new();

        // 窗口调整大小
        private bool _isResizing;
        private WinPoint _resizeStart;
        private double   _resizeStartW, _resizeStartH;

        // 置顶 / 穿透（独立状态）
        private bool _isPinned      = false;
        private bool _isPassThrough = false;

        // 托盘引用（用于同步状态）
        private TrayHelper? _tray;
        public bool IsPinned      => _isPinned;
        public bool IsPassThrough => _isPassThrough;
        public bool IsPassThroughEnabled
        {
            get => _isPassThrough;
            set
            {
                if (_isPassThrough != value)
                {
                    TogglePassThroughState();
                }
            }
        }
        public void SetTray(TrayHelper tray) => _tray = tray;

        private const int WM_MOVING = 0x0216;
        private const int WM_SHOWWINDOW = 0x0018;
        private const int SW_PARENTCLOSING = 1;
        private const int SW_OTHERZOOM = 2;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int SWP_HIDEWINDOW = 0x0080;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private bool _isExplicitlyHiding = false;

        public void SafeHide()
        {
            _isExplicitlyHiding = true;
            try
            {
                StopPassThroughMonitor();
                Hide();
            }
            finally
            {
                _isExplicitlyHiding = false;
            }
        }

        private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                if (_isPassThrough)
                {
                    StartPassThroughMonitor();
                }
            }
            else
            {
                StopPassThroughMonitor();
            }
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        // ══════════════════════════════════════════════
        // 构造函数
        // ══════════════════════════════════════════════
        public MainWindow(SettingsManager settingsManager)
        {
            InitializeComponent();
            _settingsManager = settingsManager;

            // 监听窗口可见性切换以启停穿透监听器
            IsVisibleChanged += MainWindow_IsVisibleChanged;

            // 确保窗口句柄创建并立即挂钩 WndProc，确保 Win+D 等底层拦截从第一毫秒起生效
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            var hwndSource = HwndSource.FromHwnd(hwnd);
            hwndSource?.AddHook(WndProc);

            _todoService = new TodoService();
            _recycleBinService = new RecycleBinService(_settingsManager);
            foreach (var item in _todoService.LoadTodos())
                _items.Add(item);
            TodoList.ItemsSource = _items;

            // 监听集合变化，同步空列表占位符
            _items.CollectionChanged += (s, e) => UpdateEmptyPlaceholder();

            // 订阅状态变化：免疫 Win+D 强制最小化
            StateChanged += MainWindow_StateChanged;

            // 监听鼠标捕获丢失，安全终止拖拽与滚屏并复位视觉
            TodoList.LostMouseCapture += (s, e) =>
            {
                if (_isDragging)
                {
                    ResetAllDragVisuals();
                    _isDragging = false;
                    _dragItem = null;
                    _dragContainer = null;
                    _dragStartIndex = -1;
                    _targetDropIndex = -1;
                    _itemInitialBounds.Clear();
                }
            };

            // 初始化或恢复保存的窗口位置与大小
            ApplyInitialOrSavedBounds();

            // 载入并应用保存的设置
            LoadSettings();

            // 监听统一偏好设置变更广播
            _settingsManager.SettingsChanged += (s, settings) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_isPassThrough != settings.TodoIsPassThrough)
                    {
                        ApplyPassThroughState(settings.TodoIsPassThrough);
                    }
                    if (_isPinned != settings.TodoIsPinned)
                    {
                        _isPinned = settings.TodoIsPinned;
                        this.Topmost = _isPinned;
                        UpdatePinButton();
                    }
                });
            };

            // 初始化占位符状态
            UpdateEmptyPlaceholder();

            // 初始状态下标题栏按钮保持隐藏（仅悬停时浮现）
            Loaded += (s, e) =>
            { 
                UpdateEmptyPlaceholder(); 
                SetTitleButtonsOpacity(0); 
            };
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                if (!IsVisible) Show();
            }
        }

        // ══════════════════════════════════════════════
        // 窗口初始化：注入 WS_EX_TOOLWINDOW 并绑定宿主
        // ══════════════════════════════════════════════
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;

            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            extendedStyle |= WS_EX_TOOLWINDOW;
            if (_isPassThrough)
            {
                extendedStyle |= WS_EX_TRANSPARENT;
            }
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);

            // 将宿主所有者关联到桌面 Progman，在操作系统 Z-Order 树中将其确立为桌面从属层，不受显示桌面影响
            IntPtr progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
            {
                try
                {
                    new WindowInteropHelper(this).Owner = progman;
                }
                catch { }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 1. 免疫 Win + D（显示桌面）：多重防御拦截
            // A. 拦截系统命令最小化 (Win+M 或 Win+D 触发的最小化)
            if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
            {
                handled = true;
                return IntPtr.Zero;
            }

            // B. 拦截非显式主动调用的隐藏指令 (Win+D 发送的 WM_SHOWWINDOW 消息)
            if (msg == WM_SHOWWINDOW && wParam == IntPtr.Zero)
            {
                if (!_isExplicitlyHiding)
                {
                    handled = true;
                    // Win+D 触发后，确保窗口持续浮在桌面之上，不被提升的前台桌面遮盖
                    EnsureWindowAboveDesktop();
                    return IntPtr.Zero;
                }
            }

            // C. 拦截通过 SetWindowPos 强制注入 SWP_HIDEWINDOW 的外部指令
            if (msg == WM_WINDOWPOSCHANGING && !_isExplicitlyHiding)
            {
                try
                {
                    var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                    if ((pos.flags & (uint)SWP_HIDEWINDOW) != 0)
                    {
                        pos.flags &= ~((uint)SWP_HIDEWINDOW);
                        Marshal.StructureToPtr(pos, lParam, false);
                    }
                }
                catch { }
            }

            // 2. 接收来自新进程的激活广播消息
            if (msg == NativeMethods.WM_SHOW_EXISTING_INSTANCE)
            {
                if (!IsVisible) Show();
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Activate();
                NativeMethods.ForceSetForegroundWindow(hwnd);
                handled = true;
                return IntPtr.Zero;
            }

            // 3. 限制窗口拖动在当前屏幕工作区内
            if (msg == WM_MOVING)
            {
                // 获取当前鼠标所在的屏幕工作区（物理像素）
                POINT mousePos;
                GetCursorPos(out mousePos);
                var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(mousePos.X, mousePos.Y));
                var area = screen.WorkingArea;

                var rect = (RECT)Marshal.PtrToStructure(lParam, typeof(RECT))!;
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                if (rect.Left < area.Left)
                {
                    rect.Left = area.Left;
                    rect.Right = rect.Left + width;
                }
                else if (rect.Right > area.Right)
                {
                    rect.Right = area.Right;
                    rect.Left = rect.Right - width;
                }

                if (rect.Top < area.Top)
                {
                    rect.Top = area.Top;
                    rect.Bottom = rect.Top + height;
                }
                else if (rect.Bottom > area.Bottom)
                {
                    rect.Bottom = area.Bottom;
                    rect.Top = rect.Bottom - height;
                }

                Marshal.StructureToPtr(rect, lParam, true);
                handled = true;
                return new IntPtr(1);
            }
            return IntPtr.Zero;
        }

        private void EnsureWindowAboveDesktop()
        {
            Action updatePos = () =>
            {
                var h = new WindowInteropHelper(this).Handle;
                if (h != IntPtr.Zero && IsVisible)
                {
                    if (_isPinned)
                    {
                        SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
                    }
                    else
                    {
                        // 非置顶状态：先刷到 TOPMOST 再落回 NOTOPMOST，确保窗口跃居于刚提升的桌面之上但保持非置顶
                        SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
                        SetWindowPos(h, (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
                    }
                }
            };

            // 1. 立即刷新一次
            Dispatcher.BeginInvoke(DispatcherPriority.Render, updatePos);

            // 2. 50ms 延时再次确认，跨越 Windows Shell 显示桌面切换的动画滞后
            Task.Delay(50).ContinueWith(_ =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Render, updatePos);
            });
        }

        // ══════════════════════════════════════════════
        // TitleBar 区域悬停：严格鼠标悬停显隐控制
        // ══════════════════════════════════════════════
        private void TitleBar_MouseEnter(object sender, WinMouse e) => SetTitleButtonsOpacity(1);
        private void TitleBar_MouseLeave(object sender, WinMouse e) => SetTitleButtonsOpacity(0);

        private void SetTitleButtonsOpacity(double opacity)
        {
            if (AddBtn != null) AddBtn.Opacity                 = opacity;
            if (PinBtn != null) PinBtn.Opacity                 = opacity;
            if (PassThroughBtn != null) PassThroughBtn.Opacity = opacity;
            if (CloseBtn != null) CloseBtn.Opacity             = opacity;
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            ShowCreateTodoDialog();
        }

        // ResizeGrip 区域悬停：控制 Grip 显示
        private void ResizeGrip_MouseEnter(object sender, WinMouse e)
        {
            if (!_isPinned) ResizeGripArea.Opacity = 1;
        }
        private void ResizeGrip_MouseLeave(object sender, WinMouse e) => ResizeGripArea.Opacity = 0;

        // ══════════════════════════════════════════════
        // 标题栏拖动
        // ══════════════════════════════════════════════
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
                SaveWindowBounds();
            }
        }

        // ══════════════════════════════════════════════
        // 标题栏按钮
        // ══════════════════════════════════════════════
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => SafeHide();

        private void PinBtn_Click(object sender, RoutedEventArgs e)
        {
            TogglePinState();
            _tray?.SyncPinMenuItem();
        }

        internal void TogglePinFromTray()
        {
            TogglePinState();
        }

        private void TogglePinState()
        {
            _isPinned    = !_isPinned;
            this.Topmost = _isPinned;
            UpdatePinButton();

            // 若取消置顶，且当前正处于穿透模式，则必须同步关闭穿透模式，避免非置顶穿透被下层窗口遮挡
            if (!_isPinned && _isPassThrough)
            {
                ApplyPassThroughState(false);
                _tray?.SyncPassThroughMenuItem();
            }

            SaveSettings();
        }

        private void UpdatePinButton()
        {
            PinBtn.Content = "📌";
            if (TopAccentLine != null)
                TopAccentLine.Visibility = _isPinned ? Visibility.Visible : Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════
        // 鼠标穿透控制逻辑
        // ══════════════════════════════════════════════
        private void PassThroughBtn_Click(object sender, RoutedEventArgs e)
        {
            TogglePassThroughState();
            _tray?.SyncPassThroughMenuItem();
        }

        internal void TogglePassThroughFromTray()
        {
            TogglePassThroughState();
        }

        private void TogglePassThroughState()
        {
            _isPassThrough = !_isPassThrough;
            ApplyPassThroughState(_isPassThrough);
            SaveSettings();
        }

        private DispatcherTimer? _passThroughMonitorTimer;
        private bool _isPassThroughHovered = false;

        private void StartPassThroughMonitor()
        {
            if (_passThroughMonitorTimer == null)
            {
                _passThroughMonitorTimer = new DispatcherTimer(DispatcherPriority.Normal)
                {
                    Interval = TimeSpan.FromMilliseconds(40)
                };
                _passThroughMonitorTimer.Tick += PassThroughMonitorTimer_Tick;
            }
            _passThroughMonitorTimer.Start();
        }

        private void StopPassThroughMonitor()
        {
            _passThroughMonitorTimer?.Stop();
            _isPassThroughHovered = false;
        }

        private void PassThroughMonitorTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isPassThrough || !IsVisible || WindowState == WindowState.Minimized)
            {
                StopPassThroughMonitor();
                return;
            }

            // 若当前有正在交互的弹窗或偏好设置窗口，保持控制按钮可点击状态，暂不恢复穿透
            if (IsPreferencesWindowOpen || ActiveEditDialog != null)
            {
                return;
            }

            if (!GetCursorPos(out POINT screenPoint)) return;

            WinPoint relPos;
            try
            {
                relPos = PointFromScreen(new WinPoint(screenPoint.X, screenPoint.Y));
            }
            catch
            {
                return;
            }

            // 顶部区域：涵盖 TitleBar 范围（高度 40px，向外宽容 4px 缓冲区以便顺滑移入）
            double barHeight = TitleBar.ActualHeight > 0 ? TitleBar.ActualHeight : 40;
            bool inTitleBarZone = (relPos.X >= 0 && relPos.X <= ActualWidth && relPos.Y >= 0 && relPos.Y <= barHeight + 4);

            if (inTitleBarZone)
            {
                if (!_isPassThroughHovered)
                {
                    _isPassThroughHovered = true;
                    // 1. 自动浮现顶部控制按钮
                    SetTitleButtonsOpacity(1);
                    // 2. 临时剥除 WS_EX_TRANSPARENT 穿透样式，使控制按钮能够正常接收点击与 Hover
                    SetWindowPassThrough(false);
                }
            }
            else
            {
                if (_isPassThroughHovered)
                {
                    _isPassThroughHovered = false;
                    // 1. 自动隐藏顶部控制按钮
                    SetTitleButtonsOpacity(0);
                    // 2. 恢复 WS_EX_TRANSPARENT 鼠标穿透样式
                    SetWindowPassThrough(true);
                }
            }
        }

        private void SetWindowPassThrough(bool transparent)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (transparent)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
            }
        }

        private void ApplyPassThroughState(bool isPassThrough)
        {
            _isPassThrough = isPassThrough;
            PassThroughBtn.Content = _isPassThrough ? "◉" : "⊙";
            if (_isPassThrough)
            {
                ResizeGripArea.Opacity = 0;
                // 开启穿透模式时，必须默认置顶，防止穿透点击下层窗口时导致 TodoList 被下层窗口覆盖
                if (!_isPinned)
                {
                    _isPinned = true;
                    this.Topmost = true;
                    UpdatePinButton();
                    _tray?.SyncPinMenuItem();
                }
            }

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                if (_isPassThrough)
                {
                    SetTitleButtonsOpacity(0);
                    SetWindowPassThrough(true);
                    StartPassThroughMonitor();
                }
                else
                {
                    StopPassThroughMonitor();
                    SetWindowPassThrough(false);
                }
            }
        }

        // ══════════════════════════════════════════════
        // 右下角自定义 ResizeGrip
        // ══════════════════════════════════════════════
        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isResizing   = true;
            _resizeStart  = e.GetPosition(null);
            _resizeStartW = Width;
            _resizeStartH = Height;
            ((UIElement)sender).CaptureMouse();
            ((UIElement)sender).MouseMove        += ResizeGrip_MouseMove;
            ((UIElement)sender).MouseLeftButtonUp += ResizeGrip_MouseLeftButtonUp;
            e.Handled = true;
        }

        private void ResizeGrip_MouseMove(object sender, WinMouse e)
        {
            if (!_isResizing) return;
            var pos   = e.GetPosition(null);
            var delta = pos - _resizeStart;
            
            double newW = Math.Max(MinWidth, _resizeStartW + delta.X);
            double newH = Math.Max(MinHeight, _resizeStartH + delta.Y);

            var area = GetCurrentScreenWorkArea();
            if (Left + newW > area.Right)
            {
                newW = area.Right - Left;
            }
            if (Top + newH > area.Bottom)
            {
                newH = area.Bottom - Top;
            }

            Width  = newW;
            Height = newH;
        }

        public bool IsPreferencesWindowOpen { get; set; } = false;
        public EditDialog? ActiveEditDialog { get; set; } = null;

        private Rect GetCurrentScreenWorkArea()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
            var area = screen.WorkingArea;

            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            double dpiScaleX = dpi.DpiScaleX;
            double dpiScaleY = dpi.DpiScaleY;

            return new Rect(
                area.Left / dpiScaleX,
                area.Top / dpiScaleY,
                area.Width / dpiScaleX,
                area.Height / dpiScaleY
            );
        }

        private void ResizeGrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isResizing = false;
            ((UIElement)sender).ReleaseMouseCapture();
            ((UIElement)sender).MouseMove        -= ResizeGrip_MouseMove;
            ((UIElement)sender).MouseLeftButtonUp -= ResizeGrip_MouseLeftButtonUp;
            SaveWindowBounds();
        }

        // ══════════════════════════════════════════════
        // 待办创建与编辑统一弹窗呼出
        // ══════════════════════════════════════════════
        private void ShowCreateTodoDialog()
        {
            if (IsPreferencesWindowOpen) return;

            var dlg = new EditDialog("新建待办", "") { Owner = this };
            ActiveEditDialog = dlg;
            try
            {
                if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.ResultText))
                {
                    AddTodoItem(dlg.ResultText);
                }
            }
            finally
            {
                ActiveEditDialog = null;
            }
        }

        private void TodoList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IsPreferencesWindowOpen) return;

            var src = e.OriginalSource as DependencyObject;
            while (src != null)
            {
                if (src is System.Windows.Controls.Button) return;
                src = System.Windows.Media.VisualTreeHelper.GetParent(src);
            }

            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is TodoItem item)
            {
                ShowEditDialog(item);
                return;
            }

            ShowCreateTodoDialog();
        }

        public void HideInlineInput()
        {
            // 内联输入已全面统一为独立弹窗，保留此方法兼容外部托盘调用
            if (ActiveEditDialog != null)
            {
                ActiveEditDialog.Close();
                ActiveEditDialog = null;
            }
        }

        public void HideInlineInputAndWindow()
        {
            HideInlineInput();
            SafeHide();
        }

        private void UpdateEmptyPlaceholder()
        {
            EmptyPlaceholder.Visibility =
                _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════
        // 编辑 / 删除 / 恢复
        // ══════════════════════════════════════════════
        private void EditBtn_Click(object sender, RoutedEventArgs e)
        {
            if (IsPreferencesWindowOpen) return;

            var id   = (Guid)((WinButton)sender).Tag;
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null) ShowEditDialog(item);
        }

        public void AddTodoItem(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            _items.Add(new TodoItem { Title = title });
            _todoService.SaveTodos(_items);
            if (_items.Count > 0)
                TodoList.ScrollIntoView(_items[^1]);
        }

        /// <summary>
        /// 从回收站还原待办事项（保持原始 ID 和创建时间）
        /// </summary>
        public void RestoreTodoItem(TodoItem restoredItem)
        {
            if (restoredItem == null || string.IsNullOrWhiteSpace(restoredItem.Title)) return;
            _items.Add(restoredItem);
            _todoService.SaveTodos(_items);
            if (_items.Count > 0)
                TodoList.ScrollIntoView(_items[^1]);
        }

        private HistoryWindow? _historyWindow;

        public void SetHistoryWindow(HistoryWindow historyWindow)
        {
            _historyWindow = historyWindow;
        }

        public void DeleteTodoItem(TodoItem item)
        {
            if (item == null) return;
            _recycleBinService.AddToRecycleBin(item);
            _items.Remove(item);
            _todoService.SaveTodos(_items);

            _historyWindow?.Dispatcher.Invoke(() =>
            {
                if (_historyWindow.IsVisible)
                {
                    _historyWindow.ReloadRecycleBin();
                }
            });
        }

        private void ShowEditDialog(TodoItem item)
        {
            if (IsPreferencesWindowOpen) return;

            var dlg = new EditDialog("编辑待办", item.Title) { Owner = this };
            ActiveEditDialog = dlg;
            try
            {
                if (dlg.ShowDialog() == true)
                {
                    if (string.IsNullOrWhiteSpace(dlg.ResultText))
                    {
                        // 空值即删除
                        DeleteTodoItem(item);
                    }
                    else
                    {
                        item.Title = dlg.ResultText;
                        _todoService.SaveTodos(_items);
                    }
                }
            }
            finally
            {
                ActiveEditDialog = null;
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var id   = (Guid)((WinButton)sender).Tag;
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                DeleteTodoItem(item);
            }
        }

        // ══════════════════════════════════════════════
        // 拖拽排序与平滑位移动画
        // ══════════════════════════════════════════════
        private bool IsClickOnInteractiveControl(DependencyObject? src)
        {
            while (src != null && src != TodoList)
            {
                if (src is System.Windows.Controls.Button || src is System.Windows.Controls.Primitives.ButtonBase)
                    return true;
                src = System.Windows.Media.VisualTreeHelper.GetParent(src);
            }
            return false;
        }

        private ScrollViewer? GetTodoScrollViewer()
        {
            if (_todoScrollViewer != null) return _todoScrollViewer;
            if (TodoList.Template != null)
            {
                _todoScrollViewer = TodoList.Template.FindName("PART_ScrollViewer", TodoList) as ScrollViewer;
            }
            if (_todoScrollViewer == null)
            {
                _todoScrollViewer = FindVisualChild<ScrollViewer>(TodoList);
            }
            return _todoScrollViewer;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                var descendant = FindVisualChild<T>(child);
                if (descendant != null) return descendant;
            }
            return null;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void StartAutoScrollTimer()
        {
            if (_autoScrollTimer == null)
            {
                _autoScrollTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(20)
                };
                _autoScrollTimer.Tick += AutoScrollTimer_Tick;
            }
            _autoScrollTimer.Start();
        }

        private void StopAutoScrollTimer()
        {
            _autoScrollVelocity = 0;
            _autoScrollTimer?.Stop();
        }

        private void AutoScrollTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isDragging || _autoScrollVelocity == 0) return;

            var sv = GetTodoScrollViewer();
            if (sv == null || sv.ScrollableHeight <= 0) return;

            double oldOffset = sv.VerticalOffset;
            double newOffset = Math.Clamp(oldOffset + _autoScrollVelocity, 0, sv.ScrollableHeight);
            if (Math.Abs(newOffset - oldOffset) > 0.1)
            {
                sv.ScrollToVerticalOffset(newOffset);
                UpdateDragPositionAndSlots();
            }
        }

        private void CheckAutoScrollZone(WinPoint currentPos)
        {
            const double edgeThreshold = 36.0;
            double listHeight = TodoList.ActualHeight;

            if (listHeight <= 0)
            {
                _autoScrollVelocity = 0;
                return;
            }

            if (currentPos.Y < edgeThreshold)
            {
                // 向上滚动：像素步长 2px ~ 9px
                double d = edgeThreshold - currentPos.Y;
                _autoScrollVelocity = -Math.Min(9.0, Math.Max(2.0, d * 0.25));
            }
            else if (currentPos.Y > listHeight - edgeThreshold)
            {
                // 向下滚动：像素步长 2px ~ 9px
                double d = currentPos.Y - (listHeight - edgeThreshold);
                _autoScrollVelocity = Math.Min(9.0, Math.Max(2.0, d * 0.25));
            }
            else
            {
                _autoScrollVelocity = 0;
            }
        }

        private void TodoList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsClickOnInteractiveControl(e.OriginalSource as DependencyObject))
            {
                _dragItem = null;
                _dragContainer = null;
                return;
            }

            var container = FindAncestor<System.Windows.Controls.ListViewItem>(e.OriginalSource as DependencyObject);
            if (container != null && container.DataContext is TodoItem item)
            {
                _dragItem = item;
                _dragContainer = container;
                _dragStartPos = e.GetPosition(TodoList);
                _lastMousePos = _dragStartPos;
                _isDragging = false;
                _dragStartIndex = _items.IndexOf(item);
                _targetDropIndex = _dragStartIndex;

                _dragGrabOffsetY = e.GetPosition(_dragContainer).Y;
                _dragStartContainerTopInList = _dragContainer.TranslatePoint(new WinPoint(0, 0), TodoList).Y;

                var sv = GetTodoScrollViewer();
                _dragStartScrollOffset = sv?.VerticalOffset ?? 0;
            }
        }

        private void TodoList_PreviewMouseMove(object sender, WinMouse e)
        {
            if (_dragItem == null || _dragContainer == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var currentPos = e.GetPosition(TodoList);
            _lastMousePos = currentPos;
            var deltaY = currentPos.Y - _dragStartPos.Y;

            // 移动超过阈值才启动拖拽状态，保护正常单击与双击
            if (!_isDragging && Math.Abs(deltaY) > 4)
            {
                _isDragging = true;
                TodoList.CaptureMouse();
                WinPanel.SetZIndex(_dragContainer, 999);
                _dragContainer.Opacity = 0.88;

                var sv = GetTodoScrollViewer();
                _dragStartScrollOffset = sv?.VerticalOffset ?? 0;
                _dragStartContainerTopInList = _dragContainer.TranslatePoint(new WinPoint(0, 0), TodoList).Y;

                // 确保所有容器都有全新的、未冻结的独立 TranslateTransform，并一次性缓存初始几何坐标
                _itemInitialBounds.Clear();
                for (int i = 0; i < _items.Count; i++)
                {
                    var container = TodoList.ItemContainerGenerator.ContainerFromItem(_items[i]) as System.Windows.Controls.ListViewItem;
                    if (container != null)
                    {
                        if (container.RenderTransform is not TranslateTransform trans || trans.IsFrozen)
                        {
                            container.RenderTransform = new TranslateTransform(0, 0);
                        }
                        double top = container.TranslatePoint(new WinPoint(0, 0), TodoList).Y;
                        _itemInitialBounds.Add((top, container.ActualHeight));
                    }
                    else
                    {
                        _itemInitialBounds.Add((0, 0));
                    }
                }

                StartAutoScrollTimer();
            }

            if (!_isDragging) return;

            // 检查边缘自动滚动感应区
            CheckAutoScrollZone(currentPos);

            // 更新被拖拽项坐标与插槽避让动画
            UpdateDragPositionAndSlots(currentPos);
        }

        private void UpdateDragPositionAndSlots(WinPoint? mousePos = null)
        {
            if (!_isDragging || _dragContainer == null || _dragItem == null) return;

            var currentPos = mousePos ?? _lastMousePos;
            var sv = GetTodoScrollViewer();
            double currentScrollOffset = sv?.VerticalOffset ?? 0;

            // 1. 计算视觉目标 Top 并进行严格边界钳制 (Clamp)
            // 目标：让被拖动物项完全在 [0, TodoList.ActualHeight - itemHeight] 之间，绝不飞出可视区域
            double desiredVisualTop = currentPos.Y - _dragGrabOffsetY;
            double maxVisualTop = Math.Max(0, TodoList.ActualHeight - _dragContainer.ActualHeight);
            double clampedVisualTop = Math.Clamp(desiredVisualTop, 0, maxVisualTop);

            // 2. 根据当前 ScrollViewer 滚动物理偏移，计算出当前条目无 Transform 时的实际 Top
            double scrollDelta = currentScrollOffset - _dragStartScrollOffset;
            double currentBaseTop = _dragStartContainerTopInList - scrollDelta;
            double transformY = clampedVisualTop - currentBaseTop;

            if (_dragContainer.RenderTransform is TranslateTransform dragTransform && !dragTransform.IsFrozen)
            {
                dragTransform.Y = transformY;
            }
            else
            {
                _dragContainer.RenderTransform = new TranslateTransform(0, transformY);
            }

            // 3. 计算目标插入插槽（Target Drop Index）
            // 关键策略：
            // A. 顶部贴边吸附：当拖动到最顶部区域，直接锁定第 0 项！
            // B. 底部贴边吸附：当拖动到最底部区域，直接锁定末项！
            // C. 中间区域：利用缓存坐标线性计算基准中心，只要拖拽项中心跨越目标项中线（50%），立刻响应避让，零延迟不卡顿！

            int newTargetIndex = _dragStartIndex;

            bool isAtTopEdge = clampedVisualTop <= 6 || (currentPos.Y <= 28 && (sv == null || sv.VerticalOffset <= 2));
            bool isAtBottomEdge = (clampedVisualTop >= maxVisualTop - 6 && maxVisualTop > 0) || 
                                  (currentPos.Y >= TodoList.ActualHeight - 28 && (sv == null || sv.VerticalOffset >= (sv.ScrollableHeight - 2)));

            if (isAtTopEdge)
            {
                newTargetIndex = 0;
            }
            else if (isAtBottomEdge)
            {
                newTargetIndex = _items.Count - 1;
            }
            else
            {
                double dragCenterY = clampedVisualTop + _dragContainer.ActualHeight / 2;

                for (int i = 0; i < _items.Count; i++)
                {
                    if (i == _dragStartIndex || i >= _itemInitialBounds.Count) continue;

                    // 高性能基准中心计算：消除高频 TranslatePoint 矩阵变换与虚假动画干扰
                    double otherBaseCenterY = _itemInitialBounds[i].Top - scrollDelta + _itemInitialBounds[i].Height / 2;

                    if (i < _dragStartIndex)
                    {
                        // 向上跨越：只要拖拽中心越过对方中线，立即让位
                        if (dragCenterY < otherBaseCenterY)
                        {
                            newTargetIndex = Math.Min(newTargetIndex, i);
                        }
                    }
                    else if (i > _dragStartIndex)
                    {
                        // 向下跨越：只要拖拽中心越过对方中线，立即让位
                        if (dragCenterY > otherBaseCenterY)
                        {
                            newTargetIndex = Math.Max(newTargetIndex, i);
                        }
                    }
                }
            }

            // 4. 当目标插槽改变时，触发平滑的避让挤开动画
            if (newTargetIndex != _targetDropIndex)
            {
                _targetDropIndex = newTargetIndex;
                double itemHeight = _dragContainer.ActualHeight + 4; // 包含上下 Margin

                for (int i = 0; i < _items.Count; i++)
                {
                    if (i == _dragStartIndex) continue;
                    var container = TodoList.ItemContainerGenerator.ContainerFromItem(_items[i]) as System.Windows.Controls.ListViewItem;
                    if (container == null) continue;

                    double targetOffsetY = 0;
                    if (_targetDropIndex > _dragStartIndex)
                    {
                        // 向下拖动：跨过的项向上挪出空位
                        if (i > _dragStartIndex && i <= _targetDropIndex)
                        {
                            targetOffsetY = -itemHeight;
                        }
                    }
                    else if (_targetDropIndex < _dragStartIndex)
                    {
                        // 向上拖动：跨过的项向下挪出空位
                        if (i >= _targetDropIndex && i < _dragStartIndex)
                        {
                            targetOffsetY = itemHeight;
                        }
                    }

                    if (container.RenderTransform is not TranslateTransform trans || trans.IsFrozen)
                    {
                        trans = new TranslateTransform(0, 0);
                        container.RenderTransform = trans;
                    }

                    var anim = new DoubleAnimation(targetOffsetY, new Duration(TimeSpan.FromMilliseconds(160)))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    trans.BeginAnimation(TranslateTransform.YProperty, anim);
                }
            }
        }

        private void ResetAllDragVisuals()
        {
            StopAutoScrollTimer();
            foreach (var it in _items)
            {
                var container = TodoList.ItemContainerGenerator.ContainerFromItem(it) as System.Windows.Controls.ListViewItem;
                if (container != null)
                {
                    WinPanel.SetZIndex(container, 0);
                    container.Opacity = 1.0;
                    if (container.RenderTransform is TranslateTransform trans && !trans.IsFrozen)
                    {
                        trans.BeginAnimation(TranslateTransform.YProperty, null);
                        trans.Y = 0;
                    }
                    container.RenderTransform = new TranslateTransform(0, 0);
                }
            }
        }

        private void TodoList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                _dragItem = null;
                _dragContainer = null;
                return;
            }

            int fromIndex = _dragStartIndex;
            int toIndex = _targetDropIndex;
            var dragItem = _dragItem;

            // 关键：必须在 ReleaseMouseCapture 之前将 _isDragging 设为 false，
            // 避免 Release 激发的 LostMouseCapture 事件将目标索引意外重置为 -1
            _isDragging = false;
            _dragItem = null;
            _dragContainer = null;
            _dragStartIndex = -1;
            _targetDropIndex = -1;
            _itemInitialBounds.Clear();

            TodoList.ReleaseMouseCapture();

            // 1. 彻底清除并复位所有容器上的动画与偏移，确保绝无任何残留 Transform
            ResetAllDragVisuals();

            // 2. 执行数据重排与保存
            if (toIndex >= 0 && toIndex != fromIndex && toIndex < _items.Count && dragItem != null)
            {
                _items.Move(fromIndex, toIndex);
                _todoService.SaveTodos(_items);
                AnimateItemFadeIn(dragItem);
            }

            e.Handled = true;
        }

        private void AnimateItemFadeIn(TodoItem item)
        {
            var container = TodoList.ItemContainerGenerator.ContainerFromItem(item) as System.Windows.Controls.ListViewItem;
            if (container == null) return;
            var fade = new DoubleAnimation(0.5, 1.0, new Duration(TimeSpan.FromMilliseconds(200)));
            container.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void LoadSettings()
        {
            var settings = _settingsManager.Settings;
            _isPinned = settings.TodoIsPinned;
            _isPassThrough = settings.TodoIsPassThrough;

            // 应用置顶状态
            this.Topmost = _isPinned;
            UpdatePinButton();

            // 应用穿透状态
            ApplyPassThroughState(_isPassThrough);
        }

        private void SaveSettings()
        {
            var settings = _settingsManager.Settings;
            settings.TodoIsPinned = _isPinned;
            settings.TodoIsPassThrough = _isPassThrough;
            _settingsManager.SaveSettings(settings);
        }

        // ══════════════════════════════════════════════
        // 置顶显示快捷键：
        // 按下快捷键 -> 检查是否处于置顶状态
        // - 置顶状态 -> 取消置顶
        // - 取消置顶状态 -> 置顶 + 取消穿透
        // ══════════════════════════════════════════════
        public void ToggleTopmostAndPassThrough()
        {
            if (!IsVisible)
            {
                Show();
            }

            if (_isPinned)
            {
                // 当前处于置顶状态 -> 取消置顶
                _isPinned = false;
                this.Topmost = false;
                UpdatePinButton();
            }
            else
            {
                // 当前处于非置顶状态 -> 置顶 + 取消穿透
                _isPinned = true;
                this.Topmost = true;
                UpdatePinButton();

                // 取消穿透（若处于穿透状态）
                if (_isPassThrough)
                {
                    ApplyPassThroughState(false);
                }
            }

            SetTitleButtonsOpacity(0);
            Activate();
            _tray?.SyncPinMenuItem();
            _tray?.SyncPassThroughMenuItem();
            SaveSettings();
        }

        // ══════════════════════════════════════════════
        // 窗口大小与位置记忆 / 恢复 / 越界保护
        // ══════════════════════════════════════════════
        private void ApplyInitialOrSavedBounds()
        {
            var settings = _settingsManager.Settings;
            if (settings.TodoWindowLeft.HasValue &&
                settings.TodoWindowTop.HasValue &&
                settings.TodoWindowWidth.HasValue &&
                settings.TodoWindowHeight.HasValue)
            {
                double savedLeft   = settings.TodoWindowLeft.Value;
                double savedTop    = settings.TodoWindowTop.Value;
                double savedWidth  = Math.Max(MinWidth, settings.TodoWindowWidth.Value);
                double savedHeight = Math.Max(MinHeight, settings.TodoWindowHeight.Value);

                if (IsWindowBoundsVisible(savedLeft, savedTop, savedWidth, savedHeight))
                {
                    Width  = savedWidth;
                    Height = savedHeight;
                    Left   = savedLeft;
                    Top    = savedTop;
                    return;
                }
            }

            // 首次启动或保存的坐标已在屏幕外（如外接显示器断开），恢复默认右上角
            ResetToDefaultPosition(false);
        }

        private bool IsWindowBoundsVisible(double left, double top, double width, double height)
        {
            try
            {
                // 1. 全局虚拟屏幕碰撞检测（DIP 逻辑像素）
                var virtualRect = new Rect(
                    SystemParameters.VirtualScreenLeft,
                    SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenWidth,
                    SystemParameters.VirtualScreenHeight);

                var winRect = new Rect(left, top, width, height);
                winRect.Intersect(virtualRect);

                // 窗口在可视区域内至少有 60x40 像素的有效面积（确保标题栏可点击）
                if (winRect.IsEmpty || winRect.Width < 60 || winRect.Height < 40)
                {
                    return false;
                }

                // 2. 真实活动屏幕探测（针对多显示器异形布局盲区）
                var probePoint = new System.Drawing.Point((int)left + 30, (int)top + 20);
                var screen = System.Windows.Forms.Screen.FromPoint(probePoint);
                if (screen != null && screen.Bounds.Contains(probePoint))
                {
                    return true;
                }

                var centerPoint = new System.Drawing.Point((int)(left + width / 2), (int)(top + height / 2));
                var centerScreen = System.Windows.Forms.Screen.FromPoint(centerPoint);
                if (centerScreen != null && centerScreen.Bounds.Contains(centerPoint))
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }

            return false;
        }

        public void ResetToDefaultPosition(bool save = true)
        {
            var area = SystemParameters.WorkArea;
            Width  = 280;
            Height = Width * 1.3;
            Left   = area.Right - Width - 20;
            Top    = area.Top + 20;

            if (save)
            {
                SaveWindowBounds();
            }
        }

        public void SaveWindowBounds()
        {
            if (double.IsNaN(Left) || double.IsNaN(Top) || double.IsNaN(Width) || double.IsNaN(Height) ||
                double.IsInfinity(Left) || double.IsInfinity(Top) || double.IsInfinity(Width) || double.IsInfinity(Height))
            {
                return;
            }

            var settings = _settingsManager.Settings;
            settings.TodoWindowLeft   = Left;
            settings.TodoWindowTop    = Top;
            settings.TodoWindowWidth  = Math.Max(MinWidth, Width);
            settings.TodoWindowHeight = Math.Max(MinHeight, Height);
            _settingsManager.SaveSettings(settings);
        }

        /// <summary>
        /// 确保待办数据立即刷盘
        /// </summary>
        public void Flush()
        {
            _todoService.Flush();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowBounds();
            _todoService.Flush();
            if (App.IsExiting)
            {
                base.OnClosing(e);
                return;
            }
            e.Cancel = true;
            SafeHide();
        }
    }
}
