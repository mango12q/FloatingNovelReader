using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;


namespace FloatingNovelReader.Converters;

public sealed class ColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            if (s.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
                return Brushes.Transparent;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(s);
                return new SolidColorBrush(c);
            }
            catch { return Brushes.White; }
        }
        if (value is Color c2) return new SolidColorBrush(c2);
        return Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush b)
            return b.Color.ToString();
        return "#FFFFFF";
    }
}
