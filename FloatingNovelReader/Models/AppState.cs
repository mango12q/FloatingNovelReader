using FloatingNovelReader.Core;

namespace FloatingNovelReader.Models;

public enum ReadingMode
{
    Manual,
    AutoRead,
}

public enum ClickThroughState
{
    Normal,
    ClickThrough,
}

public enum TopmostState
{
    Normal,
    Topmost,
}

/// <summary>
/// 应用运行时状态（在进程内维护，不持久化）。
/// </summary>
public sealed class AppState
{
    public ReadingMode ReadingMode { get; set; } = ReadingMode.Manual;
    public ClickThroughState ClickThrough { get; set; } = ClickThroughState.Normal;
    public TopmostState Topmost { get; set; } = TopmostState.Topmost;
    public Book? CurrentBook { get; set; }
    public Chapter? CurrentChapter { get; set; }
    public int CurrentPage { get; set; }
    public int AutoReadIntervalSec { get; set; } = Constants.DefaultAutoReadIntervalSec;
}
