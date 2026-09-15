using System.Collections.Generic;
using System.Linq;
using FloatingNovelReader.Models;
using FloatingNovelReader.ViewModels;
using Xunit;

namespace FloatingNovelReader.Tests.ViewModels;

/// <summary>
/// 章节序列的纯查询逻辑。
/// 原来这套 "FlatChapters → FindIndex → 边界判断" 在 ReaderViewModel 里重复了 4 次、
/// 且与分页/会话/状态栏耦合，无法单测。
/// </summary>
public class ChapterSequenceTests
{
    private static Book MakeBook(params int[] chapterIds)
    {
        var book = new Book { Id = 1, Title = "测试书" };
        var volume = new Volume { Id = 1, BookId = 1, Title = "第一卷" };
        volume.Chapters = chapterIds.Select(id => new Chapter { Id = id, BookId = 1, VolumeId = 1, Title = $"第{id}章" }).ToList();
        book.Volumes = new List<Volume> { volume };
        return book;
    }

    [Fact]
    public void Flatten_NullBook_ReturnsEmpty()
    {
        Assert.Empty(ChapterSequence.Flatten(null));
    }

    [Fact]
    public void IndexOf_MissingChapter_ReturnsMinusOne()
    {
        var chapters = ChapterSequence.Flatten(MakeBook(10, 20, 30));
        Assert.Equal(-1, ChapterSequence.IndexOf(chapters, 999));
        Assert.Equal(-1, ChapterSequence.IndexOf(chapters, null));
    }

    [Fact]
    public void Next_WalksForwardAndStopsAtLast()
    {
        var chapters = ChapterSequence.Flatten(MakeBook(10, 20, 30));

        Assert.Equal(20, ChapterSequence.Next(chapters, 10)!.Id);
        Assert.Equal(30, ChapterSequence.Next(chapters, 20)!.Id);
        // 最后一章之后没有下一章
        Assert.Null(ChapterSequence.Next(chapters, 30));
    }

    [Fact]
    public void Previous_WalksBackwardAndStopsAtFirst()
    {
        var chapters = ChapterSequence.Flatten(MakeBook(10, 20, 30));

        Assert.Equal(20, ChapterSequence.Previous(chapters, 30)!.Id);
        Assert.Equal(10, ChapterSequence.Previous(chapters, 20)!.Id);
        // 第一章之前没有上一章
        Assert.Null(ChapterSequence.Previous(chapters, 10));
    }

    [Fact]
    public void Next_UnknownCurrentChapter_ReturnsNull()
    {
        var chapters = ChapterSequence.Flatten(MakeBook(10, 20));
        Assert.Null(ChapterSequence.Next(chapters, 777));
        Assert.Null(ChapterSequence.Previous(chapters, 777));
    }

    [Fact]
    public void Next_EmptySequence_ReturnsNull()
    {
        var empty = ChapterSequence.Flatten(MakeBook());
        Assert.Null(ChapterSequence.Next(empty, null));
        Assert.Null(ChapterSequence.Previous(empty, null));
    }

    [Fact]
    public void IsLast_OnlyTrueForFinalChapter()
    {
        var chapters = ChapterSequence.Flatten(MakeBook(10, 20, 30));
        Assert.False(ChapterSequence.IsLast(chapters, 10));
        Assert.True(ChapterSequence.IsLast(chapters, 30));
        Assert.False(ChapterSequence.IsLast(chapters, 999));
    }

    [Fact]
    public void ReadingPercent_FirstPageOfFirstChapter_IsNearZero()
    {
        var chapters = ChapterSequence.Flatten(MakeBook(10, 20, 30, 40));
        // (0 + 1/2) / 4 = 0.125
        Assert.Equal(0.125, ChapterSequence.ReadingPercent(chapters, 10, 0, 2), 6);
    }

    [Fact]
    public void ReadingPercent_LastPageOfLastChapter_IsOne()
    {
        var chapters = ChapterSequence.Flatten(MakeBook(10, 20));
        // (1 + 2/2) / 2 = 1.0
        Assert.Equal(1.0, ChapterSequence.ReadingPercent(chapters, 20, 1, 2), 6);
    }

    [Fact]
    public void ReadingPercent_ZeroTotalPages_DoesNotDivideByZero()
    {
        var chapters = ChapterSequence.Flatten(MakeBook(10));
        var percent = ChapterSequence.ReadingPercent(chapters, 10, 0, 0);
        Assert.InRange(percent, 0, 1);
    }

    [Fact]
    public void ReadingPercent_UnknownChapter_IsZero()
    {
        var chapters = ChapterSequence.Flatten(MakeBook(10, 20));
        Assert.Equal(0, ChapterSequence.ReadingPercent(chapters, 999, 0, 1));
    }
}
