using System;

namespace WinKit.Clipboard.Models
{
    /// <summary>
    /// 常用短语实体模型 (用于 Data/phrases.jsonl)
    /// </summary>
    public class QuickPhraseItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 单行截断预览文本（显示第一行文本，超出或含多行显示 ...）
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (string.IsNullOrEmpty(Content)) return string.Empty;
                var lines = Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var firstLine = lines.Length > 0 ? lines[0].Trim() : string.Empty;
                if (lines.Length > 1 && !firstLine.EndsWith("..."))
                {
                    firstLine += "...";
                }
                return firstLine;
            }
        }
    }
}
