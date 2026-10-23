using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.ComponentModel;
using System.Windows.Data;
using WinKit.Clipboard.Models;
using WinKit.Clipboard.Services;
using WinKit.Common;
using WinPoint = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Button = System.Windows.Controls.Button;

namespace WinKit.Clipboard
{
    public partial class MainWindow : Window
    {
        private readonly ClipboardManager _clipboardManager;
        private readonly ClipboardService _clipboardService;
        private readonly SettingsManager  _settingsManager;

        // ── 前台目标窗口句柄（呼出剪贴板前的活动窗口） ──
        private IntPtr _lastTargetHwnd = IntPtr.Zero;

        // ── 全局鼠标点击监听（用于精准外部点击隐藏）────
        private readonly GlobalMouseClickMonitor _mouseMonitor = new();

        // ── Pin（固定）状态 ────────────────────────────
        private bool _isPinned = false;
        public  bool IsPinned  => _isPinned;

        // ── 搜索过滤视图 ──────────────────────────────
        private readonly ICollectionView _view;

        // ══════════════════════════════════════════════
        // 构造函数
        // ══════════════════════════════════════════════
        public MainWindow(ClipboardManager clipboardManager, ClipboardService clipboardService, SettingsManager settingsManager)
        {
            InitializeComponent();
            _clipboardManager = clipboardManager;
            _clipboardService = clipboardService;
            _settingsManager  = settingsManager;

            ClipboardList.ItemsSource = _clipboardManager.Items;

            // 搜索过滤：作用于默认视图，不改动底层数据集合
            _view = CollectionViewSource.GetDefaultView(_clipboardManager.Items);
            _view.Filter = FilterItem;

            ((System.Collections.Specialized.INotifyCollectionChanged)_clipboardManager.Items)
                .CollectionChanged += (s, e) => UpdateUIStates();

            Loaded += (s, e) =>
            {
                UpdateUIStates();
                LoadSettings();
                SetTitleButtonsOpacity(_isPinned ? 1 : 0);
            };

            this.KeyDown += Window_KeyDown;

            // 窗口可见性变化：隐藏时停止鼠标监听
            this.IsVisibleChanged += (s, e) =>
            {
                if (!(bool)e.NewValue)
                {
                    _mouseMonitor.Stop();
                }
            };

            // 配置鼠标点击监听 — 点击剪贴板窗口以外的区域时，若未固定则隐藏
            _mouseMonitor.ClickedOutside += OnClickedOutsideWindow;

            // 统一挂载右下角拖拽缩放
            NativeMethods.AttachResizeGrip(this, ResizeGripArea);
        }

        // ══════════════════════════════════════════════
        // 初始化 — 设置 WS_EX_TOOLWINDOW（不再使用 WS_EX_NOACTIVATE，以便搜索框获得键盘焦点）
        // ══════════════════════════════════════════════
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd    = new WindowInteropHelper(this).Handle;
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
        }

        // ══════════════════════════════════════════════
        // 标题栏按钮悬浮显隐控制
        // ══════════════════════════════════════════════
        private void TitleBar_MouseEnter(object sender, MouseEventArgs e) => SetTitleButtonsOpacity(1);
        private void TitleBar_MouseLeave(object sender, MouseEventArgs e) => SetTitleButtonsOpacity(0);

        private void SetTitleButtonsOpacity(double opacity)
        {
            PinBtn.Opacity   = opacity;
            CloseBtn.Opacity = opacity;
        }

        // ══════════════════════════════════════════════
        // 设置加载与保存
        // ══════════════════════════════════════════════
        private void LoadSettings()
        {
            _isPinned    = false;
            this.Topmost = false;
            UpdatePinButton();
        }

