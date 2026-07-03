# 浮窗小说阅读器 Floating Novel Reader

<p align="center">
  <img src="FloatingNovelReader/Resources/Icons/app.ico" width="80" alt="Logo">
</p>

<p align="center">
  <b>一个悬浮在桌面上的小说阅读器 · 透明 · 可拖动 · 可穿透 · 全局快捷键</b>
</p>

<p align="center">
  <a href="https://github.com/mango12q/FloatingNovelReader/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/mango12q/FloatingNovelReader/ci.yml?style=for-the-badge&label=CI" alt="CI"></a>
  <a href="#-下载安装"><img src="https://img.shields.io/badge/下载-Windows-0078d4?style=for-the-badge&logo=windows&logoColor=white" alt="Download"></a>
  <a href="#-使用说明"><img src="https://img.shields.io/badge/平台-Windows%2010%2F11-blue?style=for-the-badge&logo=windows" alt="Platform"></a>
  <a href="#-使用说明"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/许可证-MIT-green?style=for-the-badge" alt="License"></a>
</p>

---

> 看小说的同时不耽误工作——半透明悬浮窗，鼠标可穿透；支持 TXT 自动分章、书签、目录、全局快捷键翻页。

---

## ✨ 功能特性

### 📖 阅读体验
- **悬浮窗**：无边框 / 始终置顶 / 半透明
- **鼠标穿透**：按 `F3` 切换——悬浮窗对鼠标"隐身"，点击直接落到下层窗口
- **拖动 & 调整大小**：四边/四角拉伸，任意位置按住即可移动
- **自动阅读**：可调速、加速/减速快捷键

### 🎨 字体与颜色
- **字体族**：黑体 / 宋体 / 楷体三款中文衬线/无衬线字体可选
- **背景颜色**：白色 / 灰色 / 黑色 / 纸页黄四种预设
- **字体颜色**：黑色 / 白色两种预设，与背景自动适配

### 📚 内容 & 导航
- **TXT 导入**：自动检测编码（GBK / UTF-8 / UTF-16 / Big5 …）
- **自动分章**：识别「第 N 章 / 第一卷 / Chapter 1 / 1、」等多种写法
- **章节目录**：按 `F9` 打开，按卷/章树形展示
- **书签**：按 `F10` 添加 / `F11` 打开书签列表 / 列表点击跳转
- **进度记忆**：阅读位置自动保存，下次打开自动恢复

### ⌨️ 全局快捷键
- **单键即生效**（N / ↓ / F1 都行），不强制要求组合键
- **可自定义**：在「设置 → 快捷键」里点输入框就能录新键
- **录制模式**：单按 Esc 取消 / Backspace 清空 / **右键清空**（禁用该快捷键）
- **保存即时生效**：保存后全局钩子立即重载

### 🗂️ 数据与系统集成
- **系统托盘**：最小化到托盘
- **SQLite 存储**：本地库 / 书签 / 进度
- **彻底删除**：从书架移除时，可选同时删除源文件；外键级联清理数据库
- **Boss Key**：`F8` 一键隐藏窗口
- **边缘吸附**：拖到屏幕边缘自动贴边

### 🔧 安装与卸载
- **首次运行自动安装**：复制到用户目录 + 创建桌面/开始菜单快捷方式 + 注册到「设置 → 应用」
- **便携模式**：加 `--portable` 参数启动则不安装，直接运行
- **卸载**：从「设置 → 应用」卸载，或运行 `FloatingNovelReader.exe --uninstall`
- **运行时检测**：启动时自动检测 .NET 8 桌面运行时，未安装则引导下载

---

## 📥 下载安装

前往 [**Releases**](../../releases) 下载最新版本。

### 下载内容

每个 Release 提供以下文件：

| 文件 | 说明 |
|------|------|
| `floating-novel-reader-portable.exe` | 单个 EXE，直接双击运行 |
| `floating-novel-reader-portable-win-x64-*.zip` | 同上 EXE 的 zip 压缩包 |

### 运行环境

