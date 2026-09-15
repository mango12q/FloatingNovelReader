using System;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingNovelReader.Core;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using Serilog;

namespace FloatingNovelReader.ViewModels;

/// <summary>
/// 阅读器主窗口 ViewModel —— 只做**协调**。
///
/// 原来它是一个 458 行、13 项职责的 God Class（体检报告 §1.2）。现在按职责拆成三个子 VM：
///   - <see cref="Display"/>：字体 / 颜色 / 透明度等外观（ReaderDisplayViewModel）
///   - <see cref="Pager"/>  ：文本区尺寸 / 当前页 / 总页数 / 页文本与分页引擎（ReaderPagerViewModel）
///   - <see cref="Dialogs"/>：章节目录 / 书签列表弹窗开关（ReaderDialogsViewModel）
///
/// 本类保留的职责（都是必须跨子对象协调的）：
///   - 当前书 / 当前章、章节文本加载
///   - 翻页与跳章（跨章边界判定，走 ChapterSequence）
///   - 状态栏 / 阅读百分比
///   - 自动阅读联动、热键路由、书签
///   - 阅读进度持久化（ReadingSessionService）
///   - 对外暴露 <see cref="IPageAdvancer"/>（自动阅读 / 以后的 TTS、OCR 只依赖它）
/// </summary>
public sealed partial class ReaderViewModel : ObservableObject, IPageAdvancer
{
    private readonly ReadingSessionService _session;
    private readonly AutoReadService _autoRead;
    private readonly WindowBehaviorService _windowBehavior;
    private readonly BookmarkService _bookmark;
    private readonly SettingsService _settings;
    private readonly IEventAggregator<IEventMarker> _events;
    private readonly IWindowNavigator _navigator;
    private readonly TtsService _tts;

    [ObservableProperty] private Book? _currentBook;
    [ObservableProperty] private Chapter? _currentChapter;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private double _readingPercent;
    [ObservableProperty] private bool _isAutoRead;
    [ObservableProperty] private bool _isClickThrough;
    [ObservableProperty] private string _bookTitle = "未加载";
    [ObservableProperty] private string _chapterTitle = string.Empty;

    private string _currentChapterText = string.Empty;

    /// <summary>外观子 VM（XAML 绑定 Display.*）。</summary>
    public ReaderDisplayViewModel Display { get; }

    /// <summary>分页子 VM（XAML 绑定 Pager.*）。</summary>
    public ReaderPagerViewModel Pager { get; }

    /// <summary>弹窗子 VM。</summary>
    public ReaderDialogsViewModel Dialogs { get; }

