using System;
using FloatingNovelReader.ApplicationServices;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Infrastructure;
using FloatingNovelReader.Infrastructure.Repositories;
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

        // ── 基础设施 ──────────────────────────────────────
        services.AddSingleton<HotkeyManager>();
        services.AddSingleton<EventBus>(EventBus.Default);

        // 强类型事件聚合器（兼容层：空接口标记）
        services.AddSingleton<IEventAggregator<IEventMarker>>(sp =>
            new EventAggregator<IEventMarker>());

        // 数据库连接工厂（抽象层）
        var connString = $"Data Source={Constants.DbFile}";
        services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(connString));

        // ── Repository 层（新引入）────────────────────────
        services.AddSingleton<IBookRepository, SqliteBookRepository>();
        services.AddSingleton<IChapterRepository, SqliteChapterRepository>();
        services.AddSingleton<IBookmarkRepository, SqliteBookmarkRepository>();
        services.AddSingleton<IReadingProgressRepository, SqliteReadingProgressRepository>();

        // ── 数据访问（旧 DatabaseService 保持兼容）─────────
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ReadingSessionService>();
        services.AddSingleton<BookmarkService>();
        services.AddSingleton<BookshelfService>();

        // ── 应用服务（新引入）────────────────────────────
        services.AddSingleton<IBookService, BookService>();

        services.AddSingleton<BookImportService>();
        services.AddSingleton<PaginationService>();
        services.AddSingleton<AutoReadService>();
        services.AddSingleton<WindowBehaviorService>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<StartupService>();
        services.AddSingleton<ChapterParser>();
        services.AddSingleton<TextEncoderDetector>();
        services.AddSingleton<FontHelper>();

        // ── 视图与视图模型 ─────────────────────────────────
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

/// <summary>
/// 事件标记接口。所有强类型事件实现此接口，作为 IEventAggregator 的类型参数。
/// 当前为空接口，仅作编译时约束。
/// </summary>
public interface IEventMarker { }
