using System;
using System.IO;
using System.Linq;
using System.Text;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// EPUB/PDF 正文缓存的落盘与偏移正确性。
/// 这里是「电子书导入后还能不能正常翻页/跳章」的关键：章节偏移必须是**字节**偏移，
/// 一旦算成字符偏移，中文正文回读就会从半个字符处开始（乱码）。
/// </summary>
public class ExtractedTextCacheTests : IDisposable
{
    private readonly string _dir;
    private readonly string _source;

    public ExtractedTextCacheTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"fnr_cache_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _source = Path.Combine(_dir, "source.epub");
        File.WriteAllText(_source, "占位（真实导入时这里是 epub/pdf 二进制）", Encoding.UTF8);
    }

    private static ExtractedContent Content(params (string Title, string Text)[] sections)
    {
        var content = new ExtractedContent { Title = "测试书", Author = "作者" };
        foreach (var (title, text) in sections)
            content.Sections.Add(new ExtractedSection(title, text));
        return content;
    }

    [Fact]
    public void Write_ChaptersReadBackExactlyAndOffsetsAreByteBased()
    {
        var content = Content(
            ("第一章 开端", "中文正文第一段。\n第二段内容。"),
            ("第二章 发展", "第三章之前的全部内容。"));

        var book = ExtractedTextCache.Write(content, _source, 123, BookFormat.Epub, _dir);

        Assert.Equal("epub", book.SourceFormat);
        Assert.Equal("测试书", book.Title);
        Assert.Equal("作者", book.Author);
        Assert.Equal("utf-8", book.Encoding);
        Assert.Equal(_source, book.FilePath);
        Assert.NotNull(book.ContentPath);
        Assert.True(File.Exists(book.ContentPath));

        var chapters = book.FlatChapters().ToList();
        Assert.Equal(2, chapters.Count);
        Assert.Equal(2, book.TotalChapters);

        // 偏移连续覆盖整份缓存
        Assert.Equal(0, chapters[0].StartPosition);
        Assert.Equal(chapters[0].EndPosition, chapters[1].StartPosition);
        Assert.Equal(new FileInfo(book.ContentPath!).Length, chapters[1].EndPosition);

        // 按偏移回读：标题行 + 正文，逐字一致（含中文）
        var first = ChapterContentReader.Read(book, chapters[0]);
        Assert.Equal("第一章 开端\n中文正文第一段。\n第二段内容。\n", first);
        Assert.Equal("第二章 发展\n第三章之前的全部内容。\n", ChapterContentReader.Read(book, chapters[1]));
    }

    [Fact]
    public void Write_UsesCachedContentPathInsteadOfSourcePath()
    {
        var content = Content(("第一章", "正文"));
        var book = ExtractedTextCache.Write(content, _source, 1, BookFormat.Pdf, _dir);

        // 阅读侧必须走缓存：源文件是二进制/压缩包，按偏移回读只会得到乱码
        Assert.NotEqual(book.FilePath, book.ContentPath);
        Assert.Equal(book.ContentPath, book.ResolveContentPath());
    }

    [Fact]
    public void Write_SameSourceAlwaysMapsToSameCacheFile()
    {
        var content = Content(("第一章", "正文"));
        var first = ExtractedTextCache.Write(content, _source, 1, BookFormat.Epub, _dir);
        var second = ExtractedTextCache.Write(content, _source, 1, BookFormat.Epub, _dir);

        Assert.Equal(first.ContentPath, second.ContentPath);
        // 重复导入覆盖同一份缓存，不会在目录里堆垃圾
        Assert.Single(Directory.GetFiles(_dir, "*.txt"));
    }

    [Fact]
    public void Write_NoSections_Throws()
    {
        var content = new ExtractedContent { Title = "空书" };

        Assert.Throws<InvalidOperationException>(
            () => ExtractedTextCache.Write(content, _source, 1, BookFormat.Epub, _dir));
    }

    [Fact]
    public void Write_TxtSourceKeepsSourceAsContentPath()
    {
        // TXT 不写缓存：偏移直接落在源文件上（老行为不能变）
        var txt = Path.Combine(_dir, "plain.txt");
        File.WriteAllText(txt, "第一章 开端\n正文\n", Encoding.UTF8);

        var book = new Book { FilePath = txt, SourceFormat = "txt" };

        Assert.Null(book.ContentPath);
        Assert.Equal(txt, book.ResolveContentPath());
    }

    [Fact]
    public void TryDeleteCache_DeletesOnlyInsideCacheDirectory()
    {
        var content = Content(("第一章", "正文"));
        var book = ExtractedTextCache.Write(content, _source, 1, BookFormat.Epub, _dir);

        // 缓存目录内的文件：删得掉
        Assert.True(ExtractedTextCache.TryDeleteCache(book.ContentPath, _dir));
        Assert.False(File.Exists(book.ContentPath));

        // 目录外的文件（数据库被改坏的情况）：必须拒绝，不能变成删用户文件的入口
        var outside = Path.Combine(Path.GetTempPath(), $"fnr_outside_{Guid.NewGuid():N}.txt");
        File.WriteAllText(outside, "用户的重要文件");
        try
        {
            Assert.False(ExtractedTextCache.TryDeleteCache(outside, _dir));
            Assert.True(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void GetCachePath_IsStablePerSourcePathAndDiffersAcrossSources()
    {
        var a = ExtractedTextCache.GetCachePath(_source, _dir);
        var b = ExtractedTextCache.GetCachePath(_source.ToUpperInvariant(), _dir);
        var c = ExtractedTextCache.GetCachePath(Path.Combine(_dir, "other.epub"), _dir);

        Assert.Equal(a, b);            // 大小写不同视为同一本书（Windows 路径）
        Assert.NotEqual(a, c);
        Assert.EndsWith(".txt", a);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }
}
