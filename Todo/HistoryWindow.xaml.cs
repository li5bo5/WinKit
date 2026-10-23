using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
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

        public HistoryWindow(MainWindow todoWindow, SettingsManager settingsManager)
        {
            InitializeComponent();
            _todoWindow = todoWindow;
            _recycleBinService = todoWindow.RecycleBinService;
            _recycleBinService.ItemsChanged += OnRecycleBinItemsChanged;

            HistoryList.ItemsSource = _items;
            _items.CollectionChanged += (s, e) => UpdateUIStates();

            NativeMethods.AttachResizeGrip(this, ResizeGripArea);

            Loaded += (s, e) => ReloadRecycleBin();
        }

        private void OnRecycleBinItemsChanged(object? sender, EventArgs e)
        {
            ReloadRecycleBin();
        }

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

        private void TitleBar_MouseEnter(object sender, WinMouse e) => CloseBtn.Opacity = 1;
        private void TitleBar_MouseLeave(object sender, WinMouse e) => CloseBtn.Opacity = 0;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) NativeMethods.DragMoveWithBounds(this);
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
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

        protected override void OnClosed(EventArgs e)
        {
            _recycleBinService.ItemsChanged -= OnRecycleBinItemsChanged;
            base.OnClosed(e);
        }
    }
}
