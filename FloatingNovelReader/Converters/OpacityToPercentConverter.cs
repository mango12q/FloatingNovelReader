using System;
using System.Globalization;
using System.Windows.Data;

namespace FloatingNovelReader.Converters;

public sealed class OpacityToPercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double d = value switch
        {
            double dd => dd,
            float ff => ff,
            int i => i,
            _ => 1.0
        };
        return $"{Math.Round(d * 100)}%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && s.EndsWith("%") &&
            double.TryParse(s.Substring(0, s.Length - 1), out var v))
            return v / 100.0;
        return 1.0;
    }
}
