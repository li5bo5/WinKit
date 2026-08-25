using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinKit.Common;
using WpfTextBox = System.Windows.Controls.TextBox;
using Application = System.Windows.Application;

namespace WinKit.Common
{
    /// <summary>
    /// 统一偏好设置中心窗口
    /// </summary>
    public partial class HotkeySettingsWindow : Window
    {
        private readonly SettingsManager _settingsManager;

        // 当前正在捕获按键的输入框
        private WpfTextBox? _capturingBox = null;
        private bool _isInitializing = false;

        // 用于通知 App 重新加载快捷键与配置的回调
        public event Action? HotkeysChanged;

        public HotkeySettingsWindow(SettingsManager settingsManager)
        {
            InitializeComponent();
            _settingsManager = settingsManager;

            LoadFromSettings();
        }

        // ══════════════════════════════════════════════
        // 从配置加载到 UI
        // ══════════════════════════════════════════════
        private void LoadFromSettings()
        {
            _isInitializing = true;
            try
            {
                var s = _settingsManager.Settings;

                // 1. TodoList
                HkTodoTop.Text = s.HotkeyTodoTopToggle;
                HkSaveExit.Text = s.HotkeyTodoSaveAndExit;
                ChkTrayDoubleClick.IsChecked = s.TrayDoubleClickTodoEnabled;
                SelectComboByTag(CmbRetentionDays, s.RecycleBinRetentionDays, 1); // 默认 60 天

                // 2. Clipboard
                HkClipboard.Text = s.HotkeyClipboardToggle;
                ChkPasteMonitoring.IsChecked = s.PasteEnableMonitoring;
                ChkPasteDedup.IsChecked = s.PasteEnableTextDeduplication;
                ChkPasteRememberScroll.IsChecked = s.PasteRememberScrollPosition;
                SelectComboByTag(CmbPasteMaxItems, s.PasteMaxItems, 2); // 默认 300 条

                // 3. General
                SelectComboByStringTag(CmbThemeMode, s.ThemeMode ?? "System", 0); // 默认跟随系统
                SelectComboByTag(CmbOpacity, s.WindowOpacity, 5); // 默认 100%
                ChkAutoStart.IsChecked = AutoStartHelper.IsAutoStartEnabled();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private static void SelectComboByTag(System.Windows.Controls.ComboBox combo, int targetVal, int defaultIndex)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item &&
                    int.TryParse(item.Tag?.ToString(), out int val) && val == targetVal)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > defaultIndex) combo.SelectedIndex = defaultIndex;
        }

