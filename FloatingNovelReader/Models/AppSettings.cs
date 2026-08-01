using FloatingNovelReader.Core;

namespace FloatingNovelReader.Models;

public enum StartupBehavior
{
    LastReadingPosition,
    Bookshelf,
}

public enum HotkeyMode
{
    GlobalAlways,
    GlobalWhenReaderActive,
}

/// <summary>
/// 全局应用设置。序列化到 settings.json。
/// </summary>
public sealed class AppSettings
{
    public StartupBehavior StartupBehavior { get; set; } = StartupBehavior.LastReadingPosition;
    public HotkeyMode HotkeyMode { get; set; } = HotkeyMode.GlobalAlways;
    public bool AutoStart { get; set; }

    public HotkeyConfig Hotkeys { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();

    public int AutoReadIntervalSec { get; set; } = Constants.DefaultAutoReadIntervalSec;

    public double DefaultWidth { get; set; } = Constants.DefaultWidth;
    public double DefaultHeight { get; set; } = Constants.DefaultHeight;
    public double MinWidth { get; set; } = Constants.MinWidth;
    public double MinHeight { get; set; } = Constants.MinHeight;
    public int EdgeSnapThreshold { get; set; } = Constants.EdgeSnapThreshold;
    public int ControlBarShowDelayMs { get; set; } = Constants.ControlBarShowDelayMs;
    public int ControlBarHideDelayMs { get; set; } = Constants.ControlBarHideDelayMs;
}
