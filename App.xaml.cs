using System;
using System.Windows;
using WinKit.Common;
using WinKit.Clipboard.Services;

namespace WinKit
{
    public partial class App : System.Windows.Application
    {
        private SettingsManager? _settingsManager;
        private ClipboardService? _clipboardService;
        private ClipboardManager? _clipboardManager;
        private KeyboardHookService? _keyboardHookService;
        private TrayHelper? _trayHelper;
        private GlobalHotkeyHookService? _todoHotkeyService;

        private Todo.MainWindow? _todoWindow;
        private Clipboard.MainWindow? _pasteWindow;

        public ClipboardManager? ClipboardManager => _clipboardManager;
        public SettingsManager? SettingsManager => _settingsManager;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 初始化统一配置管理器
            _settingsManager = new SettingsManager();

            // 2. 初始化剪贴板监听和数据中心（常驻后台）
            _clipboardService = new ClipboardService();
            _clipboardManager = new ClipboardManager(_settingsManager);

            _clipboardService.TextChanged += OnClipboardTextChanged;
            if (_settingsManager.Settings.PasteEnableMonitoring)
            {
                _clipboardService.StartMonitoring();
            }

            // 3. 实例化两个功能窗口
            _todoWindow = new Todo.MainWindow(_settingsManager);
            _pasteWindow = new Clipboard.MainWindow(_clipboardManager, _settingsManager);

            // 4. 初始化合并后的托盘服务
            _trayHelper = new TrayHelper(this, _settingsManager, _todoWindow, _pasteWindow);
            _todoWindow.SetTray(_trayHelper);

            // 5. 开启低级键盘钩子，全局接管 Win + V 快捷键
            if (_settingsManager.Settings.PasteEnableMonitoring)
            {
                RegisterGlobalKeyboardHook();
            }

            // 根据软件目录下 config.json 注册 TodoList 显示/隐藏的全局快捷键
            RegisterTodoToggleHotkey();

            // 6. 默认展现 TodoList 待办主窗口
            _todoWindow.Show();
            _todoWindow.Activate();
        }

        private void OnClipboardTextChanged(object? sender, string text)
        {
            _clipboardManager?.AddTextItem(text);
        }

        public void ToggleClipboardFeature(bool enable)
        {
            if (enable)
            {
                _clipboardService?.StartMonitoring();
                RegisterGlobalKeyboardHook();
            }
            else
            {
                _clipboardService?.StopMonitoring();
                _keyboardHookService?.Dispose();
                _keyboardHookService = null;
                _pasteWindow?.Hide();
            }
        }

        private void RegisterGlobalKeyboardHook()
        {
            _keyboardHookService?.Dispose();
            _keyboardHookService = new KeyboardHookService(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_pasteWindow != null)
                    {
                        if (_pasteWindow.IsVisible)
                        {
                            _pasteWindow.Hide();
                        }
                        else
                        {
                            _pasteWindow.ShowAtMouse();
                        }
                    }
                });
            });
        }

        /// <summary>
        /// 读取软件目录下的 config.json（{"toggleHotkey": "Ctrl + ~"}），
        /// 注册用于快速显示/隐藏 TodoList 主窗口的全局快捷键
        /// </summary>
        private void RegisterTodoToggleHotkey()
        {
            var config = HotkeyConfigManager.Load();
            if (!HotkeyDefinition.TryParse(config.ToggleHotkey, out var hotkey) || hotkey == null)
            {
                System.Diagnostics.Debug.WriteLine($"App: config.json 中的 toggleHotkey 无效 - {config.ToggleHotkey}");
                return;
            }

            _todoHotkeyService = new GlobalHotkeyHookService(hotkey, () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_todoWindow == null) return;

                    if (_todoWindow.IsVisible)
                    {
                        _todoWindow.Hide();
                    }
                    else
                    {
                        _todoWindow.Show();
                        _todoWindow.Activate();
                    }
                });
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 优雅释放所有非托管钩子和资源
            _keyboardHookService?.Dispose();
            _todoHotkeyService?.Dispose();
            _clipboardService?.Dispose();
            _trayHelper?.Dispose();
            _clipboardManager?.Dispose();
            _settingsManager?.Dispose();

            base.OnExit(e);
        }
    }
}