        private static void SelectComboByStringTag(System.Windows.Controls.ComboBox combo, string targetTag, int defaultIndex)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), targetTag, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > defaultIndex) combo.SelectedIndex = defaultIndex;
        }

        private static void SetHookCapturing(bool capturing)
        {
            if (System.Windows.Application.Current is App app && app.KeyboardHookService != null)
            {
                app.KeyboardHookService.IsCapturing = capturing;
            }
        }

        // ══════════════════════════════════════════════
        // 主题模式与不透明度即时预览响应
        // ══════════════════════════════════════════════
        private void CmbThemeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyLivePreview();
        }

        private void CmbOpacity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyLivePreview();
        }

        private void ApplyLivePreview()
        {
            string mode = (CmbThemeMode?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
            int opacity = 100;
            if (CmbOpacity?.SelectedItem is ComboBoxItem opItem && int.TryParse(opItem.Tag?.ToString(), out int val))
            {
                opacity = val;
            }
            ThemeManager.ApplyTheme(mode, opacity);
        }

        // ══════════════════════════════════════════════
        // 快捷键输入框：获得焦点 -> 进入捕获状态
        // ══════════════════════════════════════════════
        private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is WpfTextBox tb)
            {
                _capturingBox = tb;
                SetHookCapturing(true);
                tb.Text = "请按下快捷键...";
                tb.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is WpfTextBox tb && _capturingBox == tb)
            {
                if (tb.Text == "请按下快捷键...")
                {
                    LoadFromSettings();
                }
                tb.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ThemeTextPrimaryBrush"];
                _capturingBox = null;
                SetHookCapturing(false);
            }
        }

        // ══════════════════════════════════════════════
        // 快捷键输入框：物理按键捕获
        // ══════════════════════════════════════════════
        private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_capturingBox == null) return;

            e.Handled = true;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt  || key == Key.RightAlt  ||
                key == Key.LeftShift|| key == Key.RightShift||
                key == Key.LWin     || key == Key.RWin      ||
                key == Key.Tab)
            {
                return;
            }

            // Esc：取消捕获并恢复原值
            if (key == Key.Escape)
            {
                LoadFromSettings();
                _capturingBox.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ThemeTextPrimaryBrush"];
                Keyboard.ClearFocus();
                _capturingBox = null;
                SetHookCapturing(false);
                return;
            }

            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) sb.Append("Win+");
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt)     != 0) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift)   != 0) sb.Append("Shift+");
            sb.Append(KeyToString(key));

            _capturingBox.Text = sb.ToString();
            _capturingBox.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ThemeTextPrimaryBrush"];
            Keyboard.ClearFocus();
            _capturingBox = null;
            SetHookCapturing(false);
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
        // 全局 Esc：关闭偏好设置窗口
        // ══════════════════════════════════════════════
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_capturingBox != null) return;

            if (e.Key == Key.Escape)
            {
                CancelAndClose();
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════
        // 保存全部配置
        // ══════════════════════════════════════════════
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = _settingsManager.Settings;

            // 1. TodoList
            settings.HotkeyTodoTopToggle        = HkTodoTop.Text.Trim();
            settings.HotkeyTodoSaveAndExit      = HkSaveExit.Text.Trim();
            settings.TrayDoubleClickTodoEnabled = ChkTrayDoubleClick.IsChecked ?? true;

            if (CmbRetentionDays.SelectedItem is ComboBoxItem retItem &&
                int.TryParse(retItem.Tag?.ToString(), out int retDays))
            {
                settings.RecycleBinRetentionDays = retDays;
            }

            // 2. Clipboard
            settings.HotkeyClipboardToggle         = HkClipboard.Text.Trim();
            settings.PasteEnableMonitoring         = ChkPasteMonitoring.IsChecked ?? true;
            settings.PasteEnableTextDeduplication  = ChkPasteDedup.IsChecked ?? true;
            settings.PasteRememberScrollPosition   = ChkPasteRememberScroll.IsChecked ?? false;

            if (CmbPasteMaxItems.SelectedItem is ComboBoxItem maxItem &&
                int.TryParse(maxItem.Tag?.ToString(), out int maxVal))
            {
                settings.PasteMaxItems = maxVal;
            }

            // 3. General
            if (CmbThemeMode.SelectedItem is ComboBoxItem themeItem)
            {
                settings.ThemeMode = themeItem.Tag?.ToString() ?? "System";
            }

            if (CmbOpacity.SelectedItem is ComboBoxItem opItem &&
                int.TryParse(opItem.Tag?.ToString(), out int opVal))
            {
                settings.WindowOpacity = opVal;
            }

            AutoStartHelper.SetAutoStart(ChkAutoStart.IsChecked ?? true);

            _settingsManager.SaveSettings(settings);
            ThemeManager.ApplyTheme(); // 确认持久化应用主题

            HotkeysChanged?.Invoke();
            Hide();
        }

        // ══════════════════════════════════════════════
        // 恢复默认配置
        // ══════════════════════════════════════════════
        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("确定恢复所有偏好设置为默认值吗？", "提示",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            var settings = _settingsManager.Settings;
            settings.HotkeyTodoTopToggle           = "Ctrl+D";
            settings.HotkeyClipboardToggle         = "Win+V";
            settings.HotkeyTodoSaveAndExit         = "Ctrl+S";
            settings.TrayDoubleClickTodoEnabled    = true;
            settings.RecycleBinRetentionDays       = 60;
            settings.PasteEnableMonitoring         = true;
            settings.PasteEnableTextDeduplication  = true;
            settings.PasteRememberScrollPosition   = false;
            settings.PasteMaxItems                 = 300;
            settings.ThemeMode                     = "System";
            settings.WindowOpacity                 = 100;

            _settingsManager.SaveSettings(settings);
            AutoStartHelper.SetAutoStart(true);

            LoadFromSettings();
            ThemeManager.ApplyTheme(); // 恢复默认主题
            HotkeysChanged?.Invoke();
        }

        private void CancelAndClose()
        {
            // 恢复已保存的配置主题（取消未保存的临时预览）
            ThemeManager.ApplyTheme();
            Hide();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => CancelAndClose();
    }
}
