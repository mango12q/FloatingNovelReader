# 浮窗小说阅读器 (Floating Novel Reader)

一款 Windows 桌面端**悬浮窗小说阅读器**。打开后是一个无边框、半透明的小窗体，常驻其他窗口之上；可拖动、可调大小、可设为"鼠标穿透"以避免干扰工作。完整支持 TXT 小说导入、自动分章、书签、目录、进度记忆，并提供全局快捷键（无需切换焦点即可翻页/调速/Boss Key 等）。

---

## 功能特性

### 阅读体验
- 始终置顶 / 半透明 / 无边框悬浮窗
- 鼠标穿透（点击直接落到下层窗口，悬浮窗"隐身"）
- 鼠标悬停自动出现控制栏（≡ 目录 / ⚙ 设置 / 🔖 书签 / 🔖+ 添加书签 / ◁ 上一页 / ◉ 置顶 / ⨯ 关闭）
- 拖动标题栏任意位置即可移动窗口；四边/四角可调整大小（Win32 + WindowChrome）
- 字体、字号、背景色、行间距、透明度均可调
- 自动阅读（可调速、加速/减速快捷键）
- 阅读进度自动保存，下次打开自动恢复

### 内容与导航
- TXT 导入（**自动检测编码** GBK / UTF-8 / UTF-16 LE/BE / Big5 …）
- 自动**卷章解析**（支持「第一卷 / 卷一 / Chapter 1 / 1、」等多种写法）
- 章节目录窗口（按卷/章树形展示，点击跳转）
- 书签功能（添加 / 列表 / 跳转）

### 全局快捷键
- 所有快捷键**可在「设置 → 快捷键」中自定义**
- **单键即生效**（如 N / ↓ / F1），不强制要求组合键
- **录制模式**：点击输入框即可按下新键，Esc 取消，按 Backspace/Delete 清空
- **右键清空**：在输入框上右键即可把该快捷键清空（"禁用此快捷键"）
- **保存即时生效**：保存后全局钩子立即重载
- **与录制冲突隔离**：录制期间全局钩子暂停，避免"刚按的 N 立刻触发下一页"
- 内置默认快捷键（见下表）

### 数据与系统集成
- 系统托盘（最小化到托盘，关闭即最小化）
- 本地 SQLite 存储（库 / 书签 / 进度）
- 完整的"从书架移除"流程：可选择仅删除记录，或同时删除源 TXT 文件；外键级联清理 Books → Volumes/Chapters/ReadingProgress/Bookmarks
- Boss Key 一键隐藏窗口
- 边缘吸附

---

## 技术栈

| 类别 | 选型 |
|------|------|
| 框架 | .NET 8 / WPF |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| 全局钩子 | MouseKeyHook |
| 数据库 | Microsoft.Data.Sqlite + EF Core（手动 SQL） |
| 编码检测 | Ude.NetStandard |
| 日志 | Serilog（按天切割，文件输出） |
| 单元测试 | xUnit |

---

## 目录结构

