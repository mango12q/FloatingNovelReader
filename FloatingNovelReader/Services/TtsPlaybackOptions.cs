using System;

namespace FloatingNovelReader.Services;

/// <summary>
/// 「从当前开始朗读」的停止条件。
///
/// 三个菜单入口（从当前开始 / 朗读 N 分钟 / 朗读 N 章）对应**同一个底层命令**
/// <c>ReaderViewModel.SpeakFromHere(TtsStopMode)</c>，只有这里的 mode 不同，
/// 因此三种模式共享合成与播放链路，互不冲突。
/// </summary>
public enum TtsStopMode
{
    /// <summary>无停止条件：从当前章播到全书末，最后一章播完停在全书末。</summary>
    BookEnd,

    /// <summary>朗读 N 分钟：到点自动停，**停在段末**。</summary>
    Minutes,

    /// <summary>朗读 N 章：播完 N 章自动停，**停在章末**。</summary>
    Chapters,
}

/// <summary>朗读会话结束的原因（决定状态栏文案，也决定阅读位置停在哪）。</summary>
public enum TtsStopReason
{
    /// <summary>播完第 N 章，停在章末。</summary>
    ChaptersReached,

    /// <summary>朗读时长到达设定值，停在段末。</summary>
    MinutesReached,

    /// <summary>最后一章播完，停在全书末。</summary>
    BookEnd,

    /// <summary>停止条件还没到，但章节序列走完了（宿主拿不到下一章）。</summary>
    EndOfChapters,

    /// <summary>用户主动停止 / 开始新的朗读 / 关闭阅读窗口。</summary>
    UserStopped,

    /// <summary>合成或播放失败。</summary>
    Failed,
}

/// <summary>一次朗读会话的参数（纯数据，便于单测与序列化）。</summary>
public sealed class TtsPlaybackOptions
{
    public TtsPlaybackOptions(TtsStopMode mode = TtsStopMode.BookEnd, int limit = 0)
    {
        Mode = mode;
        Limit = limit < 1 ? 1 : limit;
    }

    public TtsStopMode Mode { get; }

    /// <summary>Minutes 模式 = 分钟数；Chapters 模式 = 章节数；BookEnd 模式忽略。</summary>
    public int Limit { get; }

    /// <summary>按设置里的默认值构造（「从当前开始」之外的两个入口使用）。</summary>
    public static TtsPlaybackOptions FromMode(TtsStopMode mode, int maxMinutes, int maxChapters) => mode switch
    {
        TtsStopMode.Minutes => new TtsPlaybackOptions(TtsStopMode.Minutes, maxMinutes),
        TtsStopMode.Chapters => new TtsPlaybackOptions(TtsStopMode.Chapters, maxChapters),
        _ => new TtsPlaybackOptions(TtsStopMode.BookEnd, 0),
    };
}
