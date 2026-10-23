using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WinKit.Clipboard.Services;

namespace WinKit.Common
{
    public partial class AboutWindow : Window
    {
        private readonly GlobalMouseClickMonitor _mouseMonitor = new();

        public AboutWindow()
        {
            InitializeComponent();
            InitVersion();

            _mouseMonitor.ClickedOutside += () => Dispatcher.BeginInvoke(Close);

            Loaded += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    _mouseMonitor.Start(new[] { hwnd });
                }
            };

            Closed += (s, e) => _mouseMonitor.Dispose();
        }

        private void InitVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                string vStr = (version.Build > 0) ? version.ToString(3) : $"{version.Major}.{version.Minor}";
                VersionText.Text = $"v{vStr}";
            }
            else
            {
                VersionText.Text = "未知";
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 支持点击窗口任意空白区域进行拖动
            if (e.LeftButton == MouseButtonState.Pressed) NativeMethods.DragMoveWithBounds(this);
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 监听 Esc 键，按下时关闭销毁窗口
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void LinkText_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            LinkText.Foreground = System.Windows.Media.Brushes.DodgerBlue;
        }

        private void LinkText_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            LinkText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x75, 0xD3));
        }

        private void LinkText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "https://github.com/li5bo5/WinKit",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"无法打开链接: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
