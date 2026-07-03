using System;
using FloatingNovelReader.Core;

namespace FloatingNovelReader.Models;

/// <summary>
/// 阅读进度。每本书一条记录，含章节、页码、窗口位置/大小、透明度。
/// </summary>
public sealed class ReadingProgress
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int ChapterId { get; set; }
    public int PageNumber { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
    public double WindowWidth { get; set; } = Constants.DefaultWidth;
    public double WindowHeight { get; set; } = Constants.DefaultHeight;
    public double Opacity { get; set; } = 1.0;
}
