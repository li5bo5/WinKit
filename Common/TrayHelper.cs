using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using SWF = System.Windows.Forms;
using WinKit.Common;

namespace WinKit.Common
{
    /// <summary>
    /// 临时用于获取焦点的隐形辅助窗口，以实现 WPF ContextMenu 完美的失焦自动关闭机制
    /// </summary>
    internal class MenuHostWindow : Window
    {
        public MenuHostWindow()
        {
            Width = 0;
            Height = 0;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            // 移到可视范围之外
            Left = -10000;
            Top = -10000;
        }
    }

    /// <summary>
    /// 统一的系统托盘服务 — 基于 WPF ContextMenu 全自研，彻底匹配系统风格并完美支持失焦隐藏
    /// </summary>
    public class TrayHelper : IDisposable
    {
        private readonly SWF.NotifyIcon _icon;
        private readonly Window _todoWindow;
        private readonly Window _pasteWindow;
        private readonly System.Windows.Application _app;
        private readonly SettingsManager _settingsManager;

        // 隐形焦点宿主窗口
        private readonly MenuHostWindow _menuHostWindow;

        // 关于窗口
        private readonly AboutWindow _aboutWindow;

        // WPF ContextMenu 容器
        private readonly ContextMenu _contextMenu;

        // TodoList 子菜单项
        private readonly MenuItem _todoMenu;
        private readonly MenuItem _todoShow;
        private readonly MenuItem _todoHide;
        private readonly MenuItem _todoPin;
        private readonly MenuItem _todoPassThrough;

        // Clipboard 子菜单项
        private readonly MenuItem _pasteMenu;
        private readonly MenuItem _pasteShow;
        private readonly MenuItem _pasteClear;
        private readonly MenuItem _pasteDedup;
        private readonly MenuItem _pasteMonitoring; // 剪贴板开启/关闭选项

        // 顶级菜单项
        private readonly MenuItem _itemAutoStart;
        private readonly MenuItem _itemAbout; // 关于选项
        private readonly MenuItem _itemExit;

        // 不透明度子菜单
        private readonly MenuItem _opacityMenu;
        private static readonly int[] _opacityLevels = { 40, 60, 70, 80, 90, 100 };
        private readonly MenuItem[] _opacityItems;

        // 双击/单击计时器
        private readonly SWF.Timer _clickTimer;

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

