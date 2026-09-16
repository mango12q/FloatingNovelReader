using System;
using System.Collections.Generic;
using FloatingNovelReader.Services;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// 朗读播放列表状态机：给定章节序列与停止条件，判断"下一段 / 下一章要不要继续"。
///
/// 用穷举式脚本模拟 TtsService 的主循环（播完一章 → 换章 → 播完一章 …），
/// 断言**最终停下来时阅读位置落在哪一章哪一页**——这正是用户看得见的行为。
/// </summary>
public class TtsPlaylistTests
{
    private static readonly TimeSpan SegmentDuration = TimeSpan.FromSeconds(30);

    private static IReadOnlyList<string> Segment(int count)
    {
        var list = new List<string>(count);
        for (var i = 0; i < count; i++) list.Add($"第 {i + 1} 段正文。");
        return list;
    }

    /// <summary>模拟一次完整朗读，返回停下来时阅读位置在哪。</summary>
    private static (int Chapter, int Page, bool ReachedEnd, int SegmentsPlayed) RunToCompletion(
        int chapterCount,
        int segmentsPerChapter,
        TtsStopCondition condition,
        int? manualFlipToPageAtSegment = null)
    {
        var playlist = new TtsPlaylist(condition);
        var chapter = 1;
        var segmentsPlayed = 0;

        while (true)
        {
            playlist.LoadChapter(Segment(segmentsPerChapter));
            var page = 0;
            var manualFlipped = false;

            while (true)
            {
                if (manualFlipToPageAtSegment is { } flipAt
                    && !manualFlipped
                    && chapter == 1
                    && playlist.SegmentIndex == flipAt)
                {
                    page = 7;          // 用户中途自己翻到第 8 页
                    manualFlipped = true;
                }

                playlist.RecordSegmentDuration(SegmentDuration);
                var advance = playlist.AdvanceSegment();
                segmentsPlayed++;

                if (advance == TtsAdvance.StopConditionMet)
                    return (chapter, page, false, segmentsPlayed);

                if (advance == TtsAdvance.NextSegment)
                {
                    page++;            // 阅读位置跟着音频翻页
                    continue;
                }

                break;                 // ChapterFinished
            }

            // 章末：停在最后一页
            page = Math.Max(page, segmentsPerChapter - 1);

            var canAdvance = chapter < chapterCount;
            var chapterAdvance = playlist.AdvanceChapter(canAdvance);

            if (chapterAdvance == TtsAdvance.StopConditionMet)
                return (chapter, page, false, segmentsPlayed);
            if (chapterAdvance == TtsAdvance.StopChapterBoundary)
                return (chapter, page, true, segmentsPlayed);

            chapter++;                 // 换章：阅读位置回到新章第 1 页
        }
    }

    // ── 书末停止 ──

    [Fact]
    public void BookEnd_PlaysEveryChapterAndStopsAtTheVeryEnd()
    {
        var (chapter, page, reachedEnd, _) = RunToCompletion(
            chapterCount: 4, segmentsPerChapter: 3, new BookEndStopCondition());

        Assert.Equal(4, chapter);
        Assert.Equal(2, page);          // 最后一章最后一页
        Assert.True(reachedEnd);
    }

    [Fact]
    public void BookEnd_SingleChapterBook_StopsAtChapterEnd()
    {
        var (chapter, page, reachedEnd, _) = RunToCompletion(
            chapterCount: 1, segmentsPerChapter: 2, new BookEndStopCondition());

        Assert.Equal(1, chapter);
        Assert.Equal(1, page);
        Assert.True(reachedEnd);
    }

    // ── N 分钟：停在段末 ──

    [Fact]
    public void Minutes_StopsAtSegmentBoundary_NotChapterBoundary()
    {
        // 每段 30s，预算 2 分钟 = 4 段；每章 3 段
        // → 第 1 章 3 段（90s）+ 第 2 章第 1 段（120s）到点
        var condition = new MinutesStopCondition(2);

        var (chapter, page, reachedEnd, segmentsPlayed) = RunToCompletion(
            chapterCount: 5, segmentsPerChapter: 3, condition);

        Assert.Equal(2, chapter);       // 换过章，证明跨章续播真的发生了
        Assert.Equal(0, page);          // 停在第 2 章第 1 段末，不是章末
        Assert.False(reachedEnd);
        Assert.Equal(4, segmentsPlayed);
        Assert.Equal(TimeSpan.FromSeconds(120), condition.Elapsed);
    }

