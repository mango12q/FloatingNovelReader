# 浮窗小说阅读器 Floating Novel Reader

<p align="center">
  <img src="FloatingNovelReader/Resources/Icons/app.ico" width="80" alt="Logo">
</p>

<p align="center">
  <b>一个悬浮在桌面上的小说阅读器 · 透明 · 可拖动 · 可穿透 · 全局快捷键</b>
</p>

<p align="center">
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

---

## 📥 下载安装

前往 [**Releases 页面**](../../releases) 下载最新版本，提供两个版本：

| 版本 | 体积 | 依赖 | 适用场景 |
|------|------|------|----------|
| **`floating-novel-reader-portable.exe`** | 小（约 5 MB） | 需安装 .NET 8 桌面运行时 | 已装 .NET 8 的开发机 / 喜欢小文件分发 |
| **`floating-novel-reader-standalone.exe`** | 大（约 150 MB） | 无需任何依赖 | **推荐**·直接双击运行，最省心 |

### 选择指南

- 🟢 **绝大多数用户** → 下载 `standalone` 版本，双击就能用
- 🟡 已经装过 .NET 8 / 想节省带宽 → 下载 `portable` 版本
- 🔴 不确定 → 直接下 `standalone`，不会出错

### 安装 .NET 8 运行时（仅 portable 需要）

如果选择 `portable` 但没装 .NET 8，程序会启动失败并提示下载。访问：
👉 https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0 → 选择「桌面运行时」 → 下载安装 → 重启程序。

---

## 🚀 快速开始

1. 下载并运行上面的 EXE
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

## 🖼️ 界面预览

> 截图待补充

---

## 🛠️ 技术栈

| 类别 | 选型 |
|------|------|
| 框架 | .NET 8 / WPF |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| 全局钩子 | MouseKeyHook |
| 数据库 | SQLite (Microsoft.Data.Sqlite) |
| 编码检测 | Ude.NetStandard |
| 日志 | Serilog（按天切割，文件输出） |
| 单元测试 | xUnit |

---

## 📁 项目结构

```
.
├── README.md
├── LICENSE
├── .gitignore
├── .editorconfig
├── .vscode/
├── Build/
│   └── build.ps1
├── FloatingNovelReader/                  # 主项目（WPF）
│   ├── App.xaml(.cs)
│   ├── app.manifest
│   ├── Core/                             # 基础设施（事件总线 / 全局热键 / 配置）
│   ├── Models/                           # 数据模型
│   ├── Services/                         # 业务服务（数据库 / 导入 / 分页 / 书签）
│   ├── ViewModels/                       # MVVM ViewModel
│   ├── Views/                            # 窗口 XAML
│   ├── Controls/                         # 自定义控件（HotkeyTextBox 等）
│   ├── Helpers/                          # 辅助工具（卷章解析 / 编码探测 / Win32 封装）
│   ├── Converters/                       # WPF 值转换器
│   ├── Properties/
│   └── Resources/
│       ├── Icons/
│       └── Styles.xaml
└── FloatingNovelReader.Tests/            # 单元测试
    ├── Core/
    ├── Helpers/
    └── Services/
```

---

## 🔧 从源码构建

### 准备

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)
- Visual Studio 2022 或 VSCode + C# Dev Kit 扩展

### 命令行

```powershell
# 克隆
git clone https://github.com/你的用户名/floating-novel-reader.git
cd floating-novel-reader

# 还原 + 编译 + 测试
dotnet restore
dotnet build -c Release
dotnet test
```

### 发布

```powershell
# 1. 小 EXE（portable，Framework-Dependent 单文件）
dotnet publish FloatingNovelReader -c Release -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish/portable

# 2. 大 EXE（standalone，Self-Contained 单文件，无需运行时）
dotnet publish FloatingNovelReader -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -o publish/standalone

# 产物在 publish/portable/ 和 publish/standalone/ 下
```

### 一键发布

```powershell
.\Build\build.ps1
```

可选参数：

| 参数 | 说明 |
|------|------|
| `-Configuration Release` | 默认 Release |
| `-Rid win-x64` | 目标 RID |
| `-SkipTests` | 跳过单元测试 |

---

## 🗃️ 配置文件位置

| 文件 | 路径 |
|------|------|
| 库数据 | `%LocalAppData%\FloatingNovelReader\library.db` |
| 设置 | `%LocalAppData%\FloatingNovelReader\settings.json` |
| 日志 | `%LocalAppData%\FloatingNovelReader\Logs\app-YYYY-MM-DD.log` |

---

## 🧪 单元测试

```powershell
dotnet test
```

覆盖范围：

- `KeyGestureLite` —— 单键 / 组合键解析与序列化
- `ChapterParser` —— 卷章解析（第 N 章 + 章节名拼接）
- `BookImportService` —— TXT 导入完整流程（含编码检测）
- `BookshelfService` —— 移除 = 完整级联删除

---

## 📜 许可证

[MIT](LICENSE)

---

## 🙏 致谢

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [MouseKeyHook](https://github.com/gmamaladze/globalmousekeyhook)
- [Ude.NetStandard](https://github.com/errepi/ude)
- [Serilog](https://serilog.net/)
