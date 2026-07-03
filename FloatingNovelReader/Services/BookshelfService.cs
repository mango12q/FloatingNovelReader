using System.Collections.Generic;
using System.IO;
using System.Linq;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 书架管理（增删查、排序、搜索）。
/// </summary>
public sealed class BookshelfService
{
    private readonly DatabaseService _db;
    private List<Book> _cache = new();

    public BookshelfService(DatabaseService db)
    {
        _db = db;
    }

    public IReadOnlyList<Book> Books => _cache;

    public void Reload(string? search = null, string orderBy = "LastReadTime")
    {
        _cache = _db.ListBooks(search, orderBy);
        EventBus.Default.Publish(Constants.EvtBookChanged, _cache);
    }

    public Book? GetBook(int id) => _cache.FirstOrDefault(b => b.Id == id);

    public Book? GetBookWithChapters(int id)
    {
        var book = _db.GetBook(id);
        if (book == null) return null;

        var volumes = _db.GetVolumes(id);
        foreach (var v in volumes)
        {
            v.Chapters = _db.GetChapters(id).Where(c => c.VolumeId == v.Id).ToList();
        }
        book.Volumes = volumes;
        return book;
    }

    /// <summary>
    /// 从书架彻底移除一本书。
    /// 行为:
    ///   1) 关闭可能正在打开该书的 ReaderWindow
    ///   2) 数据库侧: 删除 Books 记录; Volumes/Chapters/ReadingProgress/Bookmarks 通过
    ///      ON DELETE CASCADE (依赖 PRAGMA foreign_keys=ON) 自动级联清理
    ///   3) 若 deleteSourceFile=true 且源 .txt 存在, 一并删除源文件
    /// 调用方负责弹确认框; 此方法只做实际删除。
    /// </summary>
    /// <param name="bookId">要删除的书 ID</param>
    /// <param name="deleteSourceFile">是否同时删除源 .txt 文件</param>
    /// <returns>实际删除的源文件路径 (用于日志); null 表示没删</returns>
    public string? Remove(int bookId, bool deleteSourceFile)
    {
        // 先拿到 Book 信息, 之后 _cache 会被清掉就拿不到了
        var book = _cache.FirstOrDefault(b => b.Id == bookId) ?? _db.GetBook(bookId);
        if (book == null)
        {
            Log.Warning("Remove: 找不到 BookId={Id}", bookId);
            return null;
        }

        // 如果 ReaderWindow 正在打开此书, 关闭它
        TryCloseReaderForBook(bookId);

        // 先删源文件: 这样如果文件被占用也能早期发现 (DatabaseService 删库后会 release 文件句柄)
        string? deletedFile = null;
        if (deleteSourceFile && !string.IsNullOrEmpty(book.FilePath))
        {
            try
            {
                if (File.Exists(book.FilePath))
                {
                    File.Delete(book.FilePath);
                    deletedFile = book.FilePath;
                    Log.Information("已删除源文件 {Path}", deletedFile);
                }
                else
                {
                    Log.Warning("源文件不存在, 跳过: {Path}", book.FilePath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "删除源文件失败 {Path}", book.FilePath);
                throw; // 让上层 MessageBox 显示, 不静默吞掉
            }
        }

        // 删库: Book 记录 + CASCADE 清空 Volumes/Chapters/ReadingProgress/Bookmarks
        _db.DeleteBook(bookId);
        _cache.RemoveAll(b => b.Id == bookId);
        Log.Information("从书架移除 {Id} {Title}, deleteSourceFile={Del}",
            bookId, book.Title, deleteSourceFile);
        EventBus.Default.Publish(Constants.EvtBookRemoved, bookId);
        return deletedFile;
    }

    /// <summary>兼容旧调用: 默认不删源文件</summary>
    public void Remove(int bookId) => Remove(bookId, deleteSourceFile: false);

    /// <summary>如果 ReaderWindow 正在显示该书, 关闭它避免文件被删后 FileNotFoundException</summary>
    private void TryCloseReaderForBook(int bookId)
    {
        try
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;
            foreach (var w in app.Windows)
            {
                if (w is Views.ReaderWindow rw && rw.IsLoaded)
                {
                    // 通过 DataContext 拿到当前书 ID
                    var dc = rw.DataContext;
                    if (dc is ViewModels.ReaderViewModel rvm && rvm.CurrentBook?.Id == bookId)
                    {
                        rw.Close();
                        Log.Information("已关闭正在显示 BookId={Id} 的阅读窗口", bookId);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TryCloseReaderForBook 异常 (忽略)");
        }
    }
}