    [Fact]
    public void Minutes_ExactMultipleOfSegments_StopsAtThatSegmentEnd()
    {
        // 预算 2 分钟 = 4 段 → 第 1 章 3 段（换章）+ 第 2 章第 1 段（120s）到点
        var condition = new MinutesStopCondition(2);

        var (chapter, page, _, segmentsPlayed) = RunToCompletion(
            chapterCount: 3, segmentsPerChapter: 3, condition);

        Assert.Equal(2, chapter);
        Assert.Equal(0, page);          // 第 2 章第 1 页
        Assert.Equal(4, segmentsPlayed);
        Assert.Equal(TimeSpan.FromSeconds(120), condition.Elapsed);
    }

    [Fact]
    public void Minutes_CountdownDecreasesByRealAudioLength()
    {
        var playlist = new TtsPlaylist(new MinutesStopCondition(1));
        playlist.LoadChapter(Segment(4));

        Assert.Equal("剩余 01:00", playlist.Condition.RemainingText);

        playlist.RecordSegmentDuration(TimeSpan.FromSeconds(31));
        Assert.Equal(TtsAdvance.NextSegment, playlist.AdvanceSegment());
        Assert.Equal("剩余 00:29", playlist.Condition.RemainingText);

        playlist.RecordSegmentDuration(TimeSpan.FromSeconds(29));
        Assert.Equal(TtsAdvance.StopConditionMet, playlist.AdvanceSegment());
        Assert.Equal("剩余 00:00", playlist.Condition.RemainingText);
    }

    [Fact]
    public void Minutes_StatusTextShowsRemainingTime()
    {
        var playlist = new TtsPlaylist(new MinutesStopCondition(30));
        playlist.LoadChapter(Segment(3));

        playlist.RecordSegmentDuration(TimeSpan.FromMinutes(17) + TimeSpan.FromSeconds(26));
        playlist.AdvanceSegment();

        Assert.Equal("正在朗读：第 2/3 段 · 剩余 12:34", playlist.StatusText);
    }

    // ── N 章：停在章末 ──

    [Fact]
    public void Chapters_StopsAtChapterEnd_NotAtNextChapterStart()
    {
        var condition = new ChaptersStopCondition(2);

        var (chapter, page, reachedEnd, _) = RunToCompletion(
            chapterCount: 6, segmentsPerChapter: 3, condition);

        Assert.Equal(2, chapter);       // 没有跳到第 3 章
        Assert.Equal(2, page);          // 第 2 章最后一页
        Assert.False(reachedEnd);
        Assert.Equal(2, condition.FinishedChapters);
    }

    [Fact]
    public void Chapters_SingleChapter_StopsAtFirstChapterEnd()
    {
        var (chapter, page, _, _) = RunToCompletion(
            chapterCount: 9, segmentsPerChapter: 2, new ChaptersStopCondition(1));

        Assert.Equal(1, chapter);
        Assert.Equal(1, page);
    }

    [Fact]
    public void Chapters_LongerThanBook_FallsBackToBookEnd()
    {
        // 设了 10 章但书只有 3 章 → 走"章节序列走完"这条出口，不是"章数到点"
        var condition = new ChaptersStopCondition(10);

        var (chapter, page, reachedEnd, _) = RunToCompletion(
            chapterCount: 3, segmentsPerChapter: 2, condition);

        Assert.Equal(3, chapter);
        Assert.Equal(1, page);
        Assert.True(reachedEnd);
    }

    [Fact]
    public void Chapters_StatusTextShowsChapterProgress()
    {
        var playlist = new TtsPlaylist(new ChaptersStopCondition(10));
        playlist.LoadChapter(Segment(28));

        Assert.Equal("正在朗读：第 1/28 段 · 第 1/10 章", playlist.StatusText);
    }

    // ── 中途用户干预 ──

    [Fact]
    public void UserFlipsPageMidChapter_StopReasonAndCountersAreUnaffected()
    {
        // 用户在朗读途中自己翻到第 8 页：这不该改变"还剩几章/几段"，
        // 也不该让朗读停在别的地方——换章后位置由朗读重置回新章开头。
        var condition = new ChaptersStopCondition(2);

        var (chapter, page, _, _) = RunToCompletion(
            chapterCount: 5,
            segmentsPerChapter: 4,
            condition,
            manualFlipToPageAtSegment: 1);

        Assert.Equal(2, chapter);
        Assert.Equal(3, page);          // 第 2 章最后一页（章末），不是用户翻到的第 8 页
        Assert.Equal(2, condition.FinishedChapters);
    }

