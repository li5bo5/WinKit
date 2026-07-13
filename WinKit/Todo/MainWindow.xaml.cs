using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WinKit.Common;
using WinKit.Todo.Models;
using WinKit.Todo.Services;
using WinPoint       = System.Windows.Point;
using WinKey         = System.Windows.Input.KeyEventArgs;
using WinMouse       = System.Windows.Input.MouseEventArgs;
using WinDrag        = System.Windows.DragEventArgs;
using WinButton      = System.Windows.Controls.Button;
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
        private readonly MarkdownStorage _storage;
        private readonly SettingsManager _settingsManager;

        // 拖拽排序
        private WinPoint  _dragStart;
        private TodoItem? _dragItem;

        // 窗口调整大小
        private bool _isResizing;
        private WinPoint _resizeStart;
        private double   _resizeStartW, _resizeStartH;

        // 置顶 / 穿透（独立状态）
        private bool _isPinned      = false;
        private bool _isPassThrough = false;
        private System.Windows.Threading.DispatcherTimer? _passThroughTimer;

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

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        // ══════════════════════════════════════════════
        // 构造函数
        // ══════════════════════════════════════════════
        public MainWindow(SettingsManager settingsManager)
        {
            InitializeComponent();
            _settingsManager = settingsManager;

            // 确保窗口句柄创建，以便后续初始化设置和 Win32 钩子正常运行
            new WindowInteropHelper(this).EnsureHandle();

            _storage = new MarkdownStorage();
            foreach (var item in _storage.LoadTodos())
                _items.Add(item);
            TodoList.ItemsSource = _items;

            // 监听集合变化，同步空列表占位符
            _items.CollectionChanged += (s, e) => UpdateEmptyPlaceholder();

            // 初始位置：右上角
            var area = SystemParameters.WorkArea;
            Left   = area.Right - Width - 20;
            Height = Width * 1.3;
            Top    = area.Top + 20;

            // 载入并应用保存的设置
            LoadSettings();

            // 初始化占位符状态
            UpdateEmptyPlaceholder();

            // 根据置顶或穿透状态决定标题栏按钮初始透明度
            Loaded += (s, e) => { 
                UpdateEmptyPlaceholder(); 
                SetTitleButtonsOpacity((_isPinned || _isPassThrough) ? 1 : 0); 
            };
        }

        // ══════════════════════════════════════════════
        // 窗口初始化：挂钩 WndProc（用于穿透 hit-test）
        // ══════════════════════════════════════════════
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource?.AddHook(WndProc);

            // 如果初始加载的设置中启用了鼠标穿透，立即应用穿透样式
            if (_isPassThrough)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
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

                // 限制窗口范围在当前屏幕工作区内
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

        // ══════════════════════════════════════════════
        // TitleBar 区域悬停：控制 TitleBar 按钮显示
        // ══════════════════════════════════════════════
        private void TitleBar_MouseEnter(object sender, WinMouse e) => SetTitleButtonsOpacity(1);
        private void TitleBar_MouseLeave(object sender, WinMouse e)
        {
            if (!_isPinned && !_isPassThrough) SetTitleButtonsOpacity(0);
        }

        private void SetTitleButtonsOpacity(double opacity)
        {
            PinBtn.Opacity         = opacity;
            PassThroughBtn.Opacity = opacity;
            CloseBtn.Opacity       = opacity;
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
            if (e.ClickCount == 1) DragMove();
        }

        // ══════════════════════════════════════════════
        // 标题栏按钮
        // ══════════════════════════════════════════════
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

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

            if (_isPinned)
            {
                PinBtn.Content = "📍";
                PinBtn.ToolTip = "取消置顶";
                SetTitleButtonsOpacity(1);
            }
            else
            {
                PinBtn.Content = "📌";
                PinBtn.ToolTip = "置顶";
                if (!_isPassThrough) SetTitleButtonsOpacity(0);
            }
            SaveSettings();
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

            if (_isPassThrough)
            {
                PassThroughBtn.Content = "◉";
                PassThroughBtn.ToolTip = "关闭鼠标穿透";
                SetTitleButtonsOpacity(1);
                ResizeGripArea.Opacity = 0;
                
                StartPassThroughTimer();
            }
            else
            {
                PassThroughBtn.Content = "⊙";
                PassThroughBtn.ToolTip = "开启鼠标穿透";
                if (!_isPinned) SetTitleButtonsOpacity(0);
                
                StopPassThroughTimer();
                
                var hwnd = new WindowInteropHelper(this).Handle;
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
            }
            SaveSettings();
        }

        private void StartPassThroughTimer()
        {
            if (_passThroughTimer == null)
            {
                _passThroughTimer = new System.Windows.Threading.DispatcherTimer();
                _passThroughTimer.Interval = TimeSpan.FromMilliseconds(50);
                _passThroughTimer.Tick += PassThroughTimer_Tick;
            }
            _passThroughTimer.Start();
        }

        private void StopPassThroughTimer()
        {
            _passThroughTimer?.Stop();
        }

        private void PassThroughTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isPassThrough) return;

            POINT mousePos;
            GetCursorPos(out mousePos);

            bool isOverButton = IsOverInteractiveButton((short)mousePos.X, (short)mousePos.Y);

            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (isOverButton)
            {
                if ((extendedStyle & WS_EX_TRANSPARENT) != 0)
                {
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
                }
            }
            else
            {
                if ((extendedStyle & WS_EX_TRANSPARENT) == 0)
                {
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
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
        }

        // ══════════════════════════════════════════════
        // 双击空白处：显示内联输入框
        // ══════════════════════════════════════════════
        private void Window_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (InlineInputArea.Visibility != Visibility.Visible) return;

            var src = e.OriginalSource as DependencyObject;
            while (src != null)
            {
                if (src == PinBtn || src == PassThroughBtn || src == CloseBtn)
                {
                    return;
                }
                if (src == InlineEditBox)
                {
                    return;
                }
                src = System.Windows.Media.VisualTreeHelper.GetParent(src);
            }

            CommitInlineInput();
            e.Handled = true;
        }

        private void TodoList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
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

            ShowInlineInput();
        }

        private void ShowInlineInput()
        {
            InlineInputArea.Visibility = Visibility.Visible;
            InlineEditBox.Text         = string.Empty;
            InlineEditBox.Focus();
        }

        private void HideInlineInput()
        {
            InlineInputArea.Visibility = Visibility.Collapsed;
            InlineEditBox.Text         = string.Empty;
        }

        private void InlineEditBox_KeyDown(object sender, WinKey e)
        {
            if (e.Key == Key.Enter)
            {
                CommitInlineInput();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HideInlineInput();
                e.Handled = true;
            }
        }

        private void InlineEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            HideInlineInput();
        }

        private void CommitInlineInput()
        {
            var text = InlineEditBox.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                _items.Add(new TodoItem { Title = text });
                _storage.SaveTodos(_items);
                if (_items.Count > 0)
                    TodoList.ScrollIntoView(_items[^1]);
            }
            HideInlineInput();
        }

        private void UpdateEmptyPlaceholder()
        {
            EmptyPlaceholder.Visibility =
                _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════
        // 编辑 / 删除
        // ══════════════════════════════════════════════
        private void EditBtn_Click(object sender, RoutedEventArgs e)
        {
            var id   = (Guid)((WinButton)sender).Tag;
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null) ShowEditDialog(item);
        }

        private void ShowEditDialog(TodoItem item)
        {
            var dlg = new EditDialog(item.Title) { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.ResultText))
            {
                item.Title = dlg.ResultText;
                var idx = _items.IndexOf(item);
                _items.RemoveAt(idx);
                _items.Insert(idx, item);
                _storage.SaveTodos(_items);
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var id   = (Guid)((WinButton)sender).Tag;
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null) { _items.Remove(item); _storage.SaveTodos(_items); }
        }

        // ══════════════════════════════════════════════
        // 拖拽排序
        // ══════════════════════════════════════════════
        private void TodoList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            _dragItem  = (e.OriginalSource as FrameworkElement)?.DataContext as TodoItem;
        }

        private void TodoList_PreviewMouseMove(object sender, WinMouse e)
        {
            if (_dragItem == null || e.LeftButton != MouseButtonState.Pressed) return;
            var pos   = e.GetPosition(null);
            var delta = pos - _dragStart;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            DragDrop.DoDragDrop(TodoList, _dragItem, WinDropEffects.Move);
            _dragItem = null;
        }

        private void TodoList_Drop(object sender, WinDrag e)
        {
            if (_dragItem == null) return;
            var target = (e.OriginalSource as FrameworkElement)?.DataContext as TodoItem;
            if (target == null || target == _dragItem) return;
            var oldIdx = _items.IndexOf(_dragItem);
            var newIdx = _items.IndexOf(target);
            if (oldIdx >= 0 && newIdx >= 0) { _items.Move(oldIdx, newIdx); _storage.SaveTodos(_items); }
        }

        // ══════════════════════════════════════════════
        // 按钮物理像素命中测试
        // ══════════════════════════════════════════════
        private bool IsOverInteractiveButton(short screenX, short screenY)
        {
            try
            {
                return IsScreenPointInElement(PinBtn, screenX, screenY) ||
                       IsScreenPointInElement(PassThroughBtn, screenX, screenY) ||
                       IsScreenPointInElement(CloseBtn, screenX, screenY);
            }
            catch
            {
                return false;
            }
        }

        private bool IsScreenPointInElement(System.Windows.UIElement element, short screenX, short screenY)
        {
            if (element == null || !element.IsVisible || element.Opacity == 0)
                return false;

            try
            {
                var ptScreen = element.PointToScreen(new System.Windows.Point(0, 0));
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(element);

                double left = ptScreen.X;
                double top = ptScreen.Y;
                double right = left + element.RenderSize.Width * dpi.DpiScaleX;
                double bottom = top + element.RenderSize.Height * dpi.DpiScaleY;

                return screenX >= left && screenX <= right &&
                       screenY >= top && screenY <= bottom;
            }
            catch
            {
                return false;
            }
        }

        private void LoadSettings()
        {
            var settings = _settingsManager.Settings;
            _isPinned = settings.TodoIsPinned;
            _isPassThrough = settings.TodoIsPassThrough;

            // 应用置顶状态
            this.Topmost = _isPinned;
            if (_isPinned)
            {
                PinBtn.Content = "📍";
                PinBtn.ToolTip = "取消置顶";
            }
            else
            {
                PinBtn.Content = "📌";
                PinBtn.ToolTip = "置顶";
            }

            // 应用穿透状态
            if (_isPassThrough)
            {
                PassThroughBtn.Content = "◉";
                PassThroughBtn.ToolTip = "关闭鼠标穿透";
                StartPassThroughTimer();
            }
            else
            {
                PassThroughBtn.Content = "⊙";
                PassThroughBtn.ToolTip = "开启鼠标穿透";
                StopPassThroughTimer();
            }
        }

        private void SaveSettings()
        {
            var settings = _settingsManager.Settings;
            settings.TodoIsPinned = _isPinned;
            settings.TodoIsPassThrough = _isPassThrough;
            _settingsManager.SaveSettings(settings);
        }
    }
}
