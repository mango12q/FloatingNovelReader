using System.Collections.Generic;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 书签管理。
/// </summary>
public sealed class BookmarkService
{
    private readonly DatabaseService _db;

    public BookmarkService(DatabaseService db)
    {
        _db = db;
    }

    public Bookmark Add(int bookId, int chapterId, int page, string? note = null)
    {
        var b = new Bookmark
        {
            BookId = bookId,
            ChapterId = chapterId,
            PageNumber = page,
            Note = note,
        };
        b.Id = _db.AddBookmark(b);
        Log.Information("添加书签 book={Book} chapter={Chapter} page={Page}", bookId, chapterId, page);
        EventBus.Default.Publish(Constants.EvtBookmarkAdded, b);
        return b;
    }

    public void Remove(int id)
    {
        _db.DeleteBookmark(id);
        EventBus.Default.Publish(Constants.EvtBookmarkRemoved, id);
    }

    public List<Bookmark> List(int bookId) => _db.ListBookmarks(bookId);
}
