using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinKit.Clipboard.Services;

namespace WinKit.Common
{
    public partial class AlreadyRunningWindow : Window
    {
        private readonly GlobalMouseClickMonitor _mouseMonitor = new();

        public AlreadyRunningWindow()
        {
            InitializeComponent();
            _mouseMonitor.ClickedOutside += () => Dispatcher.BeginInvoke(Close);
            Loaded += (s, e) =>
            {
                Activate();
                Focus();
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    _mouseMonitor.Start(new[] { hwnd });
                }
            };
            Closed += (s, e) => _mouseMonitor.Dispose();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 支持点击窗口任意空白区域拖动
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 监听 Esc、Enter 或 Space 键关闭
            if (e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Space)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
