using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;
using Serilog;

namespace FloatingNovelReader.Core;

/// <summary>
/// 首次运行自安装。
/// 行为：
///   1. 检测是否从安装目录运行（%LocalAppData%\Programs\FloatingNovelReader\）
///   2. 如果不是 → 弹安装确认对话框
///   3. 确认 → 复制 EXE 到安装目录 + 桌面快捷方式 + 开始菜单 + 卸载入口
///   4. 启动安装后的副本，退出当前进程
///   5. 取消 → 以 portable 模式继续运行
/// </summary>
public static class SelfInstaller
{
    private const string AppFolderName = "FloatingNovelReader";
    private const string AppExeName = "FloatingNovelReader.exe";
    private const string DisplayName = "浮窗小说阅读器";
    private const string Publisher = "mango12q";
    private const string ContactEmail = "mango12q@163.com";
    private const string DownloadUrl = "https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0/runtime";

    private static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", AppFolderName);

    private static string InstallPath => Path.Combine(InstallDir, AppExeName);

    /// <summary>
    /// 是否已安装在正式目录（而非从下载目录直接运行）。
    /// </summary>
    public static bool IsRunningFromInstallDir()
    {
        var currentPath = Environment.ProcessPath ?? "";
        return string.Equals(currentPath, InstallPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 尝试自安装。返回 true 表示已启动安装副本并应退出当前进程。
    /// </summary>
    public static bool TrySelfInstall()
    {
        // 已在安装目录 → 不需要安装
        if (IsRunningFromInstallDir())
            return false;

        // 命令行 --portable → 跳过安装
        var args = Environment.GetCommandLineArgs();
        if (Array.Exists(args, a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase)))
            return false;

        // 弹确认对话框
        var result = MessageBox.Show(
            $"{DisplayName} 尚未安装，是否现在安装？\n\n" +
            "• 安装到用户目录（无需管理员权限）\n" +
            "• 创建桌面快捷方式\n" +
            "• 创建开始菜单快捷方式\n" +
            "• 可从「设置 → 应用」卸载\n\n" +
            "点击「否」将以便携模式直接运行（不安装）。",
            "安装 " + DisplayName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return false;

        try
        {
            DoInstall();
            Log.Information("自安装完成，启动安装后的副本");

            // 启动安装后的副本
            Process.Start(new ProcessStartInfo
            {
                FileName = InstallPath,
                UseShellExecute = true
            });
            return true; // 调用方应退出
        }
        catch (Exception ex)
        {
            Log.Error(ex, "自安装失败，以便携模式继续");
            MessageBox.Show(
                $"安装失败：{ex.Message}\n\n将以便携模式继续运行。",
                "安装失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    private static void DoInstall()
    {
        // 1. 复制 EXE
        var currentPath = Environment.ProcessPath ?? "";
        Directory.CreateDirectory(InstallDir);
        File.Copy(currentPath, InstallPath, overwrite: true);

        // 2. 桌面快捷方式
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var desktopShortcut = Path.Combine(desktop, DisplayName + ".lnk");
        CreateShortcut(desktopShortcut, InstallPath, DisplayName);

        // 3. 开始菜单快捷方式
        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var startMenuFolder = Path.Combine(startMenu, DisplayName);
        Directory.CreateDirectory(startMenuFolder);
        var startMenuShortcut = Path.Combine(startMenuFolder, DisplayName + ".lnk");
        CreateShortcut(startMenuShortcut, InstallPath, DisplayName);

        // 4. 注册卸载入口
        RegisterUninstallEntry();
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string description)
    {
        // 通过 COM 调用 WScript.Shell 创建 .lnk 快捷方式
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell 不可用");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
        shortcut.Description = description;
        shortcut.IconLocation = targetPath;
        shortcut.Save();
    }

    private static void RegisterUninstallEntry()
    {
        var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + AppFolderName;
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        if (key == null) return;

        var version = App.GetVersion();
        key.SetValue("DisplayName", DisplayName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", Publisher);
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        key.SetValue("DisplayIcon", InstallPath);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        // 卸载命令：运行自身带 --uninstall 参数
        key.SetValue("UninstallString", $"\"{InstallPath}\" --uninstall");
    }

    /// <summary>
    /// 卸载：删除文件、快捷方式、注册表项。
    /// </summary>
    public static void Uninstall()
    {
        // 1. 删除桌面快捷方式
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var desktopShortcut = Path.Combine(desktop, DisplayName + ".lnk");
        SafeDelete(desktopShortcut);

        // 2. 删除开始菜单文件夹
        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var startMenuFolder = Path.Combine(startMenu, DisplayName);
        if (Directory.Exists(startMenuFolder))
            Directory.Delete(startMenuFolder, recursive: true);

        // 3. 删除注册表卸载入口
        var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + AppFolderName;
        Registry.CurrentUser.DeleteSubKey(keyPath, throwOnMissingSubKey: false);

        // 4. 删除安装目录（延迟，让当前进程先退出）
        var batPath = Path.Combine(Path.GetTempPath(), "fnr_cleanup.bat");
        File.WriteAllText(batPath,
            "@echo off\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            $"rmdir /s /q \"{InstallDir}\"\r\n" +
            "del \"%~f0\"\r\n");
        Process.Start(new ProcessStartInfo
        {
            FileName = batPath,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        });
    }

    private static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* 忽略 */ }
    }
}