        private void UpdatePinButton()
        {
            PinBtn.Content = "📌";
            if (TopAccentLine != null)
                TopAccentLine.Visibility = _isPinned ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TogglePinState()
        {
            _isPinned    = !_isPinned;
            this.Topmost = _isPinned;
            UpdatePinButton();

            if (IsVisible) RefreshMouseMonitor();
        }

        private void PinBtn_Click(object sender, RoutedEventArgs e) => TogglePinState();

        // ══════════════════════════════════════════════
        // Esc 退出（搜索框有内容时先清空，为空时隐藏窗口）
        // ══════════════════════════════════════════════
        public void HandleEscape()
        {
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = string.Empty;
                SearchBox.Focus();
            }
            else
            {
                Hide();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HandleEscape();
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════
        // 搜索过滤与键盘交互
        // ══════════════════════════════════════════════
        private bool FilterItem(object obj)
        {
            string keyword = SearchBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(keyword)) return true;

            if (obj is ClipboardItem item)
            {
                // 关键词非空时不显示图片条目，仅不区分大小写匹配文本
                if (item.IsImage) return false;
                return !string.IsNullOrEmpty(item.Content) &&
                       item.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_view == null) return;
            _view.Refresh();
            if (ClipboardList.Items.Count > 0)
            {
                ClipboardList.SelectedIndex = 0;
                ClipboardList.ScrollIntoView(ClipboardList.SelectedItem);
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                int count = ClipboardList.Items.Count;
                if (count > 0)
                {
                    int next = ClipboardList.SelectedIndex + 1;
                    ClipboardList.SelectedIndex = next < count ? next : 0;
                    ClipboardList.ScrollIntoView(ClipboardList.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                int count = ClipboardList.Items.Count;
                if (count > 0)
                {
                    int prev = ClipboardList.SelectedIndex - 1;
                    ClipboardList.SelectedIndex = prev >= 0 ? prev : count - 1;
                    ClipboardList.ScrollIntoView(ClipboardList.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (ClipboardList.SelectedItem is ClipboardItem item)
                {
                    UseSelectedItem(item);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HandleEscape();
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════
        // UI 状态更新
        // ══════════════════════════════════════════════
        private void UpdateUIStates()
        {
            int count       = _clipboardManager.Items.Count;
            CountText.Text  = $"{count} 项";
            EmptyPlaceholder.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════
        // ShowAtMouse — 在鼠标附近弹出并强制置于最顶层
        // ══════════════════════════════════════════════
        public void ShowAtMouse()
        {
            // ① 呼出前立即记录当前前台窗口（排除本进程自身窗口）
            IntPtr fgWnd = NativeMethods.GetForegroundWindow();
            var selfHwnd = new WindowInteropHelper(this).EnsureHandle();
            if (fgWnd != IntPtr.Zero && fgWnd != selfHwnd && !NativeMethods.IsOwnWindow(fgWnd) && NativeMethods.IsWindow(fgWnd))
                _lastTargetHwnd = fgWnd;

            // ② 按目标屏幕自身 DPI 计算弹出物理坐标并置顶显示
            NativeMethods.GetCursorPos(out var mousePos);
            NativeMethods.ShowPopupAtPoint(this, selfHwnd, mousePos.X, mousePos.Y, offsetLogical: 10, beforeShow: () =>
            {
                _isPinned = false;
                UpdatePinButton();
                SetTitleButtonsOpacity(0);
                if (!string.IsNullOrEmpty(SearchBox.Text))
                {
                    SearchBox.Text = string.Empty;
                }
                if (ClipboardList.Items.Count > 0)
                {
                    ClipboardList.SelectedIndex = 0;
                    if (!_settingsManager.Settings.PasteRememberScrollPosition)
                        ClipboardList.ScrollIntoView(ClipboardList.Items[0]);
                }
                UpdateLayout();
                _lastMouseMovePos = Mouse.GetPosition(this);
            });

            Activate();
            NativeMethods.ForceSetForegroundWindow(selfHwnd);
            SearchBox.Focus();

            // ③ 启动外部鼠标点击监听
            RefreshMouseMonitor();
        }

        private void RefreshMouseMonitor()
        {
            if (!IsVisible)
            {
                _mouseMonitor.Stop();
                return;
            }

            if (_isPinned)
            {
                _mouseMonitor.Stop();
            }
            else
            {
                var selfHwnd = new WindowInteropHelper(this).Handle;
                _mouseMonitor.Start(new[] { selfHwnd });
            }
        }

        private void OnClickedOutsideWindow()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!_isPinned && IsVisible) Hide();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            _mouseMonitor.Dispose();
            base.OnClosed(e);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) NativeMethods.DragMoveWithBounds(this);
        }

        public new void Hide()
        {
            _isPinned = false;
            UpdatePinButton();
            SetTitleButtonsOpacity(0);
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = string.Empty;
            }
            if (ClipboardList.Items.Count > 0)
            {
                ClipboardList.SelectedIndex = 0;
                if (!_settingsManager.Settings.PasteRememberScrollPosition)
                {
                    ClipboardList.ScrollIntoView(ClipboardList.Items[0]);
                }
            }
            UpdateLayout();
            base.Hide();
            RefreshMouseMonitor();
        }

        public void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

        // ══════════════════════════════════════════════
        // 列表项鼠标移动同步选中 & 单击零延迟直接回填
        // ══════════════════════════════════════════════
        private WinPoint _lastMouseMovePos;

        private void ListBoxItem_MouseMove(object sender, MouseEventArgs e)
        {
            NativeMethods.SelectListBoxItemOnMouseMove(this, sender, e, ref _lastMouseMovePos);
        }

        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 排除子按钮点击（预览、删除等）
            if (IsClickOnActionButton(e.OriginalSource as DependencyObject)) return;

            if (sender is ListBoxItem item && item.Content is ClipboardItem clipItem)
            {
                UseSelectedItem(clipItem);
                e.Handled = true;
            }
        }

        private bool _isPasting = false;

        /// <summary>
        /// 回填选中的文本或图片到前台窗口（未固定时隐藏，固定时保持置顶连续粘贴）
        /// </summary>
        private async void UseSelectedItem(ClipboardItem item)
        {
            if (item == null || _isPasting) return;
            _isPasting = true;

            try
            {
                // 开启内部主动回填锁，在此期间绝对不将剪贴板变化当作用户外部新复制
                _clipboardService.BeginInternalPaste();

                // 1. 设置自回填忽略通知并写入系统剪贴板
                if (item.IsImage && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                {
                    _clipboardService.NotifyUpcomingSelfPaste(null, item.ImageHash);

                    // 使用非阻塞后台 STA 线程写入图片
                    bool setOk = await FastClipboard.SetImageFastAsync(item.ImagePath);
                    if (!setOk) return;

                    uint seq = NativeMethods.GetClipboardSequenceNumber();
                    _clipboardService.RegisterSelfPasteSequence(seq, null, item.ImageHash);
                }
                else if (item.IsText && !string.IsNullOrEmpty(item.Content))
                {
                    _clipboardService.NotifyUpcomingSelfPaste(item.Content);

                    bool setOk = false;
                    for (int i = 0; i < 3 && !setOk; i++)
                    {
                        try
                        {
                            // 使用非阻塞的 FastClipboard 写入剪贴板
                            setOk = await FastClipboard.SetTextFastAsync(item.Content);
                        }
                        catch
                        {
                            // 若仍然失败，进行短暂延迟后重试
                            await Task.Delay(30);
                        }
                    }
                    if (!setOk) return;

                    uint seq = NativeMethods.GetClipboardSequenceNumber();
                    _clipboardService.RegisterSelfPasteSequence(seq, item.Content);
                }
                else
                {
                    return;
                }

                // 2. 若未固定，先隐藏窗口
                if (!_isPinned)
                {
                    Hide();
                    await Task.Delay(50);
                }

                // 3. 精准归还前台焦点并模拟 Ctrl+V 粘贴
                await NativeMethods.RestoreFocusAndPasteAsync(_lastTargetHwnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard: 回填失败 ({ex.Message})");
            }
            finally
            {
                _clipboardService.EndInternalPaste();
                _isPasting = false;
            }
        }

        // ══════════════════════════════════════════════
        // 键盘 Enter 确认回填
        // ══════════════════════════════════════════════
        private void ClipboardList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ClipboardList.SelectedItem is ClipboardItem item)
                {
                    UseSelectedItem(item);
                    e.Handled = true;
                }
            }
        }

        // ══════════════════════════════════════════════
        // “小眼睛”图片系统预览按钮
        // ══════════════════════════════════════════════
        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClipboardItem item && !string.IsNullOrEmpty(item.ImagePath))
            {
                try
                {
                    if (File.Exists(item.ImagePath))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = item.ImagePath,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"预览图片失败: {ex.Message}");
                }
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════
        // 删除单项
        // ══════════════════════════════════════════════
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClipboardItem item)
            {
                int prevIdx = ClipboardList.SelectedIndex;
                _clipboardManager.RemoveItem(item.Id);
                if (ClipboardList.SelectedIndex < 0 && ClipboardList.Items.Count > 0)
                {
                    ClipboardList.SelectedIndex = Math.Min(Math.Max(0, prevIdx), ClipboardList.Items.Count - 1);
                }
                e.Handled = true;
            }
        }

