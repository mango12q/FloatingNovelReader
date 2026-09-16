using FloatingNovelReader.Services;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// 「段在章里的比例」→ 页码。
///
/// 朗读时阅读位置要跟随音频，但音频粒度是"段"、屏幕粒度是"页"，
/// 只能按比例近似。这里把边界钉死：首页不会是 -1、末段不会越界、除零不会崩。
/// </summary>
public class SegmentPageMapperTests
{
    [Fact]
    public void FirstSegment_MapsToFirstPage()
    {
        Assert.Equal(0, SegmentPageMapper.PageForSegment(0, 10, 5));
    }

    [Fact]
    public void LastSegment_MapsToLastPage()
    {
        Assert.Equal(4, SegmentPageMapper.PageForSegment(9, 10, 5));
    }

    [Fact]
    public void SegmentCountEqualsPageCount_MapsOneToOne()
    {
        for (var i = 0; i < 4; i++)
            Assert.Equal(i, SegmentPageMapper.PageForSegment(i, 4, 4));
    }

    [Fact]
    public void MapsMonotonically()
    {
        var previous = -1;
        for (var seg = 0; seg < 28; seg++)
        {
            var page = SegmentPageMapper.PageForSegment(seg, 28, 7);
            Assert.True(page >= previous, $"第 {seg} 段映射到第 {page} 页，比上一段还靠前");
            previous = page;
        }
    }

    [Fact]
    public void NeverExceedsPageRange()
    {
        Assert.Equal(0, SegmentPageMapper.PageForSegment(0, 100, 1));
        Assert.Equal(2, SegmentPageMapper.PageForSegment(99, 100, 3));
    }

    [Theory]
    [InlineData(0, 0, 5)]
    [InlineData(0, 5, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(-3, 5, 0)]
    public void DegenerateInputs_ReturnFirstPage(int segment, int count, int pages)
    {
        Assert.Equal(0, SegmentPageMapper.PageForSegment(segment, count, pages));
    }

    [Fact]
    public void OutOfRangeSegmentIndex_IsClamped()
    {
        Assert.Equal(0, SegmentPageMapper.PageForSegment(-5, 10, 5));
        Assert.Equal(4, SegmentPageMapper.PageForSegment(999, 10, 5));
    }
}
