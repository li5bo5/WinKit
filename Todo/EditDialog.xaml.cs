using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using WinKit.Common;
using WinKey   = System.Windows.Input.KeyEventArgs;
using WinInput = System.Windows.Input;

namespace WinKit.Todo
{
    public partial class EditDialog : Window
    {
        public string ResultText { get; private set; } = string.Empty;
        private bool _isUpdatingText = false;
        private bool _isFinished = false;

        // ══════════════════════════════════════════════
        // Win32 全局低级鼠标钩子（用于捕获对话框外部区域双击）
        // ══════════════════════════════════════════════
        private IntPtr _mouseHookHandle = IntPtr.Zero;
        private NativeMethods.LowLevelProc? _mouseHookProc;
        private DateTime _lastClickTime = DateTime.MinValue;
        private NativeMethods.POINT _lastClickPos;

        public EditDialog(string title = "编辑待办", string initialText = "")
        {
            InitializeComponent();
            if (DlgTitleText != null)
            {
                DlgTitleText.Text = title;
            }
            this.Title = title;
            InputBox.Text = initialText;
            InputBox.SelectAll();
            InputBox.Focus();

            // 仅在双击或确认快捷键时保存退出；单击外部仅转移焦点，不退出编辑
            Closed += (s, e) => UninstallMouseHook();

            // 安装全局鼠标钩子捕获外部双击
            InstallMouseHook();
        }

        private void InstallMouseHook()
        {
            try
            {
                _mouseHookProc = MouseHookCallback;
                using var process = Process.GetCurrentProcess();
                using var module = process.MainModule;
                IntPtr hMod = module != null ? NativeMethods.GetModuleHandle(module.ModuleName) : IntPtr.Zero;
                _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseHookProc, hMod, 0);
                if (_mouseHookHandle == IntPtr.Zero && hMod != IntPtr.Zero)
                {
                    System.Threading.Thread.Sleep(20);
                    _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseHookProc, hMod, 0);
                }
            }
            catch
            {
                // 忽略钩子安装异常，失焦事件与窗口双击仍可作为保底
            }
        }

        private void UninstallMouseHook()
        {
            if (_mouseHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
                _mouseHookProc   = null;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN && !_isFinished)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var now = DateTime.Now;
                double elapsedMs = (now - _lastClickTime).TotalMilliseconds;
                uint maxInterval = NativeMethods.GetDoubleClickTime();
                int maxDx = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXDOUBLECLK);
                int maxDy = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYDOUBLECLK);

                if (elapsedMs <= maxInterval &&
                    Math.Abs(hookStruct.pt.X - _lastClickPos.X) <= maxDx &&
                    Math.Abs(hookStruct.pt.Y - _lastClickPos.Y) <= maxDy)
                {
                    // 检测到全局鼠标左键双击：无论在对话框内还是对话框外，均自动保存并关闭
                    _lastClickTime = DateTime.MinValue;
                    Dispatcher.BeginInvoke(new Action(() => CommitAndClose()));
                }
                else
                {
                    _lastClickTime = now;
                    _lastClickPos  = hookStruct.pt;
                }
            }

            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        private void CommitAndClose()
        {
            if (_isFinished) return;
            _isFinished = true;
            UninstallMouseHook();

            ResultText = InputBox.Text.Trim();
            try
            {
                DialogResult = true;
            }
            catch
            {
                Close();
            }
        }

        private void CancelAndClose()
        {
            if (_isFinished) return;
            _isFinished = true;
            UninstallMouseHook();

            try
            {
                DialogResult = false;
            }
            catch
            {
                Close();
            }
        }

        // 标题栏拖动（单击才拖，双击交由全局处理保存）
        private void DlgTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void InputBox_PreviewKeyDown(object sender, WinKey e)
        {
            // Enter（无 Shift）：提交保存
            if (e.Key == WinInput.Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                CommitAndClose();
                e.Handled = true;
            }
            // Win+S 或 Ctrl+S：保存并关闭
            else if (e.Key == WinInput.Key.S &&
                     ((Keyboard.Modifiers & ModifierKeys.Windows) != 0 ||
                      (Keyboard.Modifiers & ModifierKeys.Control) != 0))
            {
                CommitAndClose();
                e.Handled = true;
            }
            // Ctrl+X：剪切后异步刷新 UI 避免文本残留
            else if (e.Key == WinInput.Key.X && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    InputBox.InvalidateVisual();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            // Esc：取消关闭
            else if (e.Key == WinInput.Key.Escape)
            {
                CancelAndClose();
                e.Handled = true;
            }
        }

        // 窗口内部双击处理
        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                CommitAndClose();
                e.Handled = true;
            }
        }

        private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdatingText) return;
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                LimitTextVirtualLength(textBox);
            }
        }

        private void LimitTextVirtualLength(System.Windows.Controls.TextBox textBox)
        {
            var text = textBox.Text;
            var safeText = WinKit.Todo.Services.TextInputHelper.LimitTextVirtualLength(text, out bool isExceeded);
            if (isExceeded)
            {
                _isUpdatingText = true;
                int caret = Math.Min(textBox.CaretIndex, safeText.Length);
                textBox.Text = safeText;
                textBox.CaretIndex = caret;
                _isUpdatingText = false;
            }
        }
    }
}
