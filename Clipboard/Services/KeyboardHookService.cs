using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WinKit.Common;

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
        /// 将 "Win+Shift+T" / "Ctrl+Alt+V" / "Win+Alt+F1" / "Ctrl+`" / "Esc" 等字符串解析为描述符
        /// 支持：修饰键 (Win/Ctrl/Alt/Shift) + 字母 (A-Z) + 数字 (0-9) + 功能键 (F1-F12) + OEM符号键与特殊功能键
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
                    case "ESC":
                    case "ESCAPE":
                        vk = 0x1B; // VK_ESCAPE
                        break;
                    case "SPACE":
                        vk = 0x20; // VK_SPACE
                        break;
                    case "TAB":
                        vk = 0x09; // VK_TAB
                        break;
                    case "BACK":
                    case "BACKSPACE":
                        vk = 0x08; // VK_BACK
                        break;
                    case "DELETE":
                    case "DEL":
                        vk = 0x2E; // VK_DELETE
                        break;
                    case "HOME":
                        vk = 0x24; // VK_HOME
                        break;
                    case "END":
                        vk = 0x23; // VK_END
                        break;
                    case "PAGEUP":
                    case "PGUP":
                        vk = 0x21; // VK_PRIOR
                        break;
                    case "PAGEDOWN":
                    case "PGDN":
                        vk = 0x22; // VK_NEXT
                        break;
                    case "`":
                    case "~":
                    case "OEMTILDE":
                        vk = 0xC0; // VK_OEM_3
                        break;
                    case "-":
                    case "_":
                    case "OEMMINUS":
                        vk = 0xBD; // VK_OEM_MINUS
                        break;
                    case "=":
                    case "+":
                    case "OEMPLUS":
                        vk = 0xBB; // VK_OEM_PLUS
                        break;
                    case "[":
                    case "{":
                    case "OEMOPENBRACKETS":
                        vk = 0xDB; // VK_OEM_4
                        break;
                    case "]":
                    case "}":
                    case "OEM6":
                        vk = 0xDD; // VK_OEM_6
                        break;
                    case ";":
                    case ":":
                    case "OEMSEMICOLON":
                        vk = 0xBA; // VK_OEM_1
                        break;
                    case "'":
                    case "\"":
                    case "OEMQUOTES":
                        vk = 0xDE; // VK_OEM_7
                        break;
                    case ",":
                    case "<":
                    case "OEMCOMMA":
                        vk = 0xBC; // VK_OEM_COMMA
                        break;
                    case ".":
                    case ">":
                    case "OEMPERIOD":
                        vk = 0xBE; // VK_OEM_PERIOD
                        break;
                    case "/":
                    case "?":
                    case "OEMQUESTION":
                        vk = 0xBF; // VK_OEM_2
                        break;
                    case "\\":
                    case "|":
                    case "OEM5":
                        vk = 0xDC; // VK_OEM_5
                        break;
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
        /// <summary>
        /// 当处于快捷键录入捕获模式时设为 true，拦截所有物理按键不传递给系统和其它热键
        /// </summary>
        public bool IsCapturing { get; set; } = false;

        /// <summary>
        /// 是否启用连续输入 vv 呼出常用短语
        /// </summary>
        public bool QuickPhraseEnabled { get; set; } = true;

        /// <summary>
        /// 当命中 vv 触发条件时触发
        /// </summary>
        public event Action<IntPtr>? QuickPhraseTriggered;

        private long _lastVKeyDownTicks = 0;
        private bool _vKeyWasReleased = true;
        private readonly uint _currentProcessId = (uint)Environment.ProcessId;
        private const int VV_INTERVAL_THRESHOLD_MS = 350; // 常用短语 vv 连击判定时间阈值 (350ms)

        // ── 已注册的热键表 ─────────────────────────────────────────────
        private readonly List<(HotkeyDescriptor Desc, Func<bool> Callback)> _hotkeys = new();

        private NativeMethods.LowLevelProc? _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public KeyboardHookService()
        {
            _proc = HookCallback;
            InstallHook();
        }

        private void InstallHook()
        {
            if (_hookID != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }

            if (_proc != null)
            {
                _hookID = SetHook(_proc);
                if (_hookID == IntPtr.Zero)
                {
                    System.Threading.Thread.Sleep(50);
                    _hookID = SetHook(_proc);
                }
            }
        }

        // ── 公开 API ───────────────────────────────────────────────────
        /// <summary>动态注册热键（支持无条件拦截）</summary>
        public void RegisterHotkey(string hotkeyString, Action callback)
        {
            RegisterHotkey(hotkeyString, () => { callback(); return true; });
        }

        /// <summary>动态注册热键（支持按需条件拦截：返回 true 拦截按键，返回 false 放行物理按键）</summary>
        public void RegisterHotkey(string hotkeyString, Func<bool> callback)
        {
            var desc = HotkeyDescriptor.Parse(hotkeyString);
            if (desc == null || callback == null) return;

            // 相同 VK+修饰键组合覆盖已有注册
            _hotkeys.RemoveAll(h => h.Desc.VkCode == desc.VkCode
                                 && h.Desc.Win == desc.Win
                                 && h.Desc.Ctrl == desc.Ctrl
                                 && h.Desc.Alt == desc.Alt
                                 && h.Desc.Shift == desc.Shift);
            _hotkeys.Add((desc, callback));
        }

        /// <summary>注销所有已注册的热键</summary>
        public void UnregisterAll()
        {
            _hotkeys.Clear();
        }

        // ── 钩子核心 ──────────────────────────────────────────────────
        private IntPtr SetHook(NativeMethods.LowLevelProc proc)
        {
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule  = curProcess.MainModule;
            if (curModule?.ModuleName != null)
                return NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, proc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            return IntPtr.Zero;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

                // 软件注入的按键直接放行，避免模拟按键时死循环或被自身拦截
                if ((hookStruct.flags & NativeMethods.LLKHF_INJECTED) != 0)
                {
                    return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                int vkCode  = hookStruct.vkCode;
                int message = wParam.ToInt32();

                // 释放按键监听，重置长按状态
                if (message == NativeMethods.WM_KEYUP || message == NativeMethods.WM_SYSKEYUP)
                {
                    if (vkCode == NativeMethods.VK_V)
                    {
                        _vKeyWasReleased = true;
                    }
                }

                bool isKeyDown = message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN;
                if (isKeyDown)
                {
                    bool lwinDown  = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN)  & 0x8000) != 0;
                    bool rwinDown  = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN)  & 0x8000) != 0;
                    bool ctrlDown  = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CTRL)  & 0x8000) != 0;
                    bool altDown   = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_ALT)   & 0x8000) != 0;
                    bool shiftDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;

                    // 处于快捷键捕获录入状态时，不触发应用内部任何热键，若带 Win 键则注入 0xFF 防止呼出系统开始菜单
                    if (IsCapturing)
                    {
                        if (lwinDown || rwinDown)
                        {
                            NativeMethods.keybd_event(0xFF, 0, 0, UIntPtr.Zero);
                            NativeMethods.keybd_event(0xFF, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
                        }
                        return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                    }

                    // ── 常用短语 vv 连击判定 ───────────────────────────────
                    if (QuickPhraseEnabled && !lwinDown && !rwinDown && !ctrlDown && !altDown && !shiftDown)
                    {
                        if (vkCode == NativeMethods.VK_V)
                        {
                            if (_vKeyWasReleased)
                            {
                                _vKeyWasReleased = false;
                                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                long interval = now - _lastVKeyDownTicks;

                                if (interval > 0 && interval <= VV_INTERVAL_THRESHOLD_MS)
                                {
                                    _lastVKeyDownTicks = 0;

                                    IntPtr fgWnd = NativeMethods.GetForegroundWindow();
                                    NativeMethods.GetWindowThreadProcessId(fgWnd, out uint fgPid);

                                    // 仅在中文模式下触发 vv 快捷短语（若在英文打字、敲代码或 Shift 英文状态下，绝不触发弹窗）
                                    if (fgPid != _currentProcessId && fgWnd != IntPtr.Zero && NativeMethods.IsChineseInputMode(fgWnd))
                                    {
                                        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                                        {
                                            // 退格键擦除拼音流中的第一个 "v"
                                            NativeMethods.keybd_event(NativeMethods.VK_BACK, 0, 0, UIntPtr.Zero);
                                            System.Threading.Thread.Sleep(15);
                                            NativeMethods.keybd_event(NativeMethods.VK_BACK, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
                                            System.Threading.Thread.Sleep(35);

                                            // 唤出常用短语窗口，传递 targetHwnd
                                            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                                            {
                                                QuickPhraseTriggered?.Invoke(fgWnd);
                                            }));
                                        });

                                        // 核心拦截：直接吃掉第二个物理 v，阻止其分发到前台窗口
                                        return (IntPtr)1;
                                    }
                                }
                                else
                                {
                                    // 仅当前台窗口处于中文输入状态时才记录第一个 v 的时间戳；英文状态直接忽略
                                    IntPtr fgWnd = NativeMethods.GetForegroundWindow();
                                    _lastVKeyDownTicks = NativeMethods.IsChineseInputMode(fgWnd) ? now : 0;
                                }
                            }
                        }
                        else if (vkCode != NativeMethods.VK_LWIN && vkCode != NativeMethods.VK_RWIN && vkCode != NativeMethods.VK_CTRL && vkCode != NativeMethods.VK_ALT && vkCode != NativeMethods.VK_SHIFT)
                        {
                            // 敲击了其他字符键，重置 vv 计时
                            _lastVKeyDownTicks = 0;
                        }
                    }
                    else if (vkCode != NativeMethods.VK_V)
                    {
                        _lastVKeyDownTicks = 0;
                    }

                    foreach (var (desc, callback) in _hotkeys)
                    {
                        if (desc.Matches(vkCode, lwinDown, rwinDown, ctrlDown, altDown, shiftDown))
                        {
                            bool handled = callback();
                            if (handled)
                            {
                                // 若涉及 Win 键，注入 0xFF 防止开始菜单弹出
                                if (desc.Win)
                                {
                                    NativeMethods.keybd_event(0xFF, 0, 0, UIntPtr.Zero);
                                    NativeMethods.keybd_event(0xFF, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
                                }
                                // 拦截按键，不传递给系统
                                return (IntPtr)1;
                            }
                        }
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookID != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookID);
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
        private NativeMethods.LowLevelProc? _proc;
        private IntPtr _hookID = IntPtr.Zero;

        /// <summary>用于判断点击位置是否"属于"被保护窗口的 hwnd 集合</summary>
        private readonly HashSet<IntPtr> _protectedHwnds = new();

        /// <summary>点击发生在保护窗口外部时触发</summary>
        public event Action? ClickedOutside;

        public void Start(IEnumerable<IntPtr> protectedHwnds)
        {
            _protectedHwnds.Clear();
            foreach (var h in protectedHwnds) _protectedHwnds.Add(h);

            if (_hookID != IntPtr.Zero) return; // 已在运行

            _proc = MouseHookCallback;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule  = curProcess.MainModule;
            if (curModule?.ModuleName != null)
            {
                IntPtr hMod = NativeMethods.GetModuleHandle(curModule.ModuleName);
                _hookID = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, hMod, 0);
                if (_hookID == IntPtr.Zero)
                {
                    System.Threading.Thread.Sleep(30);
                    _hookID = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, hMod, 0);
                }
            }
        }

        public void Stop()
        {
            if (_hookID != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == NativeMethods.WM_LBUTTONDOWN || msg == NativeMethods.WM_RBUTTONDOWN ||
                    msg == NativeMethods.WM_MBUTTONDOWN || msg == NativeMethods.WM_NCLBUTTONDOWN)
                {
                    var info        = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    var hwndClicked = NativeMethods.WindowFromPoint(info.pt);
                    var rootClicked = NativeMethods.GetAncestor(hwndClicked, NativeMethods.GA_ROOT);

                    // 检查点击的根窗口是否属于受保护的集合
                    bool isProtected = _protectedHwnds.Contains(hwndClicked)
                                    || _protectedHwnds.Contains(rootClicked);

                    if (!isProtected)
                    {
                        ClickedOutside?.Invoke();
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Dispose() => Stop();
    }
}
