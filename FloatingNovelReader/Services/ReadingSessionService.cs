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

        EventBus.Default.Publish(Constants.EvtBookChanged, book);
        EventBus.Default.Publish(Constants.EvtChapterChanged, _currentChapter);
        EventBus.Default.Publish(Constants.EvtPageChanged, _currentPage);
        return progress;
    }

    public void SetPage(int page)
    {
        _currentPage = Math.Max(0, page);
        EventBus.Default.Publish(Constants.EvtPageChanged, _currentPage);
        MarkProgressDirty();
    }

    public void SetChapter(Chapter chapter)
    {
        _currentChapter = chapter;
        _currentPage = 0;
        ChapterChanged?.Invoke(this, EventArgs.Empty);
        EventBus.Default.Publish(Constants.EvtChapterChanged, chapter);
        EventBus.Default.Publish(Constants.EvtPageChanged, _currentPage);
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

    public void SaveProgress(double left, double top, double width, double height, double opacity)
    {
        if (_currentBook == null || _currentChapter == null) return;
        try
        {
            _db.SaveProgress(new ReadingProgress
            {
                BookId = _currentBook.Id,
                ChapterId = _currentChapter.Id,
                PageNumber = _currentPage,
                LastUpdated = DateTime.UtcNow,
                WindowLeft = left,
                WindowTop = top,
                WindowWidth = width,
                WindowHeight = height,
                Opacity = opacity,
            });
            _db.TouchLastReadTime(_currentBook.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存阅读进度失败");
        }
    }

    private void FlushProgress()
    {
        if (_currentBook != null && _currentChapter != null)
        {
            var progress = _db.GetProgress(_currentBook.Id);
            double left = progress?.WindowLeft ?? 0;
            double top = progress?.WindowTop ?? 0;
            double width = progress?.WindowWidth ?? Constants.DefaultWidth;
            double height = progress?.WindowHeight ?? Constants.DefaultHeight;
            double opacity = progress?.Opacity ?? 1.0;
            SaveProgress(left, top, width, height, opacity);
        }
    }

    /// <summary>立即刷新进度（关闭窗口时调用）</summary>
    public void Flush()
    {
        _saveTimer.Stop();
        FlushProgress();
    }
}
