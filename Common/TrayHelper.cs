using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WinKit.Clipboard.Services;
using SWF = System.Windows.Forms;
using WpfApp = System.Windows.Application;

namespace WinKit.Common
{
    /// <summary>
    /// 隐形宿主窗口，用于托盘 ContextMenu 承载与前台失焦自动收起
    /// </summary>
    internal class MenuHostWindow : Window
    {
        public MenuHostWindow()
        {
            Width = 0;
            Height = 0;
            WindowStyle = WindowStyle.None;
            ShowInTaskbar = false;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
        }
    }

    /// <summary>
    /// 系统托盘管理类，提供极简 4 项 Fluent 右键菜单与双击唤出 TodoList
    /// </summary>
    public class TrayHelper : IDisposable
    {
        private readonly SWF.NotifyIcon _icon;
        private readonly Window _todoWindow;
        private readonly Window _pasteWindow;
        private readonly WpfApp _app;
        private readonly SettingsManager _settingsManager;

        // WPF 自研 ContextMenu
        private readonly ContextMenu _contextMenu;
        private readonly MenuHostWindow _menuHostWindow;

        // 统一弹窗实例
        private readonly AboutWindow _aboutWindow;
        private readonly Todo.HistoryWindow _historyWindow;
        private readonly HotkeySettingsWindow _preferencesWindow;

        // 纯文字极简菜单项
        private readonly MenuItem _itemHistory;
        private readonly MenuItem _itemPreferences;
        private readonly MenuItem _itemAbout;
        private readonly MenuItem _itemExit;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public TrayHelper(WpfApp app, SettingsManager settingsManager, Window todoWindow, Window pasteWindow)
        {
            _app = app;
            _settingsManager = settingsManager;
            _todoWindow = todoWindow;
            _pasteWindow = pasteWindow;

            // ── 1. 预创建弹窗实例并在后台待命 ──────────────────────────
            _menuHostWindow = new MenuHostWindow();
            _menuHostWindow.Show();
            _menuHostWindow.Hide();

            _aboutWindow = new AboutWindow();
            _aboutWindow.Show();
            _aboutWindow.Hide();

            _historyWindow = new Todo.HistoryWindow((Todo.MainWindow)_todoWindow, _settingsManager);
            ((Todo.MainWindow)_todoWindow).SetHistoryWindow(_historyWindow);
            _historyWindow.Show();
            _historyWindow.Hide();

            _preferencesWindow = new HotkeySettingsWindow(_settingsManager);
            _preferencesWindow.IsVisibleChanged += (s, e) =>
            {
                if (_todoWindow is Todo.MainWindow todoWin)
                {
                    todoWin.IsPreferencesWindowOpen = _preferencesWindow.IsVisible;
                }
            };
            _preferencesWindow.Show();
            _preferencesWindow.Hide();

            // ── 2. 创建极简 4 项 WPF ContextMenu ──────────────────────
            _contextMenu = new ContextMenu();

            _itemHistory = new MenuItem { Header = "待办历史" };
            _itemHistory.Click += (s, e) => ShowHistoryWindow();

            _itemPreferences = new MenuItem { Header = "偏好设置" };
            _itemPreferences.Click += (s, e) => ShowPreferencesWindow();

            _itemAbout = new MenuItem { Header = "关于" };
            _itemAbout.Click += (s, e) => ShowAboutWindow();

            _itemExit = new MenuItem { Header = "退出" };
            _itemExit.Click += (s, e) => ShutdownApp();

            _contextMenu.Items.Add(_itemHistory);
            _contextMenu.Items.Add(_itemPreferences);
            _contextMenu.Items.Add(new Separator());
            _contextMenu.Items.Add(_itemAbout);
            _contextMenu.Items.Add(_itemExit);

            // ── 3. 菜单失焦自动收起 ─────────────────────────────────
            _menuHostWindow.Deactivated += (s, e) =>
            {
                _contextMenu.IsOpen = false;
                _menuHostWindow.Hide();
            };

            _contextMenu.Closed += (s, e) =>
            {
                _menuHostWindow.Hide();
            };

            // ── 4. 创建系统托盘 NotifyIcon ────────────────────────────
            var asm = Assembly.GetExecutingAssembly();
            var iconStream = asm.GetManifestResourceStream("WinKit.PTD.ico");
            int smallWidth = (int)SystemParameters.SmallIconWidth;
            int smallHeight = (int)SystemParameters.SmallIconHeight;

            Icon trayIcon;
            if (iconStream != null)
            {
                using var rawIcon = new Icon(iconStream);
                trayIcon = new Icon(rawIcon, new System.Drawing.Size(smallWidth, smallHeight));
            }
            else
            {
                trayIcon = SystemIcons.Application;
            }

            var ver = asm.GetName().Version;
            var version = ver != null ? $"{ver.Major}.{ver.Minor}" : "2.5";

            _icon = new SWF.NotifyIcon
            {
                Icon = trayIcon,
                Text = $"WinKit v{version}",
                Visible = true
            };

            // ── 5. 鼠标交互（单击置空，右键弹出菜单）──────────────────
            _icon.MouseClick += (s, e) =>
            {
                if (e.Button == SWF.MouseButtons.Right)
                {
                    _todoWindow.Dispatcher.Invoke(() =>
                    {
                        POINT mousePos;
                        GetCursorPos(out mousePos);

                        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(_todoWindow);

                        _menuHostWindow.Show();
                        _menuHostWindow.Activate();
                        var hwnd = new WindowInteropHelper(_menuHostWindow).Handle;
                        SetForegroundWindow(hwnd);

                        _contextMenu.PlacementTarget = _menuHostWindow;
                        _contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
                        _contextMenu.HorizontalOffset = mousePos.X / dpi.DpiScaleX;
                        _contextMenu.VerticalOffset = mousePos.Y / dpi.DpiScaleY - 2;

                        _contextMenu.IsOpen = true;
                    });
                }
            };

            // ── 6. 双击托盘图标：切换 TodoList（受开关控制）─────────────
            _icon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == SWF.MouseButtons.Left)
                {
                    if (!_settingsManager.Settings.TrayDoubleClickTodoEnabled) return;

                    _todoWindow.Dispatcher.Invoke(() =>
                    {
                        var todoWin = (Todo.MainWindow)_todoWindow;
                        if (todoWin.IsVisible)
                        {
                            todoWin.HideInlineInputAndWindow();
                        }
                        else
                        {
                            todoWin.ToggleTopmostAndPassThrough();
                        }
                    });
                }
            };

