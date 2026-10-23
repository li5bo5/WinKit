using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WinKit.Clipboard.Models;
using WinKit.Clipboard.Services;
using WinKit.Common;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WinKit.Clipboard
{
    public partial class QuickPhraseWindow : Window
    {
        private readonly QuickPhraseManager _phraseManager;
        private readonly ClipboardService _clipboardService;
        private readonly GlobalMouseClickMonitor _mouseMonitor = new();

        private IntPtr _lastTargetHwnd = IntPtr.Zero;

        public QuickPhraseWindow(QuickPhraseManager phraseManager, ClipboardService clipboardService)
        {
            InitializeComponent();
            _phraseManager = phraseManager;
            _clipboardService = clipboardService;

            PhrasesList.ItemsSource = _phraseManager.Items;

            this.IsVisibleChanged += (s, e) =>
            {
                if (!(bool)e.NewValue)
                {
                    _mouseMonitor.Stop();
                }
            };

            _mouseMonitor.ClickedOutside += () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (IsVisible) Hide();
                });
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            // 仅注入 WS_EX_TOOLWINDOW 防止任务栏污染，不注入 WS_EX_NOACTIVATE 确保可正常获取焦点接收键盘事件
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
        }

        /// <summary>
        /// 在前台输入光标处（或鼠标附近）弹出并激活键盘焦点
        /// </summary>
        public void ShowNearCaretOrMouse(IntPtr explicitTargetHwnd = default)
        {
            // 1. 重新加载短语并更新警告栏
            _phraseManager.Reload();
            WarningBar.Visibility = _phraseManager.HasFormatError ? Visibility.Visible : Visibility.Collapsed;

            // 2. 记录前台输入窗口
            var selfHwnd = new WindowInteropHelper(this).EnsureHandle();
            if (explicitTargetHwnd != IntPtr.Zero && NativeMethods.IsWindow(explicitTargetHwnd))
            {
                _lastTargetHwnd = explicitTargetHwnd;
            }
            else
            {
                IntPtr fgWnd = NativeMethods.GetForegroundWindow();
                if (fgWnd != IntPtr.Zero && fgWnd != selfHwnd && NativeMethods.IsWindow(fgWnd))
                {
                    _lastTargetHwnd = fgWnd;
                }
            }

            // 3. 计算坐标：优先尝试获取 Caret 坐标
            int targetX = 0, targetY = 0;
            bool gotCaret = false;

            if (_lastTargetHwnd != IntPtr.Zero)
            {
                uint threadId = NativeMethods.GetWindowThreadProcessId(_lastTargetHwnd, out _);
                var guiInfo = new NativeMethods.GUITHREADINFO();
                guiInfo.cbSize = Marshal.SizeOf(guiInfo);

                if (NativeMethods.GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.hwndCaret != IntPtr.Zero)
                {
                    var pt = new NativeMethods.POINT { X = guiInfo.rcCaret.Left, Y = guiInfo.rcCaret.Bottom };
                    if (NativeMethods.ClientToScreen(guiInfo.hwndCaret, ref pt) && (pt.X > 0 || pt.Y > 0))
                    {
                        targetX = pt.X;
                        targetY = pt.Y + 4;
                        gotCaret = true;
                    }
                }
            }

            // 4. 若未获取到 Caret 坐标，回退至鼠标位置
            if (!gotCaret)
            {
                NativeMethods.GetCursorPos(out var mousePos);
                targetX = mousePos.X + 10;
                targetY = mousePos.Y + 10;
            }

            // 5. 按目标屏幕自身 DPI 计算物理坐标与边界约束（消除混合 DPI 多屏定位偏移）
            double scale = NativeMethods.GetScaleForPoint(targetX, targetY);
            var area = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(targetX, targetY)).WorkingArea;

            double winW = Width > 0 ? Width : 300;
            double winH = ActualHeight > 0 ? ActualHeight : 200;
            int physW = (int)Math.Round(winW * scale);
            int physH = (int)Math.Round(winH * scale);
            int pad   = (int)Math.Round(10 * scale);

            int physLeft = targetX;
            int physTop  = targetY;
            if (physLeft + physW > area.Right)  physLeft = area.Right - physW - pad;
            if (physLeft < area.Left)           physLeft = area.Left + pad;
            if (physTop  + physH > area.Bottom) physTop  = targetY - physH - pad;
            if (physTop  < area.Top)            physTop  = area.Top + pad;

            Left = physLeft / scale;
            Top  = physTop  / scale;

            // 6. 默认选中第一项
            if (_phraseManager.Items.Count > 0)
            {
                PhrasesList.SelectedIndex = 0;
            }

            // 7. 显示并正常激活焦点
            Show();
            WindowState = WindowState.Normal;
            NativeMethods.SetWindowPos(selfHwnd, NativeMethods.HWND_TOPMOST, physLeft, physTop, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
            Activate();
            Focus();
            PhrasesList.Focus();

            // 8. 启动鼠标外部监听
            _mouseMonitor.Start(new[] { selfHwnd });
        }

        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.Content is QuickPhraseItem phrase)
            {
                UseSelectedPhrase(phrase);
                e.Handled = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                if (_lastTargetHwnd != IntPtr.Zero && NativeMethods.IsWindow(_lastTargetHwnd))
                {
                    NativeMethods.ForceSetForegroundWindow(_lastTargetHwnd);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (PhrasesList.SelectedItem is QuickPhraseItem selected)
                {
                    UseSelectedPhrase(selected);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (_phraseManager.Items.Count > 0)
                {
                    int next = PhrasesList.SelectedIndex - 1;
                    if (next < 0) next = _phraseManager.Items.Count - 1;
                    PhrasesList.SelectedIndex = next;
                    PhrasesList.ScrollIntoView(PhrasesList.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (_phraseManager.Items.Count > 0)
                {
                    int next = PhrasesList.SelectedIndex + 1;
                    if (next >= _phraseManager.Items.Count) next = 0;
                    PhrasesList.SelectedIndex = next;
                    PhrasesList.ScrollIntoView(PhrasesList.SelectedItem);
                }
                e.Handled = true;
            }
        }

        private bool _isPasting = false;

        /// <summary>
        /// 独立安全回填选中的短语：
        /// 1. 深拷贝备份用户原有剪贴板到独立内存
        /// 2. 注入短语并附加 Windows 10/11 防记录标记（阻止 Win+V 历史记录与云同步）
        /// 3. 开启 WinKit 内部回填锁，绝不写入 WinKit 剪贴板历史
        /// 4. 模拟 Ctrl+V 粘贴，全软件 100% 稳定兼容
        /// 5. 粘贴完成后从独立内存副本恢复用户原有剪贴板，确保后续粘贴仍为原内容
        /// </summary>
        private async void UseSelectedPhrase(QuickPhraseItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Content) || _isPasting) return;
            _isPasting = true;

            try
            {
                // 开启内部主动回填锁，防止被自身监控误判
                _clipboardService.BeginInternalPaste();

                // 1. 深拷贝备份用户当前剪贴板现场到独立内存（避免 GetDataObject 延迟代理在剪贴板覆盖后失效）
                string? backupText = null;
                System.Drawing.Bitmap? backupBitmap = null;
                System.Collections.Specialized.StringCollection? backupFiles = null;
                try
                {
                    if (System.Windows.Clipboard.ContainsFileDropList())
                    {
                        backupFiles = System.Windows.Clipboard.GetFileDropList();
                    }
                    else if (System.Windows.Forms.Clipboard.ContainsImage())
                    {
                        using var rawImg = System.Windows.Forms.Clipboard.GetImage();
                        if (rawImg != null)
                        {
                            backupBitmap = new System.Drawing.Bitmap(rawImg);
                        }
                    }
                    else if (System.Windows.Clipboard.ContainsText())
                    {
                        backupText = System.Windows.Clipboard.GetText();
                    }
                }
                catch
                {
                    // 剪贴板可能被其他进程偶发占用，静默处理
                }

                try
                {
                    // 2. 预先通知监控服务忽略本次短语写入
                    _clipboardService.NotifyUpcomingSelfPaste(item.Content);

                    // 3. 构造带防系统历史记录标记的 DataObject
                    var data = new System.Windows.DataObject();
                    data.SetData(System.Windows.DataFormats.UnicodeText, item.Content);

                    // 写入 Windows 10/11 系统的剪贴板屏蔽标记（禁止写入系统 Win+V 历史，禁止云同步）
                    byte[] zeroBytes = new byte[4];
                    data.SetData("CanIncludeInClipboardHistory", new System.IO.MemoryStream(zeroBytes));
                    data.SetData("CanUploadToCloudClipboard", new System.IO.MemoryStream(zeroBytes));
                    data.SetData("ExcludeClipboardContentFromMonitorProcessing", new System.IO.MemoryStream(zeroBytes));

                    bool setOk = false;
                    for (int i = 0; i < 3 && !setOk; i++)
                    {
                        try
                        {
                            System.Windows.Clipboard.SetDataObject(data, true);
                            setOk = true;
                        }
                        catch
                        {
                            await Task.Delay(25);
                        }
                    }
                    if (!setOk) return;

                    // 4. 记录本次写入后系统产生的最新序列号
                    uint seq = NativeMethods.GetClipboardSequenceNumber();
                    _clipboardService.RegisterSelfPasteSequence(seq, item.Content);

                    // 5. 隐藏短语窗口
                    Hide();
                    await Task.Delay(40);

                    // 6. 焦点精准归还给前台目标窗口
                    if (_lastTargetHwnd != IntPtr.Zero && NativeMethods.IsWindow(_lastTargetHwnd))
                    {
                        NativeMethods.ForceSetForegroundWindow(_lastTargetHwnd);
                        for (int wait = 0; wait < 8 && NativeMethods.GetForegroundWindow() != _lastTargetHwnd; wait++)
                        {
                            await Task.Delay(25);
                        }
                        await Task.Delay(35);
                    }

                    // 7. 模拟 Ctrl+V 粘贴（确保前台不是本进程窗口，防止误贴进自身）
                    if (!NativeMethods.IsOwnWindow(NativeMethods.GetForegroundWindow()))
                    {
                        NativeMethods.SimulateCtrlV();
                    }

                    // 8. 等待目标应用读取剪贴板完毕后（120ms），从独立内存副本将用户原有剪贴板恢复回去
                    await Task.Delay(120);

                    if (!string.IsNullOrEmpty(backupText))
                    {
                        _clipboardService.NotifyUpcomingSelfPaste(backupText);
                        if (await FastClipboard.SetTextFastAsync(backupText))
                        {
                            uint restoreSeq = NativeMethods.GetClipboardSequenceNumber();
                            _clipboardService.RegisterSelfPasteSequence(restoreSeq, backupText);
                        }
                    }
                    else if (backupBitmap != null)
                    {
                        for (int r = 0; r < 3; r++)
                        {
                            try
                            {
                                System.Windows.Forms.Clipboard.SetImage(backupBitmap);
                                uint restoreSeq = NativeMethods.GetClipboardSequenceNumber();
                                _clipboardService.RegisterSelfPasteSequence(restoreSeq, null);
                                break;
                            }
                            catch
                            {
                                await Task.Delay(25);
                            }
                        }
                    }
                    else if (backupFiles != null && backupFiles.Count > 0)
                    {
                        for (int r = 0; r < 3; r++)
                        {
                            try
                            {
                                var restoreObj = new System.Windows.DataObject();
                                restoreObj.SetFileDropList(backupFiles);
                                System.Windows.Clipboard.SetDataObject(restoreObj, true);
                                uint restoreSeq = NativeMethods.GetClipboardSequenceNumber();
                                _clipboardService.RegisterSelfPasteSequence(restoreSeq, null);
                                break;
                            }
                            catch
                            {
                                await Task.Delay(25);
                            }
                        }
                    }
                }
                finally
                {
                    backupBitmap?.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"QuickPhrase: 回填短语失败 ({ex.Message})");
            }
            finally
            {
                _clipboardService.EndInternalPaste();
                _isPasting = false;
            }
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

        protected override void OnClosed(EventArgs e)
        {
            _mouseMonitor.Dispose();
            base.OnClosed(e);
        }
    }
}
