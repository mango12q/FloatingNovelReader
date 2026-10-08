using FloatingNovelReader.Helpers;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// XHTML → 纯文本（EPUB 正文抽取的核心）。
/// 关注点：块级标签变段落、实体解码、空白压缩，以及标题下标能被后续切章正确使用。
/// </summary>
public class HtmlTextConverterTests
{
    [Fact]
    public void ToPlainText_DecodesEntitiesAndStripsTags()
    {
        var html = "<p>AT&amp;T &#20013;&lt;tag&gt;</p>";

        var result = HtmlTextConverter.ToPlainText(html);

        Assert.Equal("AT&T 中<tag>", result.Text.Trim());
    }

    [Fact]
    public void ToPlainText_TurnsBlockTagsIntoParagraphBreaks()
    {
        var html = "<div>第一段</div><div>第二段</div><p>第三段</p>";

        var result = HtmlTextConverter.ToPlainText(html);

        // 块级标签之间留一个空行 = 段落分隔（阅读窗口里正好是一段一段的观感）
        Assert.Equal(new[] { "第一段", "", "第二段", "", "第三段" }, result.Text.Split('\n'));
    }

    [Fact]
    public void ToPlainText_LineBreakTagStaysInsideTheParagraph()
    {
        var html = "<p>第一行<br/>第二行</p>";

        var result = HtmlTextConverter.ToPlainText(html);

        // <br> 是段内换行，不该被当成新段落
        Assert.Equal("第一行\n第二行", result.Text.Trim());
    }

    [Fact]
    public void ToPlainText_DropsScriptStyleAndHead()
    {
        var html = "<html><head><title>不该出现</title><style>p{color:red}</style></head>" +
                   "<body><script>var a=1;</script><p>正文</p></body></html>";

        var result = HtmlTextConverter.ToPlainText(html);

        Assert.DoesNotContain("不该出现", result.Text);
        Assert.DoesNotContain("color:red", result.Text);
        Assert.DoesNotContain("var a", result.Text);
        Assert.Contains("正文", result.Text);
    }

    [Fact]
    public void ToPlainText_CollapsesWhitespaceAndBlankLineRuns()
    {
        var html = "<p>行   内\t空白</p>\n\n\n\n<p>下一段</p>";

        var result = HtmlTextConverter.ToPlainText(html);

        Assert.Equal("行 内 空白\n\n下一段", result.Text.Trim());
    }

    [Fact]
    public void ToPlainText_RecordsHeadingPositionsThatPointAtTheHeadingLine()
    {
        var html = "<h1>第一章 开端</h1><p>正文甲</p><h2>第二章 发展</h2><p>正文乙</p>";

        var result = HtmlTextConverter.ToPlainText(html);

        Assert.Equal(2, result.Headings.Count);
        Assert.Equal("第一章 开端", result.Headings[0].Title);
        Assert.Equal("第二章 发展", result.Headings[1].Title);

        // 下标必须正好落在标题那一行的行首，切章时才能按它切开
        foreach (var heading in result.Headings)
        {
            Assert.True(result.Text.AsSpan(heading.Index).StartsWith(heading.Title));
            Assert.True(heading.Index == 0 || result.Text[heading.Index - 1] == '\n');
        }
    }

    [Fact]
    public void ToPlainText_UnclosedTagsDoNotLoseFollowingText()
    {
        // 真实 EPUB 里残缺标签很常见；用 XDocument 解析会整章失败
        var html = "<p>第一段<br>第二行<p>第三段";

        var result = HtmlTextConverter.ToPlainText(html);

        Assert.Contains("第一段", result.Text);
        Assert.Contains("第二行", result.Text);
        Assert.Contains("第三段", result.Text);
    }

    [Fact]
    public void ExtractHeadingTitle_PrefersHeadingThenTitleTag()
    {
        Assert.Equal("章标题", HtmlTextConverter.ExtractHeadingTitle("<h3>章标题</h3><title>书名</title>"));
        Assert.Equal("书名", HtmlTextConverter.ExtractHeadingTitle("<p>无标题</p><title>书名</title>"));
        Assert.Null(HtmlTextConverter.ExtractHeadingTitle("<p>什么都没有</p>"));
    }

    [Fact]
    public void NormalizeLines_KeepsSingleBlankLineBetweenParagraphs()
    {
        var normalized = HtmlTextConverter.NormalizeLines("甲\n\n\n\n乙\n");

        Assert.Equal("甲\n\n乙", normalized);
    }
}
