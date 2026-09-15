using System.Windows;
using System.Windows.Media;
using FloatingNovelReader.Models;
using FloatingNovelReader.ViewModels;
using Xunit;

namespace FloatingNovelReader.Tests.ViewModels;

/// <summary>
/// DisplaySettings → 渲染属性的纯映射。
/// 这段逻辑原来混在 ReaderViewModel.ApplyDisplaySettings 里（WPF 赋值 + 分页重算 +
/// 窗口透明度设置一起做），无法单测；抽成纯函数后可以逐条断言，包括非法颜色串的兜底。
/// </summary>
public class ReaderDisplayMapperTests
{
    [Fact]
    public void Map_CopiesFontMetricsAndOpacity()
    {
        var s = new DisplaySettings
        {
            FontFamily = "SimSun",
            FontSize = 22,
            LineHeight = 1.8,
            FontBold = true,
            FontItalic = true,
            Opacity = 0.8,
        };

        var d = ReaderDisplayMapper.Map(s);

        Assert.Equal("SimSun", d.FontFamily.Source);
        Assert.Equal(22, d.FontSize);
        Assert.Equal(1.8, d.LineHeight);
        Assert.Equal(FontWeights.Bold, d.FontWeight);
        Assert.Equal(FontStyles.Italic, d.FontStyle);
        Assert.Equal(0.8, d.WindowOpacity);
    }

    [Fact]
    public void Map_LineHeightPixels_IsFontSizeTimesLineHeight()
    {
        var d = ReaderDisplayMapper.Map(new DisplaySettings { FontSize = 20, LineHeight = 1.5 });
        Assert.Equal(30, d.LineHeightPixels, 3);
    }

    [Fact]
    public void Map_NonBoldNonItalic_UsesNormalWeights()
    {
        var d = ReaderDisplayMapper.Map(new DisplaySettings { FontBold = false, FontItalic = false });
        Assert.Equal(FontWeights.Normal, d.FontWeight);
        Assert.Equal(FontStyles.Normal, d.FontStyle);
    }

    [Theory]
    [InlineData(BackgroundPreset.PureWhite, "#FFFFFF")]
    [InlineData(BackgroundPreset.Gray, "#808080")]
    [InlineData(BackgroundPreset.PureBlack, "#1A1A1A")]
    [InlineData(BackgroundPreset.WarmYellow, "#F4ECD8")]
    public void Map_BackgroundPresets_ProduceExpectedColor(BackgroundPreset preset, string expectedHex)
    {
        var d = ReaderDisplayMapper.Map(new DisplaySettings { BackgroundPreset = preset });

        AssertBrushColor(expectedHex, d.BackgroundBrush);
    }

    [Fact]
    public void Map_TransparentPreset_ProducesTransparentBrush()
    {
        var d = ReaderDisplayMapper.Map(new DisplaySettings { BackgroundPreset = BackgroundPreset.Transparent });

        // 透明预设必须走 Brushes.Transparent（而不是拿 "Transparent" 去做颜色解析）
        Assert.Same(Brushes.Transparent, d.BackgroundBrush);
    }

    [Fact]
    public void Map_InvalidBackgroundColor_FallsBackToWhite()
    {
        var s = new DisplaySettings
        {
            BackgroundPreset = BackgroundPreset.Custom,
            CustomBackgroundColor = "这不是一个颜色",
        };

        var d = ReaderDisplayMapper.Map(s);

        var brush = Assert.IsType<SolidColorBrush>(d.BackgroundBrush);
        Assert.Equal(Colors.White, brush.Color);
    }

    [Fact]
    public void Map_InvalidCustomFontColor_FallsBackToBlack()
    {
        var d = ReaderDisplayMapper.Map(new DisplaySettings { FontColor = "##bad##" });

        var brush = Assert.IsType<SolidColorBrush>(d.ForegroundBrush);
        Assert.Equal(Colors.Black, brush.Color);
    }

    [Theory]
    [InlineData(FontColorPreset.Black, "#000000")]
    [InlineData(FontColorPreset.White, "#FFFFFF")]
    public void Map_FontColorPreset_ProducesExpectedColor(FontColorPreset preset, string expectedHex)
    {
        var d = ReaderDisplayMapper.Map(new DisplaySettings { FontColorPreset = preset });

        AssertBrushColor(expectedHex, d.ForegroundBrush);
    }

    private static void AssertBrushColor(string expectedHex, Brush brush)
    {
        var expected = (Color)ColorConverter.ConvertFromString(expectedHex)!;
        var actual = Assert.IsType<SolidColorBrush>(brush);
        Assert.Equal(expected, actual.Color);
    }
}
