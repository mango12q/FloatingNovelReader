using System;
using System.IO;
using System.Text;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// 章节按字节偏移读取的边界行为。
/// 对应体检报告 P1「2.4 EndPosition &lt; StartPosition 时 OverflowException 崩」。
/// </summary>
public class ChapterContentReaderTests : IDisposable
{
    private readonly string _file;
    private readonly long _secondChapterStart;

    public ChapterContentReaderTests()
    {
        _file = Path.Combine(Path.GetTempPath(), $"fnr_chap_{Guid.NewGuid():N}.txt");
        File.WriteAllText(_file, "第一章 开端\n内容A\n第二章 发展\n内容B\n", Encoding.UTF8);
        _secondChapterStart = Encoding.UTF8.GetByteCount("第一章 开端\n内容A\n");
    }

    [Fact]
    public void Read_NormalRange_ReturnsOnlyThatChapter()
    {
        var chapter = new Chapter
        {
            Title = "第二章 发展",
            StartPosition = _secondChapterStart,
            EndPosition = new FileInfo(_file).Length,
        };

        var text = ChapterContentReader.Read(_file, chapter, "utf-8");

        Assert.Contains("内容B", text);
        Assert.DoesNotContain("内容A", text);
    }

    [Fact]
    public void Read_EndBeforeStart_ReturnsEmptyInsteadOfThrowing()
    {
        // 修复前：int len = (int)(End - Start) 为负 → new byte[len] 抛 OverflowException
        var chapter = new Chapter { Title = "偏移倒置", StartPosition = 40, EndPosition = 10 };

        var text = ChapterContentReader.Read(_file, chapter, "utf-8");

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void Read_NegativeStart_ReturnsEmpty()
    {
        var chapter = new Chapter { Title = "负偏移", StartPosition = -5, EndPosition = 10 };
        Assert.Equal(string.Empty, ChapterContentReader.Read(_file, chapter, "utf-8"));
    }

    [Fact]
    public void Read_ZeroLengthRange_ReturnsEmpty()
    {
        var chapter = new Chapter { Title = "空章", StartPosition = 10, EndPosition = 10 };
        Assert.Equal(string.Empty, ChapterContentReader.Read(_file, chapter, "utf-8"));
    }

    [Fact]
    public void Read_StartBeyondEof_ReturnsEmpty()
    {
        var chapter = new Chapter
        {
            Title = "越界",
            StartPosition = new FileInfo(_file).Length + 1000,
            EndPosition = new FileInfo(_file).Length + 2000,
        };
        Assert.Equal(string.Empty, ChapterContentReader.Read(_file, chapter, "utf-8"));
    }

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }
}
