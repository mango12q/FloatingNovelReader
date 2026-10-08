# 浮窗小说阅读器 Floating Novel Reader

<p align="center">
  <img src="FloatingNovelReader/Resources/Icons/app.ico" width="80" alt="Logo">
</p>

<p align="center">
  <b>一个悬浮在桌面上的小说阅读器 · 透明 · 可拖动 · 可穿透 · 全局快捷键</b>
</p>

<p align="center">
  <a href="#-下载安装"><img src="https://img.shields.io/badge/下载-Windows-0078d4?style=for-the-badge&logo=windows&logoColor=white" alt="Download"></a>
  <a href="#-快速开始"><img src="https://img.shields.io/badge/平台-Windows%2010%2F11-blue?style=for-the-badge&logo=windows" alt="Platform"></a>
  <a href="#-快速开始"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/许可证-MIT-green?style=for-the-badge" alt="License"></a>
</p>

---

> 看小说的同时不耽误工作——半透明悬浮窗，鼠标可穿透；支持 TXT / EPUB / PDF 自动分章、书签、目录、全局快捷键翻页。

## 📑 目录

- [✨ 功能特性](#-功能特性)
- [📥 下载安装](#-下载安装)
- [🚀 快速开始](#-快速开始)
- [🔧 开发者文档](#-开发者文档)
- [📜 版本历史](#-版本历史)
- [📧 联系方式](#-联系方式)

---

## ✨ 功能特性

### 📖 阅读体验
- **悬浮窗**：无边框 / 始终置顶 / 半透明
- **鼠标穿透**：按 `F3` 切换——悬浮窗对鼠标"隐身"，点击直接落到下层窗口
- **拖动 & 调整大小**：四边/四角拉伸，任意位置按住即可移动
- **自动阅读**：可调速、加速/减速快捷键

### 🎨 字体与颜色
- **字体族**：全部系统字体可选；微软雅黑 / 宋体 / 新宋体 / 黑体 / 楷体 / 仿宋 / 等线 / 行楷中文名置顶，未安装的标注置灰
- **背景颜色**：白色 / 灰色 / 黑色 / 纸页黄 / 透明五种预设
- **字体颜色**：黑色 / 白色两种预设，与背景自动适配

### 📚 内容 & 导航
- **TXT 导入**：自动检测编码（GBK / UTF-8 / UTF-16 / Big5 …）
- **EPUB 导入**：按 spine 顺序解析，章节标题取正文标题（h1~h6）或 EPUB 目录（nav / ncx）条目名；「整本书塞进一个 XHTML」也能按标题切开
- **PDF 导入**：抽取文字层并按「第 N 章 / Chapter N / 卷一」等标题行分章；没有标题行时按页分块（≤200 页时**一页一节**，目录本身就能当选页表用），排版换行自动接回段落
- **自动分章**：识别「第 N 章 / 第一卷 / Chapter 1 / 1、」等多种写法
- **章节目录**：按 `F9` 打开，按卷/章树形展示
- **跳转到页码**：≡ 菜单 →「跳转到页码…」，填本章页码直接跳（分章粒度粗的 PDF 尤其省事）
- **书签**：按 `F10` 或 ≡ 菜单「添加书签」添加 / `F12` 打开书签列表 / 列表点击跳转
- **进度记忆**：阅读位置自动保存，下次打开自动恢复
- **高分屏适配**：书架 / 阅读 / 目录 / 书签 / 设置 / 跳转页码窗口都按所在显示器的 DPI 与工作区自适应尺寸与位置，跨不同缩放的显示器也不会跑到屏幕外

> **电子书格式的边界**（导入前先看一眼，能省一次失败）：
> - **扫描版 PDF 不支持**：只抽文字层，不做 OCR。整本都是图片的 PDF 会明确提示「没有可提取的文字」，请先用 OCR 工具转成带文字层的 PDF 或 TXT。
> - **受 DRM 保护的 EPUB / 带打开密码的 PDF 不支持**：会提示文件打不开/已加密。
> - **PDF 的分章依赖标题行**：识别不到「第 N 章 / Chapter N / 卷一」这类标题时按页分块——200 页以内一页一节（目录里是「第 7 页」），超过 200 页则合并到 200 节以内；多栏排版、公式表格的阅读顺序可能不完美。
> - **EPUB 的章节标题**取自正文标题或书内目录，二者都没有时按顺序编号。

### 🔊 朗读（TTS）
- **从当前开始朗读**：从当前章一直播到全书末，一章播完自动续下一章，最后一章播完停在全书末
- **朗读 N 分钟**：到设定分钟数自动停，**停在段末**；状态栏实时显示「剩余 12:34」。默认 30 分钟，可在「设置 → 朗读」改
- **朗读 N 章**：播完 N 章自动停，**停在章末**。默认 10 章，可在「设置 → 朗读」改
- **阅读位置跟随音频**：朗读时会自动翻页并跟着跨章跳转（可在设置里关）
- **声音 / 语速 / 音量**：用 edge 在线合成，声音列表可在设置里刷新；提供「试听」
- **与自动阅读互斥**：开始朗读会先停掉自动阅读，结束后按原状态恢复

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
- **记住便携选择**：安装提示点「否」后写入 settings.json，之后启动不再询问
- **便携模式**：便携版 zip 自带 `portable.mode` 标记直接免提示；也可加 `--portable` 参数启动
- **卸载**：从「设置 → 应用」卸载，或运行 `FloatingNovelReader.exe --uninstall`
- **运行时检测**：启动时自动检测 .NET 8 桌面运行时，未安装则引导下载

---

## 📥 下载安装

前往 [**Releases**](../../releases) 下载最新版本。

### 下载内容

每个 Release 提供以下文件：

| 文件 | 说明 |
|------|------|
| `floating-novel-reader-singlefile-win-x64.exe` | **单文件版**：单个 EXE（约 10 MB，含 EPUB/PDF 解析库），双击即用，首次运行询问是否安装 |
| `floating-novel-reader-portable-win-x64.zip` | **便携版**：解压即用（EXE + DLL 目录，压缩包约 4 MB），内含 `portable.mode` 标记，不弹安装提示 |

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

1. 下载 `floating-novel-reader-singlefile-win-x64.exe`（或解压便携版 zip）
2. 双击运行
3. 首次运行会弹出安装确认对话框（便携版 zip 不会弹）：

   - **点击「是」** → 自动安装到 `%LocalAppData%\Programs\FloatingNovelReader\`，创建桌面和开始菜单快捷方式，注册到系统卸载列表
   - **点击「否」** → 以便携模式直接运行，并**记住选择**，之后启动不再询问

4. 安装后，从桌面快捷方式启动即可

### 卸载

任选一种方式：

- **方式 1**：「设置 → 应用 → 已安装的应用」找到「浮窗小说阅读器」→ 卸载
- **方式 2**：命令行运行 `FloatingNovelReader.exe --uninstall`

卸载会自动删除程序文件、桌面快捷方式、开始菜单文件夹和注册表项。

---

## 🚀 快速开始

1. 下载并运行 EXE（安装选项见 [安装步骤](#安装步骤)）
2. 主窗口「书架」→ 点击「导入」按钮 → 选一个电子书文件（TXT / EPUB / PDF，也支持直接拖进窗口）
3. 等待进度条走完（TXT 1MB 以内瞬间完成；EPUB/PDF 会先解析成正文，几十 MB 的电子书通常 1~2 秒）
4. 双击书籍卡片 → 阅读窗口弹出
5. 按 `F3` 切换鼠标穿透 → 看小说不挡工作

> EPUB / PDF 导入时会生成一份纯文本正文缓存（见 [配置文件位置](#️-配置文件位置)），阅读时读的是它，因此**源文件被移动/删除也不影响已导入的书**（重新导入才会重新解析）。

鼠标操作：按住左键拖动 = 移动窗口，右键点击 = 上一页，滚轮 = 上/下翻页。

快捷键：默认 `Space` 下一页、`F3` 穿透、`F8` 隐藏窗口；完整键位与自定义改键见应用内 **设置 → 快捷键**（功能说明见 [全局快捷键](#-全局快捷键)）。

---

## 🔧 开发者文档

<details>
<summary>点击展开：架构设计 / 技术栈 / 项目结构 / 从源码构建 / 数据文件位置 / 单元测试</summary>

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
│  通过 IEventAggregator 接收热键事件，持有 Services 引用      │
└───────────────────────────┬─────────────────────────────┘
                            │ 调用
┌───────────────────────────▼─────────────────────────────┐
│  Services（BookshelfService / BookImportService / …）      │
│  业务编排层：协调 DatabaseService 完成业务场景               │
└───────────────────────────┬─────────────────────────────┘
                            │ 依赖
┌───────────────────────────▼─────────────────────────────┐
│  DatabaseService（唯一数据访问层）                          │
│  SQLite 5 表 CRUD，参数化 SQL，busy timeout，外键级联        │
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
| 数据库 | SQLite（Microsoft.Data.Sqlite 8.0.10） | 5 表，外键级联，`PRAGMA foreign_keys=ON`，busy timeout 2s |
| 事件总线 | `IEventAggregator<T>`（自研） | 强类型事件，编译期检查 |
| 编码检测 | Ude.NetStandard 1.2.0 | BOM + 启发式检测（GBK/UTF-8/UTF-16/Big5…），坏字节容错替换 |
| EPUB 解析 | System.IO.Compression + XDocument（内置） | 不引第三方库：container.xml → OPF → spine，XHTML 用容错正则转纯文本 |
| PDF 解析 | PdfPig 0.1.16（Apache-2.0） | 只抽文字层（不做 OCR），按标题行分章、排版换行重排 |
| 日志 | Serilog 4.0.0 | 按日滚动，保留 30 天，`%LocalAppData%\FloatingNovelReader\Logs\` |
| 单元测试 | xUnit 2.9.2 | 373 个测试用例 |

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
│   │   ├── Constants.cs                  # 全局常量（路径/尺寸…）
│   │   ├── IEventAggregator.cs           # 强类型事件聚合器接口
│   │   ├── EventAggregator.cs            # 强类型事件聚合器实现
│   │   ├── HotkeyManager.cs              # 全局热键 + 防抖 + 录制屏蔽（含 KeyGestureLite）
│   │   └── SelfInstaller.cs              # 首次运行自安装 / 卸载
│   │
│   ├── Models/                           # 数据模型
│   │   ├── AppSettings.cs                # 全局应用设置
│   │   ├── AppState.cs                   # 进程内运行时状态
│   │   ├── Book.cs / Volume.cs / Chapter.cs
│   │   ├── BookFormat.cs                 # 格式识别（TXT/EPUB/PDF + 魔数兜底）
│   │   ├── ExtractedContent.cs           # 电子书解析出的「章节段」中间结构
│   │   ├── Bookmark.cs / ReadingProgress.cs
│   │       ├── DisplaySettings.cs            # 字体/字体色/背景/透明度
│   │   └── HotkeyConfig.cs               # 快捷键绑定配置
│   │
│   ├── Services/                         # 业务服务
│   │   ├── BookImportService.cs          # 导入全流程（TXT 编码解析 / EPUB / PDF 分流）
│   │   ├── BookshelfService.cs           # 书架管理（增删查/排序）
│   │   ├── BookmarkService.cs            # 书签 CRUD
│   │   ├── ReadingSessionService.cs      # 阅读会话（当前书/章/页 + 进度保存）
│   │   ├── PaginationService.cs          # 分页引擎（像素级测量）
│   │   ├── AutoReadService.cs            # 自动阅读定时器
│   │   ├── WindowBehaviorService.cs      # 窗口行为（置顶/穿透/透明度/边缘吸附）
│   │   ├── TrayIconService.cs            # 系统托盘
│   │   ├── SettingsService.cs            # 设置读写（settings.json）
│   │   ├── StartupService.cs             # 启动行为（恢复位置 or 打开书架）
│   │   └── DatabaseService.cs            # 数据库初始化 + 全部 CRUD（单一数据访问层）
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
│   │   ├── BookmarkWindow.xaml(.cs)      # 书签列表弹窗
│   │   └── PageJumpWindow.xaml(.cs)      # 跳转到页码弹窗
│   │
│   ├── Controls/                         # 自定义 WPF 控件
│   │   ├── HotkeyTextBox.cs              # 快捷键录入控件（录制态 + 防误触）
│   │   └── OverlayControlBar.xaml(.cs)   # 悬浮控制栏（菜单/设置/关闭）
│   │
│   ├── Helpers/                          # 辅助工具
│   │   ├── ChapterParser.cs              # 卷章正则解析引擎（TXT 与 PDF 共用）
│   │   ├── EpubTextExtractor.cs          # EPUB → 章节段（container/OPF/spine + 目录标题）
│   │   ├── PdfTextExtractor.cs           # PDF → 章节段（文字层抽取 + 标题分章 + 换行重排）
│   │   ├── HtmlTextConverter.cs          # XHTML → 纯文本（含标题下标）
│   │   ├── ExtractedTextCache.cs         # 正文缓存落盘 + 章节字节偏移
│   │   ├── ChapterContentReader.cs       # 按偏移回读章节（TXT 源文件或正文缓存）
│   │   ├── TextEncoderDetector.cs        # 编码自动检测（BOM + Ude 启发式）
│   │   ├── Win32Helper.cs                # Win32 API P/Invoke（置顶/穿透/显示器工作区/DPI）
│   │   ├── DpiHelper.cs                  # 窗口高分屏适配（尺寸/位置/DpiChanged）
│   │   ├── DialogSizing.cs               # 窗口尺寸位置的纯函数计算（可单测）
│   │   ├── PageJumpInput.cs              # 「跳转到页码」输入校验（纯函数）
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
└── FloatingNovelReader.Tests/            # 单元测试（xUnit，373 用例）
    ├── Core/KeyGestureLiteTests.cs
    ├── Fixtures/EbookFixtures.cs         # 现场生成 EPUB / PDF 样本（不往仓库塞二进制）
    ├── Helpers/ChineseNumberTests.cs
    ├── Helpers/TextEncoderDetectorTests.cs
    ├── Helpers/HtmlTextConverterTests.cs
    ├── Helpers/EpubTextExtractorTests.cs
    ├── Helpers/PdfTextExtractorTests.cs
    ├── Helpers/ExtractedTextCacheTests.cs
    ├── Helpers/DialogSizingTests.cs / DpiHelperTests.cs
    └── Services/
        ├── BookImportServiceTests.cs
        ├── BookshelfServiceTests.cs
        ├── ChapterContentRoundTripTests.cs
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
```

产物在 `publish/` 目录下：

```
publish/
├── win-x64-singlefile/
│   └── floating-novel-reader-singlefile-win-x64.exe   # 单文件版（约 10 MB）
├── win-x64-portable/                                  # 便携版目录（含 portable.mode）
└── floating-novel-reader-portable-win-x64.zip         # 便携版压缩包（约 4 MB）
```

---

## 🗃️ 配置文件位置

| 文件 | 路径 |
|------|------|
| 库数据 | `%LocalAppData%\FloatingNovelReader\library.db` |
| 设置 | `%LocalAppData%\FloatingNovelReader\settings.json` |
| EPUB/PDF 正文缓存 | `%LocalAppData%\FloatingNovelReader\ImportCache\<sha256>.txt` |
| 朗读音频缓存 | `%LocalAppData%\FloatingNovelReader\TtsCache\` |
| 日志 | `%LocalAppData%\FloatingNovelReader\Logs\app-YYYY-MM-DD.log` |
| 安装目录 | `%LocalAppData%\Programs\FloatingNovelReader\` |

> ⚠️ **手改 `settings.json` 时注意枚举要写数字**。`JsonHelper.Options` 没有挂
> `JsonStringEnumConverter`，所以 `StartupBehavior` / `HotkeyMode` 必须是 `0` / `1` 这类**数值**。
> 写成 `"LastReadingPosition"` 会让**整份设置**解析失败：日志里出现
> 「读取设置失败，已回退默认设置」，原文件被备份成 `settings.json.corrupt-*.json`，
> 所有个性化设置（含快捷键绑定）静默回到默认值。快捷键是**按名字**存的（`"SpeakFromHere": "F14"`），
> 只有这两个枚举例外。

---

## 🧪 单元测试

```powershell
dotnet test
```

当前覆盖：

| 模块 | 测试内容 |
|------|---------|
| `KeyGestureLite` | 单键/组合键解析往返（19 条） |
| `ChapterParser` | 卷章解析全场景（23 条） |
| `PaginationService` | 分页正确性 + 性能 < 200ms + 字符级完整性（6 条） |
| `BookImportService` | TXT / EPUB / PDF 导入端到端、格式识别（魔数兜底）、扫描版 PDF 报错（9 条） |
| `BookshelfService` | 级联删除 CASCADE + 源文件删除 + 正文缓存清理（6 条） |
| `TextEncoderDetector` | BOM / UTF-16 / GBK 编码检测（6 条） |
| `ChineseNumber` | 中文数字→阿拉伯数字（12 条） |
| `ChapterContentRoundTrip` | 章节字节偏移往返（UTF-8/UTF-16/GBK/CRLF，7 条） |
| `FontHelper` | 系统字体枚举：常用中文名置顶 + 未安装标注（5 条） |
| `TtsSegmenter` | 朗读文本清洗 / XML 转义 / 按 600 字节切片（12 条） |
| `TtsProtocol` | edge-tts 的 GEC 哈希、URL、SSML 与二进制帧解析（22 条） |
| `TtsStopCondition` | 三种停止条件（书末 / N 分钟 / N 章）与剩余时间格式化（27 条） |
| `TtsPlaylist` | 跨章播放列表状态机：段末 / 章末 / 书末停点、分钟秒表、中途翻页（22 条） |
| `TtsSegmentDuration` | mp3 音频时长测量（N 分钟预算的基准，7 条） |
| `SegmentPageMapper` | 「段在章里的比例」→ 页码的边界（8 条） |
| `TtsPanelViewModel` | 朗读设置页：语速/音量钳制、默认 N 分钟 / N 章（14 条） |
| `ChapterSequence` | 章节序列前后查找 / 阅读百分比（10 条） |
| `ReaderPagerViewModel` | 分页子 VM 的翻页与重算（8 条） |
| `ReaderDisplayViewModel` | 显示设置映射与属性变更（8 条） |
| `HotkeyConfig` | 快捷键按名字存取 + 新增动作回填 + 中文名守卫（7 条） |
| `JsonHelper` / `SettingsService` | 设置读写：缺字段、损坏文件、原子写、导入白名单（9 条） |
| `ChapterContentReader` | 章节字节偏移读取的边界（5 条） |
| `HtmlTextConverter` | XHTML → 纯文本：实体解码 / 段落切分 / 标题下标 / 残缺标签（9 条） |
| `EpubTextExtractor` | EPUB 解析：元数据 / spine 顺序 / nav+ncx 标题 / 单文件多章切分 / href 解析（13 条） |
| `PdfTextExtractor` | PDF 解析：标题分章 / 无标题按页分块（含 200 节上限）/ 换行重排 / 扫描版报错（15 条） |
| `ExtractedTextCache` | 正文缓存落盘：字节偏移往返、缓存路径稳定性、越界删除防护（7 条） |
| `DialogSizing` / `DpiHelper` | 高分屏窗口：尺寸夹取 / 跨屏居中 / 位置回夹 / 无 owner 主窗口 / STA 下的 WPF 写回（9 条） |
| `PageJumpInput` | 跳转页码输入校验：范围 / 非数字 / 空章节（14 条） |
| `ReadingSessionService` | 阅读进度防抖与刷盘（5 条） |
| `Bootstrapper` / `WindowNavigator` | DI 注册完整性、`IPageAdvancer` 单例一致性（4 条） |
| `BindingPathGuard` | XAML 每个绑定路径都能在对应 VM 上解析（含 `TtsSettingsTab`，6 条） |
| `BuildScriptEncoding` | `build.ps1` 保留 UTF-8 BOM + 产物名与 README 一致（4 条） |

共 **373 条**，全部通过。

</details>

---

## 📜 版本历史

当前版本 **v0.11**：TXT / EPUB / PDF 三种来源导入（电子书解析成正文缓存，复用同一套章节偏移阅读链路）+ 跳转到页码 + 书架/阅读/目录/书签/设置窗口的高分屏 DPI 适配。

完整更新记录见 [Releases](../../releases)。

---

## 📜 许可证

[MIT](LICENSE)

---

## 📧 联系方式

- **作者邮箱**：mango12q@163.com
- **问题反馈**：[Issues](../../issues)
