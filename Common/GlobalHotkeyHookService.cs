using System;
using System.Runtime.InteropServices;

namespace WinKit.Common
{
    /// <summary>
    /// 通用全局低级键盘钩子服务 — 监听任意可配置的修饰键组合热键（如 Ctrl + ~），
    /// 与 Clipboard 专用的 Win+V 钩子相互独立
    /// </summary>
    public class GlobalHotkeyHookService : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private LowLevelKeyboardProc? _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private readonly HotkeyDefinition _hotkey;
        private readonly Action _onHotkeyPressed;

        // 长按主键时系统会重复发送 KeyDown，用该标志保证一次按下只触发一次
        private bool _isTriggered;

        public GlobalHotkeyHookService(HotkeyDefinition hotkey, Action onHotkeyPressed)
        {
            _hotkey = hotkey;
            _onHotkeyPressed = onHotkeyPressed;
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                if (curModule != null && curModule.ModuleName != null)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                }
                return IntPtr.Zero;
            }
        }

        private static bool IsKeyDown(int leftVk, int rightVk) =>
            (GetAsyncKeyState(leftVk) & 0x8000) != 0 || (GetAsyncKeyState(rightVk) & 0x8000) != 0;

        private bool IsExactModifierMatch()
        {
            // 要求的修饰键必须全部按下，未要求的修饰键必须全部松开，避免误触发
            if (IsKeyDown(VK_LCONTROL, VK_RCONTROL) != _hotkey.RequiresCtrl) return false;
            if (IsKeyDown(VK_LSHIFT, VK_RSHIFT) != _hotkey.RequiresShift) return false;
            if (IsKeyDown(VK_LMENU, VK_RMENU) != _hotkey.RequiresAlt) return false;
            if (IsKeyDown(VK_LWIN, VK_RWIN) != _hotkey.RequiresWin) return false;
            return true;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                int message = wParam.ToInt32();

                if (vkCode == _hotkey.KeyVirtualKey)
                {
                    if (message == WM_KEYUP || message == WM_SYSKEYUP)
                    {
                        _isTriggered = false;
                    }
                    else if ((message == WM_KEYDOWN || message == WM_SYSKEYDOWN) &&
                             !_isTriggered && IsExactModifierMatch())
                    {
                        _isTriggered = true;
                        _onHotkeyPressed();

                        if (_hotkey.RequiresWin)
                        {
                            // 注入 0xFF 虚拟击键，防止松开 Win 键时弹出系统开始菜单
                            keybd_event(0xFF, 0, 0, UIntPtr.Zero);
                            keybd_event(0xFF, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        }

                        // 拦截此按键，避免热键字符传入前台应用
                        return (IntPtr)1;
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
        }
    }
}
