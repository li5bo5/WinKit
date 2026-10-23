using System;
using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
        private readonly Icon _trayIcon;
        private readonly Window _todoWindow;
        private readonly Window _pasteWindow;
        private readonly WpfApp _app;
        private readonly SettingsManager _settingsManager;

        // WPF 自研 ContextMenu
        private readonly ContextMenu _contextMenu;
        private readonly MenuHostWindow _menuHostWindow;

        // 按需创建的弹窗实例（关闭即 Close 销毁）
        private AboutWindow? _aboutWindow;
        private Todo.HistoryWindow? _historyWindow;
        private HotkeySettingsWindow? _preferencesWindow;
        private Action? _hotkeysChangedHandler;

        // 纯文字极简菜单项
        private readonly MenuItem _itemHistory;
        private readonly MenuItem _itemPreferences;
        private readonly MenuItem _itemAbout;
        private readonly MenuItem _itemExit;

        public TrayHelper(WpfApp app, SettingsManager settingsManager, Window todoWindow, Window pasteWindow)
        {
            _app = app;
            _settingsManager = settingsManager;
            _todoWindow = todoWindow;
            _pasteWindow = pasteWindow;

            // ── 1. 创建托盘菜单隐形宿主窗口 ─────────────────────────────
            _menuHostWindow = new MenuHostWindow();

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

            // ── 4. 创建系统托盘 NotifyIcon（强引用驻留，避免过早 GC）──────
            var asm = Assembly.GetExecutingAssembly();
            int smallWidth = (int)SystemParameters.SmallIconWidth;
            int smallHeight = (int)SystemParameters.SmallIconHeight;
            if (smallWidth <= 0) smallWidth = 16;
            if (smallHeight <= 0) smallHeight = 16;

            using (var iconStream = asm.GetManifestResourceStream("WinKit.PTD.ico"))
            {
                if (iconStream != null)
                {
                    _trayIcon = new Icon(iconStream, new System.Drawing.Size(smallWidth, smallHeight));
                }
                else
                {
                    _trayIcon = SystemIcons.Application;
                }
            }

            var ver = asm.GetName().Version;
            var version = ver != null ? (ver.Build > 0 ? ver.ToString(3) : $"{ver.Major}.{ver.Minor}") : "未知";

            _icon = new SWF.NotifyIcon
            {
                Icon = _trayIcon,
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
                        NativeMethods.GetCursorPos(out var mousePos);
                        double scale = NativeMethods.GetScaleForPoint(mousePos.X, mousePos.Y);

                        _menuHostWindow.Show();
                        _menuHostWindow.Activate();
                        var hwnd = new WindowInteropHelper(_menuHostWindow).Handle;
                        NativeMethods.SetForegroundWindow(hwnd);

                        _contextMenu.PlacementTarget = _menuHostWindow;
                        _contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
                        _contextMenu.HorizontalOffset = mousePos.X / scale;
                        _contextMenu.VerticalOffset = mousePos.Y / scale - 2;

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
                        todoWin.ToggleVisibility();
                    });
                }
            };
        }

        public void SubscribeHotkeysChanged(Action handler)
        {
            _hotkeysChangedHandler += handler;
        }

        public void ShowPreferencesWindow()
        {
            _todoWindow.Dispatcher.Invoke(() =>
            {
                if (_todoWindow is Todo.MainWindow todoWin)
                {
                    todoWin.IsPreferencesWindowOpen = true;
                    todoWin.HideInlineInput();
                }

                if (_preferencesWindow == null)
                {
                    _preferencesWindow = new HotkeySettingsWindow(_settingsManager);
                    if (_hotkeysChangedHandler != null)
                    {
                        _preferencesWindow.HotkeysChanged += _hotkeysChangedHandler;
                    }
                    _preferencesWindow.ResetTodoBoundsRequested += () =>
                    {
                        if (_todoWindow is Todo.MainWindow tw)
                        {
                            tw.ResetToDefaultPosition(true);
                            if (!tw.IsVisible)
                            {
                                tw.Show();
                            }
                            tw.Activate();
                        }
                    };
                    _preferencesWindow.Closed += (s, e) =>
                    {
                        if (_todoWindow is Todo.MainWindow tw)
                        {
                            tw.IsPreferencesWindowOpen = false;
                        }
                        _preferencesWindow = null;
                    };
                }

                _preferencesWindow.Show();
                _preferencesWindow.Activate();
            });
        }

        public void ShowHistoryWindow()
        {
            _todoWindow.Dispatcher.Invoke(() =>
            {
                if (_historyWindow == null)
                {
                    _historyWindow = new Todo.HistoryWindow((Todo.MainWindow)_todoWindow, _settingsManager);
                    _historyWindow.Closed += (s, e) => _historyWindow = null;
                }

                _historyWindow.Show();
                _historyWindow.Activate();
            });
        }

        public void ShowAboutWindow()
        {
            _todoWindow.Dispatcher.Invoke(() =>
            {
                if (_aboutWindow == null)
                {
                    _aboutWindow = new AboutWindow();
                    _aboutWindow.Closed += (s, e) => _aboutWindow = null;
                }

                _aboutWindow.Show();
                _aboutWindow.Activate();
            });
        }

        private void ShutdownApp()
        {
            _icon.Visible = false;
            _todoWindow.Dispatcher.Invoke(() => _todoWindow.Close());
            _pasteWindow.Dispatcher.Invoke(() => _pasteWindow.Close());
            _aboutWindow?.Dispatcher.Invoke(() => _aboutWindow.Close());
            _historyWindow?.Dispatcher.Invoke(() => _historyWindow.Close());
            _preferencesWindow?.Dispatcher.Invoke(() => _preferencesWindow.Close());
            _menuHostWindow.Dispatcher.Invoke(() => _menuHostWindow.Close());
            _app.Shutdown();
        }

        /// <summary>
        /// 确保系统托盘图标处于可见激活状态（在主消息泵空闲或 Explorer 重启自愈时调用）
        /// </summary>
        public void EnsureVisible()
        {
            try
            {
                if (_icon != null)
                {
                    _icon.Visible = true;
                }
            }
            catch { }
        }

        /// <summary>
        /// 带延迟重试的托盘注册兜底。
        /// Shell_NotifyIcon 失败时只返回 FALSE、绝不抛异常，因此只能靠重发 NIM_ADD 兜底。
        /// </summary>
        public void EnsureVisibleWithRetry(int times = 4, int delayMs = 1200)
        {
            EnsureVisible();
            if (times <= 1) return;

            var t = new System.Threading.Thread(() =>
            {
                for (int i = 1; i < times; i++)
                {
                    System.Threading.Thread.Sleep(delayMs);
                    try
                    {
                        _todoWindow.Dispatcher.BeginInvoke(new Action(EnsureVisible));
                    }
                    catch { }
                }
            });
            t.IsBackground = true;
            t.Name = "TrayIconRetry";
            t.Start();
        }

        public void Dispose()
        {
            try
            {
                _icon.Visible = false;
                _icon.Dispose();
            }
            catch { }

            _trayIcon?.Dispose();
            _aboutWindow?.Dispatcher.Invoke(() => _aboutWindow.Close());
            _historyWindow?.Dispatcher.Invoke(() => _historyWindow.Close());
            _preferencesWindow?.Dispatcher.Invoke(() => _preferencesWindow.Close());
            _menuHostWindow.Dispatcher.Invoke(() => _menuHostWindow.Close());
        }
    }
}
