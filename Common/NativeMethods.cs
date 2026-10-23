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
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint GA_ROOT = 2;

        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_NCLBUTTONDOWN = 0x00A1;

        public const int SM_CXDOUBLECLK = 36;
        public const int SM_CYDOUBLECLK = 37;

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
        public struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
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
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion u;
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
        public static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

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

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

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
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

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
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll")]
        public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardLayout(uint idThread);

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
            bool? tsfResult = CheckTsfInputMode();
            if (tsfResult.HasValue)
            {
                return tsfResult.Value;
            }

            // 3. 补充通道：跨进程通过 WM_IME_CONTROL 向 Default IME 窗口查询状态
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
                IntPtr openStatus = SendMessage(defImeWnd, WM_IME_CONTROL, (IntPtr)IMC_GETOPENSTATUS, IntPtr.Zero);
                if (openStatus == IntPtr.Zero)
                {
                    return false;
                }

                IntPtr convMode = SendMessage(defImeWnd, WM_IME_CONTROL, (IntPtr)IMC_GETCONVERSIONMODE, IntPtr.Zero);
                if ((convMode.ToInt64() & IME_CMODE_NATIVE) == 0)
                {
                    return false;
                }

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
        /// 检查指定窗口句柄是否属于本进程自身窗口
        /// </summary>
        public static bool IsOwnWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;
            GetWindowThreadProcessId(hWnd, out uint processId);
            return processId == (uint)Environment.ProcessId;
        }

        /// <summary>
        /// 使用 Windows 原生 SendInput 高效模拟 Ctrl+V 粘贴动作（含修饰键释放保护，绝不阻塞 UI 线程）
        /// </summary>
        public static void SimulateCtrlV()
        {
            // 确保物理修饰键全部释放，防止带 Shift/Alt/Win 触发组合粘贴或开始菜单
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_RWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_ALT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CTRL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            int inputSize = Marshal.SizeOf(typeof(INPUT));
            var inputs = new INPUT[]
            {
                // Ctrl Down
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT { wVk = (ushort)VK_CTRL, dwFlags = 0 }
                    }
                },
                // V Down
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT { wVk = (ushort)VK_V, dwFlags = 0 }
                    }
                },
                // V Up
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT { wVk = (ushort)VK_V, dwFlags = KEYEVENTF_KEYUP }
                    }
                },
                // Ctrl Up
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT { wVk = (ushort)VK_CTRL, dwFlags = KEYEVENTF_KEYUP }
                    }
                }
            };

            SendInput((uint)inputs.Length, inputs, inputSize);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int RegisterWindowMessage(string lpString);

        public static readonly int WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");

        // ── 多屏拖拽边界 / 桌面宿主 / 每显示器 DPI / 窗口交互统一辅助 ────────
        private const int GWLP_HWNDPARENT = -8;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        /// <summary>
        /// 统一窗口拖动与松手超界回弹（方案 A）：
        /// 0. 拖动期间交由系统原生 DragMove 自由跟随，跨屏幕零跳变；
        /// 1. 多屏幕交界边允许跨越显示与停留；
        /// 2. 无相邻屏幕的上边缘不允许把顶部标题栏拖出屏幕顶边；
        /// 3. 无相邻屏幕的下边缘留在屏幕内的高度不小于 2/5；
        /// 4. 无相邻屏幕的左右边缘留在屏幕内的宽度不小于 1/5。
        /// </summary>
        public static void DragMoveWithBounds(System.Windows.Window window)
        {
            try
            {
                window.DragMove();
            }
            catch
            {
                return;
            }

            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out RECT rect)) return;

            int width  = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return;

            var curScreen = GetCursorPos(out POINT cursor)
                ? System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y))
                : System.Windows.Forms.Screen.FromHandle(hwnd);

            var area   = curScreen.WorkingArea;
            var bounds = curScreen.Bounds;

            int minAreaLeft   = area.Left;
            int maxAreaRight  = area.Right;
            int minAreaTop    = area.Top;
            int maxAreaBottom = area.Bottom;

            // 检测当前屏幕四边是否存在相邻屏幕，若存在则延伸交界边允许自由跨越显示
            foreach (var other in System.Windows.Forms.Screen.AllScreens)
            {
                if (other.DeviceName == curScreen.DeviceName) continue;
                var ob = other.Bounds;
                var oa = other.WorkingArea;

                // 垂直方向有重叠：检测左/右相邻屏幕
                if (Math.Min(ob.Bottom, bounds.Bottom) > Math.Max(ob.Top, bounds.Top))
                {
                    if (ob.Left < bounds.Left && ob.Right >= bounds.Left - 4)
                        minAreaLeft = Math.Min(minAreaLeft, oa.Left);
                    if (ob.Right > bounds.Right && ob.Left <= bounds.Right + 4)
                        maxAreaRight = Math.Max(maxAreaRight, oa.Right);
                }

                // 水平方向有重叠：检测上/下相邻屏幕
                if (Math.Min(ob.Right, bounds.Right) > Math.Max(ob.Left, bounds.Left))
                {
                    if (ob.Top < bounds.Top && ob.Bottom >= bounds.Top - 4)
                        minAreaTop = Math.Min(minAreaTop, oa.Top);
                    if (ob.Bottom > bounds.Bottom && ob.Top <= bounds.Bottom + 4)
                        maxAreaBottom = Math.Max(maxAreaBottom, oa.Bottom);
                }
            }

            // 左右外边缘：最少保留 1/5 宽度
            int minKeepW = Math.Max(1, width / 5);
            int minLeft  = minAreaLeft - (width - minKeepW);
            int maxLeft  = maxAreaRight - minKeepW;

            int newLeft = minLeft <= maxLeft ? Math.Clamp(rect.Left, minLeft, maxLeft) : minLeft;

            // 上外边缘：不允许把顶部标题栏拖出屏幕顶边；下外边缘：最少保留 2/5 高度
            int minKeepH = Math.Max(1, height * 2 / 5);
            int minTop   = minAreaTop;
            int maxTop   = maxAreaBottom - minKeepH;

            // 上下错位多屏：窗口水平中心所在列若顶边更低（上方无屏幕），标题栏主体也不得越过该列顶边
            int centerX = newLeft + width / 2;
            int columnTop = int.MaxValue;
            foreach (var s in System.Windows.Forms.Screen.AllScreens)
            {
                if (centerX >= s.Bounds.Left && centerX < s.Bounds.Right)
                    columnTop = Math.Min(columnTop, s.WorkingArea.Top);
            }
            if (columnTop != int.MaxValue) minTop = Math.Max(minTop, columnTop);

            int newTop  = minTop  <= maxTop  ? Math.Clamp(rect.Top,  minTop,  maxTop)  : minTop;

            if (newLeft != rect.Left || newTop != rect.Top)
            {
                SetWindowPos(hwnd, IntPtr.Zero, newLeft, newTop, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
        }

        /// <summary>
        /// 将窗口所有者绑定为桌面 Progman，使非置顶窗口在 Win+D 时随桌面一同显示
        /// </summary>
        public static void BindOwnerToProgman(IntPtr hwnd)
        {
            IntPtr progman = FindWindow("Progman", null);
            if (hwnd != IntPtr.Zero && progman != IntPtr.Zero)
                SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, progman);
        }

        /// <summary>
        /// 获取指定屏幕坐标所在显示器的 DPI 缩放比例（1.0 = 96 DPI）
        /// </summary>
        public static double GetScaleForPoint(int x, int y)
        {
            IntPtr monitor = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
            if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                return dpiX / 96.0;
            return 1.0;
        }

        /// <summary>
        /// 统一处理窗口右下角拖拽缩放，并钳制在当前所在屏幕的工作区右/下边界内
        /// </summary>
        public static void ApplyResizeWithScreenClamp(System.Windows.Window window, double startW, double startH, System.Windows.Vector delta)
        {
            double newW = Math.Max(window.MinWidth, startW + delta.X);
            double newH = Math.Max(window.MinHeight, startH + delta.Y);

            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out RECT rect))
            {
                // 统一在物理像素坐标系下计算窗口左上角到工作区右/下边界的距离，再按窗口自身 DPI 换算为逻辑尺寸上限
                var area = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
                double maxW = (area.Right - rect.Left) / (dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0);
                double maxH = (area.Bottom - rect.Top) / (dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0);

                if (newW > maxW) newW = Math.Max(window.MinWidth, maxW);
                if (newH > maxH) newH = Math.Max(window.MinHeight, maxH);
            }

            window.Width = newW;
            window.Height = newH;
        }

        /// <summary>
        /// 为窗口右下角缩放手柄统一挂载拖拽缩放事件（消除各窗口重复的缩放状态字段与事件方法）
        /// </summary>
        public static void AttachResizeGrip(System.Windows.Window window, System.Windows.UIElement grip, Action? onResizeCompleted = null)
        {
            bool isResizing = false;
            System.Windows.Point resizeStart = default;
            double startW = 0, startH = 0;

            System.Windows.Input.MouseEventHandler onMove = (_, e) =>
            {
                if (!isResizing) return;
                ApplyResizeWithScreenClamp(window, startW, startH, e.GetPosition(null) - resizeStart);
            };

            System.Windows.Input.MouseButtonEventHandler? onUp = null;
            onUp = (_, e) =>
            {
                if (!isResizing) return;
                isResizing = false;
                grip.ReleaseMouseCapture();
                grip.MouseMove -= onMove;
                grip.MouseLeftButtonUp -= onUp;
                onResizeCompleted?.Invoke();
            };

            grip.MouseLeftButtonDown += (_, e) =>
            {
                isResizing = true;
                resizeStart = e.GetPosition(null);
                startW = window.Width;
                startH = window.Height;
                grip.CaptureMouse();
                grip.MouseMove += onMove;
                grip.MouseLeftButtonUp += onUp;
                e.Handled = true;
            };
        }

        /// <summary>
        /// 按目标屏幕自身 DPI 计算不溢出工作区的物理坐标，并在执行 beforeShow 回调后置顶弹出窗口
        /// </summary>
        public static void ShowPopupAtPoint(System.Windows.Window window, IntPtr hwnd, int anchorX, int anchorY, int offsetLogical = 0, Action? beforeShow = null)
        {
            double scale = GetScaleForPoint(anchorX, anchorY);
            var area = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(anchorX, anchorY)).WorkingArea;

            double winW = window.Width > 0 ? window.Width : 300;
            double winH;
            if (window.Height > 0 && !double.IsNaN(window.Height))
            {
                winH = window.Height;
            }
            else
            {
                // SizeToContent 窗口：按当前内容实测高度（受 MaxHeight 约束），避免首次弹出时 ActualHeight 为 0 导致底部溢出
                window.Measure(new System.Windows.Size(winW, double.PositiveInfinity));
                winH = window.DesiredSize.Height > 0 ? window.DesiredSize.Height : (window.ActualHeight > 0 ? window.ActualHeight : 200);
            }
            int physW  = (int)Math.Round(winW * scale);
            int physH  = (int)Math.Round(winH * scale);
            int offset = (int)Math.Round(offsetLogical * scale);
            int pad    = (int)Math.Round(10 * scale);

            int physLeft = anchorX + offset;
            int physTop  = anchorY + offset;

            if (physLeft + physW > area.Right)  physLeft = offset > 0 ? (anchorX - physW - offset) : (area.Right - physW - pad);
            if (physLeft < area.Left)           physLeft = area.Left;
            if (physTop  + physH > area.Bottom) physTop  = anchorY - physH - (offset > 0 ? offset : pad);
            if (physTop  < area.Top)            physTop  = area.Top;

            window.Left = physLeft / scale;
            window.Top  = physTop  / scale;

            beforeShow?.Invoke();

            window.Show();
            window.WindowState = System.Windows.WindowState.Normal;
            SetWindowPos(hwnd, HWND_TOPMOST, physLeft, physTop, 0, 0, SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// 精准归还前台焦点至目标输入窗口并模拟 Ctrl+V 粘贴（确保不误贴进本进程自身窗口）
        /// </summary>
        public static async System.Threading.Tasks.Task RestoreFocusAndPasteAsync(IntPtr targetHwnd)
        {
            if (targetHwnd != IntPtr.Zero && IsWindow(targetHwnd))
            {
                ForceSetForegroundWindow(targetHwnd);
                for (int wait = 0; wait < 8 && GetForegroundWindow() != targetHwnd; wait++)
                {
                    await System.Threading.Tasks.Task.Delay(25);
                }
                await System.Threading.Tasks.Task.Delay(30);
            }

            if (!IsOwnWindow(GetForegroundWindow()))
            {
                SimulateCtrlV();
            }
        }

        /// <summary>
        /// 当鼠标在列表项上发生真实物理位移时，同步将该项设为选中高亮（统一键盘、搜索与鼠标的单一高亮状态）
        /// </summary>
        public static void SelectListBoxItemOnMouseMove(System.Windows.Window window, object sender, System.Windows.Input.MouseEventArgs e, ref System.Windows.Point lastMouseMovePos)
        {
            var pos = e.GetPosition(window);
            if (pos != lastMouseMovePos)
            {
                lastMouseMovePos = pos;
                if (sender is System.Windows.Controls.ListBoxItem item && !item.IsSelected)
                {
                    item.IsSelected = true;
                }
            }
        }
    }
}