        public TrayHelper(System.Windows.Application app, SettingsManager settingsManager, Window todoWindow, Window pasteWindow)
        {
            _app = app;
            _settingsManager = settingsManager;
            _todoWindow = todoWindow;
            _pasteWindow = pasteWindow;

            // ── 1. 创建隐形焦点宿主窗口 & 关于窗口 ───────────────
            _menuHostWindow = new MenuHostWindow();
            _menuHostWindow.Show();
            _menuHostWindow.Hide(); // 触发句柄创建并在后台待命

            _aboutWindow = new AboutWindow();
            _aboutWindow.Show();
            _aboutWindow.Hide();

            // ── 2. 创建 WPF ContextMenu ───────────────────────
            _contextMenu = new ContextMenu();

            // ── 3. 组建 TodoList 子菜单 ────────────────────────
            _todoMenu = new MenuItem { Header = "TodoList" };
            
            _todoShow = new MenuItem { Header = "显示" };
            _todoShow.Click += (s, e) => ShowTodoWindow();
            
            _todoHide = new MenuItem { Header = "隐藏" };
            _todoHide.Click += (s, e) => HideTodoWindow();

            _todoPin = new MenuItem { Header = "置顶" };
            _todoPin.Click += (s, e) => ToggleTodoPin();

            _todoPassThrough = new MenuItem { Header = "鼠标穿透" };
            _todoPassThrough.Click += (s, e) => ToggleTodoPassThrough();

            _todoMenu.Items.Add(_todoShow);
            _todoMenu.Items.Add(_todoHide);
            _todoMenu.Items.Add(_todoPin);
            _todoMenu.Items.Add(_todoPassThrough);

            // ── 4. 组建 Clipboard 子菜单 ───────────────────────
            _pasteMenu = new MenuItem { Header = "Clipboard" };

            _pasteShow = new MenuItem { Header = "显示历史" };
            _pasteShow.Click += (s, e) => ShowPasteWindow();

            _pasteClear = new MenuItem { Header = "清空历史" };
            _pasteClear.Click += (s, e) => ClearPasteHistory();

            _pasteDedup = new MenuItem { Header = "启用去重" };
            _pasteDedup.IsCheckable = true;
            _pasteDedup.IsChecked = _settingsManager.Settings.PasteEnableTextDeduplication;
            _pasteDedup.Click += (s, e) => TogglePasteDedup();

            _pasteMonitoring = new MenuItem { Header = "启用" };
            _pasteMonitoring.IsCheckable = true;
            _pasteMonitoring.IsChecked = _settingsManager.Settings.PasteEnableMonitoring;
            _pasteMonitoring.Click += (s, e) => TogglePasteMonitoring();

            _pasteMenu.Items.Add(_pasteShow);
            _pasteMenu.Items.Add(_pasteClear);
            _pasteMenu.Items.Add(_pasteDedup);
            _pasteMenu.Items.Add(_pasteMonitoring);

            // ── 5. 组建不透明度子菜单 ──────────────────────
            _opacityMenu = new MenuItem { Header = "不透明度" };
            _opacityItems = new MenuItem[_opacityLevels.Length];
            for (int i = 0; i < _opacityLevels.Length; i++)
            {
                int level = _opacityLevels[i]; // 捕获循环变量
                var item = new MenuItem
                {
                    Header       = $"{level}%",
                    IsCheckable  = true,
                    IsChecked    = (_settingsManager.Settings.WindowOpacity == level)
                };
                item.Click += (s, e) => SetOpacity(level);
                _opacityItems[i] = item;
                _opacityMenu.Items.Add(item);
            }

            // ── 6. 组建开机自启顶级菜单 ───────────────────────
            _itemAutoStart = new MenuItem { Header = "开机启动" };
            _itemAutoStart.IsCheckable = true;
            _itemAutoStart.IsChecked = AutoStartHelper.IsAutoStartEnabled();
            _itemAutoStart.Click += (s, e) => {
                AutoStartHelper.SetAutoStart(_itemAutoStart.IsChecked);
            };

            // ── 7. 组建关于顶级菜单 ───────────────────────────
            _itemAbout = new MenuItem { Header = "关于" };
            _itemAbout.Click += (s, e) => ShowAboutWindow();

            // ── 8. 组建退出顶级菜单 ───────────────────────────
            _itemExit = new MenuItem { Header = "退出" };
            _itemExit.Click += (s, e) => ShutdownApp();

            // ── 9. 上下文菜单组装 ──────────────────────────────
            _contextMenu.Items.Add(_todoMenu);
            _contextMenu.Items.Add(_pasteMenu);
            _contextMenu.Items.Add(new Separator());
            _contextMenu.Items.Add(_opacityMenu);
            _contextMenu.Items.Add(new Separator());
            _contextMenu.Items.Add(_itemAutoStart);
            _contextMenu.Items.Add(_itemAbout);
            _contextMenu.Items.Add(new Separator());
            _contextMenu.Items.Add(_itemExit);

            // ── 9. 事件监听联动：失焦自动隐藏菜单 ──────────────────
            _menuHostWindow.Deactivated += (s, e) =>
            {
                // 当隐形宿主窗口失去焦点时，主动收回菜单
                _contextMenu.IsOpen = false;
                _menuHostWindow.Hide();
            };

            _contextMenu.Closed += (s, e) =>
            {
                // 菜单完全闭合时隐藏辅助窗口
                _menuHostWindow.Hide();
            };

            // ── 10. 创建托盘图标 ────────────────────────────────
            var asm = Assembly.GetExecutingAssembly();
            var iconStream = asm.GetManifestResourceStream("WinKit.PTD.ico");
            int smallWidth = (int)SystemParameters.SmallIconWidth;
            int smallHeight = (int)SystemParameters.SmallIconHeight;
            int targetWidth = smallWidth <= 16 ? 32 : (smallWidth <= 24 ? 32 : 48);
            int targetHeight = smallHeight <= 16 ? 32 : (smallHeight <= 24 ? 32 : 48);
            var trayIcon = iconStream != null ? new Icon(iconStream, new System.Drawing.Size(targetWidth, targetHeight)) : SystemIcons.Application;
            var version = asm.GetName().Version?.ToString(3) ?? "1.0.0";

            _icon = new SWF.NotifyIcon
            {
                Icon = trayIcon,
                Text = $"WinKit v{version}",
                Visible = true
            };

            // ── 11. 单击判定定时器 (处理左键单击/双击) ─────────────
            _clickTimer = new SWF.Timer();
            _clickTimer.Interval = 200;
            _clickTimer.Tick += (s, e) =>
            {
                _clickTimer.Stop();
                _todoWindow.Dispatcher.Invoke(() =>
                {
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
            };

            _icon.MouseClick += (s, e) =>
            {
                if (e.Button == SWF.MouseButtons.Left)
                {
                    _clickTimer.Start();
                }
                else if (e.Button == SWF.MouseButtons.Right)
                {
                    // 右键点击：弹出 WPF 自研 ContextMenu 菜单
                    _todoWindow.Dispatcher.Invoke(() =>
                    {
                        SyncMenuStates();

                        POINT mousePos;
                        GetCursorPos(out mousePos);

                        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(_todoWindow);

                        // 强行展现隐形宿主窗口并激活到系统前台，确保失焦响应在任何全局环境下都绝对生效
                        _menuHostWindow.Show();
                        _menuHostWindow.Activate();
                        var hwnd = new WindowInteropHelper(_menuHostWindow).Handle;
                        SetForegroundWindow(hwnd);

                        // 弹出菜单，绑定到隐形宿主上
                        _contextMenu.PlacementTarget = _menuHostWindow;
                        _contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
                        _contextMenu.HorizontalOffset = mousePos.X / dpi.DpiScaleX;
                        _contextMenu.VerticalOffset = mousePos.Y / dpi.DpiScaleY - 2;
                        
                        _contextMenu.IsOpen = true;
                    });
                }
            };

            _icon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == SWF.MouseButtons.Left)
                {
                    _clickTimer.Stop();
                    _todoWindow.Dispatcher.Invoke(() =>
                    {
                        if (_todoWindow.IsVisible) _todoWindow.Hide();
                        else { _todoWindow.Show(); _todoWindow.Activate(); }
                    });
                }
            };

            SyncPinMenuItem();
            SyncPassThroughMenuItem();

            // 初始化应用已保存的不透明度
            ApplyOpacity(_settingsManager.Settings.WindowOpacity);
        }

