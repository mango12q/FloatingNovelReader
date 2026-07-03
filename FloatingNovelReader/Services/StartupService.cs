using System;
using FloatingNovelReader;
using FloatingNovelReader.Models;
using FloatingNovelReader.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 启动服务：根据用户设置决定打开阅读窗口还是书架。
/// </summary>
public sealed class StartupService
{
    private readonly SettingsService _settings;
    private readonly DatabaseService _db;
    private readonly BookshelfService _bookshelf;
    private readonly ReadingSessionService _session;

    public StartupService(
        SettingsService settings,
        DatabaseService db,
        BookshelfService bookshelf,
        ReadingSessionService session)
    {
        _settings = settings;
        _db = db;
        _bookshelf = bookshelf;
        _session = session;
    }

    public void Startup()
    {
        var setting = _settings.Current.StartupBehavior;
        if (setting == StartupBehavior.LastReadingPosition)
        {
            var progress = _db.GetMostRecentProgress();
            if (progress != null)
            {
                var book = _bookshelf.GetBookWithChapters(progress.BookId);
                if (book != null)
                {
                    Log.Information("恢复上次阅读: {Book}", book.Title);
                    var readerVm = App.Services.GetRequiredService<ViewModels.ReaderViewModel>();
                    readerVm.LoadBook(book, progress);
                    var w = App.Services.GetRequiredService<ReaderWindow>();
                    w.Show();
                    return;
                }
            }
        }
        // 兜底：显示书架
        var shelf = App.Services.GetRequiredService<BookshelfWindow>();
        shelf.Show();
    }
}
