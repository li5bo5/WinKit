using System;
using System.Runtime.InteropServices;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 全局低级键盘钩子服务 — 专门拦截 Win + V 热键，防止系统剪贴板弹出
    /// </summary>
    public class KeyboardHookService : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_V = 0x56;

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
        private readonly Action _onWinVPressed;

        public KeyboardHookService(Action onWinVPressed)
        {
            _onWinVPressed = onWinVPressed;
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

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                int message = wParam.ToInt32();

                // 当按下 V 键且为 KeyDown 事件时
                if (vkCode == VK_V && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
                {
                    // 实时检查物理键盘上 Left Windows 或 Right Windows 键是否处于按下状态
                    bool isLWinPressed = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0;
                    bool isRWinPressed = (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;

                    if (isLWinPressed || isRWinPressed)
                    {
                        // 触发外部回调
                        _onWinVPressed();

                        // 注入 0xFF 空击键以防松开 Win 时弹出开始菜单
                        keybd_event(0xFF, 0, 0, UIntPtr.Zero);
                        keybd_event(0xFF, 0, 2, UIntPtr.Zero);

                        // 返回 1 吞掉该事件，阻止 Windows 原生剪切板弹出
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
