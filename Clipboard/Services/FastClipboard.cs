using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 提供基于 Win32 API 的快速剪贴板写入，避免 WPF Clipboard 的阻塞重试。
    /// </summary>
    public static class FastClipboard
    {
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("ole32.dll")]
        private static extern int OleFlushClipboard();

        /// <summary>
        /// 将文本写入剪贴板（异步），使用短退避重试。
        /// </summary>
        public static async Task<bool> SetTextFastAsync(string text, int maxRetries = 5, int delayMs = 10)
        {
            return await Task.Run(() => SetTextFast(text, maxRetries, delayMs));
        }

        /// <summary>
        /// 同步写入文本到剪贴板。
        /// </summary>
        public static bool SetTextFast(string text, int maxRetries = 5, int delayMs = 10)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < maxRetries; i++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        EmptyClipboard();
                        int bytes = (text.Length + 1) * 2;
                        IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                        if (hGlobal == IntPtr.Zero)
                            return false;
                        IntPtr target = GlobalLock(hGlobal);
                        if (target != IntPtr.Zero)
                        {
                            // Copy the characters into the allocated memory
                            var chars = text.ToCharArray();
                            Marshal.Copy(chars, 0, target, chars.Length);
                            // Null-terminate
                            Marshal.WriteInt16(target, chars.Length * 2, 0);
                            GlobalUnlock(hGlobal);
                            SetClipboardData(CF_UNICODETEXT, hGlobal);
                        }
                        OleFlushClipboard();
                        return true;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                // 重试前稍作等待
                System.Threading.Thread.Sleep(delayMs);
            }
            return false;
        }

        /// <summary>
        /// 在独立的后台 STA 线程中快速将图片文件写入剪贴板，彻底避开 UI 线程阻塞。
        /// </summary>
        public static Task<bool> SetImageFastAsync(string imagePath, int maxRetries = 3, int delayMs = 30)
        {
            var tcs = new TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    if (!System.IO.File.Exists(imagePath))
                    {
                        tcs.TrySetResult(false);
                        return;
                    }

                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            using var img = System.Drawing.Image.FromFile(imagePath);
                            System.Windows.Forms.Clipboard.SetImage(img);
                            tcs.TrySetResult(true);
                            return;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(delayMs);
                        }
                    }
                    tcs.TrySetResult(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }
    }
}
