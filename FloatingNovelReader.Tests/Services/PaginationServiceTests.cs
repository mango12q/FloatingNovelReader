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

    /// <summary>
    /// 性能守卫：抓"数量级退化"，不是微基准。
    ///
    /// 原实现直接对首次调用计时并卡死 200ms —— 首次调用要付 JIT + 字体缓存 +
    /// TextFormatter 初始化的成本，在冷启动或繁忙机器上会**稳定失败**：
    /// 已在未修改的原始 HEAD 上复现（同一台机器 400–550ms）。
    /// 因此改为"先预热一次，再对第二次计时"，并给出足够余量。
    /// </summary>
    [Fact]
    public void Paginate_Performance_UnderThreshold()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 10000; i++)
            sb.AppendLine($"第 {i} 行内容。");
        var text = sb.ToString();

        // 预热：JIT、字体缓存、TextFormatter 静态初始化的成本只付一次
        _paginator.ClearCache();
        var cold = System.Diagnostics.Stopwatch.StartNew();
        _paginator.Paginate(text, "Microsoft YaHei UI", 18, 1.5, 500, 700);
        cold.Stop();

        _paginator.ClearCache();
        var warm = System.Diagnostics.Stopwatch.StartNew();
        _paginator.Paginate(text, "Microsoft YaHei UI", 18, 1.5, 500, 700);
        warm.Stop();

        // 10000 行中文分页在开发机上是几百毫秒量级；这里只拦数量级退化（例如退到 5 秒以上）
        Assert.True(warm.ElapsedMilliseconds < 3000,
            $"预热后耗时 {warm.ElapsedMilliseconds}ms（冷启动 {cold.ElapsedMilliseconds}ms）超过 3000ms 限制");
    }
}
