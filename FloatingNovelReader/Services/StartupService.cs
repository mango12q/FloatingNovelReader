using System;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 启动服务：根据用户设置决定打开阅读窗口还是书架。
/// 只依赖 <see cref="IWindowNavigator"/>，不再认识 ReaderViewModel / ReaderWindow / BookshelfWindow
/// （体检报告 §1.3 的 Service → View 反向依赖）。
/// </summary>
public sealed class StartupService
{
    private readonly SettingsService _settings;
    private readonly DatabaseService _db;
    private readonly BookshelfService _bookshelf;
    private readonly IWindowNavigator _navigator;

    public StartupService(
        SettingsService settings,
        DatabaseService db,
        BookshelfService bookshelf,
        IWindowNavigator navigator)
    {
        _settings = settings;
        _db = db;
        _bookshelf = bookshelf;
        _navigator = navigator;
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
                    _navigator.OpenReader(book, progress);
                    return;
                }
            }
        }
        // 兜底：显示书架
        _navigator.ShowBookshelf();
    }
}
