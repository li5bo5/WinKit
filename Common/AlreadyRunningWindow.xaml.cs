using System;
using System.Windows;
using System.Windows.Input;

namespace WinKit.Common
{
    public partial class AlreadyRunningWindow : Window
    {
        public AlreadyRunningWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                Activate();
                Focus();
            };
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

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // 点击屏幕其他位置（窗口失焦）时关闭弹窗
            Close();
        }
    }
}
