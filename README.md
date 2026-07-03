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
│   └── build.ps1                          # 一键构建 + 发布 portable EXE + zip
├── FloatingNovelReader/                   # 主项目（WPF）
│   ├── App.xaml(.cs)                       # 入口：运行时检测 + 自安装 + DI
│   ├── app.manifest
│   ├── Core/                               # 基础设施
│   │   ├── SelfInstaller.cs                # 首次运行自安装 + 卸载
│   │   ├── EventBus.cs
│   │   ├── HotkeyManager.cs                # 全局热键 + 录制态隔离
│   │   ├── KeyGestureLite.cs               # 单键/组合键描述
│   │   └── SettingsService.cs
│   ├── Models/                             # 数据模型
│   ├── Services/                           # 业务服务
│   ├── ViewModels/                         # MVVM ViewModel
│   ├── Views/                              # 窗口 XAML
│   ├── Controls/                           # 自定义控件（HotkeyTextBox）
│   ├── Helpers/                            # 辅助工具（卷章解析/编码探测/Win32）
│   ├── Converters/
│   ├── Properties/
│   └── Resources/
│       ├── Icons/
│       └── Styles.xaml
└── FloatingNovelReader.Tests/              # 单元测试（64 用例）
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
git clone https://github.com/你的用户名/floating-novel-reader.git
cd floating-novel-reader

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
├── win-x64-portable/
│   └── floating-novel-reader-portable.exe       # 单文件 EXE（约 4 MB）
└── floating-novel-reader-portable-win-x64-*.zip # zip 压缩包（约 2 MB）
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

覆盖范围：

- `KeyGestureLite` —— 单键 / 组合键解析与序列化
- `ChapterParser` —— 卷章解析（第 N 章 + 章节名拼接）
- `BookImportService` —— TXT 导入完整流程（含编码检测）
- `BookshelfService` —— 移除 = 完整级联删除

---

## 📜 许可证

[MIT](LICENSE)

---

## 📧 联系方式

- **作者邮箱**：mango12q@163.com
- **问题反馈**：[Issues](../../issues)
