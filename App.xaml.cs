using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WinKit.Clipboard;
using WinKit.Clipboard.Models;
using WinKit.Clipboard.Services;
using WinKit.Common;

namespace WinKit
{
    public partial class App : System.Windows.Application
    {
        private SettingsManager?      _settingsManager;
        private ClipboardService?     _clipboardService;
        private ClipboardManager?     _clipboardManager;
        private QuickPhraseManager?   _quickPhraseManager;
        private ImageCleanupService?  _imageCleanupService;
        private KeyboardHookService?  _keyboardHookService;
        private TrayHelper?           _trayHelper;

        private Todo.MainWindow?      _todoWindow;
        private Clipboard.MainWindow? _pasteWindow;
        private QuickPhraseWindow?    _quickPhraseWindow;

        public ClipboardManager?    ClipboardManager    => _clipboardManager;
        public SettingsManager?     SettingsManager     => _settingsManager;
        public KeyboardHookService? KeyboardHookService => _keyboardHookService;
        public QuickPhraseManager?  QuickPhraseManager  => _quickPhraseManager;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 初始化统一配置管理器与全局主题引擎
            _settingsManager = new SettingsManager();
            ThemeManager.Initialize(_settingsManager);

            // 2. 初始化常用短语与剪贴板数据中心
            _quickPhraseManager = new QuickPhraseManager();
            _clipboardManager   = new ClipboardManager(_settingsManager);
            _clipboardService   = new ClipboardService();

            // 3. 初始化独立图片缓存清理服务
            _imageCleanupService = new ImageCleanupService(
                _settingsManager,
                () => _clipboardManager.Items.ToList(),
                itemsToRemove => Dispatcher.Invoke(() => _clipboardManager.RemoveItems(itemsToRemove))
            );

            // 4. 监听剪贴板新增条目
            _clipboardService.ItemDetected += OnClipboardItemDetected;
            if (_settingsManager.Settings.PasteEnableMonitoring)
            {
                _clipboardService.StartMonitoring();
            }

            // 5. 实例化全部功能窗口
            _todoWindow        = new Todo.MainWindow(_settingsManager);
            _pasteWindow       = new Clipboard.MainWindow(_clipboardManager, _clipboardService, _settingsManager);
            _quickPhraseWindow = new QuickPhraseWindow(_quickPhraseManager, _clipboardService);

            // 6. 初始化托盘服务
            _trayHelper = new TrayHelper(this, _settingsManager, _todoWindow, _pasteWindow);
            _todoWindow.SetTray(_trayHelper);

            // 7. 开启低级键盘钩子，注册全局快捷键与 Esc / vv 连击分发
            _keyboardHookService = new KeyboardHookService();
            _keyboardHookService.QuickPhraseTriggered += OnQuickPhraseTriggered;
            RegisterAllHotkeys();

            // 8. 订阅快捷键与配置变更通知：用户保存新配置后热重载
            _trayHelper.SubscribeHotkeysChanged(ReloadHotkeys);
            _settingsManager.SettingsChanged += (s, settings) =>
            {
                _imageCleanupService?.TriggerCleanup();
            };

            // 9. 默认展现 TodoList 待办主窗口
            _todoWindow.Show();
            _todoWindow.Activate();
        }

        private void OnClipboardItemDetected(object? sender, ClipboardItem item)
        {
            Dispatcher.Invoke(() =>
            {
                _clipboardManager?.AddItem(item);
                if (item.IsImage)
                {
                    _imageCleanupService?.TriggerCleanup();
                }
            });
        }

        private void OnQuickPhraseTriggered(IntPtr targetHwnd)
        {
            Dispatcher.Invoke(() =>
            {
                if (_quickPhraseWindow != null)
                {
                    _quickPhraseWindow.ShowNearCaretOrMouse(targetHwnd);
                }
            });
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
        // 注册 / 热重载全局键盘钩子（Todo、Clipboard 与 QuickPhrase 独立）
        // ══════════════════════════════════════════════
        private void RegisterAllHotkeys()
        {
            if (_keyboardHookService == null || _settingsManager == null) return;

            _keyboardHookService.UnregisterAll();
            var settings = _settingsManager.Settings;

            // 同步常用短语连击开关
            _keyboardHookService.QuickPhraseEnabled = settings.QuickPhraseEnabled;

            // ── 0. 全局 Esc 键：优先收起短语窗口与剪贴板窗口 ─────────────
            _keyboardHookService.RegisterHotkey("Esc", () =>
            {
                if (_quickPhraseWindow != null && _quickPhraseWindow.IsVisible)
                {
                    Dispatcher.Invoke(() => _quickPhraseWindow.Hide());
                    return true;
                }
                if (_pasteWindow != null && _pasteWindow.IsVisible)
                {
                    Dispatcher.Invoke(() => _pasteWindow.Hide());
                    return true;
                }
                return false; // 放行 Esc 给前台活动窗口
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
            _imageCleanupService?.Dispose();
            _clipboardManager?.Dispose();

            base.OnExit(e);
        }
    }
}