            // 监听偏好设置变更实时应用不透明度
            _settingsManager.SettingsChanged += (s, settings) => ApplyOpacity(settings.WindowOpacity);

            // 应用已保存的窗口不透明度
            ApplyOpacity(_settingsManager.Settings.WindowOpacity);
        }

        public void SubscribeHotkeysChanged(Action handler)
        {
            _preferencesWindow.HotkeysChanged += handler;
        }

        public void SyncPinMenuItem() { }
        public void SyncPassThroughMenuItem() { }

        public void ShowPreferencesWindow()
        {
            _todoWindow.Dispatcher.Invoke(() =>
            {
                if (_todoWindow is Todo.MainWindow todoWin)
                {
                    todoWin.IsPreferencesWindowOpen = true;
                    todoWin.HideInlineInput();
                    if (todoWin.ActiveEditDialog != null)
                    {
                        todoWin.ActiveEditDialog.Close();
                        todoWin.ActiveEditDialog = null;
                    }
                }
            });

            _preferencesWindow.Dispatcher.Invoke(() =>
            {
                _preferencesWindow.Show();
                _preferencesWindow.Activate();
            });
        }

        public void ShowHistoryWindow()
        {
            _historyWindow.Dispatcher.Invoke(() =>
            {
                _historyWindow.ReloadRecycleBin();
                _historyWindow.Show();
                _historyWindow.Activate();
            });
        }

        public void ShowAboutWindow()
        {
            _aboutWindow.Dispatcher.Invoke(() =>
            {
                _aboutWindow.Show();
                _aboutWindow.Activate();
            });
        }

        public void ApplyOpacity(int level)
        {
            _todoWindow.Dispatcher.Invoke(() =>
            {
                double opacity = level / 100.0;
                _todoWindow.Opacity = opacity;
                _pasteWindow.Opacity = opacity;
            });
        }

        private void ShutdownApp()
        {
            _icon.Visible = false;
            _todoWindow.Dispatcher.Invoke(() => _todoWindow.Close());
            _pasteWindow.Dispatcher.Invoke(() => _pasteWindow.Close());
            _aboutWindow.Dispatcher.Invoke(() => _aboutWindow.Close());
            _historyWindow.Dispatcher.Invoke(() => _historyWindow.Close());
            _preferencesWindow.Dispatcher.Invoke(() => _preferencesWindow.Close());
            _menuHostWindow.Dispatcher.Invoke(() => _menuHostWindow.Close());
            _app.Shutdown();
        }

        public void Dispose()
        {
            _icon.Dispose();
            _aboutWindow.Dispatcher.Invoke(() => _aboutWindow.Close());
            _historyWindow.Dispatcher.Invoke(() => _historyWindow.Close());
            _preferencesWindow.Dispatcher.Invoke(() => _preferencesWindow.Close());
            _menuHostWindow.Dispatcher.Invoke(() => _menuHostWindow.Close());
        }
    }
}
