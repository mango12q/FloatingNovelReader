namespace FloatingNovelReader.Models;

/// <summary>
/// 章节。StartPosition / EndPosition 为文件中的字节偏移，
/// StartLineNumber / LineCount 用于分页定位与日志追踪。
/// </summary>
public sealed class Chapter
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int VolumeId { get; set; }
    public int ChapterNumber { get; set; }
    public int DisplayNumber { get; set; }  // 归一化后的数字
    public string Title { get; set; } = string.Empty;
    public long StartPosition { get; set; }
    public long EndPosition { get; set; }
    public int StartLineNumber { get; set; }
    public int LineCount { get; set; }

    /// <summary>章节长度（字节数）</summary>
    public long Length => EndPosition - StartPosition;
}
