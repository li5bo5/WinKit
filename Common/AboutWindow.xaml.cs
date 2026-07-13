using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WinKit.Common
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            InitVersion();
        }

        private void InitVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"v{version.ToString(3)}";
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }

        private void LinkText_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            LinkText.Foreground = System.Windows.Media.Brushes.DodgerBlue;
        }

        private void LinkText_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            LinkText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0x66, 0xCC));
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