    [Fact]
    public void UserStopsMidChapter_PlaylistHoldsCurrentSegmentForStatusBar()
    {
        // 「停止」由 TtsService 取消令牌实现，播放列表本身只是停在原处：
        // 状态栏要读的就是"停在第几段"，所以这里断言游标没有被清掉。
        var playlist = new TtsPlaylist(new BookEndStopCondition());
        playlist.LoadChapter(Segment(5));

        playlist.RecordSegmentDuration(SegmentDuration);
        playlist.AdvanceSegment();
        playlist.RecordSegmentDuration(SegmentDuration);
        playlist.AdvanceSegment();

        Assert.Equal(2, playlist.SegmentIndex);              // 第 3 段（0 基 2）
        Assert.Equal("正在朗读：第 3/5 段", playlist.StatusText);
        Assert.False(playlist.ChapterFinished);
    }

    // ── 边界 ──

    [Fact]
    public void EmptyChapter_IsReportedAsFinishedImmediately()
    {
        var playlist = new TtsPlaylist(new BookEndStopCondition());
        playlist.LoadChapter(Array.Empty<string>());

        Assert.False(playlist.HasSegments);
        Assert.True(playlist.ChapterFinished);
        Assert.Equal(TtsAdvance.ChapterFinished, playlist.AdvanceSegment());
        Assert.Equal("正在朗读…", playlist.StatusText);
    }

    [Fact]
    public void RecordSegmentDuration_IsIdempotentPerSegment()
    {
        var playlist = new TtsPlaylist(new MinutesStopCondition(1));
        playlist.LoadChapter(Segment(3));

        // 同一段上报两次（重试/重放）不能把预算算成两倍
        playlist.RecordSegmentDuration(TimeSpan.FromSeconds(20));
        playlist.RecordSegmentDuration(TimeSpan.FromSeconds(20));
        Assert.Equal(TimeSpan.FromSeconds(20), playlist.PlayedDuration);

        playlist.AdvanceSegment();
        Assert.Equal(TimeSpan.FromSeconds(40), playlist.Condition.Remaining);
    }

    [Fact]
    public void AdvanceSegment_NegativeDurationIsIgnored()
    {
        var playlist = new TtsPlaylist(new MinutesStopCondition(1));
        playlist.LoadChapter(Segment(2));

        playlist.RecordSegmentDuration(TimeSpan.FromSeconds(-5));
        playlist.AdvanceSegment();

        Assert.Equal(TimeSpan.FromMinutes(1), playlist.Condition.Remaining);
    }

    [Fact]
    public void PlayedDuration_SumsEveryRecordedSegment()
    {
        var playlist = new TtsPlaylist(new MinutesStopCondition(10));
        playlist.LoadChapter(Segment(3));

        playlist.RecordSegmentDuration(TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.FromSeconds(10), playlist.PlayedDuration);

        playlist.AdvanceSegment();
        playlist.RecordSegmentDuration(TimeSpan.FromSeconds(15));
        Assert.Equal(TimeSpan.FromSeconds(25), playlist.PlayedDuration);
    }

    [Fact]
    public void LoadChapter_ResetsSegmentCursorButKeepsChapterCount()
    {
        var playlist = new TtsPlaylist(new ChaptersStopCondition(3));
        playlist.LoadChapter(Segment(2));
        playlist.RecordSegmentDuration(SegmentDuration);
        playlist.AdvanceSegment();
        playlist.RecordSegmentDuration(SegmentDuration);
        playlist.AdvanceSegment();

        Assert.Equal(TtsAdvance.NextChapter, playlist.AdvanceChapter(canAdvanceChapter: true));

        playlist.LoadChapter(Segment(5));
        Assert.Equal(0, playlist.SegmentIndex);
        Assert.Equal(5, playlist.SegmentCount);
        Assert.Equal(1, playlist.FinishedChapters);   // 上一章已记入已播完章数
    }

