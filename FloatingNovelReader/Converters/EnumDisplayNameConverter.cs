using System;
using System.Globalization;
using System.Windows.Data;
using FloatingNovelReader.Models;

namespace FloatingNovelReader.Converters;

public sealed class EnumDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            StartupBehavior.LastReadingPosition => "恢复上次阅读位置",
            StartupBehavior.Bookshelf => "打开书架",
            HotkeyMode.GlobalAlways => "全局始终生效（可能与其他软件冲突）",
            HotkeyMode.GlobalWhenReaderActive => "仅在本程序窗口激活时生效",
            _ => value?.ToString() ?? string.Empty,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
