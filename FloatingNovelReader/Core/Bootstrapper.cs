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
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        // 基础设施
        services.AddSingleton<HotkeyManager>();
        services.AddSingleton<EventBus>(EventBus.Default);

        // 数据访问
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ReadingSessionService>();
        services.AddSingleton<BookmarkService>();
        services.AddSingleton<BookshelfService>();
        services.AddSingleton<BookImportService>();
        services.AddSingleton<PaginationService>();
        services.AddSingleton<AutoReadService>();
        services.AddSingleton<WindowBehaviorService>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<StartupService>();
        services.AddSingleton<ChapterParser>();
        services.AddSingleton<TextEncoderDetector>();
        services.AddSingleton<FontHelper>();

        // 视图与视图模型
        services.AddSingleton<ReaderViewModel>();
        services.AddSingleton<BookshelfViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<ChapterListViewModel>();
        services.AddTransient<BookmarkListViewModel>();

        services.AddSingleton<ReaderWindow>();
        services.AddSingleton<BookshelfWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<ChapterListWindow>();
        services.AddTransient<BookmarkWindow>();

        var provider = services.BuildServiceProvider();
        Log.Information("DI 容器初始化完成");
        return provider;
    }
}
