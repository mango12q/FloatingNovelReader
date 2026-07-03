using System.Collections.Generic;

namespace FloatingNovelReader.Models;

/// <summary>
/// 卷。一本书可有多个卷，0 号卷为「正文 / 序章」兜底。
/// </summary>
public sealed class Volume
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int VolumeNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public long StartPosition { get; set; }
    public long EndPosition { get; set; }

    public List<Chapter> Chapters { get; set; } = new();
}
