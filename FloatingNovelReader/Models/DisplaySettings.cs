using FloatingNovelReader.Core;

namespace FloatingNovelReader.Models;

/// <summary>
/// 背景颜色预设。
/// </summary>
public enum BackgroundPreset
{
    PureWhite,
    Gray,
    PureBlack,
    WarmYellow,
    Transparent,
    Custom,
}

/// <summary>
/// 字体颜色预设。
/// </summary>
public enum FontColorPreset
{
    Black,
    White,
}

/// <summary>
/// 字体与外观显示设置。
/// </summary>
public sealed class DisplaySettings
{
    public string FontFamily { get; set; } = "Microsoft YaHei UI";
    public int FontSize { get; set; } = Constants.DefaultFontSize;
    public string FontColor { get; set; } = "#333333";
    public bool FontBold { get; set; }
    public double LineHeight { get; set; } = Constants.DefaultLineHeight;
    public BackgroundPreset BackgroundPreset { get; set; } = BackgroundPreset.PureWhite;
    public string? CustomBackgroundColor { get; set; }
    public FontColorPreset FontColorPreset { get; set; } = FontColorPreset.Black;
    public double Opacity { get; set; } = 0.95;

    /// <summary>获取实际显示的背景色（带 #），Transparent 预设返回 "Transparent"。</summary>
    public string GetEffectiveBackground()
    {
        return BackgroundPreset switch
        {
            BackgroundPreset.PureWhite => "#FFFFFF",
            BackgroundPreset.Gray => "#808080",
            BackgroundPreset.PureBlack => "#1A1A1A",
            BackgroundPreset.WarmYellow => "#F4ECD8",
            BackgroundPreset.Transparent => "Transparent",
            BackgroundPreset.Custom => CustomBackgroundColor ?? "#FFFFFF",
            _ => "#FFFFFF"
        };
    }

    public string GetEffectiveFontColor()
    {
        if (FontColorPreset == FontColorPreset.White)
            return "#FFFFFF";
        if (FontColorPreset == FontColorPreset.Black)
            return "#000000";
        return "#000000";
    }
}
