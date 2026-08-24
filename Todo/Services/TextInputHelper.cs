using System;
using System.Text;
using System.Windows.Controls;

namespace WinKit.Todo.Services
{
    /// <summary>
    /// 待办文本输入限制工具类（限制最大 15 行 × 15 字 = 225 字符当量）
    /// </summary>
    public static class TextInputHelper
    {
        public const int MaxLines = 15;
        public const int MaxCharsPerLine = 15;
        public const int MaxVirtualTextLength = MaxLines * MaxCharsPerLine; // 225

        /// <summary>
        /// 限制文本的虚拟长度，超过上限时自动截断
        /// </summary>
        /// <param name="rawText">原始文本</param>
        /// <param name="isExceeded">是否触发截断</param>
        /// <returns>截断后的安全文本</returns>
        public static string LimitTextVirtualLength(string rawText, out bool isExceeded)
        {
            isExceeded = false;
            if (string.IsNullOrEmpty(rawText)) return string.Empty;

            var lines = rawText.Replace("\r\n", "\n").Split('\n');
            int currentLength = 0;
            var sb = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                if (i >= MaxLines)
                {
                    isExceeded = true;
                    break;
                }

                string line = lines[i];
                int lineLen = line.Length;
                int lineWeight = Math.Max(lineLen, MaxCharsPerLine);

                if (currentLength + lineWeight <= MaxVirtualTextLength)
                {
                    sb.Append(line);
                    currentLength += lineWeight;
                    if (i < lines.Length - 1 && currentLength < MaxVirtualTextLength)
                    {
                        sb.Append(Environment.NewLine);
                    }
                }
                else
                {
                    isExceeded = true;
                    int remain = MaxVirtualTextLength - currentLength;
                    if (remain > 0)
                    {
                        int take = Math.Min(line.Length, remain);
                        sb.Append(line.Substring(0, take));
                    }
                    break;
                }
            }

            return sb.ToString();
        }
    }
}
