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

        public ClipboardManager?  ClipboardManager  => _clipboardManager;
        public SettingsManager?   SettingsManager   => _settingsManager;

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
            _todoWindow  = new Todo.MainWindow(_settingsManager);
            _pasteWindow = new Clipboard.MainWindow(_clipboardManager, _settingsManager);

            // 4. 初始化合并后的托盘服务
            _trayHelper = new TrayHelper(this, _settingsManager, _todoWindow, _pasteWindow);
            _todoWindow.SetTray(_trayHelper);

            // 5. 开启低级键盘钩子，注册所有自定义全局快捷键
            if (_settingsManager.Settings.PasteEnableMonitoring)
            {
                RegisterGlobalKeyboardHook();
            }

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

        // ══════════════════════════════════════════════
        // 注册 / 热重载全局键盘钩子
        // ══════════════════════════════════════════════
        private void RegisterGlobalKeyboardHook()
        {
            // 释放旧钩子
            _keyboardHookService?.Dispose();
            _keyboardHookService = new KeyboardHookService();

            var settings = _settingsManager!.Settings;

            // ── 1. 唤出 / 隐藏剪贴板 ─────────────────────────
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

            // ── 2. 置顶显示快捷键（置顶时取消置顶，未置顶时置顶并取消穿透）───
            _keyboardHookService.RegisterHotkey(settings.HotkeyTodoTopToggle, () =>
            {
                Dispatcher.Invoke(() => _todoWindow?.ToggleTopmostAndPassThrough());
            });

            // ── 3. Win+S 保存并退出 Todo 编辑（由 Todo 窗口内部 PreviewKeyDown 已处理）────
            // KeyboardHookService 层无需额外注册，避免与 InlineEditBox 内部 Win+S 冲突
        }

        // ══════════════════════════════════════════════
        // 用户保存新快捷键配置后，热重载钩子
        // ══════════════════════════════════════════════
        private void ReloadHotkeys()
        {
            Dispatcher.Invoke(() =>
            {
                if (_settingsManager?.Settings.PasteEnableMonitoring == true)
                    RegisterGlobalKeyboardHook();
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 优雅释放所有非托管钩子和资源
            _keyboardHookService?.Dispose();
            _clipboardService?.Dispose();
            _trayHelper?.Dispose();
            _clipboardManager?.Dispose();
            _settingsManager?.Dispose();

            base.OnExit(e);
        }
    }
}
