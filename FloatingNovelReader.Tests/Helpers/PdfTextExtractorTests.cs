using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Tests.Fixtures;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// PDF 文字层抽取与章节切分。
/// 说明：测试 PDF 用标准 Helvetica（ASCII 文字层），中文 PDF 的乱码/扫描版识别在 Extract 的错误分支里覆盖。
/// </summary>
public class PdfTextExtractorTests : IDisposable
{
    private readonly string _dir;

    public PdfTextExtractorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"fnr_pdf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    private string WritePdf(byte[] bytes, string name = "book.pdf")
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static IReadOnlyList<IReadOnlyList<string>> Pages(params string[][] pages) => pages;

    [Fact]
    public void Extract_SplitsChaptersOnHeadingLines()
    {
        var path = WritePdf(EbookFixtures.BuildPdf(Pages(
            new[] { "Chapter 1 Alpha", "The quick brown fox jumps over", "the lazy dog and keeps running" },
            new[] { "Chapter 2 Beta", "Second page body text here" })));

        var content = PdfTextExtractor.Extract(path);

        Assert.Equal(2, content.Sections.Count);
        Assert.Equal("Chapter 1 Alpha", content.Sections[0].Title);
        Assert.Equal("Chapter 2 Beta", content.Sections[1].Title);
        Assert.Contains("Second page body text here", content.Sections[1].Text);
    }

    [Fact]
    public void Extract_ReflowsWrappedLinesInsideAChapter()
    {
        var path = WritePdf(EbookFixtures.BuildPdf(Pages(
            new[] { "Chapter 1 Alpha", "The quick brown fox jumps over", "the lazy dog and keeps running" })));

        var content = PdfTextExtractor.Extract(path);

        // 排版换行要被接回去，否则每行都会变成一个独立段落
        Assert.Equal(
            "The quick brown fox jumps over the lazy dog and keeps running",
            content.Sections[0].Text);
    }

    [Fact]
    public void Extract_WithoutHeadings_FallsBackToPageSections()
    {
        var pages = new List<IReadOnlyList<string>>();
        for (int i = 1; i <= 6; i++)
            pages.Add(new[] { $"Page {i} body text without any heading" });

        var path = WritePdf(EbookFixtures.BuildPdf(pages));

        var content = PdfTextExtractor.Extract(path);

        // 页数少时一页一节：目录本身就是「选页表」
        Assert.Equal(6, content.Sections.Count);
        Assert.Equal("第 1 页", content.Sections[0].Title);
        Assert.Equal("第 6 页", content.Sections[5].Title);
        Assert.Contains("Page 6", content.Sections[5].Text);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(32, 1)]
    [InlineData(200, 1)]
    [InlineData(201, 2)]
    [InlineData(1000, 5)]
    [InlineData(2000, 10)]
    [InlineData(0, 1)]
    public void PagesPerSection_KeepsSectionCountWithinTheCap(int pageCount, int expected)
    {
        Assert.Equal(expected, PdfTextExtractor.PagesPerSection(pageCount));

        if (pageCount > 0)
        {
            int sections = (int)Math.Ceiling(pageCount / (double)PdfTextExtractor.PagesPerSection(pageCount));
            Assert.True(sections <= PdfTextExtractor.MaxFallbackSections,
                $"{pageCount} 页会生成 {sections} 个目录条目，超过上限");
        }
    }

    [Fact]
    public void Extract_PdfWithoutTextLayer_ThrowsScannedMessage()
    {
        var path = WritePdf(EbookFixtures.BuildEmptyTextPdf());

        var ex = Assert.Throws<InvalidOperationException>(() => PdfTextExtractor.Extract(path));

        Assert.Contains("扫描版", ex.Message);
    }

    [Fact]
    public void Extract_NotAPdf_ThrowsFriendlyMessage()
    {
        var path = Path.Combine(_dir, "fake.pdf");
        File.WriteAllText(path, "这不是 PDF");

        var ex = Assert.Throws<InvalidOperationException>(() => PdfTextExtractor.Extract(path));

        Assert.Contains("PDF", ex.Message);
    }

    [Fact]
    public void Reflow_JoinsWrappedLinesAndKeepsParagraphBreaks()
    {
        var text = "第一行没有句号结尾的句子\n第二行接着上一行\n\n新段落开始了";

        var reflowed = PdfTextExtractor.Reflow(text);

        Assert.Equal("第一行没有句号结尾的句子第二行接着上一行\n新段落开始了", reflowed);
    }

    [Fact]
    public void Reflow_DoesNotJoinAfterSentenceEndOrBeforeHeading()
    {
        var text = "这是一句话。\n这是下一句话\nChapter 3 Gamma\n正文";

        var reflowed = PdfTextExtractor.Reflow(text);

        var lines = reflowed.Split('\n');
        Assert.Equal("这是一句话。", lines[0]);
        Assert.Equal("这是下一句话", lines[1]);
        Assert.Equal("Chapter 3 Gamma", lines[2]);
    }

    [Fact]
    public void NormalizeCjkSpacing_RemovesSpacesBetweenCjkCharacters()
    {
        var normalized = PdfTextExtractor.NormalizeCjkSpacing("第 一 章 开端 and some words");

        Assert.Equal("第一章开端 and some words", normalized);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }
}
