# WinKit - PinToDesk 与 MiniPaste 项目整合方案

本方案旨在将本地项目 `PinToD` (PinToDesk) 与开源项目 `MiniPaste` 深度整合，打造一个极致轻量、界面美化的统一桌面助手工具包 `WinKit`。

## 用户审查要点
> [!IMPORTANT]
> **关于全局热键 Win + V 的实现方案选择：**
> Windows 11 自带剪贴板历史工具占用了 `Win + V` 热键。使用常规 API 注册此热键会导致失败。
> 为此，我们提供了两种设计方案供您选择。本计划默认采用 **方案 B**，以便接管系统快捷键：
> * **方案 A**：仅使用常规 Windows 热键 API 注册，如果在系统开启了剪贴板历史的情况下，该热键无法正常生效。
> * **方案 B（推荐）**：使用低级键盘钩子 (Low-Level Keyboard Hook) 强行拦截并接管系统的 `Win + V`。当按下时，直接阻止系统自带剪贴板弹出，取而代之弹出我们的 MiniPaste 历史界面。再次按下时隐藏。

> [!TIP]
> **极致轻量化设计：**
> 原 `MiniPaste` 的设置窗口 (SettingsWindow) 被精简移除，取而代之的是将“去重功能”和“开机启动”直接作为托盘右键的勾选项。最大条目数等核心设置直接由默认配置文件或托盘选项定义，以此实现极致的轻量与精炼。

## 开放性问题
> [!WARNING]
> 1. 您是否希望在双击 MiniPaste 中的某条历史项时，不仅将其复制到系统剪贴板，同时自动模拟 `Ctrl + V` 将其粘贴到当前的活跃窗口中？（本计划默认仅执行复制，双击可模拟回填）
> 2. 合并后的项目，我们将把 `PinToD` 文件夹重命名为 `WinKit`，在其中直接提供 `WinKit.csproj`。您对此是否同意？

---

## 拟进行的修改

### 1. 结构重组与工程配置 (WinKit)
我们将建立统一的 namespace 为 `WinKit`，并且补充缺失的项目文件。

#### [NEW] [WinKit.csproj](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/WinKit.csproj)
* 这是一个 .NET 8.0 WPF 应用程序配置文件。
* 支持 `UseWPF` 和 `UseWindowsForms`（用于托盘 NotifyIcon 及其菜单支持）。
* 引入 NuGet 依赖：
  * `System.Drawing.Common` (用于处理图标和绘图)
  * `Microsoft.Data.Sqlite` (用于 MiniPaste 剪贴板数据库存储)

#### [NEW] [App.xaml](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/App.xaml)与[App.xaml.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/App.xaml.cs)
* 应用程序统一入口。
* 初始化 `SettingsManager`。
* 启动 `ClipboardService` 与 `ClipboardManager` 以持久化监听剪贴板变化（即使窗口未打开）。
* 实例化合并后的托盘辅助服务 `TrayHelper`。
* 实例化键盘钩子服务并注册 `Win + V` 热键的低级键盘监听。
* 管理 `Todo.MainWindow` 和 `Paste.MainWindow` 的可见性。

---

### 2. 托盘合并与通用模块 (Common)

#### [NEW] [AppPaths.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Common/AppPaths.cs)
* 统一文件读写路径，所有数据迁移到 `%AppData%\WinKit`：
  * 待办事项：`%AppData%\WinKit\todos.md`
  * 剪贴板数据库：`%AppData%\WinKit\clipboard.db`
  * 统一配置文件：`%AppData%\WinKit\settings.json`

#### [NEW] [AutoStartHelper.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Common/AutoStartHelper.cs)
* 实现通用的开机启动项写入与删除逻辑（注册表键名：`WinKit`）。

#### [MODIFY] [TrayHelper.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Helpers/TrayHelper.cs) (移动至 Common/TrayHelper.cs)
* 合并两个项目的托盘。右键托盘图标显示以下菜单结构：
  * **TodoList待办** (子菜单)
    * `显示待办` / `隐藏待办`
    * `置顶待办` / `取消置顶`
    * `鼠标穿透` / `关闭穿透`
  * **剪切板历史** (子菜单)
    * `显示历史` (弹出 MiniPaste 界面)
    * `清空历史` (清空 SQLite 数据库历史数据)
    * `启用去重` (Checkable, 切换去重)
  * `ToolStripSeparator` (分隔符)
  * `开机启动` (Checkable, 统一控制 WinKit 自启)
  * `ToolStripSeparator` (分隔符)
  * `退出` (干净关闭两个窗口，注销钩子并退出程序)
* 左键单击托盘图标：快速显示/隐藏 TodoList 窗口。

---

### 3. 全局热键重构 (Paste/Services)

#### [NEW] [KeyboardHookService.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Paste/Services/KeyboardHookService.cs)
* 取代原本冲突重重的 `HotKeyService`，使用 Win32 键盘钩子 `SetWindowsHookEx`。
* 捕捉 `Win + V`，当检测到该组合键时：
  * 返回 `1` 阻止 Windows 系统内置的剪贴板历史菜单弹出。
  * 触发回调函数，控制 MiniPaste 窗口在屏幕右下角（鼠标附近或屏幕托盘侧）优雅显隐。

