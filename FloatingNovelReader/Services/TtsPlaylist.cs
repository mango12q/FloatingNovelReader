using System;
using System.Collections.Generic;
using System.Globalization;

namespace FloatingNovelReader.Services;

/// <summary>
/// 一次朗读会话的**播放列表状态机**（纯逻辑，不知道 NAudio / WebSocket / WPF）。
///
/// 它回答两个问题，TtsService 只负责照着做：
///   1. 这一段播完之后还要不要继续？<see cref="AdvanceSegment"/>
///      → 下一段 / 本章播完 / 停止条件达成；
///   2. 这一章播完之后还要不要换章？<see cref="AdvanceChapter"/>
///      → 下一章 / 停在章末 / 停在全书末。
///
/// 跨章推进依赖调用方传入的 <c>canAdvanceChapter</c>（即宿主能不能给出下一章），
/// 所以"最后一章播完停在全书末"与"第 N 章播完停在章末"是同一条路径的两个出口。
///
/// 状态栏要用的计数（第几段、第几章/共几章、剩余时间）也都在这里，
/// 保证界面文案与停止判定共用同一份状态，不会互相矛盾。
/// </summary>
public sealed class TtsPlaylist
{
    private readonly List<string> _segments = new();
    private readonly List<TimeSpan> _durations = new();
    private int _segmentIndex;

    /// <summary>本次朗读已上报的音频总时长（跨章累计，见 <see cref="PlayedDuration"/>）。</summary>
    private TimeSpan _reportedTotal;

    public TtsPlaylist(TtsStopCondition condition)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public TtsStopCondition Condition { get; }

    /// <summary>当前章节的片段列表（已清洗 + 转义）。</summary>
    public IReadOnlyList<string> Segments => _segments;

    /// <summary>当前片段下标（0 基）。</summary>
    public int SegmentIndex => _segmentIndex;

    public int SegmentCount => _segments.Count;

    /// <summary>本章片段是否已全部播完。</summary>
    public bool ChapterFinished => _segmentIndex >= _segments.Count;

    public bool HasSegments => _segments.Count > 0;

    /// <summary>已完整播完的章数——唯一的真相在 <see cref="ChaptersStopCondition"/> 里，这里只是读取。</summary>
    public int FinishedChapters =>
        Condition is ChaptersStopCondition chapters ? chapters.FinishedChapters : 0;

