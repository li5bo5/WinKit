using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
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
        private const int WH_MOUSE_LL     = 14;
        private const int WM_LBUTTONDOWN  = 0x0201;
        private const int SM_CXDOUBLECLK  = 36;
        private const int SM_CYDOUBLECLK  = 37;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private IntPtr _mouseHookHandle = IntPtr.Zero;
        private LowLevelMouseProc? _mouseHookProc;
        private DateTime _lastClickTime = DateTime.MinValue;
        private POINT _lastClickPos;

        public EditDialog(string currentText)
        {
            InitializeComponent();
            InputBox.Text = currentText;
            InputBox.SelectAll();
            InputBox.Focus();

            // 监听失焦自动保存：点击外部任意位置立刻保存退出
            Deactivated += (s, e) => CommitAndClose();
            Closed      += (s, e) => UninstallMouseHook();

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
                IntPtr hMod = module != null ? GetModuleHandle(module.ModuleName) : IntPtr.Zero;
                _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, hMod, 0);
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
                UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
                _mouseHookProc   = null;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN && !_isFinished)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var now = DateTime.Now;
                double elapsedMs = (now - _lastClickTime).TotalMilliseconds;
                uint maxInterval = GetDoubleClickTime();
                int maxDx = GetSystemMetrics(SM_CXDOUBLECLK);
                int maxDy = GetSystemMetrics(SM_CYDOUBLECLK);

                if (elapsedMs <= maxInterval &&
                    Math.Abs(hookStruct.pt.x - _lastClickPos.x) <= maxDx &&
                    Math.Abs(hookStruct.pt.y - _lastClickPos.y) <= maxDy)
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

            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
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
            int virtualLength = 0;
            int limit = 225; // 15 行 * 15 字
            int truncateIndex = -1;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    virtualLength += 15;
                }
                else if (c == '\r')
                {
                    // 忽略 \r，避免重复
                }
                else
                {
                    virtualLength += 1;
                }

                if (virtualLength > limit)
                {
                    truncateIndex = i;
                    break;
                }
            }

            if (truncateIndex != -1)
            {
                _isUpdatingText = true;
                string truncatedText = text.Substring(0, truncateIndex);
                if (truncatedText.EndsWith("\r"))
                {
                    truncatedText = truncatedText.Substring(0, truncatedText.Length - 1);
                }
                int caret = Math.Min(textBox.CaretIndex, truncatedText.Length);
                textBox.Text = truncatedText;
                textBox.CaretIndex = caret;
                _isUpdatingText = false;
            }
        }
    }
}
