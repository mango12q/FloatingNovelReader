using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

public sealed class TtsProgressEventArgs : EventArgs
{
    public TtsProgressEventArgs(
        int segmentIndex,
        int segmentCount,
        string text,
        bool drivesReader,
        int chapterNumber = 1,
        int maxChapters = 0,
        int remainingChapters = -1,
        string? remainingText = null)
    {
        SegmentIndex = segmentIndex;
        SegmentCount = segmentCount;
        Text = text;
        DrivesReader = drivesReader;
        ChapterNumber = chapterNumber;
        MaxChapters = maxChapters;
        RemainingChapters = remainingChapters;
        RemainingText = remainingText;
    }

    /// <summary>0 基的段序号。</summary>
    public int SegmentIndex { get; }
    public int SegmentCount { get; }
    public string Text { get; }

    /// <summary>
    /// 这次朗读是否用于驱动阅读窗口（true）还是仅试听（false）。
    /// 试听不该改动阅读位置和状态栏。
    /// </summary>
    public bool DrivesReader { get; }

    /// <summary>本次朗读里当前正在播的是第几章（1 基）。</summary>
    public int ChapterNumber { get; }

    /// <summary>Chapters 模式下的目标章数；其他模式为 0。</summary>
    public int MaxChapters { get; }

    /// <summary>本章之后还要再播几章；无章节数概念时为 -1。</summary>
    public int RemainingChapters { get; }

    /// <summary>状态栏用的一行进度文案（已含剩余时间 / 章节数）。</summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>剩余时间文案（如「剩余 12:34」）；无时间概念时为 null。</summary>
    public string? RemainingText { get; }
}

/// <summary>
/// 朗读服务：把**整本书**（从当前章开始）切片 → 逐段合成 → 顺序播放 → 跨章续播。
///
/// 三种停止条件（<see cref="TtsStopMode"/>）共用同一条链路，只有
/// <see cref="TtsStopCondition"/> 不同；播放列表状态机在 <see cref="TtsPlaylist"/>（纯逻辑、已单测）。
///
/// 与 AutoReadService 一样是"驱动阅读的独立服务"，两者互不认识：
/// 谁被启动由 ReaderViewModel 决定（软互斥由 VM 负责），这里只做合成、播放与跨章推进。
///
/// 跨章所需的能力通过 <see cref="ITtsPlaybackHost"/> 由调用方注入，因此本类不依赖 WPF / VM。
///
/// ⚠️ 事件全部从**后台线程**抛出，订阅者负责切回 UI 线程（ReaderViewModel 用 RunOnUi 包装）。
/// </summary>
public sealed class TtsService : IDisposable
{
    private readonly TtsEdgeClient _client;
    private readonly TtsPlayer _player;
    private readonly SettingsService _settings;
    private readonly object _gate = new();

    private CancellationTokenSource? _cts;
    private Task? _worker;
    private ITtsPlaybackHost? _host;
    private TtsPlaylist? _playlist;
    private int _session;
    private volatile bool _isRunning;

    public TtsService(TtsEdgeClient client, TtsPlayer player, SettingsService settings)
    {
        _client = client;
        _player = player;
        _settings = settings;
    }

    public bool IsRunning => _isRunning;

    /// <summary>本次（或最近一次）朗读结束的原因，供状态栏区分文案。</summary>
    public TtsStopReason StopReason { get; private set; } = TtsStopReason.UserStopped;

    /// <summary>当前停止条件；未在朗读时为 null。</summary>
    public TtsStopCondition? Condition
    {
        get { lock (_gate) return _playlist?.Condition; }
    }

    /// <summary>「剩余 mm:ss」；不在朗读或模式无时间概念时为 null。</summary>
    public string? RemainingText
    {
        get { lock (_gate) return _playlist?.Condition.RemainingText; }
    }

    /// <summary>状态栏进度文案（含剩余时间 / 章节数）；不在朗读时为 null。</summary>
    public string? StatusText
    {
        get { lock (_gate) return _playlist?.StatusText; }
    }

    public event EventHandler? Started;
    public event EventHandler? Stopped;
    public event EventHandler<TtsProgressEventArgs>? Progress;
    public event EventHandler? SegmentFinished;
    public event EventHandler? ChapterStarted;
    public event EventHandler? BookFinished;
    public event EventHandler<string>? Failed;

