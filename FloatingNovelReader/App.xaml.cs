using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using FloatingNovelReader.Core;
using FloatingNovelReader.Services;
using FloatingNovelReader.ViewModels;
using FloatingNovelReader.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Serilog;

namespace FloatingNovelReader;

/// <summary>
/// 应用入口。负责：
/// 1. .NET 8 Desktop Runtime 缺失检测
/// 2. DI 容器初始化
/// 3. Serilog 日志初始化
/// 4. 数据库初始化
/// 5. 根据启动设置打开对应窗口
/// 6. 系统托盘
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private static Mutex? _singleInstanceMutex;

    private TrayIconService? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 注册 GBK / GB18030 / Big5 等 CodePages 编码（.NET 8 默认不带）
        Helpers.EncodingSupport.EnsureRegistered();

        // 0. 卸载模式
        if (Array.Exists(e.Args, a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            Core.SelfInstaller.Uninstall();
            Shutdown();
            return;
        }

        // 1. .NET 8 Runtime 缺失检测
        if (!IsDotNet8RuntimeInstalled())
        {
            var result = MessageBox.Show(
                "检测到您的电脑未安装 .NET 8 Desktop Runtime，这是运行本程序必需的组件。\n\n" +
                "点击「确定」前往微软官网下载安装，安装完成后重新启动本程序。\n" +
                "点击「取消」退出程序。\n\n" +
                "如需帮助，请联系作者：mango12q@163.com",
                ".NET 8 运行时未安装",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.OK)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0/runtime",
                    UseShellExecute = true
                });
            }

            Shutdown();
            return;
        }

        // 2. 单实例检测
        _singleInstanceMutex = new Mutex(true, "FloatingNovelReader.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("程序已经在运行。", "浮窗小说阅读器", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // 2.5 首次运行自安装（非安装目录启动时弹安装对话框）
        if (Core.SelfInstaller.TrySelfInstall())
        {
            // 已启动安装后的副本，当前进程退出
            Shutdown();
            return;
        }

        // 3. 初始化日志
        InitLogging();

        // 4. 全局异常处理
        DispatcherUnhandledException += (s, args) =>
        {
            Log.Error(args.Exception, "未处理的 UI 异常");
            MessageBox.Show(
                $"发生未处理的异常：\n{args.Exception.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log.Error((Exception)args.ExceptionObject, "未处理的域异常");
        };

        base.OnStartup(e);

        // 5. 初始化 DI 容器
        Services = Bootstrapper.Build();

        // 6. 初始化数据库
        var db = Services.GetRequiredService<DatabaseService>();
        db.Initialize();

        // 7. 启动全局热键（必须在创建窗口之前，让所有窗口都能立即收到热键事件）
        var hotkey = Services.GetRequiredService<Core.HotkeyManager>();
        hotkey.SetGlobalBindings(Services.GetRequiredService<SettingsService>().Current.Hotkeys.GlobalHotkeys);
        hotkey.Start();

        // 桥接：HotkeyManager → IEventAggregator → ReaderViewModel
        // 这样热键事件不直接绑定到 View，而是通过强类型事件总线分发
        var events = Services.GetRequiredService<IEventAggregator<IEventMarker>>();
        hotkey.HotkeyPressed += (s, action) =>
            events.Publish(new ReaderViewModel.HotkeyPressedEvent(action));

        // 设置变更时重新加载热键绑定, 让用户改完快捷键立刻生效
        var settings = Services.GetRequiredService<SettingsService>();
        settings.SettingsChanged += (s, args) =>
        {
            hotkey.SetGlobalBindings(settings.Current.Hotkeys.GlobalHotkeys);
            Log.Information("热键绑定已重新加载");
        };

        // 8. 创建系统托盘
        _trayIcon = Services.GetRequiredService<TrayIconService>();
        _trayIcon.Initialize();

        // 9. 根据启动设置打开窗口
        var startupService = Services.GetRequiredService<StartupService>();
        startupService.Startup();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("应用退出");
        Log.CloseAndFlush();
        _trayIcon?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void InitLogging()
    {
        var logDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatingNovelReader", "Logs");
        System.IO.Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: System.IO.Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("应用启动，版本: {Version}", GetVersion());
    }

    public static string GetVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v?.ToString() ?? "1.0.0";
    }

    /// <summary>
    /// 检测本机是否安装了 .NET 8 Desktop Runtime。
    /// 方法 1：检查注册表
    /// 方法 2：执行 dotnet --list-runtimes
    /// </summary>
    public static bool IsDotNet8RuntimeInstalled()
    {
        try
        {
            // 方法 1：注册表
            using (var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"))
            {
                if (key != null)
                {
                    var versions = key.GetValueNames();
                    if (versions.Any(v => v.StartsWith("8.")))
                        return true;
                }
            }
            using (var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"))
            {
                if (key != null)
                {
                    var versions = key.GetValueNames();
                    if (versions.Any(v => v.StartsWith("8.")))
                        return true;
                }
            }

            // 方法 2：执行 dotnet --list-runtimes
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-runtimes",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);

            return output.Contains("Microsoft.WindowsDesktop.App 8.");
        }
        catch
        {
            return false;
        }
    }
}