        private void SyncMenuStates()
        {
            bool todoVisible = _todoWindow.IsVisible;
            _todoShow.Visibility = todoVisible ? Visibility.Collapsed : Visibility.Visible;
            _todoHide.Visibility = todoVisible ? Visibility.Visible : Visibility.Collapsed;

            bool clipboardEnabled = _settingsManager.Settings.PasteEnableMonitoring;
            _pasteShow.IsEnabled = clipboardEnabled;
            _pasteClear.IsEnabled = clipboardEnabled;
            _pasteDedup.IsEnabled = clipboardEnabled;

            _pasteDedup.IsChecked = _settingsManager.Settings.PasteEnableTextDeduplication;
            _pasteMonitoring.IsChecked = clipboardEnabled;
            _itemAutoStart.IsChecked = AutoStartHelper.IsAutoStartEnabled();

            // 同步不透明度选中状态
            SyncOpacityMenu(_settingsManager.Settings.WindowOpacity);
        }

        public void SyncPinMenuItem()
        {
            _todoPin.Header = ((Todo.MainWindow)_todoWindow).IsPinned ? "取消置顶" : "置顶";
        }

        public void SyncPassThroughMenuItem()
        {
            _todoPassThrough.Header = ((Todo.MainWindow)_todoWindow).IsPassThrough ? "关闭鼠标穿透" : "鼠标穿透";
        }

        // ── 不透明度操作 ────────────────────────────────────────
        private void SetOpacity(int opacity)
        {
            var settings = _settingsManager.Settings;
            settings.WindowOpacity = opacity;
            _settingsManager.SaveSettings(settings);
            ApplyOpacity(opacity);
            SyncOpacityMenu(opacity);
        }