        // ══════════════════════════════════════════════
        // 清空全部
        // ══════════════════════════════════════════════
        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show(this, "确定清空全部非置顶的剪贴板历史吗？此操作不可撤销。", "提示",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _clipboardManager.ClearAll();
            }
        }

        // ══════════════════════════════════════════════
        // 取消全部置顶
        // ══════════════════════════════════════════════
        private void UnpinAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_clipboardManager.HasPinnedItems)
            {
                System.Windows.MessageBox.Show(this, "当前没有置顶的条目。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (System.Windows.MessageBox.Show(this, "确定取消所有已置顶条目吗？", "提示",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _clipboardManager.UnpinAll();
            }
        }

        // ══════════════════════════════════════════════
        // 置顶 / 取消置顶单项
        // ══════════════════════════════════════════════
        private void PinItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClipboardItem item)
            {
                bool success = _clipboardManager.TogglePin(item);
                if (!success)
                {
                    System.Windows.MessageBox.Show(this, $"最多只能置顶 {ClipboardManager.MaxPinnedItems} 个条目", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                e.Handled = true;
            }
        }

        private bool IsClickOnActionButton(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj is Button btn && (btn.Name == "DeleteBtn" || btn.Name == "PreviewBtn" || btn.Name == "PinItemBtn")) return true;
                obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
            }
            return false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (App.IsExiting)
            {
                base.OnClosing(e);
                return;
            }
            e.Cancel = true;
            Hide();
        }
    }
}
