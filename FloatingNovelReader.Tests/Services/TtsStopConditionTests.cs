using System;
using FloatingNovelReader.Services;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// 「从当前开始朗读」的三种停止条件。
///
/// 这三个条件决定了朗读什么时候停、停在段末还是章末，是本次功能的核心语义，
/// 因此独立于播放器/网络/UI 单测（<see cref="TtsStopCondition"/> 是纯逻辑）。
///
/// 判定（<see cref="TtsStopCondition.IsStopReached"/>）与推进
/// （<see cref="TtsStopCondition.OnChapterFinished"/>）是分开的：
/// 早期版本让条件类自己按段累加时长，同一段被计了两次，
/// 60 秒的预算在第 1 段播完就被判到点。时长归属现在只在 TtsPlaylist 一处。
/// </summary>
public class TtsStopConditionTests
{
    // ── 工厂 ──

    [Theory]
    [InlineData(TtsStopMode.BookEnd)]
    [InlineData(TtsStopMode.Minutes)]
    [InlineData(TtsStopMode.Chapters)]
    public void Create_ReturnsConditionMatchingMode(TtsStopMode mode)
    {
        var condition = TtsStopCondition.Create(new TtsPlaybackOptions(mode, 5));

        var expected = mode switch
        {
            TtsStopMode.Minutes => typeof(MinutesStopCondition),
            TtsStopMode.Chapters => typeof(ChaptersStopCondition),
            _ => typeof(BookEndStopCondition),
        };
        Assert.IsType(expected, condition);
    }

    [Fact]
    public void FromMode_UsesSettingsDefaults()
    {
        var options = TtsPlaybackOptions.FromMode(TtsStopMode.Minutes, maxMinutes: 45, maxChapters: 7);
        Assert.Equal(45, options.Limit);

        options = TtsPlaybackOptions.FromMode(TtsStopMode.Chapters, maxMinutes: 45, maxChapters: 7);
        Assert.Equal(7, options.Limit);

        options = TtsPlaybackOptions.FromMode(TtsStopMode.BookEnd, maxMinutes: 45, maxChapters: 7);
        Assert.Equal(TtsStopMode.BookEnd, options.Mode);
    }

    [Fact]
    public void Options_ClampsNonPositiveLimitToOne()
    {
        Assert.Equal(1, new TtsPlaybackOptions(TtsStopMode.Minutes, 0).Limit);
        Assert.Equal(1, new TtsPlaybackOptions(TtsStopMode.Minutes, -3).Limit);
    }

    // ── BookEnd：永不主动停 ──

    [Fact]
    public void BookEnd_NeverStopsOnItsOwn()
    {
        var condition = new BookEndStopCondition();

        for (var i = 0; i < 50; i++)
        {
            Assert.False(condition.IsStopReached);
            condition.OnChapterFinished();
        }
        Assert.Null(condition.Remaining);
        Assert.Null(condition.RemainingChapters);
        Assert.Null(condition.RemainingText);
    }

    // ── Minutes：到点停在段末 ──

    [Fact]
    public void Minutes_StopsWhenElapsedReachesBudget()
    {
        var condition = new MinutesStopCondition(2);   // 120s

        condition.SetElapsed(TimeSpan.FromSeconds(90));
        Assert.False(condition.IsStopReached);
        Assert.Equal(TimeSpan.FromSeconds(30), condition.Remaining);

        condition.SetElapsed(TimeSpan.FromSeconds(120));
        Assert.True(condition.IsStopReached);
        Assert.Equal(TimeSpan.Zero, condition.Remaining);
    }

    [Fact]
    public void Minutes_ElapsedIsSetNotAccumulated()
    {
        // SetElapsed 只增不减：同一段被上报两次、或换章后本章时长表归零，
        // 都不能把已经用过的时间抹掉（否则倒计时会跳回满格）。
        var condition = new MinutesStopCondition(1);

        condition.SetElapsed(TimeSpan.FromSeconds(40));
        Assert.Equal(TimeSpan.FromSeconds(40), condition.Elapsed);

        condition.SetElapsed(TimeSpan.FromSeconds(40));   // 重复上报
        Assert.Equal(TimeSpan.FromSeconds(40), condition.Elapsed);

        condition.SetElapsed(TimeSpan.FromSeconds(10));   // 变小：忽略
        Assert.Equal(TimeSpan.FromSeconds(40), condition.Elapsed);

        condition.SetElapsed(TimeSpan.FromSeconds(55));
        Assert.Equal(TimeSpan.FromSeconds(55), condition.Elapsed);
    }

    [Fact]
    public void Minutes_NonPositiveLimitBecomesOneMinute()
    {
        var condition = new MinutesStopCondition(0);
        Assert.Equal(1, condition.Minutes);

        condition.SetElapsed(TimeSpan.FromSeconds(59));
        Assert.False(condition.IsStopReached);

        condition.SetElapsed(TimeSpan.FromSeconds(60));
        Assert.True(condition.IsStopReached);
    }

