using System;
using System.Windows;
using WinKit.Common;
using WinKit.Clipboard.Services;

namespace WinKit
{
    public partial class App : System.Windows.Application
    {
        private SettingsManager?      _settingsManager;
        private ClipboardService?     _clipboardService;
        private ClipboardManager?     _clipboardManager;
        private KeyboardHookService?  _keyboardHookService;
        private TrayHelper?           _trayHelper;

        private Todo.MainWindow?      _todoWindow;
        private Clipboard.MainWindow? _pasteWindow;

        public ClipboardManager?   ClipboardManager   => _clipboardManager;
        public SettingsManager?    SettingsManager    => _settingsManager;
        public KeyboardHookService? KeyboardHookService => _keyboardHookService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 初始化统一配置管理器与全局主题引擎
            _settingsManager = new SettingsManager();
            ThemeManager.Initialize(_settingsManager);

            // 2. 初始化剪贴板监听和数据中心（常驻后台）
            _clipboardService = new ClipboardService();
            _clipboardManager = new ClipboardManager(_settingsManager);

            _clipboardService.TextChanged += OnClipboardTextChanged;
            if (_settingsManager.Settings.PasteEnableMonitoring)
            {
                _clipboardService.StartMonitoring();
            }

            // 3. 实例化两个功能窗口
            _todoWindow  = new Todo.MainWindow(_settingsManager);
            _pasteWindow = new Clipboard.MainWindow(_clipboardManager, _settingsManager);

            // 4. 初始化合并后的托盘服务
            _trayHelper = new TrayHelper(this, _settingsManager, _todoWindow, _pasteWindow);
            _todoWindow.SetTray(_trayHelper);

            // 5. 开启低级键盘钩子，注册所有自定义全局快捷键与 Esc 全局分发
            _keyboardHookService = new KeyboardHookService();
            RegisterAllHotkeys();

            // 6. 订阅快捷键变更通知：用户保存新配置后热重载
            _trayHelper.SubscribeHotkeysChanged(ReloadHotkeys);

            // 7. 默认展现 TodoList 待办主窗口
            _todoWindow.Show();
            _todoWindow.Activate();
        }

        private void OnClipboardTextChanged(object? sender, string text)
        {
            _clipboardManager?.AddTextItem(text);
        }

        // ══════════════════════════════════════════════
        // 剪贴板功能开关（由托盘"启用"菜单项调用）
        // ══════════════════════════════════════════════
        public void ToggleClipboardFeature(bool enable)
        {
            if (enable)
            {
                _clipboardService?.StartMonitoring();
            }
            else
            {
                _clipboardService?.StopMonitoring();
                _pasteWindow?.Hide();
            }
            RegisterAllHotkeys();
        }

        // ══════════════════════════════════════════════
        // 注册 / 热重载全局键盘钩子（Todo 与 Clipboard 独立）
        // ══════════════════════════════════════════════
        private void RegisterAllHotkeys()
        {
            if (_keyboardHookService == null || _settingsManager == null) return;

            _keyboardHookService.UnregisterAll();
            var settings = _settingsManager.Settings;

            // ── 0. 全局 Esc 键：仅当剪贴板窗口可见时将其隐藏并拦截，不可见时完全放行 ────
            _keyboardHookService.RegisterHotkey("Esc", () =>
            {
                if (_pasteWindow != null && _pasteWindow.IsVisible)
                {
                    Dispatcher.Invoke(() => _pasteWindow.Hide());
                    return true; // 剪贴板已可见，消费并拦截 Esc
                }
                return false; // 剪贴板未可见，完全放行 Esc 给前台活动窗口（TodoList/EditDialog 等）
            });

            // ── 1. 置顶显示快捷键（无条件常驻）───────────────────────
            _keyboardHookService.RegisterHotkey(settings.HotkeyTodoTopToggle, () =>
            {
                Dispatcher.Invoke(() => _todoWindow?.ToggleTopmostAndPassThrough());
            });

            // ── 2. 唤出 / 隐藏剪贴板（受剪贴板监控总开关控制）────────
            if (settings.PasteEnableMonitoring)
            {
                _keyboardHookService.RegisterHotkey(settings.HotkeyClipboardToggle, () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_pasteWindow == null) return;
                        if (_pasteWindow.IsVisible)
                            _pasteWindow.Hide();
                        else
                            _pasteWindow.ShowAtMouse();
                    });
                });
            }
        }

        // ══════════════════════════════════════════════
        // 用户保存新快捷键配置后，热重载钩子
        // ══════════════════════════════════════════════
        private void ReloadHotkeys()
        {
            Dispatcher.Invoke(RegisterAllHotkeys);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 优雅释放所有非托管钩子和资源
            ThemeManager.Dispose();
            _keyboardHookService?.Dispose();
            _clipboardService?.Dispose();
            _trayHelper?.Dispose();
            _clipboardManager?.Dispose();

            base.OnExit(e);
        }
    }
}
