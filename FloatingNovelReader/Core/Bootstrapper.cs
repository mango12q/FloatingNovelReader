using System;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Services;
using FloatingNovelReader.ViewModels;
using FloatingNovelReader.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FloatingNovelReader.Core;

/// <summary>
/// DI 容器装配。所有服务都在此注册为单例，ViewModel/Window 按需创建。
/// </summary>
public static class Bootstrapper
{
    public static IServiceProvider Build() =>
        ConfigureServices(new ServiceCollection()).BuildServiceProvider();

    /// <summary>
    /// 所有注册集中在此。单独暴露出来是为了让测试可以直接检查装配完整性
    /// （例如"某个接口忘了注册"，那会在运行时才炸）。
    /// </summary>
    public static IServiceCollection ConfigureServices(IServiceCollection services)
    {

        // ── 基础设施 ──────────────────────────────────────
        services.AddSingleton<HotkeyManager>();

        // 窗口解析与对话框的统一接缝：全应用只有 WindowNavigator 认识具体窗口类型，
        // 其余 Service/ViewModel 依赖接口，App.Services 这个全局 Service Locator 已删除。
        services.AddSingleton<IWindowNavigator, WindowNavigator>();
        services.AddSingleton<IDialogService, DialogService>();

        // 强类型事件聚合器（空接口标记作类型约束）
        services.AddSingleton<IEventAggregator<IEventMarker>>(sp =>
            new EventAggregator<IEventMarker>());

        // ── 数据访问 ─────────────────────────────────────
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ReadingSessionService>();
        services.AddSingleton<BookmarkService>();
        services.AddSingleton<BookshelfService>();

        // ── 业务服务 ─────────────────────────────────────
        services.AddSingleton<BookImportService>();
        services.AddSingleton<PaginationService>();
        services.AddSingleton<AutoReadService>();
        services.AddSingleton<WindowBehaviorService>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<StartupService>();
        services.AddSingleton<ChapterParser>();
        services.AddSingleton<TextEncoderDetector>();
        services.AddSingleton<FontHelper>();

        // ── 朗读（edge-tts）──────────────────────────────
        services.AddSingleton<TtsEdgeClient>();
        services.AddSingleton<TtsPlayer>();
        services.AddSingleton<TtsService>();
        services.AddSingleton<TtsVoiceCatalog>();
        services.AddSingleton<TtsPanelViewModel>();

        // ── 视图与视图模型 ─────────────────────────────────
        // ReaderViewModel 的三个子 VM（体检报告 §1.2 的 God Class 拆分）
        services.AddSingleton<ReaderDisplayViewModel>();
        services.AddSingleton<ReaderPagerViewModel>();
        services.AddSingleton<ReaderDialogsViewModel>();

        services.AddSingleton<ReaderViewModel>();
        // 同一个实例也以"驱动翻页"的最小契约暴露：自动阅读 / 以后的 TTS 只依赖 IPageAdvancer
        services.AddSingleton<IPageAdvancer>(sp => sp.GetRequiredService<ReaderViewModel>());
        services.AddSingleton<BookshelfViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<ChapterListViewModel>();
        services.AddTransient<BookmarkListViewModel>();

        services.AddSingleton<ReaderWindow>();
        services.AddTransient<BookshelfWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<ChapterListWindow>();
        services.AddTransient<BookmarkWindow>();

        Log.Information("DI 容器注册完成，共 {Count} 项", services.Count);
        return services;
    }
}

/// <summary>
/// 事件标记接口。所有强类型事件实现此接口，作为 IEventAggregator 的类型参数。
/// 当前为空接口，仅作编译时约束。
/// </summary>
public interface IEventMarker { }
