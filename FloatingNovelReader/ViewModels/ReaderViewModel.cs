using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingNovelReader;
using FloatingNovelReader.Core;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using FloatingNovelReader.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Serilog;

namespace FloatingNovelReader.ViewModels;

/// <summary>
/// 阅读器主窗口 ViewModel。
/// 职责：
///   - 当前书 / 当前章 / 当前页
///   - 翻页（下一页 / 上一页 / 上一章 / 下一章 / 跳转）
///   - 加载章节文本并分页
///   - 自动阅读联动
///   - 状态显示（页码、章节名、阅读百分比）
///   - 通过 IEventAggregator 接收热键事件（替代直接持有 HotkeyManager）
/// </summary>
public sealed partial class ReaderViewModel : ObservableObject
{
    private readonly BookshelfService _bookshelf;
    private readonly ReadingSessionService _session;
    private readonly PaginationService _paginator;
    private readonly AutoReadService _autoRead;
    private readonly WindowBehaviorService _windowBehavior;
    private readonly BookmarkService _bookmark;
    private readonly SettingsService _settings;
    private readonly IEventAggregator<IEventMarker> _events;

    [ObservableProperty] private Book? _currentBook;
    [ObservableProperty] private Chapter? _currentChapter;
    [ObservableProperty] private int _currentPage;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private string _pageText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private double _readingPercent;
    [ObservableProperty] private bool _isAutoRead;
    [ObservableProperty] private bool _isClickThrough;
    [ObservableProperty] private double _windowOpacity = 1.0;
    [ObservableProperty] private Brush? _backgroundBrush;
    [ObservableProperty] private Brush? _foregroundBrush;
    [ObservableProperty] private FontFamily? _fontFamily;
    [ObservableProperty] private double _fontSize = Constants.DefaultFontSize;
    [ObservableProperty] private double _lineHeight = Constants.DefaultLineHeight;
    [ObservableProperty] private FontWeight _fontWeight = FontWeights.Normal;
    [ObservableProperty] private double _textAreaWidth = 480;
    [ObservableProperty] private double _textAreaHeight = 600;
    [ObservableProperty] private string _bookTitle = "未加载";
    [ObservableProperty] private string _chapterTitle = string.Empty;

    private List<PaginationService.PageRange> _currentPages = new();
    private string _currentChapterText = string.Empty;

    public ReaderViewModel(
        BookshelfService bookshelf,
        ReadingSessionService session,
        PaginationService paginator,
        AutoReadService autoRead,
        WindowBehaviorService windowBehavior,
        BookmarkService bookmark,
        SettingsService settings,
        IEventAggregator<IEventMarker> events)
    {
        _bookshelf = bookshelf;
        _session = session;
        _paginator = paginator;
        _autoRead = autoRead;
        _windowBehavior = windowBehavior;
        _bookmark = bookmark;
        _settings = settings;
        _events = events;

        // 监听自动阅读
        _autoRead.Tick += (s, e) => Application.Current?.Dispatcher.Invoke(NextPage);
        _autoRead.Started += (s, e) => IsAutoRead = true;
        _autoRead.Stopped += (s, e) => IsAutoRead = false;

        // 监听设置变更
        _settings.SettingsChanged += (s, e) => ApplyDisplaySettings();

        // 通过事件聚合器接收热键事件（替代直接持有 HotkeyManager）
        _events.Subscribe<HotkeyPressedEvent>(OnHotkeyReceived);

        ApplyDisplaySettings();
    }