```
novel2/
├── README.md                              # 本文件
├── LICENSE                                # MIT
├── FloatingNovelReader.sln                # 解决方案
├── .editorconfig                          # 跨编辑器风格配置
├── .gitignore                             # 忽略 .bin/ .obj/ obj/ bin/ Logs/
├── .vscode/                               # VSCode 配置（推荐扩展/任务/调试）
│   ├── extensions.json
│   ├── launch.json
│   ├── settings.json
│   └── tasks.json
├── Build/
│   └── build.ps1                          # 一键发布脚本（PowerShell）
├── FloatingNovelReader/                   # 主项目
│   ├── App.xaml(.cs)                      # 应用入口 + DI 容器 + 事件总线接线
│   ├── app.manifest                       # UAC / DPI 清单
│   ├── AssemblyInfo.cs
│   ├── Core/                              # 基础设施
│   │   ├── EventBus.cs                    # 全局事件总线
│   │   ├── HotkeyManager.cs               # 全局热键注册 + 录制态隔离
│   │   ├── KeyGestureLite.cs              # 单键 / 组合键的统一描述
│   │   └── SettingsService.cs             # settings.json 读写 + SettingsChanged 事件
│   ├── Models/                            # 数据模型
│   │   ├── Book.cs / Volume.cs / Chapter.cs
│   │   ├── Bookmark.cs / ReadingProgress.cs
│   │   ├── HotkeyConfig.cs                # 快捷键绑定（默认值 + 索引器）
│   │   └── ChapterReference.cs
│   ├── Services/                          # 业务服务
│   │   ├── DatabaseService.cs             # SQLite（含 PRAGMA foreign_keys=ON）
│   │   ├── BookImportService.cs           # TXT 导入（编码检测 + 卷章解析）
│   │   ├── BookshelfService.cs            # 书架增删查
│   │   ├── PaginationService.cs           # 分页引擎
│   │   ├── ReadingSessionService.cs       # 阅读会话/进度
│   │   └── BookmarkService.cs             # 书签增删查
│   ├── ViewModels/                        # MVVM ViewModel
│   │   ├── MainViewModel.cs
│   │   ├── BookshelfViewModel.cs          # 含"是否删除源文件"三选一对话框
│   │   ├── ReaderViewModel.cs             # 含 JumpToChapter / RefreshPageAfterJump
│   │   ├── ChapterListViewModel.cs        # 含 Load(book) 加载完整卷章树
│   │   ├── BookmarkViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   └── ImportProgressViewModel.cs
│   ├── Views/                             # XAML 视图窗口
│   │   ├── MainWindow.xaml(.cs)           # 书架主窗口
│   │   ├── ReaderWindow.xaml(.cs)         # 阅读悬浮窗（拖动 + Resize + 穿透）
│   │   ├── ChapterListWindow.xaml(.cs)    # 章节目录弹窗
│   │   ├── BookmarkWindow.xaml(.cs)       # 书签列表弹窗
│   │   ├── SettingsWindow.xaml(.cs)       # 设置（外观 / 阅读 / 快捷键）
│   │   └── ImportProgressWindow.xaml(.cs)
│   ├── Controls/                          # 自定义控件
│   │   └── HotkeyTextBox.cs               # 快捷键录制控件（单键 / 组合键 / 右键清空）
│   ├── Helpers/                           # 辅助工具
│   │   ├── ChapterParser.cs               # 卷章解析（中文 / 英文 / 数字列表）
│   │   ├── TextEncoderDetector.cs         # 编码探测
│   │   ├── Win32Helper.cs                 # 鼠标穿透 / 窗口置顶 / 屏幕坐标
│   │   └── ...
│   ├── Converters/                        # WPF 值转换器
│   ├── Properties/                        # 程序集元数据
│   └── Resources/
│       └── Icons/                         # 应用图标 / 托盘图标
└── FloatingNovelReader.Tests/             # xUnit 单元测试
    ├── Core/KeyGestureLiteTests.cs        # 36 个：单键 / 组合键 / ToString / TryParse
    ├── Helpers/                           # 文本处理、Win32 助手测试
    ├── Services/
    │   ├── ChapterParserTests.cs          # 卷章解析（含「第N章 xxx」标题拼接）
    │   ├── BookImportServiceTests.cs      # TXT 导入完整流程
    │   └── BookshelfServiceTests.cs       # 移除 = 完整级联删除
    └── ...
```

> **构建产物隔离**：`bin/obj` 已通过 MSBuild 的 `BaseIntermediateOutputPath` 重定向到 `.bin/` `.obj/`，根目录的 `obj/` 是 NuGet restore 的中间文件，正常被 `.gitignore` 忽略。

---

## 在 Windows 上构建

### 1. 准备环境