    /// <summary>仅试听一段文本（不清空阅读位置、不跨章、不写状态栏）。</summary>
    public void SpeakText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Speak(null, () => text, new TtsPlaybackOptions(TtsStopMode.BookEnd), drivesReader: false);
    }

    /// <summary>
    /// 从宿主当前章节开始朗读，按 <paramref name="options"/> 的停止条件结束。
    /// 调用会先停掉正在进行的朗读（同一个 service 同时只跑一个会话）。
    /// </summary>
    public void Speak(
        ITtsPlaybackHost? host,
        Func<string?> chapterTextProvider,
        TtsPlaybackOptions options,
        bool drivesReader = true)
    {
        ArgumentNullException.ThrowIfNull(chapterTextProvider);
        ArgumentNullException.ThrowIfNull(options);
        if (drivesReader && host is null)
            throw new ArgumentNullException(nameof(host), "朗读驱动阅读窗口时必须提供宿主");

        // 先停旧会话：Stop 会把 _host 清掉，所以下面必须重新赋值
        Stop();

        lock (_gate)
        {
            var session = ++_session;
            var cts = new CancellationTokenSource();
            _cts = cts;
            _host = drivesReader ? host : null;
            _playlist = new TtsPlaylist(TtsStopCondition.Create(options));

            _worker = Task.Run(
                () => RunAsync(_host, chapterTextProvider, drivesReader, session, cts.Token),
                CancellationToken.None);
        }
    }

    public void Stop() => Stop(TtsStopReason.UserStopped, log: true);

    public void Dispose()
    {
        Stop(TtsStopReason.UserStopped, log: false);
        _player.Dispose();
    }

    private void Stop(TtsStopReason reason, bool log)
    {
        Task? worker;
        CancellationTokenSource? cts;
        var notifyIdleStop = false;

        lock (_gate)
        {
            cts = _cts;
            _cts = null;
            _host = null;
            worker = _worker;

            // 会话代次前进，让旧 worker 的收尾（含 Stopped 事件）失效
            _session++;

            // 非用户主动的结束原因由 worker 在收尾时写入，这里只处理用户主动停止
            if (reason != TtsStopReason.UserStopped) StopReason = reason;

            // 用户按停止时队列里可能一段都还没播（还在合成）→ 不会有 worker 的 Stopped 事件，
            // 需要在这里显式补一个，否则状态栏会永远停在"正在朗读…"
            notifyIdleStop = _isRunning;
            if (notifyIdleStop) _isRunning = false;
        }

        try { cts?.Cancel(); } catch (ObjectDisposedException) { }
        _player.Stop();
        lock (_gate) _playlist?.StopMinuteClock();

        if (notifyIdleStop)
        {
            if (log) Log.Information("朗读已停止（用户主动）");
            Stopped?.Invoke(this, EventArgs.Empty);
        }
        else if (log)
        {
            Log.Information("朗读已停止");
        }

        // 不等待 worker：Stop 可能在 UI 线程调用，等待会卡住界面。
        // worker 会观察到取消令牌自行退出。
        _ = worker;
    }

    private async Task RunAsync(
        ITtsPlaybackHost? host,
        Func<string?> chapterTextProvider,
        bool drivesReader,
        int session,
        CancellationToken ct)
    {
        var reason = TtsStopReason.UserStopped;
        var notifyBookFinished = false;

        try
        {
            // 每轮循环 = 一章：取文本 → 切片 → 播完本章 → 决定换章 / 停在章末 / 停在全书末
            while (!ct.IsCancellationRequested)
            {
                var text = chapterTextProvider() ?? string.Empty;
                var segments = TtsSegmenter.SplitChapter(text);
                if (segments.Count == 0)
                {
                    Log.Information("朗读：本章没有可合成的文本，跳过");
                    return;   // finally 里按 UserStopped 收尾
                }

                lock (_gate)
                {
                    if (!IsCurrent(session)) return;
                    _playlist!.LoadChapter(segments);
                }

                Log.Information("朗读：本章 {Count} 段（第 {Chapter} 章 / 已播完 {Done} 章）首行={Head}",
                    segments.Count, ChapterNumberOf(_playlist!.Condition), _playlist.FinishedChapters,
                    FirstLine(text));
                var chapterAdvance = await PlayChapterAsync(segments, drivesReader, session, ct)
                    .ConfigureAwait(false);

                if (ct.IsCancellationRequested) return;

                if (chapterAdvance == TtsAdvance.StopConditionMet)
                {
                    reason = _playlist!.Condition is ChaptersStopCondition
                        ? TtsStopReason.ChaptersReached
                        : TtsStopReason.MinutesReached;
                    return;
                }

                if (chapterAdvance == TtsAdvance.StopChapterBoundary)
                {
                    // 停在全书末：最后一章已经播完，不再翻页
                    reason = TtsStopReason.BookEnd;
                    notifyBookFinished = true;
                    return;
                }

                // 换下一章：所有触碰阅读位置的调用都必须在 UI 线程上
                if (host is null) return;
                var moved = RunOnUi(() => host.AdvanceToNextChapter());
                if (!moved)
                {
                    reason = TtsStopReason.EndOfChapters;
                    return;
                }

                ChapterStarted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // 用户停止 / 换新会话：正常路径，不发 Failed
        }
        catch (Exception ex)
        {
            // 网络断开 / 静默截断 / 服务端拒绝都走这里：如实上报，不静默吞掉
            Log.Error(ex, "朗读失败");
            reason = TtsStopReason.Failed;
            Failed?.Invoke(this, ex.Message);
        }
        finally
        {
            _player.Stop();

            var isCurrentSession = false;
            lock (_gate)
            {
                if (_session == session)
                {
                    _isRunning = false;
                    _host = null;
                    StopReason = reason;
                    isCurrentSession = true;
                }
            }

            // 旧会话的收尾不能覆盖新会话的状态
            if (isCurrentSession)
            {
                Log.Information("朗读结束：{Reason}（已完整播完 {Chapters} 章）",
                    StopReasonText(reason), _playlist?.FinishedChapters ?? 0);

                if (notifyBookFinished) BookFinished?.Invoke(this, EventArgs.Empty);
                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>播完一整章。返回本章之后的去向。</summary>
    private async Task<TtsAdvance> PlayChapterAsync(
        IReadOnlyList<string> segments,
        bool drivesReader,
        int session,
        CancellationToken ct)
    {
        for (var i = 0; i < segments.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsCurrent(session)) return TtsAdvance.StopConditionMet;

            // 状态栏先更新再合成：显示的是"正在合成的这一段"
            PublishProgress(segments, i, drivesReader);
            MarkRunning(session);

            var mp3 = await _client.SynthesizeAsync(_settings.Current.Tts, segments[i], ct)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (!IsCurrent(session)) return TtsAdvance.StopConditionMet;

            var budgetHit = await PlayAndWaitAsync(mp3, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (!IsCurrent(session)) return TtsAdvance.StopConditionMet;

            // 「N 分钟」的预算与「剩余 mm:ss」都按真实音频长度推进
            var segmentDuration = TtsSegmentDuration.OfMp3(mp3);
            TtsAdvance advance;
            string status;
            lock (_gate)
            {
                _playlist!.StopMinuteClock();
                _playlist.RecordSegmentDuration(segmentDuration);
                _playlist.CommitMinuteClock();
                advance = budgetHit ? TtsAdvance.StopConditionMet : _playlist.AdvanceSegment();
                status = _playlist.StatusText;
            }

            Log.Information("朗读：第 {Index}/{Total} 段播完，音频 {Seconds:F1}s{Early}，{Status}",
                i + 1, segments.Count, segmentDuration.TotalSeconds,
                budgetHit ? "（分钟数到点，提前收口）" : string.Empty, status);

            if (advance == TtsAdvance.StopConditionMet) return advance;

            // 章内翻页交给 VM：它按"段在章里的比例"对照分页结果决定翻到第几页
            if (advance == TtsAdvance.NextSegment)
                SegmentFinished?.Invoke(this, EventArgs.Empty);
        }

        var canAdvance = _host is not null && RunOnUi(() => _host!.CanAdvanceChapter());
        TtsAdvance chapterAdvance;
        lock (_gate) chapterAdvance = _playlist!.AdvanceChapter(canAdvance);
        return chapterAdvance;
    }

    /// <summary>章节号只对「N 章」模式有意义，其他模式固定报第 1 章。</summary>
    private static int ChapterNumberOf(TtsStopCondition condition) =>
        condition is ChaptersStopCondition chapters ? chapters.CurrentChapterNumber : 1;

    /// <summary>日志里标识"现在在读哪一章"：取章节文本第一行（通常是章节标题）。</summary>
    private static string FirstLine(string text)
    {
        var idx = text.IndexOfAny(new[] { '\r', '\n' });
        var line = (idx >= 0 ? text[..idx] : text).Trim();
        return line.Length <= 40 ? line : line[..40];
    }

    /// <summary>
    /// 第一次真的要出声之前才置为"正在朗读"并发 Started。
    ///
    /// 首段合成要 1 秒上下（网络差时更久），若在 Speak() 里就报"已开始"，
    /// 用户会先看到"正在朗读"却半天没声音；而且此时按停止会走进
    /// "已经停了却又报一次停止"的分支。放在这里两个问题一起消失。
    /// </summary>
    private void MarkRunning(int session)
    {
        var notify = false;
        lock (_gate)
        {
            if (_session == session && !_isRunning)
            {
                _isRunning = true;
                notify = true;
            }
        }
        if (notify) Started?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>抛出当前播放列表的状态（含剩余时间 / 章节数）供状态栏显示。</summary>
    private void PublishProgress(IReadOnlyList<string> segments, int index, bool drivesReader)
    {
        TtsProgressEventArgs args;

        lock (_gate)
        {
            var playlist = _playlist;
            var condition = playlist?.Condition;
            args = new TtsProgressEventArgs(
                index,
                segments.Count,
                segments[index],
                drivesReader,
                chapterNumber: condition is ChaptersStopCondition c ? c.CurrentChapterNumber : 1,
                maxChapters: condition is ChaptersStopCondition c2 ? c2.MaxChapters : 0,
                remainingChapters: condition?.RemainingChapters ?? -1,
                remainingText: condition?.RemainingText)
            {
                StatusText = playlist?.StatusText ?? string.Empty,
            };
        }

        Progress?.Invoke(this, args);
    }

    /// <summary>
    /// 播放一段并等到它真正结束。
    ///
    /// 返回 true 表示**分钟数在本段播放途中到点**，调用方应把它当作停止条件达成、
    /// 提前收口（秒表口径，见 <see cref="TtsPlaylist.IsTimeBudgetExhausted"/>）。
    /// 这一段可能还有几秒钟没放完，但用户看到的是"到点了就停"，而不是
    /// "倒计时已经 00:00、声音还在响 40 秒"。
    /// </summary>
    private async Task<bool> PlayAndWaitAsync(byte[] mp3, CancellationToken ct)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFinished(object? sender, EventArgs e) => finished.TrySetResult();

        // ⚠️ 绝对不能 using/Dispose：观察任务在 Task.Delay 上等待时本方法可能已经返回，
        //    提前 Dispose 会让它的 Cancel() 抛 ObjectDisposedException（未被观察的后台异常，
        //    表现为朗读停了但状态没清干净）。CancellationTokenSource 很轻，交给 GC 即可。
        var budgetHit = new CancellationTokenSource();

        _player.Finished += OnFinished;
        try
        {
            var volume = 1.0 + _settings.Current.Tts.VolumePercent / 100.0;

            lock (_gate) _playlist?.StartMinuteClock();
            _player.PlaySegment(mp3, volume);
            _ = WaitForMinuteBudgetAsync(budgetHit, ct);

            await finished.Task.WaitAsync(ct).ConfigureAwait(false);
            return budgetHit.IsCancellationRequested;
        }
        finally
        {
            _player.Finished -= OnFinished;
            lock (_gate) _playlist?.StopMinuteClock();
        }
    }

    /// <summary>
    /// 每 200ms 看一眼分钟预算；到点就把当前这一段停掉。
    ///
    /// 只用秒表计时，不依赖音频解码结果——所以一段 40 秒的音频在"N 分钟"到点时
    /// 也能停在中途，而不是等整段放完。
    /// </summary>
    private async Task WaitForMinuteBudgetAsync(CancellationTokenSource budgetHit, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(200, ct).ConfigureAwait(false);

                var exhausted = false;
                lock (_gate) exhausted = _playlist?.IsTimeBudgetExhausted ?? false;

                if (!exhausted) continue;

                budgetHit.Cancel();
                _player.Stop();     // 取消令牌不会打断音频输出，必须显式停
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // 用户停止 / 本段已播完：正常退出
        }
    }

    /// <summary>把回调切回 UI 线程；没有 Application（单测）时直接执行。</summary>
    private static bool RunOnUi(Func<bool> action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) return action();
        if (dispatcher.CheckAccess()) return action();

        var result = false;
        dispatcher.Invoke(() => result = action());
        return result;
    }

    private bool IsCurrent(int session)
    {
        lock (_gate) return _session == session;
    }

    private static string StopReasonText(TtsStopReason reason) => reason switch
    {
        TtsStopReason.BookEnd => "已到全书末尾",
        TtsStopReason.MinutesReached => "已到设定分钟数",
        TtsStopReason.ChaptersReached => "已到设定章节数",
        TtsStopReason.EndOfChapters => "章节序列走完",
        TtsStopReason.Failed => "合成/播放失败",
        _ => "用户停止",
    };
}