    [Fact]
    public void Minutes_RemainingIsNeverNegative()
    {
        var condition = new MinutesStopCondition(1);
        condition.SetElapsed(TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.Zero, condition.Remaining);
    }

    [Fact]
    public void Minutes_ChapterBoundaryDoesNotChangeTheBudget()
    {
        // 分钟预算是全书的累计量：播完再多章也不会让它提前/延后到点
        var condition = new MinutesStopCondition(5);
        condition.SetElapsed(TimeSpan.FromMinutes(2));

        for (var i = 0; i < 20; i++) condition.OnChapterFinished();

        Assert.Equal(TimeSpan.FromMinutes(2), condition.Elapsed);
        Assert.False(condition.IsStopReached);
        Assert.Equal("剩余 03:00", condition.RemainingText);
    }

    [Theory]
    [InlineData(0, 0, "00:00")]
    [InlineData(1, 0, "01:00")]
    [InlineData(59, 0, "59:00")]
    [InlineData(60, 0, "1:00:00")]
    [InlineData(61, 0, "1:01:00")]
    [InlineData(12, 34, "12:34")]
    [InlineData(1, 30, "01:30")]
    [InlineData(6, 3, "06:03")]
    [InlineData(90, 0, "1:30:00")]
    public void FormatRemaining_ProducesClockText(int minutes, int seconds, string expected)
    {
        var remaining = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        Assert.Equal(expected, TtsStopCondition.FormatRemaining(remaining));
    }

    [Fact]
    public void FormatRemaining_RoundsSubSecondUp()
    {
        Assert.Equal("00:01", TtsStopCondition.FormatRemaining(TimeSpan.FromMilliseconds(400)));
        Assert.Equal("00:00", TtsStopCondition.FormatRemaining(TimeSpan.Zero));
        Assert.Equal("00:00", TtsStopCondition.FormatRemaining(TimeSpan.FromSeconds(-5)));
    }

    [Fact]
    public void Minutes_RemainingText_IsTheStatusBarPhrase()
    {
        var condition = new MinutesStopCondition(30);
        condition.SetElapsed(TimeSpan.FromMinutes(17) + TimeSpan.FromSeconds(26));
        Assert.Equal("剩余 12:34", condition.RemainingText);
    }

    // ── Chapters：播完 N 章停在章末 ──

    [Fact]
    public void Chapters_StopsRightAfterTheNthChapter()
    {
        var condition = new ChaptersStopCondition(3);

        condition.OnChapterFinished();                 // 第 1 章播完
        Assert.Equal(1, condition.FinishedChapters);
        Assert.False(condition.IsStopReached);

        condition.OnChapterFinished();                 // 第 2 章播完
        Assert.False(condition.IsStopReached);

        condition.OnChapterFinished();                 // 第 3 章播完 → 停
        Assert.True(condition.IsStopReached);
        Assert.Equal(3, condition.FinishedChapters);
    }

    [Fact]
    public void Chapters_StopPointIsTheChapterEnd_NotTheNextChapterStart()
    {
        // 这是"停在章末"的关键断言：条件成立时**不应该**允许换到下一章。
        var condition = new ChaptersStopCondition(1);
        condition.OnChapterFinished();
        Assert.True(condition.IsStopReached);
    }

    [Fact]
    public void Chapters_ReportsHowManyChaptersRemain()
    {
        var condition = new ChaptersStopCondition(3);

        // 第 1 章正在播：本章播完后还要再播 2 章
        Assert.Equal(2, condition.RemainingChapters);
        Assert.Equal(1, condition.CurrentChapterNumber);

        condition.OnChapterFinished();
        Assert.Equal(1, condition.RemainingChapters);
        Assert.Equal(2, condition.CurrentChapterNumber);

        condition.OnChapterFinished();
        Assert.Equal(0, condition.RemainingChapters);
        Assert.Equal(3, condition.CurrentChapterNumber);

        // 已经到点，章号不再增长
        condition.OnChapterFinished();
        Assert.Equal(3, condition.CurrentChapterNumber);
    }

    [Fact]
    public void Chapters_NonPositiveLimitBecomesOne()
    {
        var condition = new ChaptersStopCondition(0);
        Assert.Equal(1, condition.MaxChapters);

        condition.OnChapterFinished();
        Assert.True(condition.IsStopReached);
    }

    [Fact]
    public void Chapters_SegmentBoundaryNeverMatters()
    {
        // N 章只按"章"计量：段播了多久都不会让它提前停
        var condition = new ChaptersStopCondition(1);
        Assert.False(condition.IsStopReached);
        Assert.Null(condition.Remaining);
        Assert.Null(condition.RemainingText);
    }
}
