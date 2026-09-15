using System;
using System.Windows;
using System.Windows.Media;
using FloatingNovelReader.Models;

namespace FloatingNovelReader.ViewModels;

/// <summary>
/// 一次 <see cref="DisplaySettings"/> 映射出来的渲染属性快照。
/// </summary>
public readonly record struct ReaderDisplay(
    FontFamily FontFamily,
    double FontSize,
    double LineHeight,
    FontWeight FontWeight,
    FontStyle FontStyle,
    Brush ForegroundBrush,
    Brush BackgroundBrush,
    double WindowOpacity)
{
    /// <summary>渲染侧显式行高（DIP）= 字号 × 行距系数，与分页引擎的行高一致。</summary>
    public double LineHeightPixels => FontSize * LineHeight;
}

/// <summary>
/// <see cref="DisplaySettings"/> → 渲染属性的**纯映射**。
///
/// 从 ReaderViewModel.ApplyDisplaySettings 抽出来的：原来这段 40 行逻辑和 WPF 赋值、
/// 分页重算、窗口透明度设置混在一起，无法单测。现在映射是纯函数，可以逐条断言
/// （含非法颜色字符串的兜底路径）。
/// </summary>
public static class ReaderDisplayMapper
{
    public static ReaderDisplay Map(DisplaySettings s)
    {
        var fontFamily = new FontFamily(s.FontFamily);
        var fontWeight = s.FontBold ? FontWeights.Bold : FontWeights.Normal;
        var fontStyle = s.FontItalic ? FontStyles.Italic : FontStyles.Normal;
        var background = ParseBrush(s.GetEffectiveBackground(), Brushes.White, allowNamedTransparent: true);
        var foreground = ParseBrush(s.GetEffectiveFontColor(), Brushes.Black, allowNamedTransparent: false);

        return new ReaderDisplay(
            fontFamily,
            s.FontSize,
            s.LineHeight,
            fontWeight,
            fontStyle,
            foreground,
            background,
            s.Opacity);
    }

    private static Brush ParseBrush(string value, Brush fallback, bool allowNamedTransparent)
    {
        if (allowNamedTransparent && string.Equals(value, "Transparent", StringComparison.OrdinalIgnoreCase))
            return Brushes.Transparent;

        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)!);
        }
        catch
        {
            // 非法颜色串不能让整个阅读窗口黑掉/白掉，退回默认色
            return fallback;
        }
    }
}
