using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FloatingNovelReader.Models;

namespace FloatingNovelReader.Controls;

/// <summary>
/// 单页文本显示控件。根据 DisplaySettings 渲染文字。
/// </summary>
public sealed class PageTextBlock : TextBlock
{
    public static readonly DependencyProperty DisplaySettingsProperty = DependencyProperty.Register(
        nameof(DisplaySettings), typeof(DisplaySettings), typeof(PageTextBlock),
        new PropertyMetadata(null, OnDisplayChanged));

    public DisplaySettings? DisplaySettings
    {
        get => (DisplaySettings?)GetValue(DisplaySettingsProperty);
        set => SetValue(DisplaySettingsProperty, value);
    }

    public PageTextBlock()
    {
        TextWrapping = TextWrapping.Wrap;
        Padding = new Thickness(16, 12, 16, 12);
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
    }

    private static void OnDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageTextBlock tb && e.NewValue is DisplaySettings s)
        {
            tb.FontFamily = new FontFamily(s.FontFamily);
            tb.FontSize = s.FontSize;
            tb.FontWeight = s.FontBold ? FontWeights.Bold : FontWeights.Normal;
            tb.LineHeight = s.FontSize * s.LineHeight * 1.35;
            try
            {
                var fc = (Color)ColorConverter.ConvertFromString(s.GetEffectiveFontColor());
                tb.Foreground = new SolidColorBrush(fc);
            }
            catch { tb.Foreground = Brushes.Black; }
        }
    }
}
