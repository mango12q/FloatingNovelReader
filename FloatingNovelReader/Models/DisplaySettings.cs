using FloatingNovelReader.Core;

namespace FloatingNovelReader.Models;

/// <summary>
/// 背景颜色预设。
/// </summary>
public enum BackgroundPreset
{
    PureWhite,
    WarmYellow,
    LightGray,
    DarkGray,
    PureBlack,
    Transparent,
    Custom,
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
    public double Opacity { get; set; } = 0.95;

    /// <summary>获取实际显示的背景色（带 #），Transparent 预设返回 "Transparent"。</summary>
    public string GetEffectiveBackground()
    {
        return BackgroundPreset switch
        {
            BackgroundPreset.PureWhite => "#FFFFFF",
            BackgroundPreset.WarmYellow => "#F4ECD8",
            BackgroundPreset.LightGray => "#E8E8E8",
            BackgroundPreset.DarkGray => "#3C3C3C",
            BackgroundPreset.PureBlack => "#1A1A1A",
            BackgroundPreset.Transparent => "Transparent",
            BackgroundPreset.Custom => CustomBackgroundColor ?? "#FFFFFF",
            _ => "#FFFFFF"
        };
    }

    public string GetEffectiveFontColor()
    {
        // 如果用户没改过（仍是默认 #333），按背景自动适配
        if (FontColor != "#333333" && FontColor != "#D0D0D0" && FontColor != "#E0E0E0" && FontColor != "#5C4033")
            return FontColor;

        return BackgroundPreset switch
        {
            BackgroundPreset.DarkGray => "#D0D0D0",
            BackgroundPreset.PureBlack => "#E0E0E0",
            BackgroundPreset.Transparent => "#FFFFFF",
            _ => "#333333"
        };
    }
}
