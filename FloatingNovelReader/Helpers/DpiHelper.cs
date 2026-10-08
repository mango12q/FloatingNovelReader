using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Serilog;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// 对话框的高分屏（DPI）适配。
///
/// 为什么不能只靠 WPF 默认行为：
///   1. WPF 的 Width/Height 是 DIP，本身会随 DPI 缩放，但窗口尺寸是写死的，
///      在 150%/200% 缩放的小屏上会超出工作区（目录窗口 700 DIP 高，1280×680 的工作区放不下）；
///   2. WindowStartupLocation=CenterOwner 在不同 DPI 的多显示器环境下按设备像素算中心，
///      弹窗会偏出所属窗口甚至跑到别的屏幕；
///   3. 窗口被拖到另一块 DPI 不同的屏幕时不会重新夹回工作区。
///
/// 这里统一按「目标显示器 DPI + 该显示器工作区」计算尺寸与位置（全部换算成 DIP 后再写回 WPF 属性），
/// 并在 DpiChanged 时重新适配一次。
/// </summary>
public static class DpiHelper
{
    public const double BaseDpi = 96.0;

    /// <summary>视觉元素的 DPI 缩放（未接入可视化树时返回 1.0）。</summary>
    public static double GetScale(Visual? visual)
    {
        if (visual == null) return 1.0;
        try
        {
            return VisualTreeHelper.GetDpi(visual).DpiScaleX;
        }
        catch
        {
            return 1.0;
        }
    }

    /// <summary>窗口所在显示器的 DPI 缩放：优先问 Win32（最准），失败退回 WPF。</summary>
    public static double GetWindowScale(Window? window)
    {
        if (window == null) return 1.0;

        var hwnd = GetHandle(window);
        var scale = Win32Helper.GetDpiScaleForWindow(hwnd);
        if (scale > 0) return scale;

        scale = GetScale(window);
        return scale > 0 ? scale : 1.0;
    }

    /// <summary>
    /// 按目标显示器的 DPI 与工作区调整窗口尺寸与位置。
    /// 适用于对话框（owner 居中）与主窗口（owner 为空时在工作区居中）。
    /// </summary>
    /// <param name="dialog">待调整的窗口（可在 Show 之前调用，SourceInitialized 时机最佳）</param>
    /// <param name="owner">所属窗口，用于居中；为空则在工作区居中</param>
    /// <param name="preferredWidth">首选宽（DIP）</param>
    /// <param name="preferredHeight">首选高（DIP）</param>
    /// <param name="minWidth">最小宽（DIP）</param>
    /// <param name="minHeight">最小高（DIP）</param>
    /// <param name="recenter">true = 重新在 owner 上居中；false = 保持当前中心（DPI 变化时用）</param>
    public static DialogSizing.Result ApplyAdaptiveLayout(
        Window dialog,
        Window? owner,
        double preferredWidth,
        double preferredHeight,
        double minWidth,
        double minHeight,
        bool recenter = true)
    {
        // 以 owner 所在显示器为准：弹窗最终会落在 owner 的那块屏幕上
        var anchorHwnd = GetHandle(owner);
        if (anchorHwnd == IntPtr.Zero) anchorHwnd = GetHandle(dialog);

        double scale = Win32Helper.GetDpiScaleForWindow(anchorHwnd);
        if (scale <= 0) scale = GetScale(owner ?? dialog);
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale)) scale = 1.0;

        var workArea = ResolveWorkArea(anchorHwnd, scale);
        var anchor = ResolveAnchor(dialog, owner, recenter);

        var result = DialogSizing.Compute(
            preferredWidth, preferredHeight, minWidth, minHeight, workArea, anchor);

        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.MinWidth = result.MinWidth;
        dialog.MinHeight = result.MinHeight;
        dialog.Width = result.Width;
        dialog.Height = result.Height;
        dialog.Left = result.Left;
        dialog.Top = result.Top;

        Log.Debug("对话框 DPI 适配: scale={Scale:F2} workArea={Work} size={W:F0}x{H:F0} pos=({L:F0},{T:F0})",
            scale, workArea, result.Width, result.Height, result.Left, result.Top);

        return result;
    }

    /// <summary>目标显示器工作区，换算成 DIP；拿不到 Win32 信息时退回主屏工作区。</summary>
    private static Rect ResolveWorkArea(IntPtr hwnd, double scale)
    {
        var rect = Win32Helper.GetMonitorWorkArea(hwnd);
        if (rect is { } r && r.Width > 0 && r.Height > 0)
            return new Rect(r.Left / scale, r.Top / scale, r.Width / scale, r.Height / scale);

        var fallback = SystemParameters.WorkArea;
        return fallback.Width > 0 && fallback.Height > 0
            ? fallback
            : new Rect(0, 0, 1024, 768);
    }

    /// <summary>居中基准：owner 的当前矩形；recenter=false 时用对话框自身矩形（保持不动）。</summary>
    private static Rect? ResolveAnchor(Window dialog, Window? owner, bool recenter)
    {
        if (owner != null && IsUsable(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight))
            return new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight);

        if (!recenter && IsUsable(dialog.Left, dialog.Top, dialog.ActualWidth, dialog.ActualHeight))
            return new Rect(dialog.Left, dialog.Top, dialog.ActualWidth, dialog.ActualHeight);

        return null;
    }

    private static bool IsUsable(double left, double top, double width, double height) =>
        !double.IsNaN(left) && !double.IsNaN(top) &&
        !double.IsInfinity(left) && !double.IsInfinity(top) &&
        width > 0 && height > 0;

    private static IntPtr GetHandle(Window? window)
    {
        if (window == null) return IntPtr.Zero;
        try
        {
            return new WindowInteropHelper(window).Handle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
}