        private void ApplyOpacity(int opacity)
        {
            // 计算 Alpha 通道值 (0-255) 并转换为 #AARRGGBB 格式
            int alpha = (int)Math.Round(opacity / 100.0 * 255);
            string hex = $"#{alpha:X2}FFFFFF";
            var color = (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var brush = new System.Windows.Media.SolidColorBrush(color);

            // 直接设置两个主窗口的 RootBorder.Background
            _todoWindow.Dispatcher.Invoke(() =>
            {
                var border = ((System.Windows.FrameworkElement)_todoWindow.Content)
                             as System.Windows.Controls.Border
                             ?? _todoWindow.FindName("RootBorder") as System.Windows.Controls.Border;
                if (border != null) border.Background = brush;
            });
            _pasteWindow.Dispatcher.Invoke(() =>
            {
                var border = _pasteWindow.FindName("RootBorder") as System.Windows.Controls.Border;
                if (border != null) border.Background = brush;
            });
        }

        private void SyncOpacityMenu(int currentOpacity)
        {
            for (int i = 0; i < _opacityLevels.Length; i++)
            {
                _opacityItems[i].IsChecked = (_opacityLevels[i] == currentOpacity);
            }
        }

        private void ShowTodoWindow()
        {
            _todoWindow.Dispatcher.Invoke(() =>
            {
                _todoWindow.Show();
                _todoWindow.Activate();
            });
        }

        private void HideTodoWindow()
        {
            _todoWindow.Dispatcher.Invoke(() => _todoWindow.Hide());
        }

        private void ToggleTodoPin()
        {
            _todoWindow.Dispatcher.Invoke(() =>
            {
                _todoWindow.Show();
                _todoWindow.Activate();
                ((Todo.MainWindow)_todoWindow).TogglePinFromTray();
                SyncPinMenuItem();
            });
        }

        private void ToggleTodoPassThrough()
        {
            _todoWindow.Dispatcher.Invoke(() =>
            {
                ((Todo.MainWindow)_todoWindow).TogglePassThroughFromTray();
                SyncPassThroughMenuItem();
            });
        }

        private void ShowPasteWindow()
        {
            _pasteWindow.Dispatcher.Invoke(() =>
            {
                ((Clipboard.MainWindow)_pasteWindow).ShowAtMouse();
            });
        }

        private void ClearPasteHistory()
        {
            if (System.Windows.MessageBox.Show("确定清空全部剪贴板历史吗？此操作不可撤销。", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _pasteWindow.Dispatcher.Invoke(() =>
                {
                    _pasteWindow.Hide();
                });
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var app = (App)System.Windows.Application.Current;
                    app.ClipboardManager?.ClearAll();
                });
            }
        }

        private void TogglePasteDedup()
        {
            var settings = _settingsManager.Settings;
            settings.PasteEnableTextDeduplication = _pasteDedup.IsChecked;
            _settingsManager.SaveSettings(settings);
        }

        private void TogglePasteMonitoring()
        {
            var settings = _settingsManager.Settings;
            settings.PasteEnableMonitoring = _pasteMonitoring.IsChecked;
            _settingsManager.SaveSettings(settings);
            ((App)System.Windows.Application.Current).ToggleClipboardFeature(_pasteMonitoring.IsChecked);
            SyncMenuStates();
        }

        private void ShowAboutWindow()
        {
            _aboutWindow.Dispatcher.Invoke(() =>
            {
                _aboutWindow.Show();
                _aboutWindow.Activate();
            });
        }

        private void ShutdownApp()
        {
            _todoWindow.Dispatcher.Invoke(() => _todoWindow.Close());
            _pasteWindow.Dispatcher.Invoke(() => _pasteWindow.Close());
            _aboutWindow.Dispatcher.Invoke(() => _aboutWindow.Close());
            _menuHostWindow.Dispatcher.Invoke(() => _menuHostWindow.Close());
            _app.Shutdown();
        }

        public void Dispose()
        {
            _clickTimer.Dispose();
            _icon.Dispose();
            _aboutWindow.Dispatcher.Invoke(() => _aboutWindow.Close());
            _menuHostWindow.Dispatcher.Invoke(() => _menuHostWindow.Close());
        }
    }
}
