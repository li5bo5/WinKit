# WinKit - 极致精美的现代 Windows 效率工具箱 🚀

`WinKit` 是一款专为 Windows 设计的、极致轻量且美观的效率工具箱。它将 **TodoList (待办工具)**、**Clipboard (高级剪贴板)** 与 **Quick Phrases (常用短语)** 深度整合，采用高档磨砂半透明（毛玻璃）视觉体系，并自研了 100% 贴合 Windows 11 Fluent 风格的系统托盘右键菜单。

---

## 📸 界面预览

| 待办清单 (TodoList) | 剪贴板历史 (Clipboard) | 托盘右键菜单与设置 |
| :---: | :---: | :---: |
| ![TodoList](UI/todo1.png) | ![Clipboard](UI/Clipboard.png) | ![设置](UI/设置.png) |

---

## ✨ 核心特性 (v2.5)

### 1. 💬 Quick Phrases (常用短语快捷回填)
* **`vv` 简码闪电唤出**：支持在任意文本输入框连续按下 `vv`，在鼠标光标处瞬间唤出常用短语面板。
* **智能输入法感知过滤**：底层集成 Windows IMM32 键盘状态监听，拼音输入过程中智能过滤不误触，退格键动态重置缓冲。
* **明文存储与便捷编辑**：短语数据完全以 JSON Lines 格式保存在 `Data/quick_phrases.jsonl` 中，支持在偏好设置中一键“编辑”与“重置”。

### 2. 📋 Clipboard (高级剪贴板历史)
* **悬浮面板与快捷唤出**：默认 `Win + V` 唤出剪贴板，支持 `Esc` 或点击外部一键关闭。
* **单击粘贴原位保持**：单击选定项直接回填粘贴至目标应用，条目保持在当前滚动位置不跳转，方便连续查阅。
* **全新覆盖去重机制**：捕获重复文本或图片时，物理删除原旧项及旧磁盘图片文件，最新复制内容自动排在首位。
* **固定模式与微光反馈**：支持点击右上角固定在最上层，置顶时顶边浮现精致半透明微光指示线，窗口关闭自动复位。
* **图片存储生命周期管理**：自动监听并保存复制的图片，支持自定义保留天数与存储上限，超限安全清理。

### 3. 📝 TodoList (无干扰桌面待办)
* **无干扰悬浮窗**：常驻桌面，支持鼠标自由拖拽调整位置与大小。
* **灰点纵向绝对居中**：无论待办文字是单行还是多行撑高，左侧灰点始终在条目纵向高度上绝对垂直居中。
* **纯净操作按钮**：悬浮操作按钮采用纯粹 `ActionBtn` 风格，悬停时浅灰微高亮，无底框遮挡，界面开阔通透。
* **待办历史与回收站**：删除待办时自动移入“待办历史”，支持保留天数自动清理（默认 60 天），支持原 ID 与创建时间完整还原（`⟲`）。
* **全局置顶快捷键**：默认 `Ctrl + D` 一键置顶，顶部呈现 1.5px 弹性伸缩微光指示线。

### 4. 🎨 智能主题色彩与自适应视觉
* **任务栏色彩自动跟随**：窗口背景色智能跟随 Windows 任务栏颜色与深浅模式，系统主题变更时 0 延迟实时同步。
* **感知亮度文字自适应**：基于感知亮度算法自动调节文字与图标对比度（深色底配浅色字、浅色底配深色字），全天候清晰易读。
* **主题三模式随心换**：在偏好设置中提供“跟随系统 / 浅色 / 深色”下拉选择框，支持即时预览与配置保存。
* **全局纯净去干扰**：全量清除冗余 ToolTip 气泡，标题栏按钮与偏好设置关闭按钮均支持鼠标悬停自动显隐。

### 5. ⚙️ 统一偏好设置中心
* **全功能配置面板**：卡片式磨砂面板，集中整合“待办清单”、“常用短语”、“高级剪贴板”、“常规与外观”全部配置项，支持快捷键物理捕获、即时保存与一键恢复默认。
* **统一设计系统规范**：全软件次级胶囊按钮统一为 `80×26px` 紧凑精致规格，设置项左右网格严丝合缝对齐。

---

## 📂 数据与配置文件存储位置（纯绿色便携免安装）

WinKit 采用绿色便携化设计，所有数据与配置文件均统一存放在**应用程序运行所在目录**下的 `Data` 文件夹中，彻底不向系统其他位置散落任何文件。

* **数据保存目录**：`[WinKit.exe 所在目录]\Data\`

### 文件结构清单

| 文件名 / 目录 | 存储格式 | 用途说明 | 特性与编辑建议 |
| :--- | :---: | :--- | :--- |
| **`settings.json`** | JSON | **全软件统一配置文件** | 存储主题模式、快捷键、窗口不透明度、双击托盘联动开关、保留天数/条数等偏好设置。 |
| **`todos.jsonl`** | JSON Lines | **待办事项数据文件** | 纯明文存储待办内容、创建时间与状态。记事本可直接双击查看与编辑，支持原子写入。 |
| **`recycle_bin.jsonl`** | JSON Lines | **待办历史/回收站文件** | 纯明文存储已删除的待办历史记录与移入时间。 |
| **`clipboard.jsonl`** | JSON Lines | **剪贴板历史数据文件** | 纯明文存储剪贴板历史文本与捕获时间。 |
| **`quick_phrases.jsonl`** | JSON Lines | **常用短语库数据文件** | 纯明文存储快捷短语列表，支持偏好设置一键重置与记事本直接自定义。 |
| **`Images/`** | 图片目录 | **剪贴板图片缓存目录** | 本地缓存剪贴板捕获的图片，支持按天数与体积上限自动安全轮转清理。 |

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

在项目根目录下执行以下命令，即可在本地打包输出 Release 版本：

```powershell
# 1. 编译并打包：框架依赖版 (Framework-Dependent)
dotnet publish WinKit.csproj -c Release -r win-x64 --self-contained false -o bin\Publish\FrameworkDependent_V2.5

# 2. 编译并打包：独立免装版 (Self-Contained)
dotnet publish WinKit.csproj -c Release -r win-x64 --self-contained true -o bin\Publish\SelfContained_V2.5
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
