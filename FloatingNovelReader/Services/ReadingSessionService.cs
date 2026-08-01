using System;
using System.Linq;
using System.Threading;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 阅读会话：跟踪当前书 / 当前章 / 当前页。
/// 翻页、章节切换、进度保存与恢复都通过本服务协调。
/// </summary>
public sealed class ReadingSessionService
{
    private readonly DatabaseService _db;
    private readonly System.Windows.Threading.DispatcherTimer _saveTimer;
    private ReadingProgress? _pending;
    private Book? _currentBook;
    private Chapter? _currentChapter;
    private int _currentPage;

    public event EventHandler? ProgressDirty;
    public event EventHandler? ChapterChanged;

    public Book? CurrentBook => _currentBook;
    public Chapter? CurrentChapter => _currentChapter;
    public int CurrentPage => _currentPage;

    public ReadingSessionService(DatabaseService db)
    {
        _db = db;
        _saveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Constants.ProgressSaveDebounceMs),
        };
        _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); FlushProgress(); };
    }

    /// <summary>打开一本书。恢复进度（含窗口位置等）。</summary>
    public ReadingProgress? Open(Book book)
    {
        // 先把上一本书未落盘的进度冲掉，避免切换后防抖定时器用新书的状态覆盖旧书进度
        Flush();
        _pending = null;

        _currentBook = book;
        _currentPage = 0;
        var progress = _db.GetProgress(book.Id);
        if (progress != null)
        {
            _currentChapter = _db.GetChapter(progress.ChapterId);
            _currentPage = progress.PageNumber;
        }
        if (_currentChapter == null)
        {
            // 默认第一章
            _currentChapter = _db.GetChapters(book.Id).FirstOrDefault();
            _currentPage = 0;
        }
        return progress;
    }

    public void SetPage(int page)
    {
        _currentPage = Math.Max(0, page);
        MarkProgressDirty();
    }

    public void SetChapter(Chapter chapter)
    {
        _currentChapter = chapter;
        _currentPage = 0;
        ChapterChanged?.Invoke(this, EventArgs.Empty);
        MarkProgressDirty();
    }

    public void MarkProgressDirty()
    {
        if (_currentBook == null || _currentChapter == null) return;
        _pending = new ReadingProgress
        {
            BookId = _currentBook.Id,
            ChapterId = _currentChapter.Id,
            PageNumber = _currentPage,
            LastUpdated = DateTime.UtcNow,
        };
        ProgressDirty?.Invoke(this, EventArgs.Empty);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>保存窗口几何（关闭窗口时调用）。不触碰章节/页码。</summary>
    public void SaveProgress(double left, double top, double width, double height, double opacity)
    {
        if (_currentBook == null) return;
        // 先落盘未保存的阅读位置，避免关闭瞬间丢进度
        Flush();
        try
        {
            _db.SaveWindowGeometry(_currentBook.Id, left, top, width, height, opacity);
            _db.TouchLastReadTime(_currentBook.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存窗口状态失败");
        }
    }

    /// <summary>把待保存的阅读位置落盘。只写章节/页码，不动窗口几何。</summary>
    private void FlushProgress()
    {
        var p = _pending;
        if (p == null) return;
        try
        {
            _db.SaveReadingPosition(p.BookId, p.ChapterId, p.PageNumber, p.LastUpdated);
            _db.TouchLastReadTime(p.BookId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存阅读进度失败");
        }
    }

    /// <summary>立即刷新进度（关闭窗口/切换书籍时调用）</summary>
    public void Flush()
    {
        _saveTimer.Stop();
        FlushProgress();
        _pending = null;
    }
}
