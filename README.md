# WinKit - 极致精美的现代 Windows 效率工具箱 🚀

`WinKit` 是一款专为 Windows 设计的、极致轻量且美观的效率工具箱。它将 **TodoList (待办工具)**、**Clipboard (高级剪贴板)** 与 **Quick Phrases (常用短语)** 深度整合，采用高档磨砂半透明（毛玻璃）视觉体系，并自研了 100% 贴合 Windows 11 Fluent 风格的系统托盘右键菜单。

---

## 📸 界面预览

| 待办清单 | 待办历史 | 剪贴板 | 常用短语 | 偏好设置 |
| :---: | :---: | :---: | :---: | :---: |
| ![待办清单](UI/待办清单.png) | ![待办历史](UI/待办历史.png) | ![剪贴板](UI/剪贴板.png) | ![常用短语](UI/常用短语.png) | ![偏好设置](UI/偏好设置.png) |

---

## 📸使用视频

Bilibili ：【集待办清单与剪贴板于一体的Win11工具箱！ 轻量、现代、美观、专注效率！-哔哩哔哩】 https://b23.tv/tLUjxVI

---

## ✨ 核心特性

### 1. 📝 待办清单 (TodoList)
* **主要功能**：桌面常驻轻量待办，支持多行展开、拖拽排序、完成归档、回收站自愈备份与鼠标完全穿透。
* **操作方式**：
  * **统一新建与编辑**：双击待办空白区域或点击右上角 `＋` 统一弹出居中独立编辑对话框；编辑时按 `Enter`、`Ctrl + S` 或**在屏幕任意外部位置双击**均可秒级自动保存并退出；按 `Esc` 取消。
  * **常驻防隐藏**：免疫系统 `Win + D`（显示桌面）隐藏影响，未置顶也能像桌面便签一样静静驻留。
  * **完成与删除**：点击条目左侧圆圈标记完成；悬停条目点击右侧 `🗑` 移入待办历史。
  * **置顶切换**：点击右上角 `📌` 按钮或按下全局快捷键 `Ctrl + D` 切换置顶。
  * **穿透模式**：点击右上角 `⊙` 开启完全穿透（只看不可点，不阻挡下层应用操作）；按下 `Ctrl + D` 或在偏好设置中一键关闭穿透。
  * **拖动与缩放**：按住顶部标题栏拖动位置；拖动右下角三角控制点自由调节窗口大小。

### 2. 🗑️ 待办历史 (Recycle Bin)
* **主要功能**：安全保存已删除的待办历史，防止误删，支持超时自动清理（默认保留 60 天）。
* **操作方式**：
  * **打开历史**：右键系统托盘图标，在菜单中选择「待办历史」。
  * **恢复待办**：点击条目右侧 `⟲` 还原按钮，按原顺序恢复回待办清单。
  * **彻底删除**：点击单条垃圾桶图标彻底删除，或点击右上角「清空全部」重置回收站。

### 3. 📋 高级剪贴板 (Clipboard)
* **主要功能**：自动监听并记录复制的文本与图片，支持搜索筛选、最新覆盖去重、条目固定置顶、极速长文本回填与图片缓存生命周期管理。
* **操作方式**：
  * **唤出与关闭**：按下全局快捷键 `Win + V` 唤出剪贴板面板；按 `Esc` 或点击窗口外部自动隐藏。
  * **快速粘贴**：鼠标左键单击列表项，自动秒级回填粘贴至当前焦点应用（长文本防误判与 500ms 防抖保存，绝不跳顶）。
  * **条目置顶与一键取消**：鼠标悬停条目点击 `📌/📍` 图钉，将常用项永久锁定前排（容量超限清理永久豁免）；点击底栏「取消置顶」可一键取消所有已置顶项。
  * **搜索与窗口固定**：在上方搜索框键入关键词实时过滤；点击右上角 `📌` 固定窗口置顶不自动关闭。
  * **单项管理**：悬停条目点击 `🗑` 删除单条，或点击底栏「清空全部」清除所有非置顶历史。

### 4. 💬 常用短语 (Quick Phrases)
* **主要功能**：在任意输入框通过按键快速唤出常用短语列表并一键回填，智能识别中文打字状态，英文打字与敲代码绝不误触发。
* **操作方式**：
  * **闪电唤出**：在输入法处于中文拼音打字状态时，快速连按两次小写字母 `vv`，即在光标处弹出短语面板（自动退格擦除拼音字符，微信窗口绝不误最小化）；在纯英文打字或敲代码时，输入 `vv` 就是正常的英文字母，绝不误弹打扰。
  * **选择回填**：使用方向键 `↑` / `↓` 移动选择目标短语并按 `Enter` 回车键入，或直接鼠标单击目标短语瞬间回填上屏。
  * **短语库管理**：在偏好设置中点击「编辑」可直接用记事本自定义短语库，点击「重置」恢复默认短语。

### 5. ⚙️ 统一偏好设置 (Preferences)
* **主要功能**：一站式集中管理快捷键录入、穿透模式、主题外观、窗口不透明度、双击托盘行为及保留周期。
* **操作方式**：
  * **打开设置**：右键系统托盘图标，选择「偏好设置」。
  * **快捷键录入**：点击目标输入框，直接按下键盘上的物理组合键自动捕获录入。
  * **模式切换**：支持开关「穿透模式」、切换颜色主题（跟随系统/浅色/深色）、调节窗口不透明度等。
  * **保存生效**：点击底部「保存配置」即时应用并全局广播。

