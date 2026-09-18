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
        public const byte VK_RETURN = 0x0D;

        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;
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

        #region TSF (Text Services Framework) 现代输入法隔室检测接口

        [ComImport]
        [Guid("aa80e801-2021-11d2-93e0-0060b067b86e")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ITfThreadMgr
        {
            void Activate(out int clientId);
            void Deactivate();
            void CreateDocumentMgr(out IntPtr docMgr);
            void EnumDocumentMgrs(out IntPtr enumDocMgrs);
            void GetFocus(out IntPtr docMgr);
            void SetFocus(IntPtr docMgr);
            void AssociateFocus(IntPtr hwnd, IntPtr newDocMgr, out IntPtr prevDocMgr);
            void IsAssocWithFocus(IntPtr hwnd, IntPtr docMgr, [MarshalAs(UnmanagedType.Bool)] out bool isAssoc);
            [PreserveSig]
            int GetGlobalCompartment(out ITfCompartmentMgr compMgr);
        }

        [ComImport]
        [Guid("7dcf57ac-18ad-438b-824d-979bffb74b7c")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ITfCompartmentMgr
        {
            [PreserveSig]
            int GetCompartment(ref Guid rguid, out ITfCompartment comp);
            void ClearCompartment(int clientId, ref Guid rguid);
            void EnumCompartments(out IntPtr enumComp);
        }

        [ComImport]
        [Guid("bb08f7a9-607a-4384-8623-056892b64371")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ITfCompartment
        {
            [PreserveSig]
            int SetValue(int clientId, ref object varValue);
            [PreserveSig]
            int GetValue(out object varValue);
        }

        [ComImport]
        [Guid("52960acf-999a-40a9-9555-bf5e0d931e05")]
        public class TfThreadMgr
        {
        }

        public static readonly Guid GUID_COMPARTMENT_KEYBOARD_OPENCLOSE = new Guid("a76c53cc-a528-4084-8309-67b58c7668e0");
        public static readonly Guid GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION = new Guid("73830354-d347-42cd-bb77-aa27225e3266");
        public const int TF_CONVERSIONMODE_NATIVE = 0x0001;

        /// <summary>
        /// 通过 TSF (Text Services Framework) 全局隔室管理器读取现代输入法真实状态
        /// 1. 检查 GUID_COMPARTMENT_KEYBOARD_OPENCLOSE → 为 0 则 IME 完全关闭（英文状态）
        /// 2. 检查 GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION → (val & TF_CONVERSIONMODE_NATIVE) != 0 为中文模式，否则为 Shift 临时英文
        /// </summary>
        private static bool? CheckTsfInputMode()
        {
            try
            {
                var threadMgr = (ITfThreadMgr)new TfThreadMgr();
                if (threadMgr.GetGlobalCompartment(out var compMgr) == 0 && compMgr != null)
                {
                    var openGuid = GUID_COMPARTMENT_KEYBOARD_OPENCLOSE;
                    var convGuid = GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION;

                    if (compMgr.GetCompartment(ref openGuid, out var openComp) == 0 && openComp != null &&
                        compMgr.GetCompartment(ref convGuid, out var convComp) == 0 && convComp != null)
                    {
                        openComp.GetValue(out object openVal);
                        convComp.GetValue(out object convVal);

                        if (openVal != null && convVal != null)
                        {
                            int openStatus = Convert.ToInt32(openVal);
                            // 1. 若 OPENCLOSE == 0，说明 IME 完全关闭，处于英文状态
                            if (openStatus == 0)
                            {
                                return false;
                            }

                            int convMode = Convert.ToInt32(convVal);
                            // 2. 检查是否开启了 Native 中文模式（单按 Shift 会将 Native 置 0 变为英文模式）
                            return (convMode & TF_CONVERSIONMODE_NATIVE) != 0;
                        }
                    }
                }
            }
            catch
            {
                // 忽略 COM 异常，交由后续 Win32 通道兜底
            }
            return null;
        }

        #endregion

        public const uint WM_IME_CONTROL = 0x0283;
        public const uint IMC_GETCONVERSIONMODE = 0x0001;
        public const uint IMC_GETOPENSTATUS = 0x0005;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 精准检测指定窗口当前是否真正处于中文输入模式（而非纯英文键盘或中文输入法下按 Shift 切换的英文打字状态）
        /// 遵循双轨检测架构（TSF 现代隔室管理器 + Win32 WM_IME_CONTROL 穿透），兼顾系统级安全性与零误触
        /// </summary>
        public static bool IsChineseInputMode(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;

            uint threadId = GetWindowThreadProcessId(hWnd, out _);

            // 1. 检查当前前台线程的键盘布局语言代码（低 16 位）
            IntPtr hkl = GetKeyboardLayout(threadId);
            ushort langId = (ushort)((long)hkl & 0xFFFF);
            // 0x0804 = 中文(中国简体), 0x0404 = 中文(台湾繁体), 0x0C04 = 中文(香港繁体), 0x1404 = 中文(澳门), 0x1004 = 中文(新加坡)
            bool isChineseLayout = (langId == 0x0804 || langId == 0x0404 || langId == 0x0C04 || langId == 0x1404 || langId == 0x1004);

            if (!isChineseLayout)
            {
                // 纯英文布局（如 en-US 0x0409）或其它语言布局 -> 100% 为英文打字
                return false;
            }

            // 2. 优先通过 TSF (Text Services Framework) 全局隔室读取现代输入法真实状态
            // ① 检查 GUID_COMPARTMENT_KEYBOARD_OPENCLOSE：为 0 则 IME 关闭（英文）
            // ② 检查 GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION：(value & TF_CONVERSIONMODE_NATIVE) != 0 为中文，否则为 Shift 英文
            bool? tsfResult = CheckTsfInputMode();
            if (tsfResult.HasValue)
            {
                return tsfResult.Value;
            }

            // 3. 补充通道：跨进程通过 WM_IME_CONTROL 向 Default IME 窗口查询状态（Win32 跨进程与输入法交互的标准方案）
            var guiInfo = new GUITHREADINFO();
            guiInfo.cbSize = Marshal.SizeOf(guiInfo);
            IntPtr targetHwnd = hWnd;
            if (GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.hwndFocus != IntPtr.Zero && IsWindow(guiInfo.hwndFocus))
            {
                targetHwnd = guiInfo.hwndFocus;
            }

            IntPtr defImeWnd = ImmGetDefaultIMEWnd(targetHwnd);
            if (defImeWnd == IntPtr.Zero && targetHwnd != hWnd)
            {
                defImeWnd = ImmGetDefaultIMEWnd(hWnd);
            }

            if (defImeWnd != IntPtr.Zero)
            {
                // 查询输入法开关状态：0 为关闭（英文状态），非 0 为开启（中文状态）
                IntPtr openStatus = SendMessage(defImeWnd, WM_IME_CONTROL, (IntPtr)IMC_GETOPENSTATUS, IntPtr.Zero);
                if (openStatus == IntPtr.Zero)
                {
                    // 当前处于关闭状态或英文打字状态
                    return false;
                }

                // 查询输入法转换模式：检查是否包含 IME_CMODE_NATIVE (0x0001)
                IntPtr convMode = SendMessage(defImeWnd, WM_IME_CONTROL, (IntPtr)IMC_GETCONVERSIONMODE, IntPtr.Zero);
                if ((convMode.ToInt64() & IME_CMODE_NATIVE) == 0)
                {
                    // 未开启 Native 中文转换模式（即单按 Shift 切换成了临时英文打字状态）
                    return false;
                }

                // 确凿处于中文开启状态
                return true;
            }

            // 4. 补充通道：尝试通过 ImmGetContext 查询
            IntPtr hImc = ImmGetContext(targetHwnd);
            if (hImc == IntPtr.Zero && targetHwnd != hWnd)
            {
                hImc = ImmGetContext(hWnd);
            }

            if (hImc != IntPtr.Zero)
            {
                try
                {
                    bool isOpen = ImmGetOpenStatus(hImc);
                    if (!isOpen) return false;

                    if (ImmGetConversionStatus(hImc, out uint conversion, out _))
                    {
                        if ((conversion & IME_CMODE_NATIVE) == 0) return false;
                    }

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

            // 5. 保守安全原则：无法确认时一律返回 false，宁可不触发，也绝不在英文打字或写代码时误触发
            return false;
        }

        public static bool IsImeComposingChinese(IntPtr hWnd) => IsChineseInputMode(hWnd);

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

        /// <summary>
        /// 使用 Windows 原生 SendInput 机制向当前焦点窗口直接注入 Unicode 字符串
        /// 全程不经过系统剪贴板，不修改剪贴板历史，支持中文、Emoji 及换行 (Shift+Enter 安全换行防误发)
        /// </summary>
        public static void SendUnicodeString(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 1. 确保物理修饰键全部释放，防止产生按键干扰
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_RWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CTRL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_ALT,  0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            int inputSize = Marshal.SizeOf(typeof(INPUT));
            var inputs = new System.Collections.Generic.List<INPUT>();

            // 统一换行符为 \n
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');

            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];

                if (c == '\n')
                {
                    // 提交之前缓存的字符
                    if (inputs.Count > 0)
                    {
                        SendInput((uint)inputs.Count, inputs.ToArray(), inputSize);
                        inputs.Clear();
                    }

                    // 关键：以 Shift + Enter 模拟换行，防止在微信/QQ等即时通讯工具中把消息误发出去
                    var shiftDown = new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_SHIFT } } };
                    var enterDown = new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_RETURN } } };
                    var enterUp   = new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_RETURN, dwFlags = KEYEVENTF_KEYUP } } };
                    var shiftUp   = new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_SHIFT, dwFlags = KEYEVENTF_KEYUP } } };

                    SendInput(4, new[] { shiftDown, enterDown, enterUp, shiftUp }, inputSize);
                    System.Threading.Thread.Sleep(5);
                }
                else
                {
                    // Unicode 字符按下 (KeyDown)
                    inputs.Add(new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = 0,
                                wScan = c,
                                dwFlags = KEYEVENTF_UNICODE
                            }
                        }
                    });
                    // Unicode 字符释放 (KeyUp)
                    inputs.Add(new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        u = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = 0,
                                wScan = c,
                                dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                            }
                        }
                    });

                    // 达到 100 个字符刷新一次，避免单次塞满宿主队列
                    if (inputs.Count >= 100)
                    {
                        SendInput((uint)inputs.Count, inputs.ToArray(), inputSize);
                        inputs.Clear();
                        System.Threading.Thread.Sleep(5);
                    }
                }
            }

            if (inputs.Count > 0)
            {
                SendInput((uint)inputs.Count, inputs.ToArray(), inputSize);
            }
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public const int HWND_BROADCAST = 0xFFFF;
        public static readonly int WM_SHOW_EXISTING_INSTANCE = RegisterWindowMessage("WinKit_ActivateExistingInstance_li5bo5");

        public static void BringExistingInstanceToFront()
        {
            PostMessage((IntPtr)HWND_BROADCAST, WM_SHOW_EXISTING_INSTANCE, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
