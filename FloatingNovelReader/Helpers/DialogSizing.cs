using System;
using System.Windows;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// 对话框尺寸/位置计算（纯函数，方便单测）。
///
/// 解决的问题：书签 / 章节目录窗口原先写死 500×600、500×700，
/// 在高分屏笔记本（例如 1920×1080 @150% ⇒ 工作区只有 1280×680 DIP）上，
/// 窗口会比屏幕还高，底部内容永远够不到；配合 CenterOwner 在不同 DPI 的显示器之间
/// 还会算错位置（WPF 的 CenterOwner 用设备像素算中心，跨 DPI 会偏）。
///
/// 这里的约定：所有输入输出都是 DIP（与 WPF 的 Width/Left 单位一致），
/// 由调用方（<see cref="DpiHelper"/>）负责把设备像素的工作区按目标显示器 DPI 换算过来。
/// </summary>
public static class DialogSizing
{
    /// <summary>窗口与屏幕边缘的最小留白（DIP）。</summary>
    public const double EdgeMargin = 24;

    /// <summary>再挤也要保证的最小可用尺寸（DIP），避免极端情况下算出 0 宽窗口。</summary>
    public const double MinimumUsableSize = 160;

    /// <summary>计算结果：宽高 + 生效的最小宽高 + 左上角坐标（都是 DIP）。</summary>
    public readonly record struct Result(
        double Width, double Height, double MinWidth, double MinHeight, double Left, double Top);

    /// <summary>
    /// 把首选尺寸夹进工作区，并在 <paramref name="anchor"/>（一般为所属窗口）上居中；
    /// anchor 为空时在工作区居中。位置会被夹回工作区内，保证标题栏始终可拖动。
    /// </summary>
    public static Result Compute(
        double preferredWidth,
        double preferredHeight,
        double minWidth,
        double minHeight,
        Rect workArea,
        Rect? anchor = null)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0)
            workArea = new Rect(0, 0, Math.Max(preferredWidth, MinimumUsableSize), Math.Max(preferredHeight, MinimumUsableSize));

        double maxWidth = Math.Max(MinimumUsableSize, workArea.Width - 2 * EdgeMargin);
        double maxHeight = Math.Max(MinimumUsableSize, workArea.Height - 2 * EdgeMargin);

        double effectiveMinWidth = Math.Min(Math.Max(0, minWidth), maxWidth);
        double effectiveMinHeight = Math.Min(Math.Max(0, minHeight), maxHeight);

        double width = Math.Clamp(preferredWidth, effectiveMinWidth, maxWidth);
        double height = Math.Clamp(preferredHeight, effectiveMinHeight, maxHeight);

        var target = anchor ?? workArea;
        double left = target.Left + (target.Width - width) / 2;
        double top = target.Top + (target.Height - height) / 2;

        left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));

        return new Result(width, height, effectiveMinWidth, effectiveMinHeight, left, top);
    }
}
