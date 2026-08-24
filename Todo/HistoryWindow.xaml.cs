using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WinKit.Common;
using WinKit.Todo.Models;
using WinKit.Todo.Services;
using WinButton = System.Windows.Controls.Button;
using WinMouse = System.Windows.Input.MouseEventArgs;

namespace WinKit.Todo
{
    public partial class HistoryWindow : Window
    {
        private readonly RecycleBinService _recycleBinService;
        private readonly ObservableCollection<RecycleBinItem> _items = new();
        private readonly MainWindow _todoWindow;

        // ResizeGrip 缩放拖动状态
        private bool _isResizing = false;
        private System.Windows.Point _resizeStart;
        private double _resizeStartW;
        private double _resizeStartH;

        public HistoryWindow(MainWindow todoWindow, SettingsManager settingsManager)
        {
            InitializeComponent();
            _todoWindow = todoWindow;
            _recycleBinService = new RecycleBinService(settingsManager);

            HistoryList.ItemsSource = _items;
            _items.CollectionChanged += (s, e) => UpdateUIStates();

            Loaded += (s, e) => ReloadRecycleBin();
        }

        public void ReloadHistory() => ReloadRecycleBin();

        public void ReloadRecycleBin()
        {
            _items.Clear();
            foreach (var item in _recycleBinService.LoadItems())
            {
                _items.Add(item);
            }
            UpdateUIStates();
        }

        private void UpdateUIStates()
        {
            CountText.Text = $"{_items.Count} 条已删除待办";
            EmptyPlaceholder.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ClearAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0) return;

            if (System.Windows.MessageBox.Show("确定清空全部待办历史记录吗？此操作不可撤销。", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _items.Clear();
                _recycleBinService.Clear();
            }
        }

        private void RestoreBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WinButton btn && btn.Tag is RecycleBinItem item)
            {
                // 还原待办：必须完整保持原 ID 和原始创建时间
                var restoredTodo = new TodoItem
                {
                    Id = item.Id,
                    Title = item.Title,
                    CreatedAt = item.CreatedAt
                };

                _todoWindow.RestoreTodoItem(restoredTodo);
                _items.Remove(item);
                _recycleBinService.SaveItems(_items);
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WinButton btn && btn.Tag is RecycleBinItem item)
            {
                _items.Remove(item);
                _recycleBinService.SaveItems(_items);
            }
        }

        // ══════════════════════════════════════════════
        // 右下角统一 ResizeGrip 缩放拖动
        // ══════════════════════════════════════════════
        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isResizing = true;
            _resizeStart = e.GetPosition(null);
            _resizeStartW = Width;
            _resizeStartH = Height;
            ((UIElement)sender).CaptureMouse();
            ((UIElement)sender).MouseMove += ResizeGrip_MouseMove;
            ((UIElement)sender).MouseLeftButtonUp += ResizeGrip_MouseLeftButtonUp;
            e.Handled = true;
        }

        private void ResizeGrip_MouseMove(object sender, WinMouse e)
        {
            if (!_isResizing) return;
            var pos = e.GetPosition(null);
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

            Width = newW;
            Height = newH;
        }

        private void ResizeGrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                ((UIElement)sender).ReleaseMouseCapture();
                ((UIElement)sender).MouseMove -= ResizeGrip_MouseMove;
                ((UIElement)sender).MouseLeftButtonUp -= ResizeGrip_MouseLeftButtonUp;
            }
        }

        private Rect GetCurrentScreenWorkArea()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            return new Rect(
                screen.WorkingArea.Left / dpi.DpiScaleX,
                screen.WorkingArea.Top / dpi.DpiScaleY,
                screen.WorkingArea.Width / dpi.DpiScaleX,
                screen.WorkingArea.Height / dpi.DpiScaleY);
        }
    }
}