| 需求 | 说明 |
|------|------|
| 操作系统 | Windows 10 / 11（64 位） |
| 运行时 | **.NET 8 桌面运行时**（Desktop Runtime） |

### 安装 .NET 8 桌面运行时

程序启动时会自动检测运行时。如果未安装，会弹出提示对话框，点击「确定」自动打开浏览器前往下载页。

也可以手动下载：
👉 https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0/runtime

> 选择「.NET 桌面运行时」（Desktop Runtime），下载 x64 安装包，双击安装即可。

### 安装步骤

1. 下载 `floating-novel-reader-portable.exe`（或解压 zip 后的 EXE）
2. 双击运行
3. 首次运行会弹出安装确认对话框：

   - **点击「是」** → 自动安装到 `%LocalAppData%\Programs\FloatingNovelReader\`，创建桌面和开始菜单快捷方式，注册到系统卸载列表
   - **点击「否」** → 以便携模式直接运行，不安装任何文件

4. 安装后，从桌面快捷方式启动即可

### 卸载

任选一种方式：

- **方式 1**：「设置 → 应用 → 已安装的应用」找到「浮窗小说阅读器」→ 卸载
- **方式 2**：命令行运行 `FloatingNovelReader.exe --uninstall`

卸载会自动删除程序文件、桌面快捷方式、开始菜单文件夹和注册表项。

### 联系作者

如有问题或建议，请联系：**mango12q@163.com**

---

## 🚀 快速开始

1. 下载并运行 EXE（首次运行点击「是」安装）
2. 主窗口「书架」→ 点击「导入」按钮 → 选一个 TXT 文件
3. 等待进度条走完（首次导入约几秒，1MB 以内瞬间完成）
4. 双击书籍卡片 → 阅读窗口弹出
5. 按 `F3` 切换鼠标穿透 → 看小说不挡工作

### 默认快捷键速查

| 快捷键 | 功能 |
|--------|------|
| `→` / `Space` | 下一页 |
| `←` / `Backspace` | 上一页 |
| `PageDown` / `PageUp` | 下一章 / 上一章 |
| `F3` | 切换鼠标穿透 |
| `F4` | 切换窗口置顶 |
| `F5` | 开始 / 暂停自动阅读 |
| `F6` / `F7` | 自动阅读加速 / 减速 |
| `F8` | Boss Key（一键隐藏） |
| `F9` | 章节目录 |
| `F10` | 添加书签 |
| `F11` | 书签列表 |
| `Ctrl+↑` / `Ctrl+↓` | 增加 / 降低透明度 |

> 所有快捷键都可在 **设置 → 快捷键** 中改键或清空。

---

## 🏗️ 架构设计

项目采用分层架构，通过接口抽象实现各层解耦：

```
┌─────────────────────────────────────────────────────────┐
│  Views（ReaderWindow / BookshelfWindow / SettingsWindow）│
│  仅负责 UI 生命周期事件、窗口交互（拖动/缩放/动画）          │
└───────────────────────────┬─────────────────────────────┘
                            │ DataBinding
┌───────────────────────────▼─────────────────────────────┐
│  ViewModels（ReaderViewModel / BookshelfViewModel 等）      │
│  通过 IEventAggregator 接收热键事件，持有 AppServices 引用   │
└───────────────────────────┬─────────────────────────────┘
                            │ 调用
┌───────────────────────────▼─────────────────────────────┐
│  ApplicationServices（IBookService / BookService）        │
│  用例编排层：协调多个 Repository 完成业务场景                │
└───────────────────────────┬─────────────────────────────┘
                            │ 依赖
┌───────────────────────────▼─────────────────────────────┐
│  Infrastructure / Repositories                          │
│  IBookRepository / IChapterRepository / IBookmarkRepository│
│  SqliteBookRepository / …  — 可 Mock，可换数据库引擎         │
└───────────────────────────┬─────────────────────────────┘
                            │ 使用
