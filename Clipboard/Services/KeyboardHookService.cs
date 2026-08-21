using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 已解析的热键描述：虚拟键码 + 各修饰键是否按下
    /// </summary>
    public class HotkeyDescriptor
    {
        public int VkCode { get; }
        public bool Win { get; }
        public bool Ctrl { get; }
        public bool Alt { get; }
        public bool Shift { get; }

        public HotkeyDescriptor(int vkCode, bool win, bool ctrl, bool alt, bool shift)
        {
            VkCode = vkCode; Win = win; Ctrl = ctrl; Alt = alt; Shift = shift;
        }

        /// <summary>
        /// 将 "Win+Shift+T" / "Ctrl+Alt+V" / "Win+Alt+F1" 等字符串解析为描述符
        /// 支持：修饰键 (Win/Ctrl/Alt/Shift) + 字母 (A-Z) + 数字 (0-9) + 功能键 (F1-F12)
        /// </summary>
        public static HotkeyDescriptor? Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var parts = raw.Split('+');
            bool win = false, ctrl = false, alt = false, shift = false;
            int vk = 0;
            foreach (var p in parts)
            {
                var upper = p.Trim().ToUpperInvariant();
                switch (upper)
                {
                    case "WIN":   win   = true; break;
                    case "CTRL":  ctrl  = true; break;
                    case "ALT":   alt   = true; break;
                    case "SHIFT": shift = true; break;
                    default:
                        if (upper.Length == 1)
                        {
                            char c = upper[0];
                            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                            {
                                vk = (int)c; // VK: 'A'=0x41 .. 'Z'=0x5A, '0'=0x30 .. '9'=0x39
                            }
                        }
                        else if (upper.Length >= 2 && upper[0] == 'F' && int.TryParse(upper.Substring(1), out int fNum)
                                 && fNum >= 1 && fNum <= 12)
                        {
                            vk = 0x70 + (fNum - 1); // VK_F1=0x70 .. VK_F12=0x7B
                        }
                        break;
                }
            }
            if (vk == 0) return null;
            return new HotkeyDescriptor(vk, win, ctrl, alt, shift);
        }

        /// <summary>判断当前按键事件是否与本描述符匹配</summary>
        public bool Matches(int pressedVk, bool lwinDown, bool rwinDown, bool ctrlDown, bool altDown, bool shiftDown)
        {
            if (pressedVk != VkCode) return false;
            bool winDown = lwinDown || rwinDown;
            if (Win   != winDown)   return false;
            if (Ctrl  != ctrlDown)  return false;
            if (Alt   != altDown)   return false;
            if (Shift != shiftDown) return false;
            return true;
        }
    }

    /// <summary>
    /// 全局低级键盘钩子服务 — 支持多热键动态注册与分发，保留 Win 键 0xFF 防弹菜单注入
    /// </summary>
    public class KeyboardHookService : IDisposable
    {
        // ── Win32 常量 ─────────────────────────────────────────────────
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_SYSKEYDOWN  = 0x0104;

        public const int VK_LWIN  = 0x5B;
        public const int VK_RWIN  = 0x5C;
        public const int VK_CTRL  = 0x11;
        public const int VK_ALT   = 0x12;
        public const int VK_SHIFT = 0x10;

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int LLKHF_INJECTED   = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public UIntPtr dwExtraInfo;
        }

        // ── P/Invoke ───────────────────────────────────────────────────
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc proc, IntPtr hMod, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // ── 已注册的热键表 ─────────────────────────────────────────────
        private readonly List<(HotkeyDescriptor Desc, Action Callback)> _hotkeys = new();

        private LowLevelKeyboardProc? _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public KeyboardHookService()
        {
            _proc   = HookCallback;
            _hookID = SetHook(_proc);
        }

        // ── 已有构造函数重载，保持向后兼容 ─────────────────────────────
        /// <summary>
        /// 直接用单个 Win+V 回调构造（向后兼容旧调用方式）
        /// </summary>
        public KeyboardHookService(Action onWinVPressed) : this()
        {
            RegisterHotkey("Win+V", onWinVPressed);
        }

        // ── 公开 API ───────────────────────────────────────────────────
        /// <summary>动态注册热键（热重载安全）</summary>
        public void RegisterHotkey(string hotkeyString, Action callback)
        {
            var desc = HotkeyDescriptor.Parse(hotkeyString);
            if (desc == null) return;

            // 相同 VK+修饰键组合覆盖已有注册
            _hotkeys.RemoveAll(h => h.Desc.VkCode == desc.VkCode
                                 && h.Desc.Win == desc.Win
                                 && h.Desc.Ctrl == desc.Ctrl
                                 && h.Desc.Alt == desc.Alt
                                 && h.Desc.Shift == desc.Shift);
            _hotkeys.Add((desc, callback));
        }

        /// <summary>注销指定热键字符串</summary>
        public void UnregisterHotkey(string hotkeyString)
        {
            var desc = HotkeyDescriptor.Parse(hotkeyString);
            if (desc == null) return;
            _hotkeys.RemoveAll(h => h.Desc.VkCode == desc.VkCode
                                 && h.Desc.Win == desc.Win
                                 && h.Desc.Ctrl == desc.Ctrl
                                 && h.Desc.Alt == desc.Alt
                                 && h.Desc.Shift == desc.Shift);
        }

        /// <summary>
        /// 模拟按下指定的快捷键组合（例如模拟发送 Win+Shift+S 或 Win+Shift+T）
        /// </summary>
        public static void SimulateKeyCombination(bool win, bool shift, bool ctrl, bool alt, byte vk)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                // 1. 强制释放当前可能处于按下状态的物理修饰键，避免系统组合键状态混淆
                keybd_event((byte)VK_LWIN,  0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event((byte)VK_RWIN,  0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event((byte)VK_CTRL,  0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event((byte)VK_ALT,   0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                System.Threading.Thread.Sleep(50); // 确保系统底层按键状态同步

                // 2. 依次模拟按下目标修饰键
                if (win)
                {
                    keybd_event((byte)VK_LWIN, 0, 0, UIntPtr.Zero);
                    System.Threading.Thread.Sleep(10);
                }
                if (ctrl)
                {
                    keybd_event((byte)VK_CTRL, 0, 0, UIntPtr.Zero);
                    System.Threading.Thread.Sleep(10);
                }
                if (alt)
                {
                    keybd_event((byte)VK_ALT, 0, 0, UIntPtr.Zero);
                    System.Threading.Thread.Sleep(10);
                }
                if (shift)
                {
                    keybd_event((byte)VK_SHIFT, 0, 0, UIntPtr.Zero);
                    System.Threading.Thread.Sleep(10);
                }

                // 3. 模拟按下并释放目标主键
                keybd_event(vk, 0, 0, UIntPtr.Zero);
                System.Threading.Thread.Sleep(30);
                keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                System.Threading.Thread.Sleep(10);

                // 4. 依次释放目标修饰键
                if (shift)
                {
                    keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    System.Threading.Thread.Sleep(10);
                }
                if (alt)
                {
                    keybd_event((byte)VK_ALT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    System.Threading.Thread.Sleep(10);
                }
                if (ctrl)
                {
                    keybd_event((byte)VK_CTRL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    System.Threading.Thread.Sleep(10);
                }
                if (win)
                {
                    keybd_event((byte)VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            });
        }

        // ── 钩子核心 ──────────────────────────────────────────────────
        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule  = curProcess.MainModule;
            if (curModule?.ModuleName != null)
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            return IntPtr.Zero;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                
                // 软件注入的按键直接放行，避免模拟按键时死循环或被自身拦截
                if ((hookStruct.flags & LLKHF_INJECTED) != 0)
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                int vkCode  = hookStruct.vkCode;
                int message = wParam.ToInt32();

                bool isKeyDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
                if (isKeyDown)
                {
                    bool lwinDown  = (GetAsyncKeyState(VK_LWIN)  & 0x8000) != 0;
                    bool rwinDown  = (GetAsyncKeyState(VK_RWIN)  & 0x8000) != 0;
                    bool ctrlDown  = (GetAsyncKeyState(VK_CTRL)  & 0x8000) != 0;
                    bool altDown   = (GetAsyncKeyState(VK_ALT)   & 0x8000) != 0;
                    bool shiftDown = (GetAsyncKeyState(VK_SHIFT)  & 0x8000) != 0;

                    foreach (var (desc, callback) in _hotkeys)
                    {
                        if (desc.Matches(vkCode, lwinDown, rwinDown, ctrlDown, altDown, shiftDown))
                        {
                            callback();

                            // 若涉及 Win 键，注入 0xFF 防止开始菜单弹出
                            if (desc.Win)
                            {
                                keybd_event(0xFF, 0, 0, UIntPtr.Zero);
                                keybd_event(0xFF, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                            }
                            // 拦截按键，不传递给系统
                            return (IntPtr)1;
                        }
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            _hotkeys.Clear();
        }
    }


    // ═══════════════════════════════════════════════════════════════════
    //  全局鼠标点击监听器 — 用于检测用户在剪贴板窗口外部的点击，实现精准失焦隐藏
    // ═══════════════════════════════════════════════════════════════════
    /// <summary>
    /// 轻量级全局鼠标低级钩子：在激活期间监听任意鼠标按键按下事件，判断是否在指定窗口外部
    /// </summary>
    public class GlobalMouseClickMonitor : IDisposable
    {
        private const int WH_MOUSE_LL   = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_NCLBUTTONDOWN = 0x00A1;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int id, LowLevelMouseProc proc, IntPtr hMod, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint  mouseData;
            public uint  flags;
            public uint  time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        private LowLevelMouseProc? _proc;
        private IntPtr  _hookID = IntPtr.Zero;

        /// <summary>用于判断点击位置是否"属于"被保护窗口的 hwnd 集合</summary>
        private readonly HashSet<IntPtr> _protectedHwnds = new();

        /// <summary>点击发生在保护窗口外部时触发</summary>
        public event Action? ClickedOutside;

        // GA_ROOT = 2（获取顶层父窗口）
        private const uint GA_ROOT = 2;

        public void Start(IEnumerable<IntPtr> protectedHwnds)
        {
            _protectedHwnds.Clear();
            foreach (var h in protectedHwnds) _protectedHwnds.Add(h);

            if (_hookID != IntPtr.Zero) return; // 已在运行

            _proc   = MouseHookCallback;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule  = curProcess.MainModule;
            if (curModule?.ModuleName != null)
                _hookID = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        public void Stop()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN || msg == WM_NCLBUTTONDOWN)
                {
                    var info  = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    var hwndClicked   = WindowFromPoint(info.pt);
                    var rootClicked   = GetAncestor(hwndClicked, GA_ROOT);

                    // 检查点击的根窗口是否属于受保护的集合
                    bool isProtected = _protectedHwnds.Contains(hwndClicked)
                                    || _protectedHwnds.Contains(rootClicked);

                    if (!isProtected)
                    {
                        ClickedOutside?.Invoke();
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Dispose() => Stop();
    }
}