    /// <summary>
    /// 接收热键事件（由 IEventAggregator 分发，HotkeyManager 发布）。
    /// 检查 CurrentBook != null 而非 ReaderWindow.IsVisible，
    /// 确保启动时即使 ReaderWindow 尚未创建（启动行为=打开书架），热键依然能被接收。
    /// </summary>
    private void OnHotkeyReceived(HotkeyPressedEvent e)
    {
        // 没有加载书时不响应阅读相关热键
        if (CurrentBook == null)
            return;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            switch (e.Action)
            {
                case HotkeyAction.NextPage: NextPageCommand.Execute(null); break;
                case HotkeyAction.PrevPage: PrevPageCommand.Execute(null); break;
                case HotkeyAction.NextChapter: NextChapterCommand.Execute(null); break;
                case HotkeyAction.PrevChapter: PrevChapterCommand.Execute(null); break;
                case HotkeyAction.IncreaseOpacity: _windowBehavior.IncreaseOpacity(); break;
                case HotkeyAction.DecreaseOpacity: _windowBehavior.DecreaseOpacity(); break;
                case HotkeyAction.ToggleClickThrough: _windowBehavior.ToggleClickThrough(); break;
                case HotkeyAction.ToggleTopmost: _windowBehavior.ToggleTopmost(); break;
                case HotkeyAction.ToggleAutoRead: ToggleAutoRead(); break;
                case HotkeyAction.AutoReadFaster: _autoRead.Faster(); break;
                case HotkeyAction.AutoReadSlower: _autoRead.Slower(); break;
                case HotkeyAction.HideWindow:
                    Application.Current?.Windows.OfType<ReaderWindow>().FirstOrDefault()?.Hide();
                    break;
                case HotkeyAction.ShowChapterList: ShowChapterListCommand.Execute(null); break;
                case HotkeyAction.ShowBookmarkList: ShowBookmarkListCommand.Execute(null); break;
                case HotkeyAction.AddBookmark: AddBookmark(); break;
            }
        });
    }

    /// <summary>
    /// 热键事件定义（强类型，替代 EventBus 字符串事件名）。
    /// </summary>
    public record HotkeyPressedEvent(HotkeyAction Action) : IEventMarker;

    /// <summary>
    /// 保存窗口状态（供 ReaderWindow.OnClosing 调用）。
    /// </summary>
    public void SaveWindowState(double left, double top, double width, double height, double opacity)
    {
        _session.SaveProgress(left, top, width, height, opacity);
    }

    /// <summary>
    /// 显示章节目录（供 ReaderWindow 命令绑定调用）。
    /// </summary>
    [RelayCommand]
    public void ShowChapterList()
    {
        if (CurrentBook == null) return;
        var w = App.Services.GetRequiredService<ChapterListWindow>();
        if (w.DataContext is ChapterListViewModel cvm)
            cvm.Load(CurrentBook);
        w.Owner = Application.Current?.Windows.OfType<ReaderWindow>().FirstOrDefault();
        w.ShowDialog();
    }

    /// <summary>
    /// 显示书签列表（供 ReaderWindow 命令绑定调用）。
    /// </summary>
    [RelayCommand]
    public void ShowBookmarkList()
    {
        if (CurrentBook == null) return;
        var w = App.Services.GetRequiredService<BookmarkWindow>();
        if (w.DataContext is BookmarkListViewModel bvm)
            bvm.Load(CurrentBook);
        w.Owner = Application.Current?.Windows.OfType<ReaderWindow>().FirstOrDefault();
        w.ShowDialog();
    }

    public void LoadBook(Book book, ReadingProgress? progress = null)
    {
        CurrentBook = book;
        BookTitle = book.Title;
        _session.Open(book);
        CurrentChapter = _session.CurrentChapter;
        LoadChapterContent();

        if (progress != null)
        {
            CurrentPage = progress.PageNumber;
            _windowBehavior.SetOpacity(progress.Opacity > 0 ? progress.Opacity : 1.0);
            WindowOpacity = _windowBehavior.AttachedOpacity;
        }
        RecomputePagination();
    }

    public double AttachedOpacity => WindowOpacity;

    [RelayCommand]
    public void NextPage()
    {
        if (CurrentChapter == null) return;
        if (CurrentPage < _currentPages.Count - 1)
        {
            CurrentPage++;
        }
        else
        {
            // 跨章
            var allChapters = CurrentBook?.FlatChapters().ToList();
            if (allChapters == null) return;
            var idx = allChapters.FindIndex(c => c.Id == CurrentChapter.Id);
            if (idx >= 0 && idx < allChapters.Count - 1)
            {
                SetChapterAndPage(allChapters[idx + 1], 0);
                return;
            }
            else
            {
                Log.Information("已到达全书末尾");
                StatusText = "已到达全书末尾";
                if (IsAutoRead) _autoRead.Stop();
                return;
            }
        }
        UpdatePage();
    }

    [RelayCommand]
    public void PrevPage()
    {
        if (CurrentPage > 0)
        {
            CurrentPage--;
        }
        else
        {
            var allChapters = CurrentBook?.FlatChapters().ToList();
            if (allChapters == null) return;
            var idx = allChapters.FindIndex(c => c.Id == CurrentChapter.Id);
            if (idx > 0)
            {
                var prev = allChapters[idx - 1];
                SetChapterAndPage(prev, int.MaxValue / 2); // 由 RecomputePagination 修正
                // 跳转后跳到最后一页
                CurrentPage = Math.Max(0, _currentPages.Count - 1);
                UpdatePage();
                return;
            }
            else
            {
                CurrentPage = 0;
            }
        }
        UpdatePage();
    }

    [RelayCommand]
    public void NextChapter()
    {
        var allChapters = CurrentBook?.FlatChapters().ToList();
        if (allChapters == null) return;
        var idx = allChapters.FindIndex(c => c.Id == CurrentChapter?.Id);
        if (idx >= 0 && idx < allChapters.Count - 1)
            SetChapterAndPage(allChapters[idx + 1], 0);
    }

    [RelayCommand]
    public void PrevChapter()
    {
        var allChapters = CurrentBook?.FlatChapters().ToList();
        if (allChapters == null) return;
        var idx = allChapters.FindIndex(c => c.Id == CurrentChapter?.Id);
        if (idx > 0)
            SetChapterAndPage(allChapters[idx - 1], 0);
    }

    public void JumpToChapter(Chapter chapter, int page = 0)
    {
        SetChapterAndPage(chapter, page);
    }

    /// <summary>
    /// 从书签跳转：通过 chapterId 查找章节并跳转。
    /// </summary>
    public void JumpToProgress(int chapterId, int page)
    {
        var db = App.Services.GetRequiredService<DatabaseService>();
        var ch = db.GetChapter(chapterId);
        if (ch != null) SetChapterAndPage(ch, page);
    }

    private void SetChapterAndPage(Chapter chapter, int page)
    {
        _currentChapterText = string.Empty;
        GC.Collect(0, GCCollectionMode.Optimized, false);

        CurrentChapter = chapter;
        _session.SetChapter(chapter);
        LoadChapterContent();
        RecomputePagination();
        CurrentPage = Math.Clamp(page, 0, Math.Max(0, _currentPages.Count - 1));
        UpdatePage();
    }

    private void LoadChapterContent()
    {
        if (CurrentChapter == null || CurrentBook == null) return;
        try
        {
            _currentChapterText = ChapterContentReader.Read(
                CurrentBook.FilePath, CurrentChapter, CurrentBook.Encoding);
            ChapterTitle = CurrentChapter.Title;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载章节失败 {Chapter}", CurrentChapter.Title);
            _currentChapterText = $"[加载章节失败: {ex.Message}]";
        }
    }

    public void RecomputePagination()
    {
        if (string.IsNullOrEmpty(_currentChapterText)) { _currentPages = new(); TotalPages = 0; PageText = ""; return; }
        var s = _settings.Current.Display;
        _currentPages = _paginator.Paginate(
            _currentChapterText,
            s.FontFamily, s.FontSize, s.LineHeight,
            TextAreaWidth, TextAreaHeight);
        TotalPages = _currentPages.Count;
        if (CurrentPage >= TotalPages) CurrentPage = Math.Max(0, TotalPages - 1);
        UpdatePage();
    }

    private void UpdatePage()
    {
        if (_currentPages.Count == 0) { PageText = ""; return; }
        var range = _currentPages[Math.Min(CurrentPage, _currentPages.Count - 1)];
        PageText = _currentChapterText.Substring(range.Start, Math.Min(range.Length, _currentChapterText.Length - range.Start));
        StatusText = $"{CurrentChapter?.Title}    {CurrentPage + 1}/{TotalPages}";

        // 阅读百分比 = (当前章节序号) / 总章节数
        if (CurrentBook != null)
        {
            var all = CurrentBook.FlatChapters().ToList();
            var idx = all.FindIndex(c => c.Id == CurrentChapter?.Id);
            if (idx >= 0)
            {
                ReadingPercent = (idx + (CurrentPage + 1.0) / Math.Max(1, TotalPages)) / Math.Max(1, all.Count);
            }
        }

        _session.SetPage(CurrentPage);
    }

    private void ApplyDisplaySettings()
    {
        var s = _settings.Current.Display;
        FontFamily = new FontFamily(s.FontFamily);
        FontSize = s.FontSize;
        LineHeight = s.LineHeight;
        FontWeight = s.FontBold ? FontWeights.Bold : FontWeights.Normal;

        var bg = s.GetEffectiveBackground();
        if (bg == "Transparent")
        {
            BackgroundBrush = Brushes.Transparent;
        }
        else
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(bg);
                BackgroundBrush = new SolidColorBrush(color);
            }
            catch
            {
                BackgroundBrush = Brushes.White;
            }
        }
        try
        {
            var fc = (Color)ColorConverter.ConvertFromString(s.GetEffectiveFontColor());
            ForegroundBrush = new SolidColorBrush(fc);
        }
        catch
        {
            ForegroundBrush = Brushes.Black;
        }
        WindowOpacity = s.Opacity;

        _paginator.ClearCache();
        RecomputePagination();
    }

    public void ApplyTextAreaSize(double w, double h)
    {
        if (Math.Abs(w - TextAreaWidth) < 0.5 && Math.Abs(h - TextAreaHeight) < 0.5) return;
        TextAreaWidth = w;
        TextAreaHeight = h;
        // 使用增量判断：尺寸变化微小时不重算分页
        if (_paginator.InvalidateIfSizeChanged(w, h))
            RecomputePagination();
    }

    [RelayCommand]
    public void AddBookmark()
    {
        if (CurrentBook == null || CurrentChapter == null) return;
        var b = _bookmark.Add(CurrentBook.Id, CurrentChapter.Id, CurrentPage);
        // 立刻给用户反馈, 否则用户以为没生效
        StatusText = $"已添加书签: {CurrentChapter.Title} 第{CurrentPage + 1}页";
        Log.Information("添加书签: {Chapter} 第{Page}页", b.ChapterId, b.PageNumber);
    }

    [RelayCommand]
    public void ToggleAutoRead()
    {
        if (IsAutoRead) _autoRead.Stop();
        else _autoRead.Start();
    }

    public void Refresh() => RecomputePagination();
}
