using System;
using System.IO;
using System.Linq;
using System.Text;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Tests.Fixtures;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// EPUB 解析：元数据、spine 顺序、章节标题来源（正文标题 &gt; 目录条目）、单文件多章切分。
/// </summary>
public class EpubTextExtractorTests : IDisposable
{
    private readonly string _dir;

    public EpubTextExtractorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"fnr_epub_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    private string WriteEpub(byte[] bytes, string name = "book.epub")
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Extract_ReadsMetadataSpineOrderAndHeadingTitles()
    {
        var path = WriteEpub(EbookFixtures.BuildEpub("百合故事", "としぞう", new[]
        {
            new EbookFixtures.EpubDocument("ch1.xhtml", "<h1>序言</h1><p>甲段</p>"),
            new EbookFixtures.EpubDocument("ch2.xhtml", "<h1>第一话</h1><p>乙段</p>"),
        }));

        var content = EpubTextExtractor.Extract(path);

        Assert.Equal("百合故事", content.Title);
        Assert.Equal("としぞう", content.Author);
        Assert.Equal(2, content.Sections.Count);
        Assert.Equal("序言", content.Sections[0].Title);
        Assert.Equal("第一话", content.Sections[1].Title);
        Assert.Contains("甲段", content.Sections[0].Text);
        Assert.Contains("乙段", content.Sections[1].Text);
    }

    [Fact]
    public void Extract_UsesNavLabelWhenDocumentHasNoHeading()
    {
        var path = WriteEpub(EbookFixtures.BuildEpub("书名", "作者", new[]
        {
            new EbookFixtures.EpubDocument("a.xhtml", "<p>没有标题的正文</p>", "第一章 目录里的名字"),
        }));

        var content = EpubTextExtractor.Extract(path);

        Assert.Single(content.Sections);
        Assert.Equal("第一章 目录里的名字", content.Sections[0].Title);
        Assert.Contains("没有标题的正文", content.Sections[0].Text);
    }

    [Fact]
    public void Extract_UsesNcxLabelsForEpub2()
    {
        var path = WriteEpub(EbookFixtures.BuildEpub("书名", "作者", new[]
        {
            new EbookFixtures.EpubDocument("a.xhtml", "<p>正文</p>", "NCX 章节名"),
        }, useNcx: true));

        var content = EpubTextExtractor.Extract(path);

        Assert.Single(content.Sections);
        Assert.Equal("NCX 章节名", content.Sections[0].Title);
    }

    [Fact]
    public void Extract_SplitsOneDocumentWithManyHeadingsIntoChapters()
    {
        // 「整本书塞进一个 XHTML」的电子书很常见
        var path = WriteEpub(EbookFixtures.BuildEpub("合集", "作者", new[]
        {
            new EbookFixtures.EpubDocument("all.xhtml",
                "<h1>第一章 开端</h1><p>内容一</p><h1>第二章 发展</h1><p>内容二</p><h1>第三章 结局</h1><p>内容三</p>"),
        }));

        var content = EpubTextExtractor.Extract(path);

        Assert.Equal(3, content.Sections.Count);
        Assert.Equal(new[] { "第一章 开端", "第二章 发展", "第三章 结局" }, content.Sections.Select(s => s.Title));
        Assert.Equal("内容一", content.Sections[0].Text);
        Assert.Equal("内容三", content.Sections[2].Text);
    }

    [Fact]
    public void Extract_KeepsPreambleBeforeFirstHeading()
    {
        var path = WriteEpub(EbookFixtures.BuildEpub("书名", "作者", new[]
        {
            new EbookFixtures.EpubDocument("a.xhtml", "<p>封面文案</p><h1>第一章</h1><p>正文</p>", "封面"),
        }));

        var content = EpubTextExtractor.Extract(path);

        Assert.Equal(2, content.Sections.Count);
        Assert.Equal("封面", content.Sections[0].Title);
        Assert.Equal("封面文案", content.Sections[0].Text);
        Assert.Equal("第一章", content.Sections[1].Title);
    }

    [Fact]
    public void Extract_DropsTitleOnlySections()
    {
        var path = WriteEpub(EbookFixtures.BuildEpub("书名", "作者", new[]
        {
            new EbookFixtures.EpubDocument("title.xhtml", "<h1>书名页</h1>"),
            new EbookFixtures.EpubDocument("ch1.xhtml", "<h1>第一章</h1><p>正文</p>"),
        }));

        var content = EpubTextExtractor.Extract(path);

        Assert.Single(content.Sections);
        Assert.Equal("第一章", content.Sections[0].Title);
    }

    [Fact]
    public void Extract_NonZipFile_ThrowsFriendlyMessage()
    {
        var path = Path.Combine(_dir, "fake.epub");
        File.WriteAllText(path, "这不是 zip", Encoding.UTF8);

        var ex = Assert.Throws<InvalidOperationException>(() => EpubTextExtractor.Extract(path));

        Assert.Contains("EPUB", ex.Message);
    }

    [Fact]
    public void Extract_MissingContainer_Throws()
    {
        var path = Path.Combine(_dir, "noContainer.epub");
        using (var zip = new System.IO.Compression.ZipArchive(
                   File.Create(path), System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("dummy.txt");
            using var w = new StreamWriter(entry.Open());
            w.Write("x");
        }

        var ex = Assert.Throws<InvalidOperationException>(() => EpubTextExtractor.Extract(path));

        Assert.Contains("container.xml", ex.Message);
    }

    [Theory]
    [InlineData("OEBPS/content.opf", "ch1.xhtml", "OEBPS/ch1.xhtml")]
    [InlineData("OEBPS/content.opf", "../text/ch1.xhtml", "text/ch1.xhtml")]
    [InlineData("OEBPS/content.opf", "./ch1.xhtml#anchor", "OEBPS/ch1.xhtml")]
    [InlineData("content.opf", "ch1.xhtml", "ch1.xhtml")]
    [InlineData("OEBPS/content.opf", "Text%20One.xhtml", "OEBPS/Text One.xhtml")]
    public void ResolveHref_NormalizesRelativePaths(string basePath, string href, string expected)
    {
        Assert.Equal(expected, EpubTextExtractor.ResolveHref(basePath, href));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }
}
