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

        foreach (var f in _fontHelper.GetChineseFontFamilies())
            FontFamilies.Add(f);
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
