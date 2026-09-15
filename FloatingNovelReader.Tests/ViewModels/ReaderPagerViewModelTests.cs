using System;
using System.Text;
using FloatingNovelReader.Services;
using FloatingNovelReader.ViewModels;
using Xunit;

namespace FloatingNovelReader.Tests.ViewModels;

/// <summary>
/// 分页子 VM。它只认"一段章节文本"，所以可以脱离书 / 会话 / 热键单独测 ——
/// 这正是把它从 ReaderViewModel 拆出来的收益。
///
/// 注意：SettingsService 目前没有可注入的路径参数，会读取真实的 settings.json
/// （只读，不存在则用默认值）。因此对页数只做范围断言，不写死具体数字。
/// </summary>
public class ReaderPagerViewModelTests
{
    private static ReaderPagerViewModel NewPager() =>
        new(new PaginationService(), new SettingsService());

    private static string LongChapter()
    {
        var sb = new StringBuilder();
        sb.AppendLine("第1章 测试章节");
        for (var i = 1; i <= 120; i++)
            sb.AppendLine($"这是第 {i} 段落。中文分页引擎需要足够多的文字才能算出多于一页，所以这里填充一些中等长度的句子。");
        return sb.ToString();
    }

    [Fact]
    public void Recompute_EmptyText_ClearsStateAndDoesNotRaisePageChanged()
    {
        var pager = NewPager();
        var raised = 0;
        pager.PageChanged += (s, e) => raised++;

        pager.Recompute(string.Empty);

        Assert.Equal(0, pager.TotalPages);
        Assert.Equal(0, pager.PageCount);
        Assert.Equal(string.Empty, pager.PageText);
        // 空文本时不能触发 PageChanged，否则会覆盖状态栏（与拆分前行为一致）
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Recompute_LongText_ProducesMultiplePagesAndFirstPageAtTextStart()
    {
        var pager = NewPager();
        var text = LongChapter();
        var raised = 0;
        pager.PageChanged += (s, e) => raised++;

        pager.Recompute(text);

        Assert.True(pager.TotalPages > 1, $"应当分出多页，实际 {pager.TotalPages}");
        Assert.Equal(0, pager.CurrentPage);
        Assert.False(string.IsNullOrEmpty(pager.PageText));
        Assert.StartsWith("第1章 测试章节", pager.PageText);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void TryMoveNext_AdvancesUntilLastPageThenReturnsFalse()
    {
        var pager = NewPager();
        var text = LongChapter();
        pager.Recompute(text);

        var moves = 0;
        while (pager.TryMoveNext(text)) moves++;

        Assert.Equal(pager.PageCount - 1, pager.CurrentPage);
        Assert.Equal(pager.PageCount - 1, moves);
        Assert.False(pager.TryMoveNext(text));
    }

    [Fact]
    public void TryMovePrevious_StopsAtFirstPage()
    {
        var pager = NewPager();
        var text = LongChapter();
        pager.Recompute(text);
        pager.SetPage(2, text);

        Assert.True(pager.TryMovePrevious(text));
        Assert.True(pager.TryMovePrevious(text));
        Assert.False(pager.TryMovePrevious(text));
        Assert.Equal(0, pager.CurrentPage);
    }

    [Fact]
    public void Recompute_ClampsCurrentPageWhenPagesShrink()
    {
        var pager = NewPager();
        var text = LongChapter();
        pager.Recompute(text);
        pager.SetPage(pager.PageCount - 1, text);
        Assert.True(pager.CurrentPage > 0);

        // 换成极短文本 → 页数塌缩成 1，CurrentPage 必须被夹回 0
        pager.Recompute("很短的一章。");

        Assert.Equal(1, pager.TotalPages);
        Assert.Equal(0, pager.CurrentPage);
    }

    [Fact]
    public void SetPage_OutOfRange_IsClamped()
    {
        var pager = NewPager();
        var text = LongChapter();
        pager.Recompute(text);

        pager.SetPage(9999, text);
        Assert.Equal(pager.PageCount - 1, pager.CurrentPage);

        pager.SetPage(-5, text);
        Assert.Equal(0, pager.CurrentPage);
    }

    [Fact]
    public void ApplyTextAreaSize_UnchangedSize_DoesNotRaisePageChanged()
    {
        var pager = NewPager();
        var text = LongChapter();
        pager.Recompute(text);

        var raised = 0;
        pager.PageChanged += (s, e) => raised++;

        pager.ApplyTextAreaSize(pager.TextAreaWidth, pager.TextAreaHeight, text);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void PageChanged_CarriesNoAssumptionAboutChapterContent()
    {
        // 分页子 VM 不认识书/章：换一段完全不同的文本也能正常工作
        var pager = NewPager();
        pager.Recompute(LongChapter());
        var firstCount = pager.TotalPages;

        pager.Recompute("另一章。总共只有一句话。");

        Assert.True(firstCount > 0);
        Assert.Equal(1, pager.TotalPages);
        Assert.Equal("另一章。总共只有一句话。", pager.PageText);
    }
}
