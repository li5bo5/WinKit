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
            // 注意：此处切勿调用 Close()。
            // 调用方为 alreadyRunningDialog.ShowDialog() 后紧跟 Shutdown()，
            // 一旦本窗口因失焦而自行关闭，第二个实例会立刻退出进程，
            // 表现为「双击后毫无反应 / 托盘图标不出现」的假象。
            // 如需失焦自动关闭，应改为由调用方显式决定是否退出，而非这里直接 Close。
        }
    }
}
