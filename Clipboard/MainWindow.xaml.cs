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
        private readonly SettingsManager _settingsManager;
        private bool _isResizing = false;
        private WinPoint _resizeStart;
        private double _resizeStartW, _resizeStartH;
        private Guid? _copiedItemId = null;
        private int _toastActiveCount = 0;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const byte KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public MainWindow(ClipboardManager clipboardManager, SettingsManager settingsManager)
        {
            InitializeComponent();
            _clipboardManager = clipboardManager;
            _settingsManager = settingsManager;

            ClipboardList.ItemsSource = _clipboardManager.Items;

            ((System.Collections.Specialized.INotifyCollectionChanged)_clipboardManager.Items).CollectionChanged += (s, e) =>
            {
                UpdateUIStates();
            };

            Loaded += (s, e) => UpdateUIStates();
        }

        private void UpdateUIStates()
        {
            int count = _clipboardManager.Items.Count;
            CountText.Text = $"{count} 项";
            EmptyPlaceholder.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 根据鼠标位置智能弹出并激活窗口，防边缘溢出
        /// </summary>
        public void ShowAtMouse()
        {
            POINT mousePos;
            GetCursorPos(out mousePos);

            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(mousePos.X, mousePos.Y));
            var area = screen.WorkingArea;

            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            double dpiScaleX = dpi.DpiScaleX;
            double dpiScaleY = dpi.DpiScaleY;

            double mouseX = mousePos.X / dpiScaleX;
            double mouseY = mousePos.Y / dpiScaleY;

            double workLeft = area.Left / dpiScaleX;
            double workTop = area.Top / dpiScaleY;
            double workRight = area.Right / dpiScaleX;
            double workBottom = area.Bottom / dpiScaleY;

            double targetLeft = mouseX + 10;
            double targetTop = mouseY + 10;

            if (targetLeft + Width > workRight)
            {
                targetLeft = mouseX - Width - 10;
            }
            if (targetLeft < workLeft)
            {
                targetLeft = workLeft;
            }

            if (targetTop + Height > workBottom)
            {
                targetTop = mouseY - Height - 10;
            }
            if (targetTop < workTop)
            {
                targetTop = workTop;
            }

            Left = targetLeft;
            Top = targetTop;

            Show();
            WindowState = WindowState.Normal;
            Activate();

            // 聚焦在列表容器上
            ClipboardList.Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        public void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }

        private void ClipboardList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ClipboardList.SelectedItem is ClipboardItem item)
            {
                UseSelectedItem(item);
            }
        }

        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.Content is ClipboardItem clipItem)
            {
                CopyItemToClipboard(clipItem);
            }
        }

        private void ClipboardList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ClipboardList.SelectedItem is ClipboardItem item)
                {
                    if (_copiedItemId == item.Id)
                    {
                        UseSelectedItem(item);
                    }
                    else
                    {
                        CopyItemToClipboard(item);
                    }
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

        private async void UseSelectedItem(ClipboardItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Content)) return;
            try
            {
                System.Windows.Clipboard.SetText(item.Content);
                _copiedItemId = null;
                Hide();
                await Task.Delay(80);
                SimulateCtrlV();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"回填粘贴失败: {ex.Message}");
            }
        }

        private async void ShowToast(string message)
        {
            CountText.Text = message;
            _toastActiveCount++;
            await Task.Delay(1200);
            _toastActiveCount--;
            if (_toastActiveCount == 0)
            {
                UpdateUIStates();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is Guid id)
            {
                _clipboardManager.RemoveItem(id);
                e.Handled = true;
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("确定清空全部剪贴板历史吗？此操作不可撤销。", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _clipboardManager.ClearAll();
            }
        }

        private static void SimulateCtrlV()
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

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

        private void ResizeGrip_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isResizing) return;
            var pos = e.GetPosition(null);
            var delta = pos - _resizeStart;

            double newW = Math.Max(MinWidth, _resizeStartW + delta.X);
            double newH = Math.Max(MinHeight, _resizeStartH + delta.Y);

            Width = newW;
            Height = newH;
        }

        private void ResizeGrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isResizing = false;
            ((UIElement)sender).ReleaseMouseCapture();
            ((UIElement)sender).MouseMove -= ResizeGrip_MouseMove;
            ((UIElement)sender).MouseLeftButtonUp -= ResizeGrip_MouseLeftButtonUp;
        }
    }
}
