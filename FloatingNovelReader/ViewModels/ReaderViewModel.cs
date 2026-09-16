using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
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
public sealed partial class ReaderViewModel : ObservableObject, IPageAdvancer, ITtsPlaybackHost
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
    [ObservableProperty] private bool _isSpeaking;

    private string _currentChapterText = string.Empty;

    /// <summary>朗读开始前的自动阅读状态（朗读结束后按它恢复）。</summary>
    private bool _autoReadWasOnBeforeTts;

    /// <summary>
    /// 朗读状态栏的刷新定时器（1 秒）。
    ///
    /// 「剩余 12:34」按段落音频时长推进，只在段与段之间跳变，
    /// 所以进度事件本身不足以让倒计时看起来在走——用这个定时器把服务里的
    /// 最新值拉出来刷新文案。只在朗读期间运行。
    /// </summary>
    private readonly DispatcherTimer _speakRefreshTimer;

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
        _tts.SegmentFinished += (s, e) => RunOnUi(OnTtsSegmentFinished);
        _tts.ChapterStarted += (s, e) => RunOnUi(() => OnTtsChapterTextChanged());
        _tts.BookFinished += (s, e) => RunOnUi(() => OnTtsBookFinished());
        _tts.Failed += (s, message) => RunOnUi(() => OnTtsFailed(message));
        _tts.Started += (s, e) => RunOnUi(OnTtsStarted);
        _tts.Stopped += (s, e) => RunOnUi(OnTtsStopped);

        // 1 秒刷新一次「剩余 mm:ss」：剩余时间按段落音频长度推进，
        // 只在段间跳变，光靠进度事件看不到倒计时在走。
        _speakRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _speakRefreshTimer.Tick += (s, e) => OnSpeakRefreshTick();
        _speakRefreshTimer.Start();

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
                case HotkeyAction.SpeakFromHereMinutes: SpeakFromHereMinutesCommand.Execute(null); break;
                case HotkeyAction.SpeakFromHereChapters: SpeakFromHereChaptersCommand.Execute(null); break;
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
    /// 从当前章节开始朗读，播到全书末（无停止条件）。
    /// 与自动阅读**软互斥**：两者都会驱动翻页，同时跑会一秒翻两页，所以启动朗读前先停自动阅读。
    /// </summary>
    [RelayCommand]
    public void SpeakFromHere() => SpeakFromHere(TtsStopMode.BookEnd);

    /// <summary>朗读 N 分钟（N = 设置里的 <see cref="TtsSettings.MaxMinutesDefault"/>，默认 30）。</summary>
    [RelayCommand]
    public void SpeakFromHereMinutes() => SpeakFromHere(TtsStopMode.Minutes);

    /// <summary>朗读 N 章（N = 设置里的 <see cref="TtsSettings.MaxChaptersDefault"/>，默认 10）。</summary>
    [RelayCommand]
    public void SpeakFromHereChapters() => SpeakFromHere(TtsStopMode.Chapters);

    /// <summary>
    /// 三个菜单入口对应的**同一个底层命令**，只有停止条件不同。
    /// </summary>
    public void SpeakFromHere(TtsStopMode mode)
    {
        if (!HasBook || CurrentChapter == null)
        {
            Log.Information("朗读：请求被忽略（mode={Mode}，HasBook={HasBook}）", mode, HasBook);
            return;
        }
        if (string.IsNullOrWhiteSpace(_currentChapterText))
        {
            StatusText = "本章没有可朗读的文本";
            return;
        }

        // 软互斥：两者都会驱动翻页（一秒翻两页 + 状态栏互相覆盖）
        _autoReadWasOnBeforeTts = IsAutoRead;
        if (IsAutoRead) _autoRead.Stop();

        // 每次朗读都用最新设置，改完语速/声音不用重启程序
        RefreshSpeakSettings();

        var options = TtsPlaybackOptions.FromMode(mode, MaxMinutes, MaxChapters);
        Log.Information("朗读请求：mode={Mode} limit={Limit}", options.Mode, options.Limit);

        _tts.Speak(this, () => _currentChapterText, options);
    }

    /// <summary>停止朗读（不清状态栏里的阅读信息，只把朗读提示换掉）。</summary>
    [RelayCommand]
    public void StopSpeaking()
    {
        _tts.Stop();
        StatusText = "朗读已停止";
    }

    /// <summary>设置页「朗读 N 分钟」的默认值。</summary>
    public int MaxMinutes => _settings.Current.Tts.MaxMinutesDefault;

    /// <summary>设置页「朗读 N 章」的默认值。</summary>
    public int MaxChapters => _settings.Current.Tts.MaxChaptersDefault;

    /// <summary>
    /// 朗读前从设置里取一遍界面/文本相关项。
    /// ReaderDisplay 已有 SettingsChanged 联动，但 Pager 的可用区域依赖于窗口尺寸，
    /// 这里只做一次"确保分页是最新的"，避免朗读开始时页码算错。
    /// </summary>
    private void RefreshSpeakSettings()
    {
        if (Pager.TotalPages == 0) RecomputePagination();
    }

    // ── 朗读 → 状态栏 / 阅读位置 ──

    private void OnTtsStarted()
    {
        IsSpeaking = true;
        StatusText = _tts.StatusText ?? "正在朗读…";
    }

    private void OnTtsProgress(TtsProgressEventArgs e)
    {
        // 试听（设置页里点"试听"）不该改动阅读位置和状态栏
        if (!e.DrivesReader) return;

        // 先跟随后端给出的真实页码
        if (_settings.Current.Tts.AutoAdvancePage && e.SegmentCount > 0)
            FollowSegment(e.SegmentIndex, e.SegmentCount);

        // 放在分页之后：让朗读状态覆盖 PageChanged 写入的阅读状态
        StatusText = string.IsNullOrEmpty(e.StatusText)
            ? $"正在朗读：第 {e.SegmentIndex + 1}/{e.SegmentCount} 段"
            : e.StatusText;
    }

    /// <summary>
    /// 一段播完 → 翻下一页。章末则由 <see cref="OnTtsChapterStarted"/> 在换章后把位置放到新章第 1 页。
    /// </summary>
    private void OnTtsSegmentFinished()
    {
        if (!_settings.Current.Tts.AutoAdvancePage) return;
        if (!HasBook) return;

        NextPageCommand.Execute(null);
    }

    /// <summary>
    /// 朗读已经切到下一章（此时 <see cref="CurrentChapterText"/> 已是新章内容）。
    /// 把游标重置到新章开头，并按新章重新分页 —— 读取顺序很关键：
    /// 先 RecomputePagination 再 SetPage，最后才 FollowSegment。
    /// </summary>
    private void OnTtsChapterTextChanged()
    {
        if (!HasBook) return;

        RecomputePagination();
        Pager.SetPage(0, _currentChapterText);

        if (_settings.Current.Tts.AutoAdvancePage)
            FollowSegment(0, TtsSegmenter.SplitChapter(_currentChapterText).Count);
    }

    /// <summary>最后一章播完：停在全书末，不再翻页。</summary>
    private void OnTtsBookFinished()
    {
        if (!HasBook) return;

        // 落到最后一页（"停在全书末"）
        if (Pager.TotalPages > 0) Pager.SetPage(Pager.TotalPages - 1, _currentChapterText);
    }

    private void OnTtsFailed(string message) => StatusText = $"朗读失败: {message}";

    private void OnTtsStopped()
    {
        IsSpeaking = false;

        // 用户主动停止时自己写了"朗读已停止"，别再覆盖一次
        StatusText = _tts.StopReason switch
        {
            TtsStopReason.BookEnd => "朗读完成（已到全书末尾）",
            TtsStopReason.MinutesReached => "朗读完成（已到设定的分钟数）",
            TtsStopReason.ChaptersReached => "朗读完成（已到设定的章节数）",
            TtsStopReason.Failed => "朗读已中断",
            _ => StatusText.StartsWith("朗读", StringComparison.Ordinal) ? "朗读已停止" : StatusText,
        };

        // 恢复朗读前的自动阅读状态（用户不用自己记得之前开没开）
        if (_autoReadWasOnBeforeTts && !IsAutoRead) _autoRead.Start();
        _autoReadWasOnBeforeTts = false;
    }

    /// <summary>定时把服务里的「剩余 mm:ss」拉出来刷新（剩余时间只在段间跳变）。</summary>
    private void OnSpeakRefreshTick()
    {
        if (!_tts.IsRunning) return;

        var status = _tts.StatusText;
        if (!string.IsNullOrEmpty(status)) StatusText = status;
    }

    /// <summary>
    /// 让阅读位置跟随音频：把"段在章里的比例"映射到页码。
    /// 抽成纯函数是为了能单测（ReaderViewModel 本身需要 WPF + 一整套服务才能构造）。
    /// </summary>
    public static int PageForSegment(int segmentIndex, int segmentCount, int totalPages) =>
        SegmentPageMapper.PageForSegment(segmentIndex, segmentCount, totalPages);

    private void FollowSegment(int segmentIndex, int segmentCount)
    {
        var total = Pager.TotalPages;
        if (total <= 0) return;

        var target = SegmentPageMapper.PageForSegment(segmentIndex, segmentCount, total);
        if (target != Pager.CurrentPage) Pager.SetPage(target, _currentChapterText);
    }

    // ── ITtsPlaybackHost：跨章朗读所需的章节导航能力 ──

    string ITtsPlaybackHost.GetCurrentChapterText() => _currentChapterText;

    bool ITtsPlaybackHost.CanAdvanceChapter() =>
        ChapterSequence.Next(ChapterSequence.Flatten(CurrentBook), CurrentChapter?.Id) != null;

    bool ITtsPlaybackHost.AdvanceToNextChapter()
    {
        var next = ChapterSequence.Next(ChapterSequence.Flatten(CurrentBook), CurrentChapter?.Id);
        if (next == null) return false;

        SetChapterAndPage(next, 0);
        return true;
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