---

### 4. 剪贴板核心服务迁移 (Paste/Services & Paste/Models)
将原 `MiniPaste` 中的剪切板轮询与存储机制复制到新项目中，清理冲突并适配新配置：

#### [NEW] [ClipboardItem.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Paste/Models/ClipboardItem.cs)
* 声明剪贴板数据实体。

#### [NEW] [AppSettings.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Common/AppSettings.cs)
* 统一的设置模型类，将待办设置和剪切板设置聚合：
  ```csharp
  public class AppSettings
  {
      public bool TodoIsPinned { get; set; } = false;
      public bool TodoIsPassThrough { get; set; } = false;
      public bool TodoWidth { get; set; } = 280;
      public bool TodoHeight { get; set; } = 360;
      
      public int PasteMaxItems { get; set; } = 100;
      public long PasteMaxStorageSizeMB { get; set; } = 50;
      public bool PasteEnableTextDeduplication { get; set; } = true;
      public bool StartWithWindows { get; set; } = false;
  }
  ```

#### [NEW] [SettingsManager.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Common/SettingsManager.cs)
* 统一的配置文件存取类，持久化读写至 `%AppData%\WinKit\settings.json`。

#### [NEW] [ClipboardService.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Paste/Services/ClipboardService.cs)
* 纯净移植，清除 Git 冲突标记。使用 800ms 的 `DispatcherTimer` 进行低资源开销文本监控。

#### [NEW] [ClipboardManager.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Paste/Services/ClipboardManager.cs)
* 纯净移植，清除 Git 冲突标记，使用优化的 `Deflate` 压缩和 SQLite 后台异步分批加载版本。

---

### 5. MiniPaste 界面美化与风格统一 (Paste/UI)

#### [NEW] [MainWindow.xaml](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Paste/MainWindow.xaml)与[MainWindow.xaml.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Paste/MainWindow.xaml.cs) (重命名为 ClipboardWindow)
* **毛玻璃半透明风格**：
  * 设置窗口为无边框透明：`AllowsTransparency="True"`, `Background="Transparent"`, `WindowStyle="None"`。
  * 引入 `RootBorder` 背景为 `#66FFFFFF`（磨砂白），圆角 10 像素。
* **标题栏统一**：
  * 类似 Todo 窗口的拖拽移动，带有标题 `剪贴板历史` 和自定义关闭按钮 `✕`。
* **搜索框美化**：
  * 摒弃强硬白色，背景使用半透明白 `#CCFFFFFF`，加圆角与悬浮反馈。
* **列表与细滚动条**：
  * 复用 Todo 窗口的细滚动条 `NarrowScrollViewer`。
  * 列表项在 hover 时呈优雅灰色高亮，右侧悬浮显示删除按钮。
* **行为适配**：
  * 双击某行：将选中的文本复制回系统剪贴板，隐藏窗口。同时可以添加选项：自动执行 `Ctrl + V` 粘贴。
  * 弹出定位：每当按下 `Win + V`，根据鼠标当前坐标或屏幕工作区，在合适的位置弹出窗口，且自动聚焦搜索框，体验流畅。

---

### 6. TodoList 待办模块迁移 (Todo)
将原 `PinToD` 中的代码转移到 `Todo` 文件夹下，调整命名空间为 `WinKit`：

#### [MODIFY] [MainWindow.xaml.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/MainWindow.xaml.cs) (移至 Todo/MainWindow.xaml.cs)
* 调整命名空间为 `WinKit.Todo`。
* 适配新的统一 `SettingsManager`。

#### [MODIFY] [EditDialog.xaml.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/EditDialog.xaml.cs) (移至 Todo/EditDialog.xaml.cs)
* 调整命名空间为 `WinKit.Todo`。

#### [MODIFY] [MarkdownStorage.cs](file:///c:/Users/LIBO/Desktop/WinKit/PinToD/Services/MarkdownStorage.cs) (移至 Todo/Services/MarkdownStorage.cs)
* 将默认保存的 `todos.md` 位置修改为 `%AppData%\WinKit\todos.md`。

---

## 验证计划

### 编译验证
* 执行命令：
  ```powershell
  dotnet build
  ```
  保证程序可以无警告、无错误地顺利编译通过。

### 功能验证
1. **统一托盘**：确认系统托盘图标是否正确加载。右键托盘菜单是否含有双子菜单，各功能是否正确响应。
2. **待办功能**：TodoList 窗口的双击添加、双击修改、拖拽排序、置顶、穿透等功能是否与整合前完全一致。
3. **剪贴板监听**：在外部复制几段不同的文本，右键托盘“显示历史”，检查是否已成功存入 SQLite 并压缩展示。
4. **去重过滤**：复制相同文字，验证去重功能是否生效。
5. **Win + V 全局热键**：在任意地方按下 `Win + V`，确认是否能完美拦截 Windows 内置历史并瞬间弹出我们的 MiniPaste 窗口。
6. **自启控制**：在托盘菜单勾选“开机启动”，查看注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下是否正常写入 `WinKit` 启动项。
