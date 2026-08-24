# WinKit - 极致精美的现代 Windows 效率工具箱 🚀

`WinKit` 是一款专为 Windows 11 设计的、极致轻量且美观的效率工具箱。它将 **TodoList (待办工具)** 与 **Clipboard (高级剪切板)** 深度整合，采用高档磨砂半透明（毛玻璃）视觉体系，并自研了 100% 贴合 Windows 11 Fluent 风格的系统托盘右键菜单。

---

## 📸 界面预览

| 待办清单 (TodoList) | 剪贴板历史 (Clipboard) | 托盘右键菜单与设置 |
| :---: | :---: | :---: |
| ![TodoList](UI/todo1.png) | ![Clipboard](UI/Clipboard.png) | ![设置](UI/设置.png) |

---

## ✨ 核心特性 (v2.3)

### 1. 📂 TodoList (待办清单)
* **无干扰悬浮窗**：常驻桌面，支持鼠标拖拽调整位置与大小。
* **快捷状态控制**：一键开启“置顶”或“穿透”，标题栏按钮常规状态默认隐藏，鼠标悬停时平滑浮现。
* **待办历史/回收站**：删除待办时自动移入“待办历史”，清晰展示删除时间，支持保留天数自动清理（默认 60 天），支持原 ID 与创建时间完整还原（`⟲`）。
* **置顶显示快捷键**：默认 `Ctrl + D`，支持全局快捷键切换置顶与穿透（未置顶时一键置顶并取消穿透，置顶时取消置顶）。
* **明文安全持久化**：待办与回收站数据采用标准 JSON Lines 格式（`.jsonl`）存储，支持原子写入与损坏自愈备份，人类可读，记事本可直接双击编辑。

### 2. 📋 Clipboard (剪贴板历史)
* **悬浮面板**：默认 `Win + V` 唤出剪贴板，支持 `Esc` 一键关闭退出（条件放行，不影响外部软件）。
* **固定模式 (Pin)**：支持将剪贴板固定在最上层，**双击/单击复制粘贴后窗口不消失**，支持侧边栏连续批量回填粘贴。
* **双击自动填充**：采用 `AttachThreadInput` 穿透前台限制与精准 `Ctrl + V` 仿真模拟，双击瞬时将选中文本回填至目标应用。
* **容量控制与折半清理**：默认保存 300 条（范围 100~500 条），超限时自动折半清理，避免频繁 IO。
* **失焦智能隐藏**：未固定模式下，点击外部任意程序区域自动隐藏。
* **滚动位置记忆**：支持“记忆滚动”配置，默认未开启时，每次打开自动复位显示顶部第一条；开启后记忆上次滚动位置。

> ⚠️ **数据升级提示**：WinKit 升级为纯明文 JSON Lines 存储架构。若您需要保留旧版本（v2.2 及以前）的待办数据（`.md`）或剪贴板历史（`.db`），请在升级前手动记录备份，新版本不会自动转换旧格式数据。

### 3. ⚙️ 统一偏好设置中心
* **全功能配置面板**：350×530 卡片式磨砂面板，集中整合“待办清单”、“高级剪贴板”、“常规与外观（不透明度与开机启动）”全部配置项，支持快捷键物理捕获、即时保存与一键恢复默认。
* **深度交互互斥**：打开偏好设置时自动收起待办输入并关闭编辑弹窗，避免焦点与快捷键冲突。

### 4. 🎨 极简 Fluent 托盘右键菜单
* **扁平 4 项纯文字菜单**：彻底取消繁杂二级菜单，一级直达 **待办历史 / 偏好设置 / 关于 / 退出**。
* **双击托盘联动**：左键单击无动作，双击托盘图标智能切换 TodoList 显示与置顶（可见时先收起输入框再隐藏，不可见时一键置顶呼出），可在偏好设置中一键启闭。

---

## 🛠️ 下一步开发计划 (Roadmap)

~~在后续的版本中，我计划为 `WinKit` 深度融入一个**截图工具**：
**截图/贴图/钉图/OCR/截图翻译**~~

 估计要很久了、目前来看得到3个月之后才会开展
 OCR、翻译在找好用的离线方案。如果有推荐的可以告诉我
 目前就是修修bug
 
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
dotnet publish WinKit.csproj -c Release -r win-x64 --self-contained false -o bin\Publish\FrameworkDependent_V2.3

# 2. 编译并打包：独立免装版 (Self-Contained)
dotnet publish WinKit.csproj -c Release -r win-x64 --self-contained true -o bin\Publish\SelfContained_V2.3
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
