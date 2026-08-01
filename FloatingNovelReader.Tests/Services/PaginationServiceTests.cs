using System;
using System.Linq;
using System.Text;
using FloatingNovelReader.Services;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// PaginationService 单元测试。
/// </summary>
public class PaginationServiceTests
{
    private readonly PaginationService _paginator = new();

    [Fact]
    public void Paginate_ShortText_SinglePage()
    {
        var text = "短文本";
        var pages = _paginator.Paginate(text, "Microsoft YaHei UI", 18, 1.5, 500, 700);
        Assert.Single(pages);
    }

    [Fact]
    public void Paginate_LongText_MultiplePages()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 1000; i++)
            sb.AppendLine($"这是第 {i} 行内容。这是一段用于测试分页的文本。");
        var text = sb.ToString();

        var pages = _paginator.Paginate(text, "Microsoft YaHei UI", 18, 1.5, 500, 700);
        Assert.True(pages.Count > 1);
    }

    [Fact]
    public void Paginate_EmptyText_ReturnsOneEmptyPage()
    {
        var pages = _paginator.Paginate("", "Microsoft YaHei UI", 18, 1.5, 500, 700);
        Assert.Single(pages);
    }

    [Fact]
    public void Paginate_SmallerArea_MorePages()
    {
        var text = string.Concat(Enumerable.Repeat("这是一段测试内容。", 200));
        var big = _paginator.Paginate(text, "Microsoft YaHei UI", 18, 1.5, 800, 800);
        var small = _paginator.Paginate(text, "Microsoft YaHei UI", 18, 1.5, 300, 300);
        Assert.True(small.Count > big.Count);
    }

    [Fact]
    public void Paginate_PagesConcat_EqualsOriginal()
    {
        // 分页结果拼接必须与原文字符级一致（不丢字、不重字、不乱序），含 \r\n / \n / 中英混排
        var sb = new StringBuilder();
        for (int i = 0; i < 500; i++)
            sb.Append($"第 {i} 行 mixed English 内容123。\r\n");
        sb.Append("结尾不带换行");
        var text = sb.ToString();

        var pages = _paginator.Paginate(text, "Microsoft YaHei UI", 18, 1.5, 476, 684);
        var joined = string.Concat(pages.Select(p => text.Substring(p.Start, p.Length)));
        Assert.Equal(text, joined);
    }

    [Fact]
    public void Paginate_Performance_Under200ms()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 10000; i++)
            sb.AppendLine($"第 {i} 行内容。");
        var text = sb.ToString();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _paginator.Paginate(text, "Microsoft YaHei UI", 18, 1.5, 500, 700);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 200, $"耗时 {sw.ElapsedMilliseconds}ms 超过 200ms 限制");
    }
}