- Windows 10/11（64 位）
- 安装 [.NET 8 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)（勾选「.NET 桌面运行时」）
- 工具二选一：
  - **Visual Studio Code**（轻量，需要安装扩展）
  - **Visual Studio 2022**（带「.NET 桌面开发」工作负载，可视化 XAML 设计器）

> ⚠️ WPF 是 Windows 专属框架，本项目**无法在 macOS / Linux 上运行**。

### 2. 使用 VSCode 打开

项目已预置完整的 VSCode 工程配置（`.vscode/`、`.editorconfig`），按下面三步即可上手。

#### 2.1 安装必备扩展

打开项目后 VSCode 会自动弹出推荐扩展安装提示：

- **C# Dev Kit**（`ms-dotnettools.csdevkit`）— 核心支持
- **C#**（`ms-dotnettools.csharp`）— 语法高亮、补全
- **IntelliCode**（`visualstudioexptteam.vscodeintellicode`）— 智能补全
- **C# Extensions**（`kreativ-software.csharpextensions`）— 一键新建类/接口
- **VSCode Solution Explorer**（`fernandoescolar.vscode-solution-explorer`）— 资源管理器式解决方案视图

一行装完：

```powershell
code --install-extension ms-dotnettools.csdevkit `
     --install-extension ms-dotnettools.csharp `
     --install-extension visualstudioexptteam.vscodeintellicode `
     --install-extension kreativ-software.csharpextensions `
     --install-extension fernandoescolar.vscode-solution-explorer
```

> 💡 C# Dev Kit 第一次打开时可能要求登录 Microsoft 账户，可点「Later」跳过，不影响编译。

#### 2.2 还原依赖

```powershell
dotnet restore
```

或按 `Ctrl+Shift+P` → `Tasks: Run Task` → `restore`。

#### 2.3 编译、运行、调试

| 操作 | 方法 |
|------|------|
| 编译（Debug） | `Ctrl+Shift+B`，或 `dotnet build -c Debug` |
| 编译（Release） | 任务面板 → `build-release` |
| **运行** | **F5** 启动调试（自动 build + 启动 `FloatingNovelReader.exe`） |
| 启动而不调试 | `Ctrl+F5` |
| 单元测试 | 任务面板 → `test`，或 `dotnet test` |
| 断点调试 | 行号左侧点红点 → F5 |

> WPF 没有 XAML 热重载。改完 XAML → `Ctrl+Shift+B` 重编译 → F5 启动。

#### 2.4 常见问题

**F5 报错「无法创建目录 obj\Debug\net8.0-windows」「Access to the path ... is denied」**

MSBuild 文件锁冲突。**解决步骤**：

1. 任务面板 → 运行 `clean-force`（强删 `bin/obj/.bin/.obj`）
2. `Ctrl+Shift+P` → `Developer: Reload Window` 重启 VSCode
3. 再按 F5

**NuGet 还原失败**（网络问题）：

```powershell
dotnet nuget add source https://nuget.cdn.azure.cn/v3/index.json -n "Azure 中国"
```

#### 2.5 VSCode 快捷速查

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+Shift+P` | 命令面板 |
| `Ctrl+Shift+B` | 运行编译任务 |
| `F5` | 启动调试 |
| `Ctrl+F5` | 启动不调试 |
| `Ctrl+Shift+F` | 全局搜索 |
| `F12` | 跳转到定义 |
| `Alt+F12` | 查看定义（悬浮） |
| `Shift+F12` | 查找所有引用 |

### 3. 直接用命令行

```powershell
# 调试构建
dotnet build -c Debug

# Release 构建
dotnet build -c Release

# 运行测试
dotnet test
```

### 4. 发布单文件 EXE

```powershell
# Framework-Dependent（推荐：体积小，需用户机装 .NET 8 桌面运行时）
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/win-x64-fd

