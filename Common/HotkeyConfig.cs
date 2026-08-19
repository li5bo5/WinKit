using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinKit.Common
{
    /// <summary>
    /// 软件目录下 config.json 的配置模型（与 settings.json 分离，面向用户手动编辑）
    /// </summary>
    public class HotkeyConfig
    {
        /// <summary>
        /// 显示/隐藏 TodoList 的全局快捷键，例如 "Ctrl + ~"、"Alt + T"
        /// </summary>
        [JsonPropertyName("toggleHotkey")]
        public string ToggleHotkey { get; set; } = "Ctrl + ~";
    }

    /// <summary>
    /// 负责加载/生成软件运行目录下的 config.json
    /// </summary>
    public static class HotkeyConfigManager
    {
        /// <summary>
        /// 配置文件路径（与 WinKit.exe 同目录）
        /// </summary>
        public static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

        /// <summary>
        /// 加载配置；文件不存在时自动生成一份默认配置，解析失败时回退到默认值
        /// </summary>
        public static HotkeyConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    var defaults = new HotkeyConfig();
                    var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(ConfigPath, json, new UTF8Encoding(false));
                    return defaults;
                }

                var text = File.ReadAllText(ConfigPath, Encoding.UTF8);
                var config = JsonSerializer.Deserialize<HotkeyConfig>(text,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return config ?? new HotkeyConfig();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HotkeyConfigManager: 加载 config.json 失败 - {ex.Message}");
                return new HotkeyConfig();
            }
        }
    }

    /// <summary>
    /// 解析后的全局热键定义（修饰键 + 主键虚拟键码）
    /// </summary>
    public class HotkeyDefinition
    {
        public bool RequiresCtrl { get; private set; }
        public bool RequiresShift { get; private set; }
        public bool RequiresAlt { get; private set; }
        public bool RequiresWin { get; private set; }
        public int KeyVirtualKey { get; private set; }

        private static readonly Dictionary<string, int> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            ["~"] = 0xC0, ["`"] = 0xC0, ["Oem3"] = 0xC0, ["OEMTilde"] = 0xC0,
            ["-"] = 0xBD, ["Minus"] = 0xBD, ["OemMinus"] = 0xBD,
            ["="] = 0xBB, ["Plus"] = 0xBB, ["OemPlus"] = 0xBB,
            ["["] = 0xDB, ["]"] = 0xDD, ["\\"] = 0xDC,
            [";"] = 0xBA, ["'"] = 0xDE, [","] = 0xBC, ["."] = 0xBE, ["/"] = 0xBF,
            ["Space"] = 0x20, ["Tab"] = 0x09, ["Enter"] = 0x0D, ["Return"] = 0x0D,
            ["Esc"] = 0x1B, ["Escape"] = 0x1B, ["Backspace"] = 0x08,
            ["Delete"] = 0x2E, ["Insert"] = 0x2D,
            ["Home"] = 0x24, ["End"] = 0x23,
            ["PageUp"] = 0x21, ["PageDown"] = 0x22,
            ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
        };

        /// <summary>
        /// 解析形如 "Ctrl + Shift + T" 的热键字符串；要求至少包含一个修饰键
        /// </summary>
        public static bool TryParse(string? text, out HotkeyDefinition? hotkey)
        {
            hotkey = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var result = new HotkeyDefinition();
            var hasKey = false;

            foreach (var rawPart in text.Split('+'))
            {
                var part = rawPart.Trim();
                if (part.Length == 0) continue;

                switch (part.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        result.RequiresCtrl = true;
                        continue;
                    case "shift":
                        result.RequiresShift = true;
                        continue;
                    case "alt":
                        result.RequiresAlt = true;
                        continue;
                    case "win":
                    case "windows":
                        result.RequiresWin = true;
                        continue;
                }

                if (!TryParseKey(part, out int vk)) return false;
                result.KeyVirtualKey = vk;
                hasKey = true;
            }

            bool hasModifier = result.RequiresCtrl || result.RequiresShift ||
                               result.RequiresAlt || result.RequiresWin;
            if (!hasKey || !hasModifier) return false;

            hotkey = result;
            return true;
        }

        private static bool TryParseKey(string part, out int vk)
        {
            vk = 0;

            // 单个字母 A-Z
            if (part.Length == 1 && char.IsLetter(part[0]))
            {
                vk = 0x41 + (char.ToUpperInvariant(part[0]) - 'A');
                return true;
            }

            // 主键盘数字 0-9
            if (part.Length == 1 && char.IsDigit(part[0]))
            {
                vk = 0x30 + (part[0] - '0');
                return true;
            }

            // 功能键 F1-F24
            if (part.Length >= 2 && (part[0] == 'F' || part[0] == 'f') &&
                int.TryParse(part.Substring(1), out int fNum) && fNum >= 1 && fNum <= 24)
            {
                vk = 0x70 + (fNum - 1);
                return true;
            }

            return NamedKeys.TryGetValue(part, out vk);
        }
    }
}