---

## 📂 数据与配置文件存储位置（纯绿色便携免安装）

WinKit 采用绿色便携化设计，所有数据与配置文件均统一存放在**应用程序运行所在目录**下的 `Data` 文件夹中，彻底不向系统其他位置散落任何文件。

* **数据保存目录**：`[WinKit.exe 所在目录]\Data\`

### 文件结构清单

| 文件名 / 目录 | 用途说明 |
| :--- | :--- |
| **`settings.json`** | 全软件统一配置文件（存储主题、快捷键、透明度、保留天数等偏好设置） |
| **`todos.jsonl`** | 待办清单数据文件（存储待办内容、创建时间与状态） |
| **`recycle_bin.jsonl`** | 待办历史/回收站数据文件（存储已删除的待办历史记录） |
| **`clipboard.jsonl`** | 剪贴板历史数据文件（存储已复制的历史文本与记录） |
| **`quick_phrases.jsonl`** | 常用短语库数据文件（存储快捷短语列表） |
| **`Images/`** | 剪贴板图片缓存目录（本地存储剪贴板捕获的图片文件） |
| **`crash.log`** | 全局未捕获异常日志（仅在系统偶发极端异常时生成） |

> 💡 **绿色迁移提示**：如需备份数据或迁移至新电脑，只需将整个 WinKit 文件夹（包含 `Data` 目录）复制到任意位置即可 100% 完整迁移。每个数据文件均内置 `.bak` 损坏自愈备份机制。

---

## 🖥️ 系统要求与版本选择

* **操作系统**：Windows 10 / 11（64 位）

| 版本名称 | 内置依赖 | 适用场景与特点 |
| :--- | :--- | :--- |
| **独立免装版** (Self-Contained) | 无，双击即用 | **开箱即用**。内置了完整 .NET 8.0 运行库，无环境要求。 |
| **框架依赖版** (Framework-Dependent) | 需预装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | **极速轻巧**。体积小巧，适合已安装过 .NET 8 运行库的用户。 |

---

## 💻 编译与发布指南

### 方式一：自动化打包脚本（推荐）
在项目根目录下执行自带的发布脚本，即可一键完成两个版本的编译与 `.zip` 压缩归档：

```powershell
.\scripts\publish.ps1 -Version "2.6"
```

### 方式二：手动执行命令
亦可手动执行以下命令分别打包：

```powershell
# 1. 编译并打包：框架依赖版 (Framework-Dependent)
dotnet publish WinKit.csproj -c Release -r win-x64 --self-contained false -o bin\Publish\FrameworkDependent_V2.6

# 2. 编译并打包：独立免装版 (Self-Contained)
dotnet publish WinKit.csproj -c Release -r win-x64 --self-contained true -o bin\Publish\SelfContained_V2.6
```

---

## 💾 下载地址

您可以通过以下渠道下载预编译好的二进制可执行文件：

1. **蓝奏云下载 (国内加速)**：[点击下载](https://li5bo5.lanzouu.com/b00egskkqb) (提取密码: `Wink`)
2. **GitHub Releases**：[GitHub 发行版页面](https://github.com/li5bo5/WinKit/releases)

---

## Star History

<a href="https://www.star-history.com/?repos=li5bo5%2FWinKit&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=li5bo5/WinKit&type=date&theme=dark&legend=top-left&sealed_token=QByHdIhs51DixtP469uzjKFznxRO-qV_XhxKR7kkNTtP7ZHYk_JpfQAjkDARmwEGZb1M_d1sGLvXswrsBwyCzJ02PGI-ds1ui20xR5rwEboA9oGnLlrXhx81b5Kkxul94oO-eouIdytDTDH5JZh7uXJX5EEFnuAw9J5FCwGb7zYVsyw_i-65UYXJKNor" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=li5bo5/WinKit&type=date&legend=top-left&sealed_token=QByHdIhs51DixtP469uzjKFznxRO-qV_XhxKR7kkNTtP7ZHYk_JpfQAjkDARmwEGZb1M_d1sGLvXswrsBwyCzJ02PGI-ds1ui20xR5rwEboA9oGnLlrXhx81b5Kkxul94oO-eouIdytDTDH5JZh7uXJX5EEFnuAw9J5FCwGb7zYVsyw_i-65UYXJKNor" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=li5bo5/WinKit&type=date&legend=top-left&sealed_token=QByHdIhs51DixtP469uzjKFznxRO-qV_XhxKR7kkNTtP7ZHYk_JpfQAjkDARmwEGZb1M_d1sGLvXswrsBwyCzJ02PGI-ds1ui20xR5rwEboA9oGnLlrXhx81b5Kkxul94oO-eouIdytDTDH5JZh7uXJX5EEFnuAw9J5FCwGb7zYVsyw_i-65UYXJKNor" />
 </picture>
</a>

---

## 📄 开源协议

本项目采用 **AGPL-3.0 (GNU Affero General Public License v3.0)** 协议开源。
