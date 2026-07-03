using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;

namespace FloatingNovelReader.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly AutoReadService _autoRead;
    private readonly Helpers.FontHelper _fontHelper;

    [ObservableProperty] private AppSettings _current;
    [ObservableProperty] private int _autoReadIntervalSec;

    public ObservableCollection<string> FontFamilies { get; } = new();
    public Array BackgroundPresets { get; } = Enum.GetValues<BackgroundPreset>();
    public Array FontColorPresets { get; } = Enum.GetValues<FontColorPreset>();
    public Array StartupOptions { get; } = Enum.GetValues<StartupBehavior>();

    public SettingsViewModel(
        SettingsService settings,
        AutoReadService autoRead,
        Helpers.FontHelper fontHelper)
    {
        _settings = settings;
        _autoRead = autoRead;
        _fontHelper = fontHelper;
        _current = settings.Current;
        _autoReadIntervalSec = Current.AutoReadIntervalSec;

        // 只保留指定的字体族：黑体、宋体、楷体
        var allowedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SimHei", "黑体", "MS黑体",
            "SimSun", "宋体",
            "KaiTi", "楷体"
        };
        foreach (var f in _fontHelper.GetChineseFontFamilies())
        {
            if (allowedFonts.Contains(f) || allowedFonts.Any(a => f.Contains(a, StringComparison.OrdinalIgnoreCase)))
                FontFamilies.Add(f);
        }
        if (FontFamilies.Count == 0)
        {
            foreach (var f in _fontHelper.GetChineseFontFamilies())
                FontFamilies.Add(f);
        }
    }

    [RelayCommand]
    public void Save()
    {
        Current.AutoReadIntervalSec = AutoReadIntervalSec;
        _settings.Save();
        _autoRead.IntervalSec = AutoReadIntervalSec;
    }

    [RelayCommand]
    public void Cancel()
    {
        _settings.Reload();
    }
}