    public ReaderViewModel(
        ReadingSessionService session,
        AutoReadService autoRead,
        WindowBehaviorService windowBehavior,
        BookmarkService bookmark,
        SettingsService settings,
        IEventAggregator<IEventMarker> events,
        IWindowNavigator navigator,
        ReaderDisplayViewModel display,
        ReaderPagerViewModel pager,
        ReaderDialogsViewModel dialogs,
        TtsService tts)
    {
        _session = session;
        _autoRead = autoRead;
        _windowBehavior = windowBehavior;
        _bookmark = bookmark;
        _settings = settings;
        _events = events;
        _navigator = navigator;
        Display = display;
        Pager = pager;
        Dialogs = dialogs;
        _tts = tts;

        // 分页变化 → 状态栏 / 阅读百分比 / 会话（等价于原先 UpdatePage() 的尾部）
        Pager.PageChanged += OnPagerPageChanged;

        // 朗读进度 → 状态栏（并可选让阅读位置跟随音频）。
        // ⚠️ TtsService 在**后台线程**抛出这些事件，而改 Pager/StatusText 会触发 View 的
        // PropertyChanged 处理器（会访问 FrameworkElement），必须切回 UI 线程，
        // 否则抛"调用线程无法访问此对象，因为另一个线程拥有该对象"。
        _tts.Progress += (s, e) => RunOnUi(() => OnTtsProgress(e));
        _tts.Failed += (s, message) => RunOnUi(() => StatusText = $"朗读失败: {message}");
        _tts.Stopped += (s, e) => RunOnUi(() =>
        {
            if (StatusText.StartsWith("正在朗读", StringComparison.Ordinal)) StatusText = "朗读已停止";
        });

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

    // ── IPageAdvancer：给自动阅读 / 以后的 TTS、OCR 驱动翻页的最小契约 ──

    public bool HasBook => CurrentBook != null;

    /// <summary>当前章节的完整文本（TTS 切段的输入）。</summary>
    public string CurrentChapterText => _currentChapterText;

    public bool CanGoNextPage =>
        Pager.CurrentPage < Pager.PageCount - 1 ||
        ChapterSequence.Next(ChapterSequence.Flatten(CurrentBook), CurrentChapter?.Id) != null;

    public bool CanGoPrevPage =>
        Pager.CurrentPage > 0 ||
        ChapterSequence.Previous(ChapterSequence.Flatten(CurrentBook), CurrentChapter?.Id) != null;

    /// <summary>
    /// 最近一次加载书籍时读到的进度（含窗口几何）。
    /// ReaderWindow 恢复窗口位置时从这里取，不再自己反向解析 DatabaseService（体检报告 §1.4）。
    /// </summary>
    public ReadingProgress? SavedProgress { get; private set; }

    public double AttachedOpacity => Display.WindowOpacity;

    /// <summary>
    /// 热键事件定义（强类型，替代 EventBus 字符串事件名）。
    /// </summary>
    public record HotkeyPressedEvent(HotkeyAction Action) : IEventMarker;

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
                    // 通过导航接口切换可见性：VM 不再直接摸 Window.Visibility
                    if (_navigator.IsReaderVisible)
                    {
                        _navigator.HideReader();
                        StatusText = "窗口已隐藏，按 F8 恢复";
                    }
                    else
                    {
                        _navigator.ShowReader();
                        StatusText = "窗口已恢复";
                    }
                    break;
                case HotkeyAction.ShowChapterList: ShowChapterListCommand.Execute(null); break;
                case HotkeyAction.ShowBookmarkList: ShowBookmarkListCommand.Execute(null); break;
                case HotkeyAction.AddBookmark: AddBookmark(); break;
                case HotkeyAction.SpeakFromHere: SpeakFromHereCommand.Execute(null); break;
                case HotkeyAction.StopSpeaking: StopSpeakingCommand.Execute(null); break;
            }
        });
    }

    /// <summary>保存窗口状态（供 ReaderWindow.OnClosing 调用）。</summary>
    public void SaveWindowState(double left, double top, double width, double height, double opacity)
    {
        _session.SaveProgress(left, top, width, height, opacity);
    }

    /// <summary>显示 / 关闭章节目录（供菜单与 F9 调用）。</summary>
    [RelayCommand]
    public void ShowChapterList()
    {
        if (CurrentBook == null) return;
        var status = Dialogs.ToggleChapterList(CurrentBook);
        if (status != null) StatusText = status;
    }

    /// <summary>显示 / 关闭书签列表（供菜单与 F12 调用）。</summary>
    [RelayCommand]
    public void ShowBookmarkList()
    {
        if (CurrentBook == null) return;
        var status = Dialogs.ToggleBookmarkList(CurrentBook);
        if (status != null) StatusText = status;
    }

    public void LoadBook(Book book, ReadingProgress? progress = null)
    {
        CurrentBook = book;
        BookTitle = book.Title;
        var saved = _session.Open(book);
        SavedProgress = progress ?? saved;
        CurrentChapter = _session.CurrentChapter;
        LoadChapterContent();

        if (progress != null)
        {
            // 直接赋值（不发事件）：分页还没算，此刻 TotalPages 仍是旧值
            Pager.CurrentPage = progress.PageNumber;
            _windowBehavior.SetOpacity(progress.Opacity > 0 ? progress.Opacity : 1.0);
            Display.WindowOpacity = _windowBehavior.AttachedOpacity;
        }
        RecomputePagination();
    }

    [RelayCommand]
    public void NextPage()
    {
        if (CurrentChapter == null) return;

        // 章内翻页：PageChanged 已经驱动状态栏 / 百分比 / 会话
        if (Pager.TryMoveNext(_currentChapterText))
            return;

        // 跨章
        var next = ChapterSequence.Next(ChapterSequence.Flatten(CurrentBook), CurrentChapter.Id);
        if (next != null)
        {
            SetChapterAndPage(next, 0);
            return;
        }

        Log.Information("已到达全书末尾");
        StatusText = "已到达全书末尾";
        if (IsAutoRead) _autoRead.Stop();
    }

    [RelayCommand]
    public void PrevPage()
    {
        if (CurrentChapter == null) return;

        if (Pager.TryMovePrevious(_currentChapterText))
            return;

        var prev = ChapterSequence.Previous(ChapterSequence.Flatten(CurrentBook), CurrentChapter.Id);
        if (prev != null)
        {
            SetChapterAndPage(prev, int.MaxValue / 2); // 由分页夹到最后一页
            // 跳转后再显式落到最后一页
            Pager.SetPage(Pager.PageCount - 1, _currentChapterText);
            return;
        }

        Pager.SetPage(0, _currentChapterText);
    }

    [RelayCommand]
    public void NextChapter()
    {
        var next = ChapterSequence.Next(ChapterSequence.Flatten(CurrentBook), CurrentChapter?.Id);
        if (next != null) SetChapterAndPage(next, 0);
    }

    [RelayCommand]
    public void PrevChapter()
    {
        var prev = ChapterSequence.Previous(ChapterSequence.Flatten(CurrentBook), CurrentChapter?.Id);
        if (prev != null) SetChapterAndPage(prev, 0);
    }

    public void JumpToChapter(Chapter chapter, int page = 0)
    {
        SetChapterAndPage(chapter, page);
    }

    /// <summary>
    /// 从书签跳转：通过 chapterId 查找章节并跳转。
    /// 书签只可能指向当前书，所以直接查内存里的章节序列 —— 这里原本会反向解析 DatabaseService。
    /// </summary>
    public void JumpToProgress(int chapterId, int page)
    {
        var chapters = ChapterSequence.Flatten(CurrentBook);
        var idx = ChapterSequence.IndexOf(chapters, chapterId);
        if (idx >= 0) SetChapterAndPage(chapters[idx], page);
    }

    public void RecomputePagination()
    {
        Pager.Recompute(_currentChapterText);
    }

    public void ApplyTextAreaSize(double w, double h)
    {
        Pager.ApplyTextAreaSize(w, h, _currentChapterText);
    }

    [RelayCommand]
    public void AddBookmark()
    {
        if (CurrentBook == null || CurrentChapter == null) return;
        var b = _bookmark.Add(CurrentBook.Id, CurrentChapter.Id, Pager.CurrentPage);
        // 立刻给用户反馈, 否则用户以为没生效
        StatusText = $"已添加书签: {CurrentChapter.Title} 第{Pager.CurrentPage + 1}页";
        Log.Information("添加书签: {Chapter} 第{Page}页", b.ChapterId, b.PageNumber);
    }

    [RelayCommand]
    public void ToggleAutoRead()
    {
        if (IsAutoRead) _autoRead.Stop();
        else _autoRead.Start();
    }

    /// <summary>
    /// 从当前章节开始朗读（第一刀：朗读本章，播完停在章末）。
    /// 与自动阅读**软互斥**：两者都会驱动翻页，同时跑会一秒翻两页，所以启动朗读前先停自动阅读。
    /// </summary>
    [RelayCommand]
    public void SpeakFromHere()
    {
        if (!HasBook || CurrentChapter == null) return;
        if (IsAutoRead) _autoRead.Stop();
        if (string.IsNullOrEmpty(_currentChapterText)) return;
        _tts.Speak(_currentChapterText);
    }

    /// <summary>停止朗读（不清状态栏里的阅读信息，只把朗读提示换掉）。</summary>
    [RelayCommand]
    public void StopSpeaking()
    {
        _tts.Stop();
        StatusText = "朗读已停止";
    }

    /// <summary>朗读进度 → 状态栏；并按设置让阅读位置跟随音频。</summary>
    private void OnTtsProgress(TtsProgressEventArgs e)
    {
        // 试听（设置页里点"试听"）不该改动阅读位置和状态栏
        if (!e.DrivesReader) return;

        if (_settings.Current.Tts.AutoAdvancePage && Pager.TotalPages > 1 && e.SegmentCount > 0)
        {
            // 片段在章节里的比例位置 → 对应页码（大致同步，不追求逐字对齐）
            var target = (int)Math.Floor((e.SegmentIndex + 1.0) / e.SegmentCount * Pager.TotalPages) - 1;
            Pager.SetPage(target, _currentChapterText);
        }

        // 放在分页之后：让朗读状态覆盖 PageChanged 写入的阅读状态
        StatusText = $"正在朗读：第 {e.SegmentIndex + 1}/{e.SegmentCount} 段";
    }

    /// <summary>把回调切回 UI 线程；没有 Application（单测）时直接执行。</summary>
    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) { action(); return; }
        if (dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    public void Refresh() => RecomputePagination();

    // ── 内部 ──

    private void SetChapterAndPage(Chapter chapter, int page)
    {
        _currentChapterText = string.Empty;

        CurrentChapter = chapter;
        _session.SetChapter(chapter);
        LoadChapterContent();
        RecomputePagination();
        Pager.SetPage(page, _currentChapterText);
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

    /// <summary>分页子 VM 报告页变化 → 更新状态栏 / 阅读百分比 / 会话（原 UpdatePage 的尾部职责）。</summary>
    private void OnPagerPageChanged(object? sender, EventArgs e)
    {
        StatusText = $"{CurrentChapter?.Title}    {Pager.CurrentPage + 1}/{Pager.TotalPages}";

        // 阅读百分比 = (当前章节序号 + 本章内进度) / 总章节数
        if (CurrentBook != null)
        {
            var all = ChapterSequence.Flatten(CurrentBook);
            if (ChapterSequence.IndexOf(all, CurrentChapter?.Id) >= 0)
                ReadingPercent = ChapterSequence.ReadingPercent(all, CurrentChapter?.Id, Pager.CurrentPage, Pager.TotalPages);
        }

        _session.SetPage(Pager.CurrentPage);
    }

    private void ApplyDisplaySettings()
    {
        Display.Apply(_settings.Current.Display);
        _windowBehavior.SetOpacity(Display.WindowOpacity);
        Pager.InvalidateAndRecompute(_currentChapterText);
    }
}