┌───────────────────────────▼─────────────────────────────┐
│  Core（HotkeyManager / EventAggregator / Constants）       │
│  HotkeyManager（全局钩子）→ EventAggregator → ViewModel    │
└─────────────────────────────────────────────────────────┘
```

**热键事件流**（全局快捷键穿透所有软件）：

```
键盘按键
  → HotkeyManager.OnKeyDown()        （全局钩子，后台线程，Gma.System.MouseKeyHook）
  → HotkeyPressed 事件
  → App.xaml.cs 桥接
     events.Publish(new HotkeyPressedEvent(action))
  → IEventAggregator<HotkeyPressedEvent>
  → ReaderViewModel.OnHotkeyReceived()  （检查 CurrentBook != null）
  → Dispatcher.Invoke()               （切换到 UI 线程）
  → 执行对应操作（翻页 / 穿透 / 置顶 / …）
```

---

## 🛠️ 技术栈

| 类别 | 选型 | 说明 |
|------|------|------|
| 框架 | .NET 8 / WPF | `net8.0-windows`，`UseWPF=true` |
| MVVM | CommunityToolkit.Mvvm 8.4.0 | `[ObservableProperty]` / `[RelayCommand]` |
| DI | Microsoft.Extensions.DependencyInjection 8.0.1 | 所有服务通过容器解析 |
| 全局钩子 | Gma.System.MouseKeyHook 5.7.1 | 后台全局键盘监听，支持单键触发 |
| 数据库 | SQLite（Microsoft.Data.Sqlite 8.0.10） | 5 表，外键级联，`PRAGMA foreign_keys=ON` |
| 仓储抽象 | 自研 `IRepository<T>` | 每个实体独立 Repository，可 Mock |
| 事件总线 | `IEventAggregator<T>`（自研） | 强类型事件，编译期检查，替代字符串 EventBus |
| 编码检测 | Ude.NetStandard 1.2.0 | BOM + 启发式检测（GBK/UTF-8/UTF-16/Big5…） |
| 日志 | Serilog 4.0.0 | 按日滚动，保留 30 天，`%LocalAppData%\FloatingNovelReader\Logs\` |
| 单元测试 | xUnit 2.9.2 + Moq | 64 个测试用例 |

---

## 📁 项目结构

```
.
├── README.md
├── LICENSE
├── .gitignore
├── .editorconfig
├── Build/
│   └── build.ps1                          # 一键构建 + 发布 portable EXE + zip
├── FloatingNovelReader/                   # 主项目（WPF）
│   ├── App.xaml(.cs)                      # 入口：运行时检测 + 自安装 + DI + 热键桥接
│   ├── app.manifest
│   ├── AssemblyInfo.cs
│   │
│   ├── Core/                              # 基础设施层
│   │   ├── Bootstrapper.cs               # DI 容器装配
│   │   ├── Constants.cs                  # 全局常量（路径/尺寸/事件名…）
│   │   ├── EventBus.cs                   # 兼容层：基于字符串的事件总线
│   │   ├── IEventAggregator.cs           # 强类型事件聚合器接口
│   │   ├── EventAggregator.cs            # 强类型事件聚合器实现
│   │   ├── HotkeyManager.cs              # 全局热键 + 防抖 + 录制屏蔽
│   │   ├── KeyGestureLite.cs             # 单键/组合键描述
│   │   └── SelfInstaller.cs              # 首次运行自安装 / 卸载
│   │
│   ├── Infrastructure/                    # 基础设施层（新增）
│   │   ├── IDbConnectionFactory.cs       # SQLite 连接工厂接口
│   │   ├── SqliteConnectionFactory.cs    # SQLite 连接工厂实现
│   │   └── Repositories/                 # 仓储层
│   │       ├── IBookRepository           # 书籍仓储接口
│   │       ├── SqliteBookRepository      # SQLite 实现
│   │       ├── IChapterRepository        # 章节仓储接口
│   │       ├── SqliteChapterRepository   # SQLite 实现
│   │       ├── IReadingProgressRepository# 进度仓储接口
│   │       ├── SqliteReadingProgressRepository # SQLite 实现
│   │       ├── IBookmarkRepository       # 书签仓储接口
│   │       ├── SqliteBookmarkRepository  # SQLite 实现
│   │       ├── IVolumeRepository         # 卷仓储接口
│   │       ├── SqliteVolumeRepository    # SQLite 实现
│   │       ├── IUnitOfWork               # 工作单元接口
│   │       └── SqliteUnitOfWork          # SQLite 工作单元实现
│   │
│   ├── ApplicationServices/              # 应用服务层（用例编排）
│   │   ├── IBookService.cs               # 书籍用例接口 + DTO
│   │   └── BookService.cs                # 用例编排实现（并发查询）
│   │
│   ├── Models/                           # 数据模型
│   │   ├── AppSettings.cs                # 全局应用设置
│   │   ├── AppState.cs                   # 进程内运行时状态
│   │   ├── Book.cs / Volume.cs / Chapter.cs
│   │   ├── Bookmark.cs / ReadingProgress.cs
│   │       ├── DisplaySettings.cs            # 字体/字体色/背景/透明度
│   │   └── HotkeyConfig.cs               # 快捷键绑定配置
│   │
│   ├── Services/                         # 业务服务
│   │   ├── BookImportService.cs          # TXT 导入全流程
│   │   ├── BookshelfService.cs           # 书架管理（增删查/排序）
│   │   ├── BookmarkService.cs            # 书签 CRUD
│   │   ├── ReadingSessionService.cs      # 阅读会话（当前书/章/页 + 进度保存）
│   │   ├── PaginationService.cs          # 分页引擎（像素级测量）
│   │   ├── AutoReadService.cs            # 自动阅读定时器
│   │   ├── WindowBehaviorService.cs      # 窗口行为（置顶/穿透/透明度/边缘吸附）
│   │   ├── TrayIconService.cs            # 系统托盘
│   │   ├── SettingsService.cs            # 设置读写（settings.json）
│   │   ├── StartupService.cs             # 启动行为（恢复位置 or 打开书架）
│   │   └── DatabaseService.cs            # 数据库初始化 + 兼容层
│   │
│   ├── ViewModels/                       # MVVM ViewModel 层
│   │   ├── ReaderViewModel.cs            # 阅读器主窗口（热键 via EventAggregator）
│   │   ├── BookshelfViewModel.cs         # 书架主窗口
│   │   ├── SettingsViewModel.cs          # 设置窗口
│   │   ├── ChapterListViewModel.cs       # 章节目录弹窗
│   │   └── BookmarkListViewModel.cs      # 书签列表弹窗
│   │
│   ├── Views/                            # WPF 窗口（XAML + Code-behind）
│   │   ├── ReaderWindow.xaml(.cs)        # 阅读窗口（无边框/透明/拖动/缩放）
│   │   ├── BookshelfWindow.xaml(.cs)     # 书架窗口
│   │   ├── SettingsWindow.xaml(.cs)      # 设置窗口
│   │   ├── ChapterListWindow.xaml(.cs)   # 章节目录弹窗
│   │   └── BookmarkWindow.xaml(.cs)      # 书签列表弹窗
│   │
│   ├── Controls/                         # 自定义 WPF 控件
│   │   ├── HotkeyTextBox.cs              # 快捷键录入控件（录制态 + 防误触）
│   │   ├── OverlayControlBar.xaml(.cs)   # 悬浮控制栏（菜单/设置/关闭）
│   │   ├── PageTextBlock.cs              # 分页文本渲染控件
│   │   └── ResizeGrip.cs                 # 8 方向拖拽缩放手柄
│   │
│   ├── Helpers/                          # 辅助工具
│   │   ├── ChapterParser.cs              # 卷章正则解析引擎
│   │   ├── TextEncoderDetector.cs        # 编码自动检测（BOM + Ude 启发式）
│   │   ├── Win32Helper.cs                # Win32 API P/Invoke（置顶/穿透）
│   │   ├── FontHelper.cs                 # 系统字体枚举
│   │   └── JsonHelper.cs                 # JSON 序列化（settings.json）
│   │
│   ├── Converters/                       # WPF 值转换器
│   │   ├── BoolToVisibilityConverter.cs
│   │   ├── ColorToBrushConverter.cs
│   │   └── OpacityToPercentConverter.cs
│   │
│   └── Resources/
│       ├── Icons/app.ico                 # 应用图标
│       └── Styles.xaml                   # 全局样式
│
└── FloatingNovelReader.Tests/            # 单元测试（xUnit，64 用例）
    ├── Core/KeyGestureLiteTests.cs
    ├── Helpers/ChineseNumberTests.cs
    ├── Helpers/TextEncoderDetectorTests.cs
    └── Services/
        ├── BookImportServiceTests.cs
        ├── BookshelfServiceTests.cs
        ├── ChapterParserTests.cs
        └── PaginationServiceTests.cs
