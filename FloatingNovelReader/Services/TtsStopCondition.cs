using System;
using System.Globalization;

namespace FloatingNovelReader.Services;

/// <summary>
/// 朗读停止条件的纯逻辑基类。
///
/// 三种模式共用「还能不能继续」这一个问题，条件只在"什么时候说停"上不同：
///   - <see cref="BookEndStopCondition"/>    ：永不主动停（播到章末由宿主报告没有下一章）
///   - <see cref="MinutesStopCondition"/>    ：累计时长到达设定分钟数 → 停
///   - <see cref="ChaptersStopCondition"/>   ：播完第 N 章 → 停
///
/// 设计约定（踩过的坑）：
///   1. **判定**只走 <see cref="IsStopReached"/>（只读、无副作用）。
///      早期版本让播放列表调 <c>OnSegmentFinished(时长)</c>，那个钩子既判定又累加时长，
///      结果同一段被计两次（60 秒预算在第 1 段播完就被判到点）；修好时长归属后它又变成
///      恒返回 false 的空钩子，"到点"反而永远不触发。判定与推进一步到位更不容易出错。
///   2. **推进**只走 <see cref="OnChapterFinished"/>（唯一的写操作）。
///
/// 这里完全不知道播放器、网络和 UI，因此可以直接单测。
/// 时长口径见 <see cref="TtsSegmentDuration.OfMp3"/>。
/// </summary>
public abstract class TtsStopCondition
{
    /// <summary>停止条件是否已满足（只读，可反复调用）。</summary>
    public abstract bool IsStopReached { get; }

    /// <summary>
    /// 一章完整播完之后**推进**计数器（唯一的写操作）。
    /// 调用后条件是否满足请看 <see cref="IsStopReached"/>，不要依赖本方法的返回值。
    /// </summary>
    public abstract void OnChapterFinished();

    /// <summary>剩余时间；无时间概念的模式返回 null。</summary>
    public virtual TimeSpan? Remaining => null;

    /// <summary>本章之后还要再播几章；无章节数概念的模式返回 null。</summary>
    public virtual int? RemainingChapters => null;

    /// <summary>剩余时间的展示文案，例如「剩余 12:34」；无时间概念的模式返回 null。</summary>
    public virtual string? RemainingText => null;

    /// <summary>按 <see cref="TtsPlaybackOptions"/> 构造对应的条件。</summary>
    public static TtsStopCondition Create(TtsPlaybackOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Mode switch
        {
            TtsStopMode.Minutes => new MinutesStopCondition(options.Limit),
            TtsStopMode.Chapters => new ChaptersStopCondition(options.Limit),
            _ => new BookEndStopCondition(),
        };
    }

    /// <summary>把剩余时长格式化成 mm:ss（超过 1 小时则 h:mm:ss）。负数按 0 处理。</summary>
    public static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        // 向上取整到秒：剩余 0.4 秒时显示 00:01 比显示 00:00 更符合"还剩一点"的直觉
        var seconds = (long)Math.Ceiling(remaining.TotalSeconds);

        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        var secs = seconds % 60;

        return hours > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, secs)
            : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes, secs);
    }
}

/// <summary>无停止条件：从当前章一直播到全书末。</summary>
public sealed class BookEndStopCondition : TtsStopCondition
{
    public override bool IsStopReached => false;

    public override void OnChapterFinished() { }
}

/// <summary>朗读 N 分钟：累计时长到达上限即停（停在段末）。</summary>
public sealed class MinutesStopCondition : TtsStopCondition
{
    private TimeSpan _elapsed;

    public MinutesStopCondition(int minutes)
    {
        // 上限至少 1 分钟：0 或负数会变成"一开口就停"，不是用户想要的
        Minutes = minutes < 1 ? 1 : minutes;
        Budget = TimeSpan.FromMinutes(Minutes);
    }

    public int Minutes { get; }

    public TimeSpan Budget { get; }

    public TimeSpan Elapsed => _elapsed;

    public override bool IsStopReached => _elapsed >= Budget;

    public override TimeSpan? Remaining
    {
        get
        {
            var left = Budget - _elapsed;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }
    }

    public override string? RemainingText => "剩余 " + FormatRemaining(Remaining ?? TimeSpan.Zero);

    /// <summary>
    /// 由 <see cref="TtsPlaylist"/> 在记录片段时长后整体覆盖。
    ///
    /// 刻意**只增不减**：
    ///   - 它可能因重试/重放被同一段上报两次；
    ///   - 本章时长表在换章时会清空，不能让倒计时跳回满格（"剩余 12:34" 突然变 "剩余 30:00"）。
    /// </summary>
    public void SetElapsed(TimeSpan elapsed)
    {
        if (elapsed > _elapsed) _elapsed = elapsed;
    }

    /// <summary>分钟预算是全书的累计量，与"播完了几章"无关。</summary>
    public override void OnChapterFinished() { }
}

/// <summary>朗读 N 章：播完第 N 章即停（停在章末，不跳到下一章）。</summary>
public sealed class ChaptersStopCondition : TtsStopCondition
{
    public ChaptersStopCondition(int chapters)
    {
        MaxChapters = chapters < 1 ? 1 : chapters;
    }

    public int MaxChapters { get; }

    /// <summary>已完整播完的章数。</summary>
    public int FinishedChapters { get; private set; }

    /// <summary>当前正在播的这一章是第几章（1 基）。</summary>
    public int CurrentChapterNumber => Math.Min(FinishedChapters + 1, MaxChapters);

    /// <summary>本章播完后还要再播几章；0 = 本章就是最后一章。</summary>
    public override int? RemainingChapters => Math.Max(0, MaxChapters - FinishedChapters - 1);

    public override bool IsStopReached => FinishedChapters >= MaxChapters;

    public override void OnChapterFinished() => FinishedChapters++;
}
