using System;
using System.Globalization;
using System.Windows.Data;
using FloatingNovelReader.Models;

namespace FloatingNovelReader.Converters;

public sealed class BackgroundPresetToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is BackgroundPreset p ? p switch
        {
            BackgroundPreset.PureWhite => "白色",
            BackgroundPreset.Gray => "灰色",
            BackgroundPreset.PureBlack => "黑色",
            BackgroundPreset.WarmYellow => "纸页黄",
            BackgroundPreset.Transparent => "透明",
            BackgroundPreset.Custom => "自定义...",
            _ => p.ToString()
        } : value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            "白色" => BackgroundPreset.PureWhite,
            "灰色" => BackgroundPreset.Gray,
            "黑色" => BackgroundPreset.PureBlack,
            "纸页黄" => BackgroundPreset.WarmYellow,
            "透明" => BackgroundPreset.Transparent,
            "自定义..." => BackgroundPreset.Custom,
            _ => BackgroundPreset.PureWhite
        };
    }
}
