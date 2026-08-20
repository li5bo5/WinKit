using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinKit.Common;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace WinKit.Common
{
    public partial class HotkeySettingsWindow : Window
    {
        private readonly SettingsManager _settingsManager;

        // 当前正在捕获按键的输入框（Tag 对应配置字段名）
        private WpfTextBox? _capturingBox = null;

        // 用于通知 App 重新加载快捷键的回调
        public event Action? HotkeysChanged;

        // ── 字段名 -> TextBox 的映射（方便批量读写） ──
        private readonly Dictionary<string, WpfTextBox> _boxMap;

        public HotkeySettingsWindow(SettingsManager settingsManager)
        {
            InitializeComponent();
            _settingsManager = settingsManager;

            _boxMap = new Dictionary<string, WpfTextBox>
            {
                { "HotkeyTodoTempShow",       HkTodoTemp   },
                { "HotkeyClipboardToggle",     HkClipboard  },
                { "HotkeyScreenshotOcr",       HkOcr        },
                { "HotkeyScreenshotSnip",      HkSnip       },
                { "HotkeyTodoSaveAndExit",     HkSaveExit   },
            };

            LoadFromSettings();
        }

        // ══════════════════════════════════════════════
        // 从配置加载到 UI
        // ══════════════════════════════════════════════
        private void LoadFromSettings()
        {
            var s = _settingsManager.Settings;
            HkTodoTemp.Text  = s.HotkeyTodoTempShow;
            HkClipboard.Text = s.HotkeyClipboardToggle;
            HkOcr.Text       = s.HotkeyScreenshotOcr;
            HkSnip.Text      = s.HotkeyScreenshotSnip;
            HkSaveExit.Text  = s.HotkeyTodoSaveAndExit;
            EscEnabled.IsChecked = s.HotkeyEscExitEnabled;
        }

        // ══════════════════════════════════════════════
        // 快捷键输入框：获得焦点 -> 进入捕获状态
        // ══════════════════════════════════════════════
        private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is WpfTextBox tb)
            {
                _capturingBox = tb;
                tb.Text = "请按下快捷键...";
                tb.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is WpfTextBox tb && _capturingBox == tb)
            {
                // 若用户没有按任何键就离开，恢复原值
                if (tb.Text == "请按下快捷键...")
                {
                    LoadFromSettings();
                }
                tb.Foreground = System.Windows.Media.Brushes.Black;
                _capturingBox = null;
            }
        }

        // ══════════════════════════════════════════════
        // 快捷键输入框：物理按键捕获
        // ══════════════════════════════════════════════
        private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_capturingBox == null) return;

            e.Handled = true; // 不让按键冒泡到其他处理

            // 忽略纯修饰键按下（等待功能键）
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt  || key == Key.RightAlt  ||
                key == Key.LeftShift|| key == Key.RightShift||
                key == Key.LWin     || key == Key.RWin      ||
                key == Key.Tab)
            {
                return;
            }

            // Esc：取消捕获，恢复原值
            if (key == Key.Escape)
            {
                LoadFromSettings();
                _capturingBox.Foreground = System.Windows.Media.Brushes.Black;
                Keyboard.ClearFocus();
                _capturingBox = null;
                return;
            }

            // 组合修饰键字符串构建
            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) sb.Append("Win+");
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt)     != 0) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift)   != 0) sb.Append("Shift+");
            sb.Append(KeyToString(key));

            _capturingBox.Text = sb.ToString();
            _capturingBox.Foreground = System.Windows.Media.Brushes.Black;
            Keyboard.ClearFocus();
            _capturingBox = null;
        }

        private static string KeyToString(Key key) => key switch
        {
            >= Key.A and <= Key.Z => ((char)('A' + (key - Key.A))).ToString(),
            >= Key.F1 and <= Key.F12 => $"F{key - Key.F1 + 1}",
            Key.D0 or Key.NumPad0 => "0",
            Key.D1 or Key.NumPad1 => "1",
            Key.D2 or Key.NumPad2 => "2",
            Key.D3 or Key.NumPad3 => "3",
            Key.D4 or Key.NumPad4 => "4",
            Key.D5 or Key.NumPad5 => "5",
            Key.D6 or Key.NumPad6 => "6",
            Key.D7 or Key.NumPad7 => "7",
            Key.D8 or Key.NumPad8 => "8",
            Key.D9 or Key.NumPad9 => "9",
            Key.OemTilde          => "`",
            Key.OemMinus          => "-",
            Key.OemPlus           => "=",
            Key.OemOpenBrackets   => "[",
            Key.Oem6              => "]",
            Key.OemSemicolon      => ";",
            Key.OemQuotes         => "'",
            Key.OemComma          => ",",
            Key.OemPeriod         => ".",
            Key.OemQuestion       => "/",
            Key.Back              => "BackSpace",
            Key.Delete            => "Delete",
            Key.Home              => "Home",
            Key.End               => "End",
            Key.PageUp            => "PageUp",
            Key.PageDown          => "PageDown",
            Key.Space             => "Space",
            _                     => key.ToString()
        };

        // ══════════════════════════════════════════════
        // 全局 Esc：关闭窗口
        // ══════════════════════════════════════════════
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 若正在捕获按键，不拦截（由 HotkeyBox_PreviewKeyDown 处理）
            if (_capturingBox != null) return;

            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════
        // 保存按钮
        // ══════════════════════════════════════════════
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = _settingsManager.Settings;
            settings.HotkeyTodoTempShow      = HkTodoTemp.Text.Trim();
            settings.HotkeyClipboardToggle   = HkClipboard.Text.Trim();
            settings.HotkeyScreenshotOcr     = HkOcr.Text.Trim();
            settings.HotkeyScreenshotSnip    = HkSnip.Text.Trim();
            settings.HotkeyTodoSaveAndExit   = HkSaveExit.Text.Trim();
            settings.HotkeyEscExitEnabled    = EscEnabled.IsChecked == true;
            _settingsManager.SaveSettings(settings);

            HotkeysChanged?.Invoke();
            Hide();
        }

        // ══════════════════════════════════════════════
        // 恢复默认
        // ══════════════════════════════════════════════
        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("确定恢复所有快捷键为默认值吗？", "提示",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            var settings = _settingsManager.Settings;
            settings.HotkeyTodoTempShow    = "Win+Alt+D";
            settings.HotkeyClipboardToggle = "Win+V";
            settings.HotkeyScreenshotOcr   = "Win+Shift+T";
            settings.HotkeyScreenshotSnip  = "Win+Shift+S";
            settings.HotkeyTodoSaveAndExit = "Win+S";
            settings.HotkeyEscExitEnabled  = true;
            _settingsManager.SaveSettings(settings);

            LoadFromSettings();
            HotkeysChanged?.Invoke();
        }

        // ══════════════════════════════════════════════
        // Esc 开关
        // ══════════════════════════════════════════════
        private void EscEnabled_Changed(object sender, RoutedEventArgs e)
        {
            // 实时预览（保存后才真正生效）
        }

        // ══════════════════════════════════════════════
        // 标题栏拖动与关闭
        // ══════════════════════════════════════════════
        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();
    }
}