# 自包含（不依赖运行时：体积大，可直接双击运行）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish/win-x64-sc
```

### 5. 一键发布

```powershell
.\Build\build.ps1
```

可选参数：

| 参数 | 说明 |
|------|------|
| `-Configuration Release` | 默认 Release |
| `-Rid win-x64` | 目标 RID |
| `-SkipTests` | 跳过单元测试 |
| `-SkipPublish` | 跳过 publish 阶段 |

---

## 首次运行

1. 双击 `FloatingNovelReader.exe`
2. 如果提示「.NET 8 运行时未安装」，按引导前往微软官网下载
3. 打开后默认进入「书架」窗口
4. 点击「导入」选 TXT 文件 → 自动检测编码、解析卷章、入库
5. 双击书籍卡片开始阅读
6. 阅读窗口按 F3 切换鼠标穿透 → 看书不干扰工作

---

## 默认快捷键

| 快捷键 | 功能 |
|--------|------|
| `Space` / `→` | 下一页 |
| `Back`（←）/ `Backspace` | 上一页 |
| `PageDown` | 下一章 |
| `PageUp` | 上一章 |
| `F3` | 切换鼠标穿透 |
| `F4` | 切换窗口置顶 |
| `F5` | 切换自动阅读 |
| `F6` | 加快自动阅读 |
| `F7` | 减慢自动阅读 |
| `F8` | 隐藏窗口（Boss Key） |
| `F9` | 打开章节目录 |
| `F10` | 添加书签 |
| `F11` | 打开书签列表 |
| `Ctrl+↑` / `+` | 增加透明度 |
| `Ctrl+↓` / `-` | 降低透明度 |

**所有快捷键都可在「设置 → 快捷键」中自定义：**
- 单击输入框 → 按新键（**单键即生效**，如 N、↓、F1）
- 按 Esc 取消录制，恢复原值
- 按 Backspace/Delete 清空当前快捷键
- **右键输入框** 清空并禁用该快捷键

---

## 配置文件位置

| 文件 | 路径 |
|------|------|
| 库数据 | `%LocalAppData%\FloatingNovelReader\library.db` |
| 设置 | `%LocalAppData%\FloatingNovelReader\settings.json` |
| 日志 | `%LocalAppData%\FloatingNovelReader\Logs\app-YYYY-MM-DD.log` |

---

## 项目状态

- ✅ Phase 1：核心骨架（窗口、Win32 辅助、拖拽缩放、置顶、穿透、透明度、边缘吸附）
- ✅ Phase 2：文本引擎（编码检测、卷章解析、SQLite 导入、分页引擎）
- ✅ Phase 3：阅读功能（ReaderViewModel、悬停控制栏、全局快捷键、防抖、进度保存/恢复）
- ✅ Phase 4：书架、书签、章节目录
- ✅ Phase 5：自动阅读、设置、字体、背景、行间距
- ✅ Phase 6：托盘、窗口记忆、Framework-Dependent 单文件发布
- ✅ Phase 7：自定义快捷键（单键 / 录制 / 右键清空 / 全局钩子隔离）
- ✅ Phase 8：彻底删除（外键级联 + 可选删除源文件 + 关闭打开的 ReaderWindow）
- ✅ 单元测试覆盖：KeyGestureLite、ChapterParser、BookImport、Bookshelf

### 测试覆盖

```
$ dotnet test
Passed: 64
Failed: 0
```

主要覆盖：
- `KeyGestureLite`（单键 / 组合键解析与序列化、36 用例）
- `ChapterParser`（卷章解析、第 N 章 + 章节名拼接、6 用例）
- `BookImportService`（TXT 导入完整流程、含编码检测）
- `BookshelfService`（移除 = 完整级联删除，5 用例）
- `Win32Helper`（窗口置顶、屏幕坐标等 Win32 封装）

---

## 已知限制

- 仅支持 TXT 文件（EPUB 作为后续扩展）
- Windows 专属（依赖 Win32 API 与 WPF）
- 当前为单实例模式（再次启动会被拒绝并提示）

---

## License

MIT
