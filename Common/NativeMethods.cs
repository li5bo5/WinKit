using System;
using System.Runtime.InteropServices;

namespace WinKit.Common
{
    /// <summary>
    /// 统一收敛的 Win32 API 原生方法、数据结构与常量定义
    /// </summary>
    public static class NativeMethods
    {
        // ── 常见常量定义 ───────────────────────────────────────────────
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TRANSPARENT = 0x00000020;

        public static readonly IntPtr HWND_TOPMOST = new(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new(-2);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_NCHITTEST = 0x0084;

        public const int HTTRANSPARENT = -1;
        public const int HTCLIENT = 1;

        public const byte VK_CTRL = 0x11;
        public const byte VK_V = 0x56;
        public const byte VK_LWIN = 0x5B;
        public const byte VK_RWIN = 0x5C;
        public const byte VK_ALT = 0x12;
        public const byte VK_SHIFT = 0x10;
        public const byte VK_ESCAPE = 0x1B;
        public const byte VK_BACK = 0x08;

        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const int LLKHF_INJECTED = 0x0010;

        public const int INPUT_KEYBOARD = 1;

        // ── 常用结构体定义 ─────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        // ── P/Invoke 签名 ──────────────────────────────────────────────
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll")]
        public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetDoubleClickTime();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        public delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        public const uint GCS_COMPSTR = 0x0008;
        public const uint IME_CMODE_NATIVE = 0x0001;

        [DllImport("imm32.dll")]
        public static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ImmGetOpenStatus(IntPtr hIMC);

        [DllImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ImmGetConversionStatus(IntPtr hIMC, out uint lpfdwConversion, out uint lpfdwSentence);

        [DllImport("imm32.dll")]
        public static extern int ImmGetCompositionString(IntPtr hIMC, uint dwIndex, [Out] byte[]? lpBuf, uint dwBufLen);

        [DllImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll")]
        public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardLayout(uint idThread);

        /// <summary>
        /// 检测指定窗口当前关联线程的键盘布局是否为中文输入法（简体/繁体/香港/澳门/新加坡）
        /// 若为纯英文布局（如 en-US 0x0409）或其它语言布局则返回 false
        /// </summary>
        public static bool IsChineseKeyboardLayout(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;
            uint threadId = GetWindowThreadProcessId(hWnd, out _);
            IntPtr hkl = GetKeyboardLayout(threadId);
            ushort langId = (ushort)((long)hkl & 0xFFFF);
            // 0x0804 = 中文(简体), 0x0404 = 中文(台湾繁体), 0x0C04 = 中文(香港繁体), 0x1404 = 中文(澳门), 0x1004 = 中文(新加坡)
            return (langId == 0x0804 || langId == 0x0404 || langId == 0x0C04 || langId == 0x1404 || langId == 0x1004);
        }

        /// <summary>
        /// 检测指定输入窗口当前是否处于中文拼音输入状态（有候选框）
        /// 若为纯英文布局，或中文输入法下按 Shift 切换到了英文模式，则返回 false（字符已直接上屏）
        /// </summary>
        public static bool IsImeComposingChinese(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;

            uint threadId = GetWindowThreadProcessId(hWnd, out _);

            // 1. 检查当前前台线程的键盘布局语言
            IntPtr hkl = GetKeyboardLayout(threadId);
            ushort langId = (ushort)((long)hkl & 0xFFFF);
            // 0x0804 = 中文(中国简体), 0x0404 = 中文(台湾繁体), 0x0C04 = 中文(香港繁体)
            bool isChineseLayout = (langId == 0x0804 || langId == 0x0404 || langId == 0x0C04);

            if (!isChineseLayout)
            {
                // 纯英文/非中文键盘布局 -> 直接上屏模式
                return false;
            }

            // 2. 获取真正持有焦点的控件
            var guiInfo = new GUITHREADINFO();
            guiInfo.cbSize = Marshal.SizeOf(guiInfo);
            IntPtr targetHwnd = hWnd;
            if (GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.hwndFocus != IntPtr.Zero && IsWindow(guiInfo.hwndFocus))
            {
                targetHwnd = guiInfo.hwndFocus;
            }

            // 3. 在中文输入法下，检查是否按 Shift 切换到了英文模式
            IntPtr hImc = ImmGetContext(targetHwnd);
            if (hImc == IntPtr.Zero)
            {
                IntPtr defIme = ImmGetDefaultIMEWnd(targetHwnd);
                if (defIme != IntPtr.Zero) hImc = ImmGetContext(defIme);
            }
            if (hImc == IntPtr.Zero && targetHwnd != hWnd)
            {
                hImc = ImmGetContext(hWnd);
            }

            if (hImc != IntPtr.Zero)
            {
                try
                {
                    bool isOpen = ImmGetOpenStatus(hImc);
                    if (!isOpen)
                    {
                        // 用户关闭了输入法或切到了英文模式 -> 直接上屏
                        return false;
                    }

                    if (ImmGetConversionStatus(hImc, out uint conversion, out _))
                    {
                        // 若不包含 IME_CMODE_NATIVE (0x0001)，说明用户单按 Shift 切到了英文状态
                        if ((conversion & IME_CMODE_NATIVE) == 0)
                        {
                            return false;
                        }
                    }

                    // 处于中文模式且开启中
                    return true;
                }
                catch
                {
                    // 容错
                }
                finally
                {
                    ImmReleaseContext(targetHwnd, hImc);
                }
            }

            // 默认如果在中文布局下，视为中文模式
            return true;
        }

        /// <summary>
        /// 穿透 Windows 权限限制强制将指定窗口切换为前台焦点窗口
        /// </summary>
        public static void ForceSetForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return;

            IntPtr currentFg = GetForegroundWindow();
            if (currentFg == hWnd) return;

            uint currentThreadId = GetCurrentThreadId();
            uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);
            uint fgThreadId = GetWindowThreadProcessId(currentFg, out _);

            if (currentThreadId != targetThreadId && targetThreadId != 0)
                AttachThreadInput(currentThreadId, targetThreadId, true);
            if (fgThreadId != 0 && fgThreadId != currentThreadId)
                AttachThreadInput(currentThreadId, fgThreadId, true);

            SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);

            if (currentThreadId != targetThreadId && targetThreadId != 0)
                AttachThreadInput(currentThreadId, targetThreadId, false);
            if (fgThreadId != 0 && fgThreadId != currentThreadId)
                AttachThreadInput(currentThreadId, fgThreadId, false);
        }

        /// <summary>
        /// 高兼容性模拟 Ctrl+V 粘贴动作（含微延迟与修饰键释放保护）
        /// </summary>
        public static void SimulateCtrlV()
        {
            // 确保物理 Win 键释放，防止误触发系统快捷键
            keybd_event((byte)VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_RWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            // 1. Ctrl Down
            keybd_event((byte)VK_CTRL, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(15);

            // 2. V Down & Up
            keybd_event((byte)VK_V, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(25);
            keybd_event((byte)VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(15);

            // 3. Ctrl Up
            keybd_event((byte)VK_CTRL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