```

---

## 🔧 从源码构建

### 准备

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)
- Visual Studio 2022 或 VSCode + C# Dev Kit 扩展

### 命令行

```powershell
git clone https://github.com/mango12q/FloatingNovelReader.git
cd FloatingNovelReader

dotnet restore
dotnet build -c Release
dotnet test
```

### 发布

```powershell
# 一键发布（还原 + 构建 + 测试 + 发布 EXE + 打包 zip）
.\Build\build.ps1

# 跳过测试
.\Build\build.ps1 -SkipTests

# 指定版本号（覆盖 csproj 中的版本，zip 也按版本命名）
.\Build\build.ps1 -Version 0.5.0
```

产物在 `publish/` 目录下：

```
publish/
├── win-x64-portable/
│   └── floating-novel-reader-portable.exe   # 单文件 EXE（约 4 MB）
└── floating-novel-reader-portable-win-x64-*.zip  # zip 压缩包（约 2 MB）
```

### 持续集成与自动发版

- **CI**（[ci.yml](.github/workflows/ci.yml)）：每次 push / PR 到 `main`，GitHub Actions 在 Windows runner 上自动执行 构建 → 单元测试 → portable 发布冒烟
- **自动发版**（[release.yml](.github/workflows/release.yml)）：推送 `v*` 格式的 tag 即自动构建并创建 GitHub Release，附带 portable EXE 与 zip：

```powershell
git tag v0.5.0
git push origin v0.5.0
```

---

## 🗃️ 配置文件位置

| 文件 | 路径 |
|------|------|
| 库数据 | `%LocalAppData%\FloatingNovelReader\library.db` |
| 设置 | `%LocalAppData%\FloatingNovelReader\settings.json` |
| 日志 | `%LocalAppData%\FloatingNovelReader\Logs\app-YYYY-MM-DD.log` |
| 安装目录 | `%LocalAppData%\Programs\FloatingNovelReader\` |

---

## 🧪 单元测试

```powershell
dotnet test
```

当前覆盖：

| 模块 | 测试内容 |
|------|---------|
| `KeyGestureLite` | 单键/组合键解析往返（8 条） |
| `ChapterParser` | 卷章解析全场景（12 条） |
| `PaginationService` | 分页正确性 + 性能 < 200ms（5 条） |
| `BookImportService` | TXT 导入端到端 + 重复导入检测（3 条） |
| `BookshelfService` | 级联删除 CASCADE + 源文件删除（6 条） |
| `TextEncoderDetector` | BOM / UTF-16 / GBK 编码检测（4 条） |
| `ChineseNumber` | 中文数字→阿拉伯数字 0~9999（14 条） |

共 **64 条**，全部通过。

---

## 📜 版本历史

| 版本 | 说明 |
|------|------|
| **v0.4** | 字体/颜色预设选择：黑体/宋体/楷体、背景四色、字体黑白、DisplaySettings 模型重构 |
| v0.3 | 架构大重构：Repository 层 / 事件总线 / 热键桥接 / 64 单元测试 |
| v0.2 | 运行环境检测 + 自安装 + 卸载 |
| v0.1 | 初代版本：悬浮窗 / TXT 导入 / 分章 / 书签 / 快捷键 |

---

## 📜 许可证

[MIT](LICENSE)

---

## 📧 联系方式

- **作者邮箱**：mango12q@163.com
- **问题反馈**：[Issues](../../issues)