    [Fact]
    public void MinutesBudget_SurvivesChapterChange()
    {
        // 换章时本章时长表会清空，预算**不能**跟着归零，否则倒计时会跳回满格
        var condition = new MinutesStopCondition(10);
        var playlist = new TtsPlaylist(condition);

        playlist.LoadChapter(Segment(2));
        playlist.RecordSegmentDuration(TimeSpan.FromMinutes(3));
        playlist.AdvanceSegment();
        playlist.RecordSegmentDuration(TimeSpan.FromMinutes(3));
        playlist.AdvanceSegment();
        Assert.Equal(TtsAdvance.NextChapter, playlist.AdvanceChapter(canAdvanceChapter: true));

        playlist.LoadChapter(Segment(2));
        Assert.Equal(TimeSpan.FromMinutes(6), condition.Elapsed);
        Assert.Equal("剩余 04:00", condition.RemainingText);

        playlist.RecordSegmentDuration(TimeSpan.FromMinutes(1));
        playlist.AdvanceSegment();
        Assert.True(condition.Elapsed >= TimeSpan.FromMinutes(7));
    }

    // ── 分钟预算的实时收口（秒表） ──

    [Fact]
    public void MinuteClock_CommitsElapsedSoLongSegmentCanStopMidPlayback()
    {
        // 一段 40 秒的音频、预算 1 分钟：应该在第 2 段播放**途中**就停，
        // 而不是等整段放完——否则用户会看到倒计时 00:00 却还在响 20 秒。
        var playlist = new TtsPlaylist(new MinutesStopCondition(1));
        playlist.LoadChapter(Segment(5));

        // 第 1 段播完 30 秒
        playlist.StartMinuteClock();
        playlist.StopMinuteClock();
        playlist.RecordSegmentDuration(TimeSpan.FromSeconds(30));
        playlist.CommitMinuteClock();
        Assert.Equal(TtsAdvance.NextSegment, playlist.AdvanceSegment());
        Assert.False(playlist.IsTimeBudgetExhausted);

        // 第 2 段开始播放（秒表在跑），此刻还没到点
        playlist.StartMinuteClock();
        Assert.False(playlist.IsTimeBudgetExhausted);

        // 这一段实际放了 31 秒后被收口停掉
        playlist.StopMinuteClock();
        playlist.RecordSegmentDuration(TimeSpan.FromSeconds(31));
        playlist.CommitMinuteClock();

        Assert.True(playlist.IsTimeBudgetExhausted);
        Assert.Equal("剩余 00:00", playlist.Condition.RemainingText);
    }

    [Fact]
    public void MinuteClock_NotStartedMeansNoBudgetConsumption()
    {
        // 秒表没开（正在等合成、没在出声）时不能消耗预算
        var playlist = new TtsPlaylist(new MinutesStopCondition(1));
        playlist.LoadChapter(Segment(3));

        Assert.False(playlist.IsTimeBudgetExhausted);
        Assert.Equal(TimeSpan.FromMinutes(1), playlist.Condition.Remaining);
    }

    [Fact]
    public void MinuteClock_IsIgnoredByNonMinuteModes()
    {
        var playlist = new TtsPlaylist(new ChaptersStopCondition(2));
        playlist.LoadChapter(Segment(3));
        playlist.StartMinuteClock();

        Assert.False(playlist.IsTimeBudgetExhausted);

        playlist.StopMinuteClock();
        playlist.CommitMinuteClock();
        Assert.Null(playlist.Condition.Remaining);
    }

    [Fact]
    public void MinuteClock_CommitIsMonotonic()
    {
        // CommitMinuteClock 只增不减：换章清空时长表时不能把已用时间抹掉
        var condition = new MinutesStopCondition(10);
        var playlist = new TtsPlaylist(condition);
        playlist.LoadChapter(Segment(2));

        playlist.StartMinuteClock();
        playlist.StopMinuteClock();
        playlist.RecordSegmentDuration(TimeSpan.FromMinutes(4));
        playlist.CommitMinuteClock();
        Assert.True(condition.Elapsed >= TimeSpan.FromMinutes(4));
        Assert.True(condition.Elapsed < TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(5));

        playlist.LoadChapter(Segment(2));            // 换章：PlayedDuration 归零
        playlist.CommitMinuteClock();
        Assert.True(condition.Elapsed >= TimeSpan.FromMinutes(4));
    }

    [Fact]
    public void AdvanceChapter_OnLastChapterWithoutNext_ReportsChapterBoundary()
    {
        var playlist = new TtsPlaylist(new BookEndStopCondition());
        playlist.LoadChapter(Segment(1));

        Assert.Equal(TtsAdvance.StopChapterBoundary, playlist.AdvanceChapter(canAdvanceChapter: false));
    }
}
