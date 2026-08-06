using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinKit.Todo.Models;
using WinKit.Todo.Services;
using WinButton = System.Windows.Controls.Button;

namespace WinKit.Todo
{
    public partial class HistoryWindow : Window
    {
        private readonly HistoryStorage _storage;
        private readonly ObservableCollection<HistoryItem> _items = new();
        private readonly MainWindow _todoWindow;

        public HistoryWindow(MainWindow todoWindow)
        {
            InitializeComponent();
            _todoWindow = todoWindow;
            _storage = new HistoryStorage();

            HistoryList.ItemsSource = _items;
            _items.CollectionChanged += (s, e) => UpdateUIStates();

            Loaded += (s, e) => ReloadHistory();
        }

        public void ReloadHistory()
        {
            _items.Clear();
            foreach (var item in _storage.LoadHistory())
            {
                _items.Add(item);
            }
            UpdateUIStates();
        }

        private void UpdateUIStates()
        {
            CountText.Text = $"{_items.Count} 条记录";
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

            if (System.Windows.MessageBox.Show("确定清空全部历史记录吗？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _items.Clear();
                _storage.ClearHistory();
            }
        }

        private void RestoreBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WinButton btn && btn.Tag is HistoryItem item)
            {
                _todoWindow.AddTodoItem(item.Title);
                _items.Remove(item);
                _storage.SaveHistory(_items);
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WinButton btn && btn.Tag is HistoryItem item)
            {
                _items.Remove(item);
                _storage.SaveHistory(_items);
            }
        }
    }
}
