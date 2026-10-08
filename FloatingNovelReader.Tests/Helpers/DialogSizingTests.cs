using System.Windows;
using FloatingNovelReader.Helpers;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// 高分屏对话框尺寸/位置计算。
/// 关键场景：150% 缩放的 1080p 屏工作区只有约 1280×680 DIP，
/// 而目录窗口首选 700 DIP 高 —— 必须被收进工作区，且位置始终在屏幕内。
/// </summary>
public class DialogSizingTests
{
    // 典型高分屏笔记本：1920×1080 @150% ⇒ 1280×680 DIP 工作区
    private static readonly Rect Laptop150 = new(0, 0, 1280, 680);

    [Fact]
    public void Compute_KeepsPreferredSizeWhenItFits()
    {
        var result = DialogSizing.Compute(500, 600, 400, 400, new Rect(0, 0, 1920, 1040));

        Assert.Equal(500, result.Width);
        Assert.Equal(600, result.Height);
        Assert.Equal(400, result.MinWidth);
        Assert.Equal(400, result.MinHeight);
    }

    [Fact]
    public void Compute_ShrinksChapterListWindowToFitHighDpiWorkArea()
    {
        // 目录窗口：500×700 首选、最小 400×500 —— 高度必须让位
        var result = DialogSizing.Compute(500, 700, 400, 500, Laptop150);

        Assert.Equal(500, result.Width);
        Assert.Equal(Laptop150.Height - 2 * DialogSizing.EdgeMargin, result.Height);   // 632
        Assert.Equal(400, result.MinWidth);
        Assert.Equal(500, result.MinHeight);
    }

    [Fact]
    public void Compute_ShrinksMinimumSizeWhenWorkAreaIsSmallerThanMinimum()
    {
        var result = DialogSizing.Compute(500, 700, 400, 500, new Rect(0, 0, 300, 200));

        Assert.Equal(300 - 2 * DialogSizing.EdgeMargin, result.MinWidth);   // 252
        Assert.Equal(DialogSizing.MinimumUsableSize, result.MinHeight);     // 160（再挤也要能用）
        Assert.Equal(result.MinWidth, result.Width);
        Assert.Equal(result.MinHeight, result.Height);
    }

    [Fact]
    public void Compute_CentersOnAnchor()
    {
        var result = DialogSizing.Compute(
            500, 600, 400, 400, new Rect(0, 0, 1920, 1040), new Rect(100, 100, 500, 700));

        Assert.Equal(100, result.Left);   // 100 + (500-500)/2
        Assert.Equal(150, result.Top);    // 100 + (700-600)/2
    }

    [Fact]
    public void Compute_ClampsPositionBackInsideWorkArea()
    {
        // owner 贴着屏幕右下角：居中后必须被夹回工作区内，标题栏不能被推出屏幕
        var work = new Rect(0, 0, 1920, 1040);
        var result = DialogSizing.Compute(500, 600, 400, 400, work, new Rect(1800, 1000, 120, 40));

        Assert.True(result.Left >= work.Left);
        Assert.True(result.Top >= work.Top);
        Assert.True(result.Left + result.Width <= work.Right);
        Assert.True(result.Top + result.Height <= work.Bottom);
    }

    [Fact]
    public void Compute_HandlesSecondaryMonitorOffsetWorkArea()
    {
        // 副屏工作区带偏移（主屏 1920 宽，副屏在其右侧）
        var work = new Rect(1920, 0, 1280, 680);
        var result = DialogSizing.Compute(500, 700, 400, 500, work, new Rect(1920, 0, 1280, 680));

        Assert.Equal(1920 + (1280 - 500) / 2.0, result.Left);
        Assert.True(result.Top >= 0);
        Assert.True(result.Left >= work.Left);
    }

    [Fact]
    public void Compute_DegenerateWorkArea_StillReturnsUsableSize()
    {
        // 拿不到显示器信息时不能算出 0 宽窗口
        var result = DialogSizing.Compute(500, 600, 400, 400, new Rect(0, 0, 0, 0));

        Assert.True(result.Width >= DialogSizing.MinimumUsableSize);
        Assert.True(result.Height >= DialogSizing.MinimumUsableSize);
    }
}