    /// <summary>状态栏进度文案，例如「正在朗读：第 3/28 段 · 剩余 12:34」。</summary>
    public string StatusText
    {
        get
        {
            if (_segments.Count == 0) return "正在朗读…";

            var head = string.Format(
                CultureInfo.InvariantCulture,
                "正在朗读：第 {0}/{1} 段",
                Math.Min(_segmentIndex + 1, _segments.Count),
                _segments.Count);

            if (Condition is ChaptersStopCondition chapters)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} · 第 {1}/{2} 章",
                    head,
                    chapters.CurrentChapterNumber,
                    chapters.MaxChapters);
            }

            var remaining = Condition.RemainingText;
            return remaining is null ? head : head + " · " + remaining;
        }
    }

    /// <summary>
    /// 装载本章的片段（后续用 <see cref="RecordSegmentDuration"/> 补时长），片段游标归零。
    /// <see cref="_reportedTotal"/> 与分钟预算**刻意不清零**——它们是整场朗读的累计值。
    /// </summary>
    public void LoadChapter(IReadOnlyList<string> segments)
    {
        _segments.Clear();
        _durations.Clear();
        if (segments != null) _segments.AddRange(segments);
        _segmentIndex = 0;
    }

    /// <summary>
    /// 记录当前片段的真实音频时长（mp3 解码结果），播完立刻调用。
    ///
    /// 「朗读 N 分钟」的预算与状态栏「剩余 mm:ss」都来自这里登记的时长，
    /// 而不是墙钟时间——否则合成慢的时候 N 分钟会被网络悄悄吃掉。
    /// 同一个下标重复调用以**最后一次**为准（不会重复累加）。
    ///
    /// 注意：这里**只登记、不判定**。预算的推进在 <see cref="AdvanceSegment"/> 里通过
    /// <see cref="TtsStopCondition.OnSegmentFinished"/> 完成，避免同一段被计两次。
    /// </summary>
    public void RecordSegmentDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        if (_segmentIndex >= _segments.Count) return;

        // 同一段可能被上报两次（重试/重放）：先扣掉旧值再加新值，保证"每段只算一次"
        while (_durations.Count <= _segmentIndex) _durations.Add(TimeSpan.Zero);
        _reportedTotal += duration - _durations[_segmentIndex];
        if (_reportedTotal < TimeSpan.Zero) _reportedTotal = TimeSpan.Zero;
        _durations[_segmentIndex] = duration;
    }

    /// <summary>
    /// 本次朗读已上报的音频总时长。
    ///
    /// ⚠️ 这是**跨章**累计的：换章会用 <see cref="LoadChapter"/> 清空本章的时长表，
    /// 若只按本章求和，"朗读 N 分钟"的预算会在换章时归零（倒计时跳回满格）。
    /// </summary>
    public TimeSpan PlayedDuration => _reportedTotal;

    /// <summary>
    /// 当前片段播完之后的去向。
    ///
    /// 先把这一段的真实音频时长并入预算，再问停止条件"到点了吗"，最后才推进游标：
    /// 顺序不能反——"第 N 段刚好用完整段预算"时必须报 StopConditionMet 而不是 NextSegment。
    /// </summary>
    public TtsAdvance AdvanceSegment()
    {
        if (ChapterFinished) return TtsAdvance.ChapterFinished;

        // 预算推进的**唯一**入口：秒表 + 已上报时长（CommitMinuteClock 内部只增不减）。
        CommitMinuteClock();

        _segmentIndex++;

        // 注意用 IsStopReached 而不是"让条件自己按段累加"：时长归属只在播放列表这一处，
        // 条件类只读不写，避免同一段被计两次。
        if (Condition.IsStopReached) return TtsAdvance.StopConditionMet;

        return _segmentIndex >= _segments.Count ? TtsAdvance.ChapterFinished : TtsAdvance.NextSegment;
    }

    // ── 「朗读 N 分钟」的实时时钟 ──

    /// <summary>
    /// 当前这一段的播放时钟（仅"从当前开始朗读"的分钟模式使用）。
    ///
    /// 为什么需要它：预算是按**真实音频长度**累计的，而那个数字只有等这一段
    /// 播完才知道。问题是一段音频可能有 40 秒，用户设了 1 分钟，那么倒计时会
    /// 在播完第 1 段后跳到"剩余 00:20"，第 2 段还要再放 40 秒才到点——
    /// 用户看到的是"已经 00:00 了声音还在响"。
    ///
    /// 所以播放期间额外跑一个秒表：到点就把当前这一段**立即停掉**（仍然停在段末），
    /// 而不是等整段放完。段与段之间的合成间隙不计入（那时秒表是停的）。
    /// </summary>
    private readonly System.Diagnostics.Stopwatch _minuteClock = new();

    /// <summary>开始播放一段时启动分钟时钟（等待合成的时间不计入）。</summary>
    public void StartMinuteClock() => _minuteClock.Start();

    /// <summary>本段播放结束（或被中断）时停表。</summary>
    public void StopMinuteClock() => _minuteClock.Stop();

    /// <summary>把已播时长与正在跑的秒表合起来算，得到"到今天为止"的用量。</summary>
    private TimeSpan ObservedElapsed => PlayedDuration + _minuteClock.Elapsed;

    /// <summary>
    /// 分钟预算是否已经在**本段播放途中**用尽（用于中途收口）。
    /// 非分钟模式永远返回 false。
    /// </summary>
    public bool IsTimeBudgetExhausted =>
        Condition is MinutesStopCondition minutes && ObservedElapsed >= minutes.Budget;

    /// <summary>把秒表里的时间并入预算（段播完时调用，保证累计值单调且不重复计算）。</summary>
    public void CommitMinuteClock()
    {
        if (Condition is not MinutesStopCondition minutes) return;
        var observed = ObservedElapsed;
        if (observed > minutes.Elapsed) minutes.SetElapsed(observed);
    }
    /// <summary>
    /// 本章播完之后决定下一步。三条出口：
    ///   - <see cref="TtsAdvance.StopConditionMet"/>：已达停止条件（N 章到点），停在章末，**不换章**；
    ///   - <see cref="TtsAdvance.StopChapterBoundary"/>：章节序列走完（全书末），停在末章末页；
    ///   - <see cref="TtsAdvance.NextChapter"/>：换下一章继续。
    /// </summary>
    public TtsAdvance AdvanceChapter(bool canAdvanceChapter)
    {
        Condition.OnChapterFinished();

        if (Condition.IsStopReached) return TtsAdvance.StopConditionMet;
        return canAdvanceChapter ? TtsAdvance.NextChapter : TtsAdvance.StopChapterBoundary;
    }
}

/// <summary>一段 / 一章播完之后的去向。</summary>
public enum TtsAdvance
{
    /// <summary>还有下一段，继续播。</summary>
    NextSegment,

    /// <summary>本章播完，可以换下一章。</summary>
    NextChapter,

    /// <summary>停止条件达成，停在这里。</summary>
    StopConditionMet,

    /// <summary>章节序列走完（全书末），停在这里。</summary>
    StopChapterBoundary,

    /// <summary>本章片段已播完，调用方应改用 <see cref="TtsPlaylist.AdvanceChapter"/>。</summary>
    ChapterFinished,
}
